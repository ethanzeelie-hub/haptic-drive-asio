using System.Text.Json;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;

namespace HapticDrive.Simagic.PHPR.Research.MockProtocol;

public sealed record SimagicMockProtocolExample(
    string Id,
    string Description,
    string SafetyBoundary,
    IReadOnlyList<SimagicMockProtocolExampleFrame> Frames);

public sealed record SimagicMockProtocolExampleFrame(
    string Family,
    string TargetModule,
    string State,
    int ScheduledOffsetMs,
    string PayloadHex,
    int PayloadLengthBytes,
    bool MockOnly,
    string EvidenceConfidence);

public static class SimagicMockProtocolExamples
{
    public const string SafetyBanner = """
STAGE 2K MOCK P-HPR PROTOCOL SAFETY
Mock-only examples. No USB writes. No HID output reports. No feature reports.
No real P-HPR vibration. No hardware access. Not approved for real control.
""";

    public static IReadOnlyList<SimagicMockProtocolExample> Create()
    {
        var encoder = new SimHubF1EcMockEncoder();
        var scheduler = new PHprMockDurationScheduler();

        return
        [
            CreateExample(
                "simhub-f1ec-brake-50hz-10pct-start",
                "SimHub F1 EC mock active/start frame for brake at 50 Hz and 10 percent.",
                encoder.Encode(Command(PHprModuleId.Brake, PHprMockProtocolState.Start, 50d, 0.10d, 100))),
            CreateExample(
                "simhub-f1ec-brake-50hz-20pct-start",
                "SimHub F1 EC mock active/start frame for brake at 50 Hz and 20 percent.",
                encoder.Encode(Command(PHprModuleId.Brake, PHprMockProtocolState.Start, 50d, 0.20d, 100))),
            CreateExample(
                "simhub-f1ec-brake-50hz-40pct-start",
                "SimHub F1 EC mock active/start frame for brake at 50 Hz and 40 percent.",
                encoder.Encode(Command(PHprModuleId.Brake, PHprMockProtocolState.Start, 50d, 0.40d, 100))),
            CreateExample(
                "simhub-f1ec-throttle-50hz-10pct-start",
                "SimHub F1 EC mock active/start frame for throttle at 50 Hz and 10 percent.",
                encoder.Encode(Command(PHprModuleId.Throttle, PHprMockProtocolState.Start, 50d, 0.10d, 100))),
            CreateExample(
                "simhub-f1ec-brake-stop",
                "SimHub F1 EC mock stop frame for brake.",
                encoder.Encode(Command(PHprModuleId.Brake, PHprMockProtocolState.Stop, 10d, 0d, 0))),
            CreateExample(
                "simhub-f1ec-throttle-stop",
                "SimHub F1 EC mock stop frame for throttle.",
                encoder.Encode(Command(PHprModuleId.Throttle, PHprMockProtocolState.Stop, 10d, 0d, 0))),
            CreateExample(
                "simhub-f1ec-both-expands",
                "Both target expands to explicit brake and throttle mock frames; module 00 is not used.",
                encoder.Encode(Command(PHprModuleId.Both, PHprMockProtocolState.Start, 50d, 0.10d, 100))),
            CreateExample(
                "simhub-f1ec-duration-100ms",
                "Duration model: active/start at 0 ms plus stop at 100 ms.",
                scheduler.Plan(Command(PHprModuleId.Brake, PHprMockProtocolState.Start, 50d, 0.10d, 100))),
            CreateExample(
                "simhub-f1ec-duration-500ms",
                "Duration model: active/start at 0 ms plus stop at 500 ms.",
                scheduler.Plan(Command(PHprModuleId.Brake, PHprMockProtocolState.Start, 50d, 0.10d, 500))),
            CreateExample(
                "simhub-f1ec-emergency-stop",
                "Emergency stop emits immediate stop frames for brake and throttle.",
                encoder.Encode(Command(PHprModuleId.Both, PHprMockProtocolState.EmergencyStop, 10d, 0d, 0)))
        ];
    }

    public static string FormatConsole(IReadOnlyList<SimagicMockProtocolExample> examples)
    {
        var lines = new List<string>
        {
            "Stage 2K mock protocol examples loaded.",
            $"Example count: {examples.Count}",
            "Nothing in this mock protocol may be sent to real hardware.",
            string.Empty
        };

        foreach (var example in examples)
        {
            lines.Add($"{example.Id}: {example.Description}");
            foreach (var frame in example.Frames)
            {
                lines.Add($"  +{frame.ScheduledOffsetMs} ms {frame.Family} {frame.TargetModule} {frame.State} {frame.PayloadHex}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static async Task<string> ExportJsonAsync(IReadOnlyList<SimagicMockProtocolExample> examples, string outputPath)
    {
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var export = new
        {
            Stage = "Stage 2K",
            SafetyBoundary = SafetyBanner,
            RealWriteStatus = "BlockedForRealWrite",
            Examples = examples
        };
        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(fullPath, json);
        return fullPath;
    }

    private static PHprMockProtocolCommand Command(
        PHprModuleId targetModule,
        PHprMockProtocolState state,
        double frequencyHz,
        double strength01,
        int durationMs)
    {
        return PHprMockProtocolCommand.Create(
            targetModule,
            state,
            frequencyHz,
            strength01,
            durationMs,
            PHprMockProtocolFamily.SimHubF1EcMock,
            PHprCommandSource.TestBench);
    }

    private static SimagicMockProtocolExample CreateExample(
        string id,
        string description,
        PHprMockProtocolEncodingResult encoding)
    {
        if (!encoding.Succeeded)
        {
            return new SimagicMockProtocolExample(
                id,
                $"{description} Encoding failed safely: {encoding.Message}",
                SafetyBanner,
                []);
        }

        return new SimagicMockProtocolExample(
            id,
            description,
            SafetyBanner,
            encoding.Frames.Select(frame => new SimagicMockProtocolExampleFrame(
                frame.Family.ToString(),
                frame.TargetModule.ToString(),
                frame.State.ToString(),
                (int)frame.ScheduledOffset.TotalMilliseconds,
                frame.PayloadHex,
                frame.Payload.Length,
                frame.MockOnly,
                frame.EvidenceConfidence)).ToArray());
    }
}
