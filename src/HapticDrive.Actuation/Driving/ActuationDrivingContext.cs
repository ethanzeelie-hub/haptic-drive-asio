using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Actuation.Driving;

public sealed record ActuationDrivingContext(
    bool IsArmed,
    DrivingPhase DrivingPhase,
    bool IsPaused,
    bool AllowsDrivingOutput,
    DateTimeOffset CapturedAtUtc,
    long CapturedAtTimestamp,
    string Source);

public static class ActuationDrivingContextFactory
{
    public static ActuationDrivingContext FromHapticFrame(HapticFrame frame, bool isArmed)
    {
        ArgumentNullException.ThrowIfNull(frame);

        return new ActuationDrivingContext(
            isArmed,
            frame.Context.DrivingPhase,
            frame.Context.IsPaused,
            frame.Context.AllowsDrivingOutput,
            frame.Identity.CreatedAtUtc,
            frame.Identity.CreatedAtTimestamp,
            frame.Identity.Source);
    }

    public static ActuationDrivingContext SafeDefault(
        string source,
        DateTimeOffset? capturedAtUtc = null,
        long capturedAtTimestamp = 0)
    {
        return new ActuationDrivingContext(
            IsArmed: false,
            DrivingPhase: DrivingPhase.Unknown,
            IsPaused: true,
            AllowsDrivingOutput: false,
            CapturedAtUtc: capturedAtUtc ?? DateTimeOffset.UtcNow,
            CapturedAtTimestamp: capturedAtTimestamp,
            Source: string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim());
    }
}
