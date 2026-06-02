using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class GearShiftEffect : IHapticEffectSource
{
    private sbyte? _lastObservedGear;
    private sbyte? _lastForwardGear;
    private float? _lastShiftSessionTime;
    private uint? _lastShiftFrameIdentifier;
    private bool _pendingPulse;
    private float _pendingPulseAmplitude;
    private int _remainingPulseFrames;
    private int _totalPulseFrames;
    private double _phase;

    public GearShiftEffect()
        : this(GearShiftEffectOptions.Default)
    {
    }

    public GearShiftEffect(GearShiftEffectOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Snapshot = new GearShiftEffectSnapshot(
            Options.IsEnabled,
            IsActive: false,
            LastObservedGear: null,
            LastForwardGear: null,
            LastShiftFrameIdentifier: null,
            LastShiftSessionTime: null,
            RemainingPulseFrames: 0,
            PeakLevel: 0f);
    }

    public string Name => "Gear shift";

    public GearShiftEffectOptions Options { get; }

    public GearShiftEffectSnapshot Snapshot { get; private set; }

    public void Reset()
    {
        _lastObservedGear = null;
        _lastForwardGear = null;
        _lastShiftSessionTime = null;
        _lastShiftFrameIdentifier = null;
        _pendingPulse = false;
        _pendingPulseAmplitude = 0f;
        _remainingPulseFrames = 0;
        _totalPulseFrames = 0;
        _phase = 0.0;
        Snapshot = Snapshot with
        {
            IsActive = false,
            LastObservedGear = null,
            LastForwardGear = null,
            LastShiftFrameIdentifier = null,
            LastShiftSessionTime = null,
            RemainingPulseFrames = 0,
            PeakLevel = 0f
        };
    }

    public void Update(VehicleState vehicleState)
    {
        ArgumentNullException.ThrowIfNull(vehicleState);

        if (!Options.IsEnabled || vehicleState.Telemetry is null)
        {
            Snapshot = CreateSnapshot(isActive: _pendingPulse || _remainingPulseFrames > 0, peakLevel: 0f);
            return;
        }

        var telemetry = vehicleState.Telemetry.Value;
        var currentGear = telemetry.Gear;
        _lastObservedGear = currentGear;

        if (!IsValidForwardGear(currentGear, vehicleState))
        {
            Snapshot = CreateSnapshot(isActive: _pendingPulse || _remainingPulseFrames > 0, peakLevel: 0f);
            return;
        }

        if (_lastForwardGear is null)
        {
            _lastForwardGear = currentGear;
            Snapshot = CreateSnapshot(isActive: _pendingPulse || _remainingPulseFrames > 0, peakLevel: 0f);
            return;
        }

        if (_lastForwardGear == currentGear)
        {
            Snapshot = CreateSnapshot(isActive: _pendingPulse || _remainingPulseFrames > 0, peakLevel: 0f);
            return;
        }

        if (CanTrigger(vehicleState))
        {
            _pendingPulse = true;
            _pendingPulseAmplitude = CalculatePulseAmplitude(vehicleState);
            _lastShiftSessionTime = vehicleState.Telemetry.Stamp.SessionTime;
            _lastShiftFrameIdentifier = vehicleState.Telemetry.Stamp.FrameIdentifier;
        }

        _lastForwardGear = currentGear;
        Snapshot = CreateSnapshot(isActive: _pendingPulse || _remainingPulseFrames > 0, peakLevel: 0f);
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
        var amplitude = HapticEffectMath.Clamp(_pendingPulseAmplitude, 0f, 1f);

        for (var frame = 0; frame < destination.FrameCount && _remainingPulseFrames > 0; frame++)
        {
            var envelope = _remainingPulseFrames / (double)Math.Max(1, _totalPulseFrames);
            var sample = (float)(amplitude * envelope * Math.Sin(_phase));
            HapticEffectMath.WriteMonoFrame(destination, frame, sample);

            _phase += HapticEffectMath.TwoPi * frequencyHz / destination.SampleRate;
            if (_phase >= HapticEffectMath.TwoPi)
            {
                _phase %= HapticEffectMath.TwoPi;
            }

            _remainingPulseFrames--;
        }

        var peak = HapticEffectMath.CalculatePeak(destination);
        var isActive = peak > 0f || _remainingPulseFrames > 0;
        Snapshot = CreateSnapshot(isActive, peak);
        return new HapticEffectRenderResult(Name, Options.IsEnabled, isActive, peak);
    }

    private bool CanTrigger(VehicleState vehicleState)
    {
        if (_lastShiftSessionTime is null)
        {
            return true;
        }

        var currentSessionTime = vehicleState.Telemetry?.Stamp.SessionTime;
        if (currentSessionTime is null || !float.IsFinite(currentSessionTime.Value))
        {
            return true;
        }

        var elapsed = currentSessionTime.Value - _lastShiftSessionTime.Value;
        if (elapsed < 0f)
        {
            return true;
        }

        return elapsed >= Options.EngagingDebounceDuration.TotalSeconds;
    }

    private float CalculatePulseAmplitude(VehicleState vehicleState)
    {
        var gain = HapticEffectMath.Clamp(Options.Gain, 0f, 1f);
        if (!Options.ModulateGainByRpm || vehicleState.Telemetry is null)
        {
            return gain;
        }

        var rpm = vehicleState.Telemetry.Value.EngineRpm;
        if (rpm == 0)
        {
            return gain * 0.5f;
        }

        var idleRpm = vehicleState.CarStatus?.Value.IdleRpm ?? Options.DefaultIdleRpm;
        var maxRpm = vehicleState.CarStatus?.Value.MaxRpm ?? Options.DefaultMaxRpm;
        if (idleRpm == 0 || maxRpm <= idleRpm)
        {
            idleRpm = Options.DefaultIdleRpm;
            maxRpm = Options.DefaultMaxRpm;
        }

        var rpmAmount = HapticEffectMath.Clamp((rpm - idleRpm) / (double)(maxRpm - idleRpm), 0.0, 1.0);
        return gain * (float)(0.5 + (0.5 * rpmAmount));
    }

    private bool IsValidForwardGear(sbyte gear, VehicleState vehicleState)
    {
        if (Options.DetectionMode != GearShiftDetectionMode.ForwardGearChangesOnly)
        {
            return false;
        }

        var maxGears = vehicleState.CarStatus?.Value.MaxGears;
        var maximumForwardGear = maxGears is > 0 and <= 12 ? maxGears.Value : (byte)8;
        return gear >= 1 && gear <= maximumForwardGear;
    }

    private int ResolvePulseFrameCount(int sampleRate)
    {
        var durationSeconds = Options.PulseDuration.TotalSeconds;
        if (double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds <= 0.0)
        {
            durationSeconds = GearShiftEffectOptions.Default.PulseDuration.TotalSeconds;
        }

        return Math.Max(1, (int)Math.Round(durationSeconds * sampleRate));
    }

    private GearShiftEffectSnapshot CreateSnapshot(bool isActive, float peakLevel)
    {
        return new GearShiftEffectSnapshot(
            Options.IsEnabled,
            isActive,
            _lastObservedGear,
            _lastForwardGear,
            _lastShiftFrameIdentifier,
            _lastShiftSessionTime,
            _remainingPulseFrames,
            peakLevel);
    }
}
