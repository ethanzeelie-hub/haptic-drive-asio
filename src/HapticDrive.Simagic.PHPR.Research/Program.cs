using HapticDrive.Simagic.PHPR.Research.Inventory;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || IsHelp(args[0]))
    {
        PrintHelp();
        return 0;
    }

    if (!string.Equals(args[0], "inventory", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"Unknown command '{args[0]}'.");
        PrintHelp();
        return 2;
    }

    var exportJson = true;
    var exportMarkdown = true;
    var outputDirectory = Path.Combine(Environment.CurrentDirectory, "local-device-inventory");

    for (var index = 1; index < args.Length; index++)
    {
        var arg = args[index];
        if (string.Equals(arg, "--no-export", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "--console-only", StringComparison.OrdinalIgnoreCase))
        {
            exportJson = false;
            exportMarkdown = false;
        }
        else if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
        {
            exportJson = true;
        }
        else if (string.Equals(arg, "--no-json", StringComparison.OrdinalIgnoreCase))
        {
            exportJson = false;
        }
        else if (string.Equals(arg, "--markdown", StringComparison.OrdinalIgnoreCase))
        {
            exportMarkdown = true;
        }
        else if (string.Equals(arg, "--no-markdown", StringComparison.OrdinalIgnoreCase))
        {
            exportMarkdown = false;
        }
        else if (string.Equals(arg, "--output-dir", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
        {
            outputDirectory = Path.GetFullPath(args[++index]);
        }
        else
        {
            Console.Error.WriteLine($"Unknown inventory option '{arg}'.");
            PrintHelp();
            return 2;
        }
    }

    Console.WriteLine(SimagicDeviceInventorySummaryFormatter.SafetyBanner);
    Console.WriteLine();

    var provider = CompositeSimagicDeviceInventoryProvider.CreateDefault();
    var snapshot = await provider.DiscoverAsync();
    var exporter = new SimagicDeviceInventoryExporter();

    string? jsonPath = null;
    string? markdownPath = null;
    if (exportJson)
    {
        jsonPath = await exporter.ExportJsonAsync(snapshot, outputDirectory);
    }

    if (exportMarkdown)
    {
        markdownPath = await exporter.ExportMarkdownSummaryAsync(snapshot, outputDirectory);
    }

    Console.WriteLine(SimagicDeviceInventorySummaryFormatter.FormatConsole(snapshot, jsonPath, markdownPath));
    return 0;
}

static bool IsHelp(string arg)
{
    return string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "help", StringComparison.OrdinalIgnoreCase);
}

static void PrintHelp()
{
    Console.WriteLine(SimagicDeviceInventorySummaryFormatter.SafetyBanner);
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src\\HapticDrive.Simagic.PHPR.Research\\HapticDrive.Simagic.PHPR.Research.csproj -- inventory [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --output-dir <path>  Export sanitized files to this directory. Default: local-device-inventory");
    Console.WriteLine("  --no-export          Print console summary only.");
    Console.WriteLine("  --no-json            Skip sanitized JSON export.");
    Console.WriteLine("  --no-markdown        Skip sanitized Markdown summary export.");
    Console.WriteLine();
    Console.WriteLine("The inventory command is safe when Simagic hardware is absent. Missing devices are reported as pending user inventory, not as a failure.");
}
