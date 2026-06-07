using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Research.Safety;

public sealed record SimagicPhprSafetyExample(
    string Id,
    string Description,
    string DecisionKind,
    string Violation,
    string EffectiveCommand,
    string SafetyMessage);

public static class SimagicPhprSafetyExamples
{
    public const string SafetyBanner = """
STAGE 2L P-HPR SAFETY LAYER
Safety/mock only. No USB writes. No HID output reports. No feature reports.
No real P-HPR vibration. No hardware access. Not approved for real control.
""";

    public static IReadOnlyList<SimagicPhprSafetyExample> Create()
    {
        var examples = new List<SimagicPhprSafetyExample>
        {
            Evaluate(
                "accepted-safe-brake-pulse",
                "Safe mock brake pulse within Stage 2L defaults.",
                PHprCommand.Create(PHprModuleId.Brake, 0.05d, 50d, 60, PHprCommandSource.TestBench)),
            Evaluate(
                "clamped-strength-duration-frequency",
                "Unsafe numeric values are clamped before mock output.",
                PHprCommand.Create(PHprModuleId.Brake, 0.80d, 999d, 2_000, PHprCommandSource.TestBench)),
            Evaluate(
                "telemetry-stale-rejects-start",
                "Telemetry-stale context rejects start commands.",
                PHprCommand.Create(PHprModuleId.Throttle, 0.05d, 40d, 60, PHprCommandSource.TestBench),
                PHprSafetyContext.DefaultMock with { TelemetryStale = true }),
            Evaluate(
                "simpro-conflict-rejects-start",
                "Synthetic SimPro/SimHub conflict placeholder rejects start commands.",
                PHprCommand.Create(PHprModuleId.Both, 0.05d, 50d, 60, PHprCommandSource.TestBench),
                PHprSafetyContext.DefaultMock with { SoftwareConflictStatus = PHprSoftwareConflictStatus.ActiveConflict }),
            Evaluate(
                "real-writes-blocked",
                "Direct-control request remains blocked because AllowRealDeviceWrites is false by default.",
                PHprCommand.Create(PHprModuleId.Brake, 0.05d, 50d, 60, PHprCommandSource.TestBench),
                PHprSafetyContext.DefaultMock with { RequiresRealDeviceWrites = true })
        };

        var output = new MockPhprOutputDevice();
        var limited = new SafetyLimitedPhprOutputDevice(output);
        limited.EmergencyStopAsync().AsTask().GetAwaiter().GetResult();
        var snapshot = limited.SafetySnapshot;
        examples.Add(new SimagicPhprSafetyExample(
            "emergency-stop-latches",
            "Emergency stop records brake/throttle mock stop frames and latches the safety state.",
            snapshot.LastDecision?.Kind.ToString() ?? "Unknown",
            snapshot.LastViolation?.Code.ToString() ?? PHprSafetyViolationCode.None.ToString(),
            "EmergencyStop",
            snapshot.LastDecision?.Message ?? "Emergency stop recorded."));

        return examples;
    }

    public static string FormatConsole(IReadOnlyList<SimagicPhprSafetyExample> examples)
    {
        var lines = new List<string>
        {
            "Stage 2L P-HPR safety examples loaded.",
            $"Example count: {examples.Count}",
            "Mock mode can proceed without enabling real writes. Direct real-write mode remains blocked.",
            string.Empty
        };

        foreach (var example in examples)
        {
            lines.Add($"{example.Id}: {example.Description}");
            lines.Add($"  decision={example.DecisionKind}; violation={example.Violation}; command={example.EffectiveCommand}");
            lines.Add($"  {example.SafetyMessage}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static SimagicPhprSafetyExample Evaluate(
        string id,
        string description,
        PHprCommand command,
        PHprSafetyContext? context = null)
    {
        var limiter = new PHprSafetyLimiter();
        var decision = limiter.Evaluate(command, context);
        return new SimagicPhprSafetyExample(
            id,
            description,
            decision.Kind.ToString(),
            decision.Violation.Code.ToString(),
            FormatCommand(decision.Command),
            decision.Message);
    }

    private static string FormatCommand(PHprCommand? command)
    {
        if (command is null)
        {
            return "none";
        }

        return $"{command.TargetModule} strength={command.Strength01:0.###} freq={command.FrequencyHz:0.###}Hz duration={command.DurationMs}ms flags={command.SafetyFlags}";
    }
}
