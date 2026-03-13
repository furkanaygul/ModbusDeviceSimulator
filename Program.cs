using ModbusDeviceSimulator;

try
{
    var options = SimulatorOptions.Parse(args);
    using var cancellationTokenSource = new CancellationTokenSource();

    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationTokenSource.Cancel();
    };

    var simulator = new ModbusSimulatorHost(options);
    await simulator.RunAsync(cancellationTokenSource.Token);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine();
    SimulatorOptions.PrintUsage();
    Environment.ExitCode = 1;
}
