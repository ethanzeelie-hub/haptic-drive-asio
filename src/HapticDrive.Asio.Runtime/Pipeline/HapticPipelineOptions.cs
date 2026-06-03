namespace HapticDrive.Asio.Runtime.Pipeline;

public sealed record HapticPipelineOptions(
    TimeSpan TelemetryMuteTimeout,
    bool UseOutputOwnedRendering)
{
    public static HapticPipelineOptions Default { get; } = new(
        TelemetryMuteTimeout: TimeSpan.FromSeconds(1),
        UseOutputOwnedRendering: true);

    public static HapticPipelineOptions ManualRendering { get; } = Default with
    {
        UseOutputOwnedRendering = false
    };
}
