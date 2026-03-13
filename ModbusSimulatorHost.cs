namespace ModbusDeviceSimulator;

internal sealed class ModbusSimulatorHost
{
    private readonly SimulatorOptions _options;

    public ModbusSimulatorHost(SimulatorOptions options)
    {
        _options = options;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();

        Console.WriteLine("Modbus device simulator basladi.");
        Console.WriteLine($"Holding register araligi: 0..{_options.RegisterCount - 1}");
        Console.WriteLine("Tum registerler 500 ms'de bir random guncellenir.");
        Console.WriteLine("Her register kendi cihazina ait farkli bir deger bandinda degisir.");
        Console.WriteLine();

        for (var index = 0; index < _options.TcpDeviceCount; index++)
        {
            var unitId = checked((byte)(_options.TcpUnitStart + index));
            var port = _options.TcpBasePort + index;
            var holdingSeed = checked((ushort)(1000 + (index * 600)));
            var inputSeed = checked((ushort)(15000 + (index * 600)));
            var device = new VirtualModbusDevice($"TCP-{index + 1:00}", unitId, TransportKind.Tcp, _options.RegisterCount, holdingSeed, inputSeed);
            var server = new TcpDeviceServer(device, port);
            tasks.Add(RunServerAsync(server.RunAsync, $"TCP-{index + 1:00}", cancellationToken));
        }

        if (_options.RtuDeviceCount > 0)
        {
            if (string.IsNullOrWhiteSpace(_options.RtuPortName))
            {
                Console.WriteLine("RTU simulasyonu atlandi. Calistirmak icin --rtu-port COMx verin.");
            }
            else
            {
                var devices = new List<VirtualModbusDevice>();
                for (var index = 0; index < _options.RtuDeviceCount; index++)
                {
                    var unitId = checked((byte)(_options.RtuUnitStart + index));
                    var holdingSeed = checked((ushort)(30000 + (index * 600)));
                    var inputSeed = checked((ushort)(45000 + (index * 600)));
                    devices.Add(new VirtualModbusDevice($"RTU-{index + 1:00}", unitId, TransportKind.Rtu, _options.RegisterCount, holdingSeed, inputSeed));
                }

                var rtuServer = new RtuBusServer(_options, devices);
                tasks.Add(RunServerAsync(rtuServer.RunAsync, "RTU", cancellationToken));
            }
        }

        if (tasks.Count == 0)
        {
            Console.WriteLine("Baslatilacak cihaz yok.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Durdurmak icin Ctrl+C.");

        await Task.WhenAll(tasks);
    }

    private static async Task RunServerAsync(Func<CancellationToken, Task> serverRunner, string serverName, CancellationToken cancellationToken)
    {
        try
        {
            await serverRunner(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Console.WriteLine($"{serverName} baslatilamadi veya durdu: {exception.Message}");
        }
    }
}
