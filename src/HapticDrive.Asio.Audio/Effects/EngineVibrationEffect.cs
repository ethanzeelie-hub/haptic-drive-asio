using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class EngineVibrationEffect : IHapticEffectSource, IConfigurableHapticEffectSource<EngineVibrationEffectOptions>
{
    private double _basePhase;
    private double _highPhase;
    private long _frameCursor;
    private EngineEvaluation _evaluation = EngineEvaluation.Inactive;

    public EngineVibrationEffect()
        : this(EngineVibrationEffectOptions.Default)
    {
    }

    public EngineVibrationEffect(EngineVibrationEffectOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Snapshot = new EngineVibrationEffectSnapshot(
            Options.IsEnabled,
            IsActive: false,
            LastRpm: null,
            LastThrottle: 0f,
            CurrentFrequencyHz: 0f,
            CurrentAmplitude: 0f,
            PeakLevel: 0f);
    }

    public string Name => "Engine vibration";

    public EngineVibrationEffectOptions Options { get; private set; }

    public EngineVibrationEffectSnapshot Snapshot { get; private set; }

    public void Reset()
    {
        _basePhase = 0.0;
        _highPhase = 0.0;
        _frameCursor = 0;
        _evaluation = EngineEvaluation.Inactive;
        Snapshot = Snapshot with
        {
            IsActive = false,
            CurrentFrequencyHz = 0f,
            CurrentAmplitude = 0f,
            PeakLevel = 0f
        };
    }

    public void Update(HapticEffectInput input)
    {
        _evaluation = Evaluate(input, Options);
        Snapshot = new EngineVibrationEffectSnapshot(
            Options.IsEnabled,
            _evaluation.IsActive,
            _evaluation.Rpm,
            _evaluation.Throttle,
            _evaluation.FrequencyHz,
            _evaluation.Amplitude,
            PeakLevel: 0f);
    }

    public void UpdateOptions(EngineVibrationEffectOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Snapshot = Snapshot with { IsEnabled = Options.IsEnabled };
    }

    public HapticEffectRenderResult Render(AudioSampleBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Clear();

        if (!Options.IsEnabled || !_evaluation.IsActive)
        {
            Snapshot = Snapshot with { IsActive = false, PeakLevel = 0f };
            return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: false, PeakLevel: 0f);
        }

        var baseFrequencyHz = Math.Max(0.0, _evaluation.FrequencyHz);
        var highFrequencyHz = SanitizeFrequency(Options.HighFrequencyHz);
        var highGain = Options.HighFrequencyEnabled
            ? HapticEffectMath.Clamp(Options.HighFrequencyGain, 0f, 1f)
            : 0f;
        var normalizer = 1.0 + highGain;

        for (var frame = 0; frame < destination.FrameCount; frame++)
        {
            var jitterHz = CalculateDeterministicJitterHz(_frameCursor, Options.FrequencyJitterHz);
            var effectiveBaseFrequencyHz = Math.Max(0.0, baseFrequencyHz + jitterHz);

            var sample = Math.Sin(_basePhase);
            if (highGain > 0f)
            {
                sample += highGain * Math.Sin(_highPhase);
            }

            var scaledSample = (float)((sample / normalizer) * _evaluation.Amplitude);
            HapticEffectMath.WriteMonoFrame(destination, frame, scaledSample);

            _basePhase = AdvancePhase(_basePhase, effectiveBaseFrequencyHz, destination.SampleRate);
            _highPhase = AdvancePhase(_highPhase, highFrequencyHz, destination.SampleRate);
            _frameCursor++;
        }

        var peak = HapticEffectMath.CalculatePeak(destination);
        Snapshot = Snapshot with { IsActive = true, PeakLevel = peak };
        return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: true, peak);
    }

    private static EngineEvaluation Evaluate(HapticEffectInput input, EngineVibrationEffectOptions options)
    {
        if (!options.IsEnabled || input.Frame.Signals.EngineRpm is null)
        {
            return EngineEvaluation.Inactive;
        }

        var rpm = (ushort)Math.Max(0, input.Frame.Signals.EngineRpm.Value);
        if (rpm == 0 || rpm > options.MaximumAllowedRpm)
        {
            return EngineEvaluation.Inactive with { Rpm = rpm };
        }

        if (HapticFrameEffectGuards.ShouldMuteForDrivingState(input))
        {
            return EngineEvaluation.Inactive with { Rpm = rpm, Throttle = SanitizeThrottle(input.Frame.Signals.Throttle ?? 0f) };
        }

        var throttle = SanitizeThrottle(input.Frame.Signals.Throttle ?? 0f);
        var idleRpm = ResolveIdleRpm(input, options);
        var maxRpm = ResolveMaxRpm(input, options, idleRpm);
        var rpmAmount = HapticEffectMath.Clamp((rpm - idleRpm) / (double)(maxRpm - idleRpm), 0.0, 1.0);
        var minimumFrequencyHz = SanitizeFrequency(options.MinimumFrequencyHz);
        var maximumFrequencyHz = Math.Max(minimumFrequencyHz, SanitizeFrequency(options.MaximumFrequencyHz));
        var frequencyHz = HapticEffectMath.Lerp(minimumFrequencyHz, maximumFrequencyHz, rpmAmount);
        var gain = HapticEffectMath.Clamp(options.Gain, 0f, 1f);
        var idleThrottleGain = HapticEffectMath.Clamp(options.IdleThrottleGain, 0f, 1f);
        var throttleAmount = idleThrottleGain + ((1f - idleThrottleGain) * throttle);
        var rpmGain = 0.5 + (0.5 * rpmAmount);
        var pitMultiplier = IsInPit(input.Frame)
            ? HapticEffectMath.Clamp(options.PitGainMultiplier, 0f, 1f)
            : 1f;
        var amplitude = (float)(gain * throttleAmount * rpmGain * pitMultiplier);

        if (amplitude <= 0f)
        {
            return EngineEvaluation.Inactive with
            {
                Rpm = rpm,
                Throttle = throttle,
                FrequencyHz = (float)frequencyHz
            };
        }

        return new EngineEvaluation(
            IsActive: true,
            rpm,
            throttle,
            (float)frequencyHz,
            amplitude);
    }

    private static bool IsInPit(HapticFrame frame)
    {
        return frame.Context.PitState is PitState.Pitting or PitState.InPitArea;
    }

    private static float SanitizeThrottle(float throttle)
    {
        return HapticEffectMath.Clamp(throttle, 0f, 1f);
    }

    private static ushort ResolveIdleRpm(HapticEffectInput input, EngineVibrationEffectOptions options)
    {
        var idleRpm = (ushort?)input.Frame.Signals.IdleRpm ?? options.DefaultIdleRpm;
        if (idleRpm == 0 || idleRpm >= options.MaximumAllowedRpm)
        {
            return options.DefaultIdleRpm;
        }

        return idleRpm;
    }

    private static ushort ResolveMaxRpm(
        HapticEffectInput input,
        EngineVibrationEffectOptions options,
        ushort idleRpm)
    {
        var maxRpm = (ushort?)input.Frame.Signals.MaxRpm ?? options.DefaultMaxRpm;
        if (maxRpm <= idleRpm || maxRpm > options.MaximumAllowedRpm)
        {
            maxRpm = options.DefaultMaxRpm;
        }

        if (maxRpm <= idleRpm)
        {
            maxRpm = (ushort)Math.Min(options.MaximumAllowedRpm, idleRpm + 1_000);
        }

        return maxRpm;
    }

    private static float SanitizeFrequency(float frequencyHz)
    {
        return float.IsFinite(frequencyHz) && frequencyHz > 0f ? frequencyHz : 0f;
    }

    private static double AdvancePhase(double phase, double frequencyHz, int sampleRate)
    {
        phase += HapticEffectMath.TwoPi * frequencyHz / sampleRate;
        if (phase >= HapticEffectMath.TwoPi)
        {
            phase %= HapticEffectMath.TwoPi;
        }

        return phase;
    }

    private static double CalculateDeterministicJitterHz(long frameCursor, float jitterRangeHz)
    {
        if (!float.IsFinite(jitterRangeHz) || jitterRangeHz <= 0f)
        {
            return 0.0;
        }

        var unit = DeterministicNoise(frameCursor);
        return unit * jitterRangeHz;
    }

    private static double DeterministicNoise(long frameCursor)
    {
        unchecked
        {
            var value = (ulong)frameCursor + 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return ((value >> 11) * (1.0 / (1UL << 53)) * 2.0) - 1.0;
        }
    }

    private readonly record struct EngineEvaluation(
        bool IsActive,
        ushort? Rpm,
        float Throttle,
        float FrequencyHz,
        float Amplitude)
    {
        public static EngineEvaluation Inactive { get; } = new(
            IsActive: false,
            Rpm: null,
            Throttle: 0f,
            FrequencyHz: 0f,
            Amplitude: 0f);
    }
}
