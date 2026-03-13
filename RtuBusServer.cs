using System.IO.Ports;

namespace ModbusDeviceSimulator;

internal sealed class RtuBusServer
{
    private readonly Dictionary<byte, VirtualModbusDevice> _devices;
    private readonly SerialPort _serialPort;

    public RtuBusServer(SimulatorOptions options, IReadOnlyCollection<VirtualModbusDevice> devices)
    {
        _devices = devices.ToDictionary(device => device.UnitId);
        _serialPort = new SerialPort(options.RtuPortName!, options.BaudRate, options.Parity, options.DataBits, options.StopBits)
        {
            ReadTimeout = 500,
            WriteTimeout = 500
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _serialPort.Open();
        Console.WriteLine($"RTU bus dinliyor: {_serialPort.PortName} ({_serialPort.BaudRate} {_serialPort.DataBits}{ParityToText(_serialPort.Parity)}{StopBitsToText(_serialPort.StopBits)})");
        Console.WriteLine($"RTU slave id listesi: {string.Join(", ", _devices.Keys.OrderBy(key => key))}");

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }
            catch
            {
            }
        });

        try
        {
            await ReadLoopAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var stream = _serialPort.BaseStream;
        var singleByte = new byte[1];
        var buffer = new byte[256];
        var count = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(singleByte.AsMemory(0, 1), cancellationToken);
            if (read == 0)
            {
                continue;
            }

            if (count == buffer.Length)
            {
                count = 0;
            }

            buffer[count] = singleByte[0];
            count++;

            while (count >= 8)
            {
                if (TryParseReadRequest(buffer.AsSpan(0, 8), out var request))
                {
                    ShiftLeft(buffer, ref count, 8);
                    await HandleRequestAsync(stream, request, cancellationToken);
                    continue;
                }

                ShiftLeft(buffer, ref count, 1);
            }
        }
    }

    private async Task HandleRequestAsync(Stream stream, RtuReadRequest request, CancellationToken cancellationToken)
    {
        if (!_devices.TryGetValue(request.UnitId, out var device))
        {
            return;
        }

        var requestPdu = new byte[]
        {
            request.FunctionCode,
            (byte)(request.StartAddress >> 8),
            (byte)(request.StartAddress & 0xFF),
            (byte)(request.Quantity >> 8),
            (byte)(request.Quantity & 0xFF)
        };

        if (!ModbusProtocol.TryBuildReadResponse(device, requestPdu, out var responsePdu))
        {
            return;
        }

        var responseFrame = new byte[1 + responsePdu.Length + 2];
        responseFrame[0] = request.UnitId;
        Buffer.BlockCopy(responsePdu, 0, responseFrame, 1, responsePdu.Length);

        var crc = ModbusProtocol.ComputeCrc(responseFrame.AsSpan(0, responseFrame.Length - 2));
        responseFrame[^2] = (byte)(crc & 0xFF);
        responseFrame[^1] = (byte)(crc >> 8);

        await stream.WriteAsync(responseFrame, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static bool TryParseReadRequest(ReadOnlySpan<byte> candidateFrame, out RtuReadRequest request)
    {
        request = default;
        var functionCode = candidateFrame[1];
        if (functionCode is not 0x03 and not 0x04)
        {
            return false;
        }

        var expectedCrc = ModbusProtocol.ComputeCrc(candidateFrame[..6]);
        var actualCrc = (ushort)(candidateFrame[6] | (candidateFrame[7] << 8));
        if (expectedCrc != actualCrc)
        {
            return false;
        }

        request = new RtuReadRequest(
            candidateFrame[0],
            functionCode,
            (ushort)((candidateFrame[2] << 8) | candidateFrame[3]),
            (ushort)((candidateFrame[4] << 8) | candidateFrame[5]));

        return true;
    }

    private static void ShiftLeft(byte[] buffer, ref int count, int length)
    {
        if (length >= count)
        {
            count = 0;
            return;
        }

        Buffer.BlockCopy(buffer, length, buffer, 0, count - length);
        count -= length;
    }

    private static string ParityToText(Parity parity)
    {
        return parity switch
        {
            Parity.None => "N",
            Parity.Odd => "O",
            Parity.Even => "E",
            _ => parity.ToString()
        };
    }

    private static string StopBitsToText(StopBits stopBits)
    {
        return stopBits switch
        {
            StopBits.One => "1",
            StopBits.Two => "2",
            _ => stopBits.ToString()
        };
    }

    private readonly record struct RtuReadRequest(byte UnitId, byte FunctionCode, ushort StartAddress, ushort Quantity);
}
