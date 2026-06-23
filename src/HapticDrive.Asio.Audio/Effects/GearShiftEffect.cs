using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class GearShiftEffect : IHapticEffectSource, IConfigurableHapticEffectSource<GearShiftEffectOptions>
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

    public GearShiftEffectOptions Options { get; private set; }

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

    public void Update(HapticEffectInput input)
    {
        if (!Options.IsEnabled || input.Frame.Signals.Gear is null)
        {
            Snapshot = CreateSnapshot(isActive: _pendingPulse || _remainingPulseFrames > 0, peakLevel: 0f);
            return;
        }

        var currentGear = (sbyte)input.Frame.Signals.Gear.Value;
        _lastObservedGear = currentGear;

        if (!IsValidForwardGear(currentGear, input))
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

        if (CanTrigger(input.Frame))
        {
            _pendingPulse = true;
            _pendingPulseAmplitude = CalculatePulseAmplitude(input);
            _lastShiftSessionTime = input.Frame.Identity.SessionTime;
            _lastShiftFrameIdentifier = input.Frame.Identity.OverallFrameIdentifier ?? input.Frame.Identity.FrameIdentifier;
        }

        _lastForwardGear = currentGear;
        Snapshot = CreateSnapshot(isActive: _pendingPulse || _remainingPulseFrames > 0, peakLevel: 0f);
    }

    public void UpdateOptions(GearShiftEffectOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Snapshot = CreateSnapshot(_pendingPulse || _remainingPulseFrames > 0, Snapshot.PeakLevel);
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

    private bool CanTrigger(HapticFrame frame)
    {
        if (_lastShiftSessionTime is null)
        {
            return true;
        }

        var currentSessionTime = frame.Identity.SessionTime;
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

    private float CalculatePulseAmplitude(HapticEffectInput input)
    {
        var gain = HapticEffectMath.Clamp(Options.Gain, 0f, 1f);
        if (!Options.ModulateGainByRpm || input.Frame.Signals.EngineRpm is null)
        {
            return gain;
        }

        var rpm = input.Frame.Signals.EngineRpm.Value;
        if (rpm == 0)
        {
            return gain * 0.5f;
        }

        var idleRpm = (ushort?)input.Frame.Signals.IdleRpm ?? Options.DefaultIdleRpm;
        var maxRpm = (ushort?)input.Frame.Signals.MaxRpm ?? Options.DefaultMaxRpm;
        if (idleRpm == 0 || maxRpm <= idleRpm)
        {
            idleRpm = Options.DefaultIdleRpm;
            maxRpm = Options.DefaultMaxRpm;
        }

        var rpmAmount = HapticEffectMath.Clamp((rpm - idleRpm) / (double)(maxRpm - idleRpm), 0.0, 1.0);
        return gain * (float)(0.5 + (0.5 * rpmAmount));
    }

    private bool IsValidForwardGear(sbyte gear, HapticEffectInput input)
    {
        if (Options.DetectionMode != GearShiftDetectionMode.ForwardGearChangesOnly)
        {
            return false;
        }

        var maxGears = input.Frame.Signals.MaxGears;
        var maximumForwardGear = maxGears is > 0 and <= 12 ? (byte)maxGears.Value : (byte)8;
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
