using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class ImpactEffect : IHapticEffectSource
{
    private ImpactMetrics? _lastMetrics;
    private uint? _lastConsumedCollisionFrame;
    private float? _lastImpactSessionTime;
    private uint? _lastImpactFrameIdentifier;
    private bool _pendingPulse;
    private float _pendingPulseAmplitude;
    private float _currentIntensity;
    private int _remainingPulseFrames;
    private int _totalPulseFrames;
    private double _phase;

    public ImpactEffect()
        : this(ImpactEffectOptions.Default)
    {
    }

    public ImpactEffect(ImpactEffectOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Snapshot = CreateSnapshot(isActive: false, peakLevel: 0f);
    }

    public string Name => "Impact";

    public ImpactEffectOptions Options { get; }

    public ImpactEffectSnapshot Snapshot { get; private set; }

    public void Reset()
    {
        _lastMetrics = null;
        _lastConsumedCollisionFrame = null;
        _lastImpactSessionTime = null;
        _lastImpactFrameIdentifier = null;
        _pendingPulse = false;
        _pendingPulseAmplitude = 0f;
        _currentIntensity = 0f;
        _remainingPulseFrames = 0;
        _totalPulseFrames = 0;
        _phase = 0.0;
        Snapshot = CreateSnapshot(isActive: false, peakLevel: 0f);
    }

    public void Update(VehicleState vehicleState)
    {
        ArgumentNullException.ThrowIfNull(vehicleState);

        if (!Options.IsEnabled || VehicleStateEffectGuards.ShouldMuteForDrivingState(vehicleState))
        {
            Snapshot = CreateSnapshot(_pendingPulse || _remainingPulseFrames > 0, peakLevel: 0f);
            return;
        }

        var metrics = ReadMetrics(vehicleState, Options);
        if (_lastMetrics is null)
        {
            _lastMetrics = metrics;
            Snapshot = CreateSnapshot(_pendingPulse || _remainingPulseFrames > 0, peakLevel: 0f);
            return;
        }

        var intensity = CalculateIntensity(metrics, _lastMetrics, Options);
        if (HasNewCollision(metrics))
        {
            intensity = Math.Max(intensity, HapticEffectMath.Clamp(Options.CollisionEventIntensity, 0f, 1f));
            _lastConsumedCollisionFrame = metrics.CollisionFrameIdentifier;
        }

        if (intensity > 0f && CanTrigger(metrics))
        {
            _currentIntensity = intensity;
            _pendingPulseAmplitude = HapticEffectMath.Clamp(
                Options.Gain * intensity,
                0f,
                Options.MaximumAmplitude);
            _pendingPulse = _pendingPulseAmplitude > 0f;
            _lastImpactSessionTime = metrics.SessionTime;
            _lastImpactFrameIdentifier = metrics.FrameIdentifier;
        }

        _lastMetrics = metrics;
        Snapshot = CreateSnapshot(_pendingPulse || _remainingPulseFrames > 0, peakLevel: 0f);
    }

    public HapticEffectRenderResult Render(AudioSampleBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Clear();

        if (!Options.IsEnabled)
        {
            _pendingPulse = false;
            _remainingPulseFrames = 0;
            Snapshot = CreateSnapshot(isActive: false, peakLevel: 0f);
            return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: false, PeakLevel: 0f);
        }

        if (_pendingPulse)
        {
            _totalPulseFrames = ResolvePulseFrameCount(destination.SampleRate);
            _remainingPulseFrames = _totalPulseFrames;
            _phase = 0.0;
            _pendingPulse = false;
        }

        if (_remainingPulseFrames <= 0)
        {
            Snapshot = CreateSnapshot(isActive: false, peakLevel: 0f);
            return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: false, PeakLevel: 0f);
        }

        var frequencyHz = float.IsFinite(Options.PulseFrequencyHz) && Options.PulseFrequencyHz > 0f
            ? Options.PulseFrequencyHz
            : 0f;
        var amplitude = HapticEffectMath.Clamp(_pendingPulseAmplitude, 0f, Options.MaximumAmplitude);

        for (var frame = 0; frame < destination.FrameCount && _remainingPulseFrames > 0; frame++)
        {
            var progress = _remainingPulseFrames / (double)Math.Max(1, _totalPulseFrames);
            var envelope = progress * progress;
            var sample = (float)(amplitude * envelope * Math.Sin(_phase));
            HapticEffectMath.WriteMonoFrame(destination, frame, sample);

            _phase = HapticEffectMath.AdvancePhase(_phase, frequencyHz, destination.SampleRate);
            _remainingPulseFrames--;
        }

        var peak = HapticEffectMath.CalculatePeak(destination);
        var isActive = peak > 0f || _remainingPulseFrames > 0;
        Snapshot = CreateSnapshot(isActive, peak);
        return new HapticEffectRenderResult(Name, Options.IsEnabled, isActive, peak);
    }

    private bool HasNewCollision(ImpactMetrics metrics)
    {
        return metrics.CollisionFrameIdentifier is not null
            && metrics.CollisionFrameIdentifier != _lastConsumedCollisionFrame;
    }

    private bool CanTrigger(ImpactMetrics metrics)
    {
        var timeAllowsTrigger = true;
        if (_lastImpactSessionTime is not null && metrics.SessionTime is not null)
        {
            var elapsed = metrics.SessionTime.Value - _lastImpactSessionTime.Value;
            timeAllowsTrigger = elapsed < 0f || elapsed >= Options.CooldownDuration.TotalSeconds;
        }

        var frameAllowsTrigger = true;
        if (_lastImpactFrameIdentifier is not null && metrics.FrameIdentifier is not null)
        {
            frameAllowsTrigger = metrics.FrameIdentifier.Value < _lastImpactFrameIdentifier.Value
                || metrics.FrameIdentifier.Value - _lastImpactFrameIdentifier.Value >= Options.MinimumFrameGap;
        }

        return timeAllowsTrigger && frameAllowsTrigger;
    }

    private static ImpactMetrics ReadMetrics(VehicleState vehicleState, ImpactEffectOptions options)
    {
        float? verticalG = null;
        if (VehicleStateEffectGuards.IsMotionFresh(vehicleState, options.MaximumTelemetryFrameLag))
        {
            var value = vehicleState.Motion!.Value.GForceVertical;
            if (float.IsFinite(value) && Math.Abs(value) <= 20f)
            {
                verticalG = value;
            }
        }

        float? wheelVerticalForce = null;
        float? suspensionAcceleration = null;
        if (VehicleStateEffectGuards.IsMotionExFresh(vehicleState, options.MaximumTelemetryFrameLag))
        {
            var motionEx = vehicleState.MotionEx!.Value;
            var force = VehicleStateEffectGuards.CalculateWheelMaximum(
                motionEx.WheelVertForce,
                value => value >= 0f && value <= 200_000f ? value : null);
            if (force > 0f)
            {
                wheelVerticalForce = force;
            }

            var acceleration = VehicleStateEffectGuards.CalculateWheelMaximum(
                motionEx.SuspensionAcceleration,
                value => float.IsFinite(value) && Math.Abs(value) <= 2_000f ? Math.Abs(value) : null);
            if (acceleration > 0f)
            {
                suspensionAcceleration = acceleration;
            }
        }

        uint? collisionFrame = null;
        if (VehicleStateEffectGuards.IsLastEventFresh(vehicleState, options.MaximumTelemetryFrameLag)
            && vehicleState.LastEvent!.Value.EventCode == "COLL"
            && vehicleState.LastEvent.Value.InvolvesPlayer)
        {
            collisionFrame = vehicleState.LastEvent.Stamp.OverallFrameIdentifier;
        }

        return new ImpactMetrics(
            verticalG,
            wheelVerticalForce,
            suspensionAcceleration,
            collisionFrame,
            vehicleState.Frame.OverallFrameIdentifier,
            vehicleState.Frame.SessionTime);
    }

    private static float CalculateIntensity(
        ImpactMetrics current,
        ImpactMetrics previous,
        ImpactEffectOptions options)
    {
        var intensity = 0f;

        intensity = Math.Max(
            intensity,
            CalculateDeltaIntensity(current.VerticalG, previous.VerticalG, options.VerticalGDeltaThreshold, absoluteDelta: true));
        intensity = Math.Max(
            intensity,
            CalculateDeltaIntensity(current.WheelVerticalForce, previous.WheelVerticalForce, options.WheelVerticalForceDeltaThreshold, absoluteDelta: false));
        intensity = Math.Max(
            intensity,
            CalculateDeltaIntensity(current.SuspensionAcceleration, previous.SuspensionAcceleration, options.SuspensionAccelerationDeltaThreshold, absoluteDelta: false));

        return intensity;
    }

    private static float CalculateDeltaIntensity(
        float? current,
        float? previous,
        float threshold,
        bool absoluteDelta)
    {
        if (current is null || previous is null || !float.IsFinite(threshold) || threshold <= 0f)
        {
            return 0f;
        }

        var delta = current.Value - previous.Value;
        if (absoluteDelta)
        {
            delta = Math.Abs(delta);
        }

        if (delta < threshold)
        {
            return 0f;
        }

        return HapticEffectMath.Clamp(delta / (threshold * 2f), 0.05f, 1f);
    }

    private int ResolvePulseFrameCount(int sampleRate)
    {
        var durationSeconds = Options.PulseDuration.TotalSeconds;
        if (double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds <= 0.0)
        {
            durationSeconds = ImpactEffectOptions.Default.PulseDuration.TotalSeconds;
        }

        return Math.Max(1, (int)Math.Round(durationSeconds * sampleRate));
    }

    private ImpactEffectSnapshot CreateSnapshot(bool isActive, float peakLevel)
    {
        return new ImpactEffectSnapshot(
            Options.IsEnabled,
            isActive,
            _lastImpactFrameIdentifier,
            _lastImpactSessionTime,
            _currentIntensity,
            _remainingPulseFrames,
            peakLevel);
    }

    private sealed record ImpactMetrics(
        float? VerticalG,
        float? WheelVerticalForce,
        float? SuspensionAcceleration,
        uint? CollisionFrameIdentifier,
        uint? FrameIdentifier,
        float? SessionTime);
}
