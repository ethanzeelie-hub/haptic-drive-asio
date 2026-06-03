namespace HapticDrive.Asio.Core.Audio;

public delegate AudioOutputRenderCallbackResult AudioOutputRenderCallback(
    AudioSampleBuffer destination,
    AudioOutputRenderContext context);

public readonly record struct AudioOutputRenderContext(
    long CallbackIndex,
    DateTimeOffset CallbackStartedAtUtc,
    TimeSpan ExpectedPeriod,
    TimeSpan? CallbackJitter);

public readonly record struct AudioOutputRenderCallbackResult(
    bool Succeeded,
    string Message,
    TimeSpan? TelemetryAge = null,
    bool TelemetryTimedOut = false)
{
    public static AudioOutputRenderCallbackResult Success(
        string message,
        TimeSpan? telemetryAge = null,
        bool telemetryTimedOut = false)
    {
        return new(true, message, telemetryAge, telemetryTimedOut);
    }

    public static AudioOutputRenderCallbackResult Failure(
        string message,
        TimeSpan? telemetryAge = null,
        bool telemetryTimedOut = false)
    {
        return new(false, message, telemetryAge, telemetryTimedOut);
    }
}
