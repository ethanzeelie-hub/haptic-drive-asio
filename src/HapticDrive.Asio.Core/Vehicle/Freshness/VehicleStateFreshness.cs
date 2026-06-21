namespace HapticDrive.Asio.Core.Vehicle.Freshness;

public static class VehicleStateFreshness
{
    public static VehicleSignalFreshness EvaluateTelemetry(
        VehicleState state,
        DateTimeOffset nowUtc,
        long nowTimestamp,
        TimeProvider timeProvider,
        TelemetryFreshnessPolicy policy)
    {
        return Evaluate(state, state.Telemetry, nowUtc, nowTimestamp, timeProvider, policy.MaxTelemetryAge, policy.MaxFrameLag);
    }

    public static VehicleSignalFreshness EvaluateMotion(
        VehicleState state,
        DateTimeOffset nowUtc,
        long nowTimestamp,
        TimeProvider timeProvider,
        TelemetryFreshnessPolicy policy)
    {
        return Evaluate(state, state.Motion, nowUtc, nowTimestamp, timeProvider, policy.MaxMotionAge, policy.MaxFrameLag);
    }

    public static VehicleSignalFreshness EvaluateSession(
        VehicleState state,
        DateTimeOffset nowUtc,
        long nowTimestamp,
        TimeProvider timeProvider,
        TelemetryFreshnessPolicy policy)
    {
        return Evaluate(state, state.Session, nowUtc, nowTimestamp, timeProvider, policy.MaxSessionAge, policy.MaxFrameLag);
    }

    public static VehicleSignalFreshness EvaluateLap(
        VehicleState state,
        DateTimeOffset nowUtc,
        long nowTimestamp,
        TimeProvider timeProvider,
        TelemetryFreshnessPolicy policy)
    {
        return Evaluate(state, state.Lap, nowUtc, nowTimestamp, timeProvider, policy.MaxLapAge, policy.MaxFrameLag);
    }

    public static VehicleSignalFreshness EvaluateCarStatus(
        VehicleState state,
        DateTimeOffset nowUtc,
        long nowTimestamp,
        TimeProvider timeProvider,
        TelemetryFreshnessPolicy policy)
    {
        return Evaluate(state, state.CarStatus, nowUtc, nowTimestamp, timeProvider, policy.MaxStatusAge, policy.MaxFrameLag);
    }

    public static VehicleSignalFreshness EvaluateDamage(
        VehicleState state,
        DateTimeOffset nowUtc,
        long nowTimestamp,
        TimeProvider timeProvider,
        TelemetryFreshnessPolicy policy)
    {
        return Evaluate(state, state.Damage, nowUtc, nowTimestamp, timeProvider, policy.MaxStatusAge, policy.MaxFrameLag);
    }

    public static VehicleSignalFreshness EvaluateMotionEx(
        VehicleState state,
        DateTimeOffset nowUtc,
        long nowTimestamp,
        TimeProvider timeProvider,
        TelemetryFreshnessPolicy policy)
    {
        return Evaluate(state, state.MotionEx, nowUtc, nowTimestamp, timeProvider, policy.MaxStatusAge, policy.MaxFrameLag);
    }

    public static VehicleSignalFreshness EvaluateLastEvent(
        VehicleState state,
        DateTimeOffset nowUtc,
        long nowTimestamp,
        TimeProvider timeProvider,
        TelemetryFreshnessPolicy policy)
    {
        return Evaluate(state, state.LastEvent, nowUtc, nowTimestamp, timeProvider, policy.MaxStatusAge, policy.MaxFrameLag);
    }

    private static VehicleSignalFreshness Evaluate<T>(
        VehicleState state,
        VehicleStateSample<T>? sample,
        DateTimeOffset nowUtc,
        long nowTimestamp,
        TimeProvider timeProvider,
        TimeSpan maxAge,
        uint maxFrameLag)
    {
        if (sample is null)
        {
            return new(false, false, false, false, false, null, null);
        }

        var sameSession = state.Frame.SessionUid is null
            || sample.Stamp.SessionUid == state.Frame.SessionUid.Value;
        var notFutureFrame = state.Frame.OverallFrameIdentifier is null
            || sample.Stamp.OverallFrameIdentifier <= state.Frame.OverallFrameIdentifier.Value;
        uint? frameLag = null;
        var withinFrameLag = true;

        if (state.Frame.OverallFrameIdentifier is { } currentFrame)
        {
            if (sample.Stamp.OverallFrameIdentifier > currentFrame)
            {
                withinFrameLag = false;
            }
            else
            {
                frameLag = currentFrame - sample.Stamp.OverallFrameIdentifier;
                withinFrameLag = frameLag <= maxFrameLag;
            }
        }

        var utcAge = nowUtc >= sample.Stamp.ReceivedAtUtc
            ? nowUtc - sample.Stamp.ReceivedAtUtc
            : TimeSpan.Zero;
        var age = sample.Stamp.ReceivedAtTimestamp > 0
            && nowTimestamp >= sample.Stamp.ReceivedAtTimestamp
            ? timeProvider.GetElapsedTime(sample.Stamp.ReceivedAtTimestamp, nowTimestamp)
            : utcAge;
        var withinAge = age <= maxAge;

        return new(
            IsPresent: true,
            IsSameSession: sameSession,
            IsNotFutureFrame: notFutureFrame,
            IsWithinFrameLag: withinFrameLag,
            IsWithinAge: withinAge,
            Age: age,
            FrameLag: frameLag);
    }
}
