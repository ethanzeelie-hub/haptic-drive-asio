namespace HapticDrive.Simagic.PHPR.Abstractions.Readiness;

public sealed record PHprManualTestResultTemplate(IReadOnlyList<string> RequiredFields)
{
    public static PHprManualTestResultTemplate Stage2P { get; } = new(
    [
        "Date/time",
        "App branch/commit",
        "P700 connected",
        "P-HPR brake module installed",
        "P-HPR throttle module installed",
        "SimPro Manager status",
        "SimHub status",
        "Selected device/interface/report",
        "Brake pulse result",
        "Throttle pulse result",
        "Stop result",
        "Emergency stop result",
        "Telemetry stale gate result",
        "Emergency mute gate result",
        "DrivingArmed gate result",
        "SimPro conflict gate result",
        "Unexpected behavior",
        "Notes",
        "Pass/fail decision"
    ]);
}
