namespace ModbusDeviceSimulator;

internal static class ModbusProtocol
{
    public static bool TryBuildReadResponse(VirtualModbusDevice device, ReadOnlySpan<byte> requestPdu, out byte[] responsePdu)
    {
        responsePdu = Array.Empty<byte>();

        if (requestPdu.Length < 5)
        {
            return false;
        }

        var functionCode = requestPdu[0];
        var startAddress = (ushort)((requestPdu[1] << 8) | requestPdu[2]);
        var quantity = (ushort)((requestPdu[3] << 8) | requestPdu[4]);

        if (device.TryReadRegisters(functionCode, startAddress, quantity, out responsePdu, out var exceptionCode))
        {
            return true;
        }

        responsePdu = BuildException(functionCode, exceptionCode);
        return true;
    }

    public static byte[] BuildException(byte functionCode, byte exceptionCode)
    {
        return new[]
        {
            (byte)(functionCode | 0x80),
            exceptionCode
        };
    }

    public static ushort ComputeCrc(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (var index = 0; index < 8; index++)
            {
                var leastSignificantBitSet = (crc & 0x0001) != 0;
                crc >>= 1;
                if (leastSignificantBitSet)
                {
                    crc ^= 0xA001;
                }
            }
        }

        return checked((ushort)crc);
    }
}
