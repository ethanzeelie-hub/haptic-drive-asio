using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Asio.Core.Haptics;

public static class HapticFrameFreshnessEvaluator
{
    public static HapticFrameFreshnessSnapshot Evaluate(
        in HapticFrame frame,
        TimeProvider timeProvider,
        TelemetryFreshnessPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(policy);

        var nowUtc = timeProvider.GetUtcNow();
        var nowTimestamp = timeProvider.GetTimestamp();
        return new HapticFrameFreshnessSnapshot(
            Evaluate(frame.Identity, frame.SignalStamps.Telemetry, nowUtc, nowTimestamp, timeProvider, policy.MaxTelemetryAge, policy.MaxFrameLag),
            Evaluate(frame.Identity, frame.SignalStamps.Motion, nowUtc, nowTimestamp, timeProvider, policy.MaxMotionAge, policy.MaxFrameLag),
            Evaluate(frame.Identity, frame.SignalStamps.Session, nowUtc, nowTimestamp, timeProvider, policy.MaxSessionAge, policy.MaxFrameLag),
            Evaluate(frame.Identity, frame.SignalStamps.Lap, nowUtc, nowTimestamp, timeProvider, policy.MaxLapAge, policy.MaxFrameLag),
            Evaluate(frame.Identity, frame.SignalStamps.Participant, nowUtc, nowTimestamp, timeProvider, policy.MaxSessionAge, policy.MaxFrameLag),
            Evaluate(frame.Identity, frame.SignalStamps.CarStatus, nowUtc, nowTimestamp, timeProvider, policy.MaxStatusAge, policy.MaxFrameLag),
            Evaluate(frame.Identity, frame.SignalStamps.Damage, nowUtc, nowTimestamp, timeProvider, policy.MaxStatusAge, policy.MaxFrameLag),
            Evaluate(frame.Identity, frame.SignalStamps.MotionEx, nowUtc, nowTimestamp, timeProvider, policy.MaxStatusAge, policy.MaxFrameLag),
            Evaluate(frame.Identity, frame.SignalStamps.Event, nowUtc, nowTimestamp, timeProvider, policy.MaxStatusAge, policy.MaxFrameLag));
    }

    private static VehicleSignalFreshness Evaluate(
        HapticFrameIdentity identity,
        HapticSignalStamp? stamp,
        DateTimeOffset nowUtc,
        long nowTimestamp,
        TimeProvider timeProvider,
        TimeSpan maxAge,
        uint maxFrameLag)
    {
        if (stamp is null)
        {
            return VehicleSignalFreshness.Missing;
        }

        var currentStamp = stamp.Value;
        var sameSession = identity.SessionUid is null || currentStamp.SessionUid == identity.SessionUid.Value;
        var sameSourceGeneration = EvaluateSameSourceGeneration(identity, currentStamp);
        var notFutureFrame = identity.OverallFrameIdentifier is null
            || currentStamp.OverallFrameIdentifier <= identity.OverallFrameIdentifier.Value;
        uint? frameLag = null;
        var withinFrameLag = true;

        if (identity.OverallFrameIdentifier is { } currentFrame)
        {
            if (currentStamp.OverallFrameIdentifier > currentFrame)
            {
                withinFrameLag = false;
            }
            else
            {
                frameLag = currentFrame - currentStamp.OverallFrameIdentifier;
                withinFrameLag = frameLag <= maxFrameLag;
            }
        }

        var utcAge = nowUtc >= currentStamp.ReceivedAtUtc
            ? nowUtc - currentStamp.ReceivedAtUtc
            : TimeSpan.Zero;
        var age = currentStamp.ReceivedAtTimestamp > 0
            && nowTimestamp >= currentStamp.ReceivedAtTimestamp
            ? timeProvider.GetElapsedTime(currentStamp.ReceivedAtTimestamp, nowTimestamp)
            : utcAge;

        return new VehicleSignalFreshness(
            IsPresent: true,
            IsSameSession: sameSession,
            IsNotFutureFrame: notFutureFrame,
            IsWithinFrameLag: withinFrameLag,
            IsWithinAge: age <= maxAge,
            Age: age,
            FrameLag: frameLag)
        {
            IsSameSourceGeneration = sameSourceGeneration
        };
    }

    private static bool EvaluateSameSourceGeneration(
        HapticFrameIdentity identity,
        HapticSignalStamp stamp)
    {
        if (identity.SourceIdentity is not null && stamp.SourceIdentity is not null)
        {
            return identity.SourceIdentity.Generation == stamp.SourceIdentity.Generation;
        }

        return string.Equals(identity.Source, stamp.Source, StringComparison.Ordinal);
    }
}
