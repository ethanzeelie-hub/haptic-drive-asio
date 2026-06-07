using System.Text.Json;
using HapticDrive.Simagic.PHPR.Research.Capture;
using HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;
using HapticDrive.Simagic.PHPR.Research.Hypotheses;
using HapticDrive.Simagic.PHPR.Research.Inventory;
using HapticDrive.Simagic.PHPR.Research.MockProtocol;

namespace HapticDrive.Simagic.PHPR.Research;

public static class SimagicResearchCli
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp(output);
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "inventory" => await RunInventoryAsync(args[1..], output, error),
            "capture-scenarios" => RunCaptureScenarios(output),
            "capture-template" => await RunCaptureTemplateAsync(args[1..], output, error),
            "validate-capture-metadata" => await RunValidateCaptureMetadataAsync(args[1..], output, error),
            "capture-manifest" => await RunCaptureManifestAsync(args[1..], output, error),
            "capture-analysis" => await RunCaptureAnalysisAsync(args[1..], output, error),
            "capture-diff" => await RunCaptureDiffAsync(args[1..], output, error),
            "hypotheses-list" => RunHypothesesList(output),
            "hypotheses-export" => await RunHypothesesExportAsync(args[1..], output, error),
            "mock-protocol-examples" => RunMockProtocolExamples(output),
            "mock-protocol-export" => await RunMockProtocolExportAsync(args[1..], output, error),
            _ => UnknownCommand(args[0], output, error)
        };
    }

    private static async Task<int> RunInventoryAsync(string[] args, TextWriter output, TextWriter error)
    {
        var exportJson = true;
        var exportMarkdown = true;
        var outputDirectory = Path.Combine(Environment.CurrentDirectory, "local-device-inventory");

        for (var index = 0; index < args.Length; index++)
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
                await error.WriteLineAsync($"Unknown inventory option '{arg}'.");
                PrintHelp(output);
                return 2;
            }
        }

        await output.WriteLineAsync(SimagicDeviceInventorySummaryFormatter.SafetyBanner);
        await output.WriteLineAsync();

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

        await output.WriteLineAsync(SimagicDeviceInventorySummaryFormatter.FormatConsole(snapshot, jsonPath, markdownPath));
        return 0;
    }

    private static int RunCaptureScenarios(TextWriter output)
    {
        output.WriteLine(SimagicCaptureToolFormatter.SafetyBanner);
        output.WriteLine();
        output.WriteLine(SimagicCaptureToolFormatter.FormatScenarios());
        return 0;
    }

    private static async Task<int> RunCaptureTemplateAsync(string[] args, TextWriter output, TextWriter error)
    {
        SimagicCaptureScenarioId? scenarioId = null;
        SimagicCaptureTargetModule? targetModule = null;
        var outputDirectory = Path.Combine(Environment.CurrentDirectory, "capture-metadata", "generated");
        var writeToStdout = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--scenario", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                if (!Enum.TryParse<SimagicCaptureScenarioId>(args[++index], ignoreCase: true, out var parsedScenario))
                {
                    await error.WriteLineAsync($"Unknown capture scenario '{args[index]}'.");
                    return 2;
                }

                scenarioId = parsedScenario;
            }
            else if (string.Equals(arg, "--target", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                if (!Enum.TryParse<SimagicCaptureTargetModule>(args[++index], ignoreCase: true, out var parsedTarget))
                {
                    await error.WriteLineAsync($"Unknown target module '{args[index]}'.");
                    return 2;
                }

                targetModule = parsedTarget;
            }
            else if (string.Equals(arg, "--output-dir", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                outputDirectory = Path.GetFullPath(args[++index]);
            }
            else if (string.Equals(arg, "--stdout", StringComparison.OrdinalIgnoreCase))
            {
                writeToStdout = true;
            }
            else
            {
                await error.WriteLineAsync($"Unknown capture-template option '{arg}'.");
                return 2;
            }
        }

        if (scenarioId is null)
        {
            await error.WriteLineAsync("capture-template requires --scenario <ScenarioId>.");
            return 2;
        }

        await output.WriteLineAsync(SimagicCaptureToolFormatter.SafetyBanner);
        await output.WriteLineAsync();

        var template = SimagicCaptureTemplateFactory.Create(scenarioId.Value, targetModule);
        var json = JsonSerializer.Serialize(template, SimagicCaptureJson.Options);
        if (writeToStdout)
        {
            await output.WriteLineAsync(json);
            return 0;
        }

        Directory.CreateDirectory(outputDirectory);
        var fileName = $"{SimagicCaptureFilenameBuilder.Slugify(scenarioId.Value.ToString())}-{SimagicCaptureFilenameBuilder.Slugify(template.Device.TargetModule.ToString())}-metadata-template.json";
        var path = Path.Combine(outputDirectory, fileName);
        await File.WriteAllTextAsync(path, json);
        await output.WriteLineAsync($"Capture metadata template written: {path}");
        return 0;
    }

    private static async Task<int> RunValidateCaptureMetadataAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length != 1)
        {
            await error.WriteLineAsync("validate-capture-metadata requires exactly one metadata JSON path.");
            return 2;
        }

        await output.WriteLineAsync(SimagicCaptureToolFormatter.SafetyBanner);
        await output.WriteLineAsync();

        await using var stream = File.OpenRead(args[0]);
        var metadata = await JsonSerializer.DeserializeAsync<SimagicCaptureMetadata>(stream, SimagicCaptureJson.Options);
        if (metadata is null)
        {
            await error.WriteLineAsync("Metadata JSON did not contain a capture metadata object.");
            return 2;
        }

        var result = new SimagicCaptureMetadataValidator().Validate(metadata);
        await output.WriteLineAsync(SimagicCaptureToolFormatter.FormatValidation(result));
        return result.IsValid ? 0 : 1;
    }

    private static async Task<int> RunCaptureManifestAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length is < 1 or > 3)
        {
            await error.WriteLineAsync("capture-manifest requires <metadata-folder> and optional --output-dir <path>.");
            return 2;
        }

        var metadataFolder = Path.GetFullPath(args[0]);
        var outputDirectory = Path.Combine(Environment.CurrentDirectory, "capture-metadata", "generated");
        for (var index = 1; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--output-dir", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                outputDirectory = Path.GetFullPath(args[++index]);
            }
            else
            {
                await error.WriteLineAsync($"Unknown capture-manifest option '{args[index]}'.");
                return 2;
            }
        }

        await output.WriteLineAsync(SimagicCaptureToolFormatter.SafetyBanner);
        await output.WriteLineAsync();

        var exporter = new SimagicCaptureManifestExporter();
        var manifest = await exporter.LoadManifestFromFolderAsync(metadataFolder);
        var path = await exporter.ExportJsonAsync(manifest, outputDirectory);
        await output.WriteLineAsync(SimagicCaptureToolFormatter.FormatManifestSummary(manifest, path));
        return 0;
    }

    private static async Task<int> RunCaptureAnalysisAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length is < 1 or > 4)
        {
            await error.WriteLineAsync("capture-analysis requires <capture-or-export-path> and optional --output-dir <path>.");
            return 2;
        }

        var inputPath = Path.GetFullPath(args[0]);
        var outputDirectory = Path.Combine(Environment.CurrentDirectory, "capture-metadata", "generated");
        for (var index = 1; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--output-dir", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                outputDirectory = Path.GetFullPath(args[++index]);
            }
            else
            {
                await error.WriteLineAsync($"Unknown capture-analysis option '{args[index]}'.");
                return 2;
            }
        }

        await output.WriteLineAsync(SimagicCaptureAnalysisFormatter.SafetyBanner);
        await output.WriteLineAsync();

        var report = await new SimagicCaptureAnalysisReader().AnalyzePathAsync(inputPath);
        var path = await new SimagicCaptureAnalysisExporter().ExportJsonAsync(report, outputDirectory);
        await output.WriteLineAsync(SimagicCaptureAnalysisFormatter.FormatReport(report, path));
        return 0;
    }

    private static async Task<int> RunCaptureDiffAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length is < 2 or > 5)
        {
            await error.WriteLineAsync("capture-diff requires <left-capture-or-export-path> <right-capture-or-export-path> and optional --output-dir <path>.");
            return 2;
        }

        var leftPath = Path.GetFullPath(args[0]);
        var rightPath = Path.GetFullPath(args[1]);
        var outputDirectory = Path.Combine(Environment.CurrentDirectory, "capture-metadata", "generated");
        for (var index = 2; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--output-dir", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                outputDirectory = Path.GetFullPath(args[++index]);
            }
            else
            {
                await error.WriteLineAsync($"Unknown capture-diff option '{args[index]}'.");
                return 2;
            }
        }

        await output.WriteLineAsync(SimagicCaptureAnalysisFormatter.SafetyBanner);
        await output.WriteLineAsync();

        var report = await new SimagicCaptureAnalysisReader().AnalyzeDiffAsync(leftPath, rightPath);
        var path = await new SimagicCaptureAnalysisExporter().ExportJsonAsync(
            report,
            outputDirectory,
            "simagic-capture-diff-sanitized.json");
        await output.WriteLineAsync(SimagicCaptureAnalysisFormatter.FormatDiff(report, path));
        return 0;
    }

    private static int RunHypothesesList(TextWriter output)
    {
        output.WriteLine(SimagicProtocolHypothesisFormatter.SafetyBanner);
        output.WriteLine();

        var hypothesisSet = BuiltInProtocolHypotheses.Create();
        output.WriteLine(SimagicProtocolHypothesisFormatter.FormatSummary(hypothesisSet));
        return 0;
    }

    private static async Task<int> RunHypothesesExportAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length is < 2 or > 3)
        {
            await error.WriteLineAsync("hypotheses-export requires --output <path>.");
            return 2;
        }

        string? outputPath = null;
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--output", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                outputPath = args[++index];
            }
            else
            {
                await error.WriteLineAsync($"Unknown hypotheses-export option '{args[index]}'.");
                return 2;
            }
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("hypotheses-export requires --output <path>.");
            return 2;
        }

        await output.WriteLineAsync(SimagicProtocolHypothesisFormatter.SafetyBanner);
        await output.WriteLineAsync();

        var hypothesisSet = BuiltInProtocolHypotheses.Create();
        var exporter = new SimagicProtocolHypothesisExporter();
        var path = Path.GetExtension(outputPath).Equals(".md", StringComparison.OrdinalIgnoreCase)
            ? await exporter.ExportMarkdownAsync(hypothesisSet, outputPath)
            : await exporter.ExportJsonAsync(hypothesisSet, outputPath);
        await output.WriteLineAsync(SimagicProtocolHypothesisFormatter.FormatSummary(hypothesisSet, path));
        return 0;
    }

    private static int RunMockProtocolExamples(TextWriter output)
    {
        output.WriteLine(SimagicMockProtocolExamples.SafetyBanner);
        output.WriteLine();
        output.WriteLine(SimagicMockProtocolExamples.FormatConsole(SimagicMockProtocolExamples.Create()));
        return 0;
    }

    private static async Task<int> RunMockProtocolExportAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length is < 2 or > 3)
        {
            await error.WriteLineAsync("mock-protocol-export requires --output <path>.");
            return 2;
        }

        string? outputPath = null;
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--output", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                outputPath = args[++index];
            }
            else
            {
                await error.WriteLineAsync($"Unknown mock-protocol-export option '{args[index]}'.");
                return 2;
            }
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("mock-protocol-export requires --output <path>.");
            return 2;
        }

        await output.WriteLineAsync(SimagicMockProtocolExamples.SafetyBanner);
        await output.WriteLineAsync();

        var examples = SimagicMockProtocolExamples.Create();
        var path = await SimagicMockProtocolExamples.ExportJsonAsync(examples, outputPath);
        await output.WriteLineAsync($"Sanitized mock protocol export written: {path}");
        await output.WriteLineAsync($"Example count: {examples.Count}");
        await output.WriteLineAsync("Nothing in this mock protocol may be sent to real hardware.");
        return 0;
    }

    private static int UnknownCommand(string command, TextWriter output, TextWriter error)
    {
        error.WriteLine($"Unknown command '{command}'.");
        PrintHelp(output);
        return 2;
    }

    private static bool IsHelp(string arg)
    {
        return string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "help", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintHelp(TextWriter output)
    {
        output.WriteLine("Simagic P700 / P-HPR research utility");
        output.WriteLine();
        output.WriteLine("Inventory command:");
        output.WriteLine("  dotnet run --project src\\HapticDrive.Simagic.PHPR.Research\\HapticDrive.Simagic.PHPR.Research.csproj -- inventory [options]");
        output.WriteLine();
        output.WriteLine("Capture metadata commands:");
        output.WriteLine("  dotnet run --project src\\HapticDrive.Simagic.PHPR.Research\\HapticDrive.Simagic.PHPR.Research.csproj -- capture-scenarios");
        output.WriteLine("  dotnet run --project src\\HapticDrive.Simagic.PHPR.Research\\HapticDrive.Simagic.PHPR.Research.csproj -- capture-template --scenario BrakeTestVibration --target Brake");
        output.WriteLine("  dotnet run --project src\\HapticDrive.Simagic.PHPR.Research\\HapticDrive.Simagic.PHPR.Research.csproj -- validate-capture-metadata <path>");
        output.WriteLine("  dotnet run --project src\\HapticDrive.Simagic.PHPR.Research\\HapticDrive.Simagic.PHPR.Research.csproj -- capture-manifest <metadata-folder>");
        output.WriteLine("  dotnet run --project src\\HapticDrive.Simagic.PHPR.Research\\HapticDrive.Simagic.PHPR.Research.csproj -- capture-analysis <capture-or-export-path>");
        output.WriteLine("  dotnet run --project src\\HapticDrive.Simagic.PHPR.Research\\HapticDrive.Simagic.PHPR.Research.csproj -- capture-diff <left-capture-or-export-path> <right-capture-or-export-path>");
        output.WriteLine("  dotnet run --project src\\HapticDrive.Simagic.PHPR.Research\\HapticDrive.Simagic.PHPR.Research.csproj -- hypotheses-list");
        output.WriteLine("  dotnet run --project src\\HapticDrive.Simagic.PHPR.Research\\HapticDrive.Simagic.PHPR.Research.csproj -- hypotheses-export --output capture-metadata\\generated\\simagic-protocol-hypotheses.json");
        output.WriteLine("  dotnet run --project src\\HapticDrive.Simagic.PHPR.Research\\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples");
        output.WriteLine("  dotnet run --project src\\HapticDrive.Simagic.PHPR.Research\\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-export --output capture-metadata\\generated\\simagic-mock-protocol-examples.json");
        output.WriteLine();
        output.WriteLine("Inventory options:");
        output.WriteLine("  --output-dir <path>  Export sanitized files to this directory. Default: local-device-inventory");
        output.WriteLine("  --no-export          Print console summary only.");
        output.WriteLine("  --no-json            Skip sanitized JSON export.");
        output.WriteLine("  --no-markdown        Skip sanitized Markdown summary export.");
        output.WriteLine();
        output.WriteLine("Capture options:");
        output.WriteLine("  --scenario <id>      Scenario ID for capture-template.");
        output.WriteLine("  --target <module>    Brake, Throttle, Both, or Unknown.");
        output.WriteLine("  --output-dir <path>  Default: capture-metadata/generated.");
        output.WriteLine("  --stdout             Print template JSON instead of writing a file.");
        output.WriteLine();
        output.WriteLine("Capture analysis options:");
        output.WriteLine("  --output-dir <path>  Default: capture-metadata/generated.");
        output.WriteLine();
        output.WriteLine("Hypotheses options:");
        output.WriteLine("  --output <path>      Export sanitized Stage 2J hypotheses to JSON, or Markdown when the path ends in .md.");
        output.WriteLine();
        output.WriteLine("Mock protocol options:");
        output.WriteLine("  --output <path>      Export sanitized Stage 2K mock protocol examples to JSON.");
        output.WriteLine();
        output.WriteLine("Capture metadata tooling is Stage 2H. Capture analysis is Stage 2I. Protocol hypotheses are Stage 2J. Mock protocol examples are Stage 2K. None sends USB writes, creates real vibration commands, controls SimPro/SimHub, creates live hardware encoders, or routes haptics.");
    }
}
