namespace ModbusDeviceSimulator;

internal enum TransportKind : ushort
{
    Tcp = 1,
    Rtu = 2
}

internal sealed class VirtualModbusDevice
{
    private const int TagValueBandSize = 10;

    private readonly object _syncRoot = new();
    private readonly int _registerCount;
    private readonly ushort[] _holdingRegisters;
    private readonly ushort[] _inputRegisters;
    private readonly ushort _holdingSeed;
    private readonly ushort _inputSeed;
    private readonly Random _random;
    private readonly Timer _randomizerTimer;

    public VirtualModbusDevice(string name, byte unitId, TransportKind transportKind, int registerCount, ushort holdingSeed, ushort inputSeed)
    {
        Name = name;
        UnitId = unitId;
        TransportKind = transportKind;
        _registerCount = registerCount;
        _holdingRegisters = new ushort[registerCount];
        _inputRegisters = new ushort[registerCount];
        _holdingSeed = holdingSeed;
        _inputSeed = inputSeed;
        _random = new Random(HashCode.Combine(name, unitId, (int)transportKind));

        RandomizeRegisters(null);
        _randomizerTimer = new Timer(RandomizeRegisters, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
    }

    public string Name { get; }

    public byte UnitId { get; }

    public TransportKind TransportKind { get; }

    public int RegisterCount => _registerCount;

    public bool TryReadRegisters(byte functionCode, ushort startAddress, ushort quantity, out byte[] responsePdu, out byte exceptionCode)
    {
        responsePdu = Array.Empty<byte>();
        exceptionCode = 0x00;

        if (functionCode is not 0x03 and not 0x04)
        {
            exceptionCode = 0x01;
            return false;
        }

        if (quantity is 0 or > 125)
        {
            exceptionCode = 0x03;
            return false;
        }

        var endAddressExclusive = startAddress + quantity;
        if (startAddress >= _registerCount || endAddressExclusive > _registerCount)
        {
            exceptionCode = 0x02;
            return false;
        }

        responsePdu = new byte[2 + (quantity * 2)];
        responsePdu[0] = functionCode;
        responsePdu[1] = checked((byte)(quantity * 2));

        lock (_syncRoot)
        {
            for (var offset = 0; offset < quantity; offset++)
            {
                var address = startAddress + offset;
                var value = functionCode == 0x03
                    ? _holdingRegisters[address]
                    : _inputRegisters[address];

                responsePdu[2 + (offset * 2)] = (byte)(value >> 8);
                responsePdu[3 + (offset * 2)] = (byte)(value & 0xFF);
            }
        }

        return true;
    }

    private void RandomizeRegisters(object? _)
    {
        lock (_syncRoot)
        {
            for (var address = 0; address < _registerCount; address++)
            {
                _holdingRegisters[address] = CreateRandomValue(_holdingSeed, address);
                _inputRegisters[address] = CreateRandomValue(_inputSeed, address);
            }
        }
    }

    private ushort CreateRandomValue(ushort seed, int address)
    {
        var baseValue = seed + (address * TagValueBandSize);
        return checked((ushort)(baseValue + _random.Next(0, TagValueBandSize)));
    }
}
