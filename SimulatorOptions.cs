using System.Globalization;
using System.IO.Ports;

namespace ModbusDeviceSimulator;

internal sealed record SimulatorOptions
{
    public int TcpDeviceCount { get; init; } = 10;
    public int TcpBasePort { get; init; } = 1502;
    public byte TcpUnitStart { get; init; } = 1;
    public int RtuDeviceCount { get; init; } = 10;
    public byte RtuUnitStart { get; init; } = 1;
    public string? RtuPortName { get; init; }
    public int BaudRate { get; init; } = 9600;
    public Parity Parity { get; init; } = Parity.None;
    public int DataBits { get; init; } = 8;
    public StopBits StopBits { get; init; } = StopBits.One;
    public int RegisterCount { get; init; } = 50;

    public static SimulatorOptions Parse(string[] args)
    {
        var options = new SimulatorOptions();

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument.ToLowerInvariant())
            {
                case "--help":
                case "-h":
                case "/?":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
                case "--tcp-count":
                    options = options with { TcpDeviceCount = ParseInt(args, ref index, argument, 0, 200) };
                    break;
                case "--tcp-base-port":
                    options = options with { TcpBasePort = ParseInt(args, ref index, argument, 1, 65535) };
                    break;
                case "--tcp-unit-start":
                    options = options with { TcpUnitStart = ParseByte(args, ref index, argument, 1, 247) };
                    break;
                case "--rtu-count":
                    options = options with { RtuDeviceCount = ParseInt(args, ref index, argument, 0, 247) };
                    break;
                case "--rtu-unit-start":
                    options = options with { RtuUnitStart = ParseByte(args, ref index, argument, 1, 247) };
                    break;
                case "--rtu-port":
                    options = options with { RtuPortName = ParseString(args, ref index, argument) };
                    break;
                case "--baud":
                    options = options with { BaudRate = ParseInt(args, ref index, argument, 1200, 115200) };
                    break;
                case "--data-bits":
                    options = options with { DataBits = ParseInt(args, ref index, argument, 5, 8) };
                    break;
                case "--parity":
                    options = options with { Parity = ParseParity(ParseString(args, ref index, argument)) };
                    break;
                case "--stop-bits":
                    options = options with { StopBits = ParseStopBits(ParseString(args, ref index, argument)) };
                    break;
                case "--register-count":
                    options = options with { RegisterCount = ParseInt(args, ref index, argument, 1, 125) };
                    break;
                default:
                    throw new ArgumentException($"Bilinmeyen parametre: {argument}");
            }
        }

        ValidateDeviceRanges(options);
        return options;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("ModbusDeviceSimulator");
        Console.WriteLine("Varsayilan: 10 TCP cihaz + 10 RTU cihaz, her cihazda 50 register.");
        Console.WriteLine();
        Console.WriteLine("Kullanim:");
        Console.WriteLine("  dotnet run -- --rtu-port COM10");
        Console.WriteLine("  dotnet run -- --rtu-port COM10 --tcp-count 20 --rtu-count 20");
        Console.WriteLine();
        Console.WriteLine("Parametreler:");
        Console.WriteLine("  --tcp-count <adet>         Varsayilan 10");
        Console.WriteLine("  --tcp-base-port <port>     Varsayilan 1502, her TCP cihaz icin +1 artar");
        Console.WriteLine("  --tcp-unit-start <id>      Konsola yazilan TCP unit id baslangici");
        Console.WriteLine("  --rtu-count <adet>         Varsayilan 10");
        Console.WriteLine("  --rtu-unit-start <id>      Varsayilan 1");
        Console.WriteLine("  --rtu-port <COMx>          RTU hatti icin zorunlu");
        Console.WriteLine("  --baud <deger>             Varsayilan 9600");
        Console.WriteLine("  --data-bits <deger>        Varsayilan 8");
        Console.WriteLine("  --parity <None|Odd|Even>   Varsayilan None");
        Console.WriteLine("  --stop-bits <One|Two>      Varsayilan One");
        Console.WriteLine("  --register-count <adet>    Varsayilan 50");
    }

    private static int ParseInt(string[] args, ref int index, string argumentName, int minValue, int maxValue)
    {
        var value = ParseString(args, ref index, argumentName);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
        {
            throw new ArgumentException($"{argumentName} icin sayisal bir deger bekleniyor.");
        }

        if (parsedValue < minValue || parsedValue > maxValue)
        {
            throw new ArgumentException($"{argumentName} {minValue} ile {maxValue} arasinda olmalidir.");
        }

        return parsedValue;
    }

    private static byte ParseByte(string[] args, ref int index, string argumentName, byte minValue, byte maxValue)
    {
        var value = ParseInt(args, ref index, argumentName, minValue, maxValue);
        return checked((byte)value);
    }

    private static string ParseString(string[] args, ref int index, string argumentName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{argumentName} icin bir deger bekleniyor.");
        }

        index++;
        return args[index];
    }

    private static Parity ParseParity(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "none" => Parity.None,
            "odd" => Parity.Odd,
            "even" => Parity.Even,
            _ => throw new ArgumentException("Parity icin None, Odd veya Even kullanin.")
        };
    }

    private static StopBits ParseStopBits(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "one" => StopBits.One,
            "two" => StopBits.Two,
            _ => throw new ArgumentException("StopBits icin One veya Two kullanin.")
        };
    }

    private static void ValidateDeviceRanges(SimulatorOptions options)
    {
        if (options.TcpDeviceCount > 0 && options.TcpUnitStart + options.TcpDeviceCount - 1 > 247)
        {
            throw new ArgumentException("TCP unit id araligi 247'yi asiyor.");
        }

        if (options.RtuDeviceCount > 0 && options.RtuUnitStart + options.RtuDeviceCount - 1 > 247)
        {
            throw new ArgumentException("RTU unit id araligi 247'yi asiyor.");
        }

        if (options.TcpBasePort + options.TcpDeviceCount - 1 > 65535)
        {
            throw new ArgumentException("TCP port araligi 65535'i asiyor.");
        }
    }
}
