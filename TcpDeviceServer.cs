using System.Net;
using System.Net.Sockets;

namespace ModbusDeviceSimulator;

internal sealed class TcpDeviceServer
{
    private readonly VirtualModbusDevice _device;
    private readonly int _port;

    public TcpDeviceServer(VirtualModbusDevice device, int port)
    {
        _device = device;
        _port = port;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();

        Console.WriteLine($"TCP {_device.Name} dinliyor: 127.0.0.1:{_port} (unit id {_device.UnitId}, {_device.RegisterCount} register)");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), CancellationToken.None);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            var mbapHeader = new byte[7];

            while (!cancellationToken.IsCancellationRequested)
            {
                if (!await ReadExactlyAsync(stream, mbapHeader, cancellationToken))
                {
                    break;
                }

                var protocolId = (ushort)((mbapHeader[2] << 8) | mbapHeader[3]);
                var length = (ushort)((mbapHeader[4] << 8) | mbapHeader[5]);
                var unitId = mbapHeader[6];

                if (protocolId != 0 || length < 2)
                {
                    break;
                }

                var pduLength = length - 1;
                var requestPdu = new byte[pduLength];
                if (!await ReadExactlyAsync(stream, requestPdu, cancellationToken))
                {
                    break;
                }

                if (!ModbusProtocol.TryBuildReadResponse(_device, requestPdu, out var responsePdu))
                {
                    break;
                }

                var response = new byte[7 + responsePdu.Length];
                Buffer.BlockCopy(mbapHeader, 0, response, 0, 7);
                var responseLength = checked((ushort)(responsePdu.Length + 1));
                response[4] = (byte)(responseLength >> 8);
                response[5] = (byte)(responseLength & 0xFF);
                response[6] = unitId;
                Buffer.BlockCopy(responsePdu, 0, response, 7, responsePdu.Length);

                await stream.WriteAsync(response, cancellationToken);
            }
        }
    }

    private static async Task<bool> ReadExactlyAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
