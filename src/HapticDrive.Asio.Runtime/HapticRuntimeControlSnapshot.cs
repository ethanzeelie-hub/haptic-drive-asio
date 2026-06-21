using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Asio.Runtime;

public sealed record HapticRuntimeControlSnapshot(
    bool IsMuted,
    long OutputInterlockGeneration,
    string SelectedOutputId,
    string ActiveProfileId,
    string ActiveProfileHash,
    int SampleRate,
    int BufferSize,
    bool ManualTestActive,
    TelemetryFreshnessPolicy TelemetryFreshnessPolicy,
    DateTimeOffset CapturedAtUtc)
{
    public static HapticRuntimeControlSnapshot Default { get; } = new(
        IsMuted: true,
        OutputInterlockGeneration: 0,
        SelectedOutputId: "null",
        ActiveProfileId: "default",
        ActiveProfileHash: "unknown",
        SampleRate: 48_000,
        BufferSize: 256,
        ManualTestActive: false,
        TelemetryFreshnessPolicy.Default,
        DateTimeOffset.UnixEpoch);
}
