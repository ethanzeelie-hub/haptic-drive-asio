using HapticDrive.Asio.Recording;

namespace HapticDrive.Asio.App;

internal sealed record ReplayTimingModeOption(
    string Label,
    string HelpText,
    TelemetryReplayOptions Options,
    bool IsFastDebug)
{
    public static ReplayTimingModeOption RealTime { get; } = new(
        "Real-time",
        "Real-time replay preserves recorded packet timing for haptic testing.",
        TelemetryReplayOptions.TimePreserving,
        IsFastDebug: false);

    public static ReplayTimingModeOption FastDebug { get; } = new(
        "Fast debug",
        "Fast debug replays packets as quickly as possible for parser diagnostics and is not suitable for feel/latency testing.",
        TelemetryReplayOptions.Fast,
        IsFastDebug: true);

    public static IReadOnlyList<ReplayTimingModeOption> Defaults { get; } =
    [
        RealTime,
        FastDebug
    ];
}
