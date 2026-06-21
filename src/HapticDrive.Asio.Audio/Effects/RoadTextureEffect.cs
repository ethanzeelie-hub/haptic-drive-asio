using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class RoadTextureEffect : IHapticEffectSource
{
    private readonly RoadTextureEvaluator _evaluator;
    private double _basePhase;
    private long _frameCursor;
    private int _grainHoldFramesRemaining;
    private float _heldNoiseSample;
    private DateTimeOffset? _lastGearPulseAtUtc;
    private RoadTextureSignal _signal;

    public RoadTextureEffect()
        : this(RoadTextureEffectOptions.Default)
    {
    }

    public RoadTextureEffect(RoadTextureEffectOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _evaluator = new RoadTextureEvaluator(Options.ToEvaluatorOptions());
        _signal = RoadTextureSignal.Inactive(DateTimeOffset.UtcNow, "not evaluated");
        Snapshot = CreateSnapshot(_signal, peakLevel: 0f, rmsLevel: 0f);
    }

    public string Name => "Road texture";

    public RoadTextureEffectOptions Options { get; }

    public RoadTextureEffectSnapshot Snapshot { get; private set; }

    public RoadTextureSignal CurrentSignal => _signal;

    public void Reset()
    {
        _basePhase = 0.0;
        _frameCursor = 0;
        _grainHoldFramesRemaining = 0;
        _heldNoiseSample = 0f;
        _lastGearPulseAtUtc = null;
        _evaluator.Reset();
        _signal = RoadTextureSignal.Inactive(DateTimeOffset.UtcNow, "reset");
        Snapshot = CreateSnapshot(_signal, peakLevel: 0f, rmsLevel: 0f);
    }

    public void NotifyGearPulseAccepted(DateTimeOffset? timestampUtc = null)
    {
        _lastGearPulseAtUtc = timestampUtc ?? DateTimeOffset.UtcNow;
    }

    public void Update(HapticEffectInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _signal = _evaluator.Evaluate(
            input.VehicleState,
            new RoadTextureEvaluationContext(
                DateTimeOffset.UtcNow,
                HapticsRunning: true,
                DrivingArmed: true,
                AllowWhenDrivingNotArmed: true,
                TelemetryStale: false,
                _lastGearPulseAtUtc));
        Snapshot = CreateSnapshot(_signal, peakLevel: 0f, rmsLevel: 0f);
    }

    public void Update(VehicleState vehicleState)
    {
        Update(LegacyHapticEffectInputFactory.FromVehicleState(vehicleState));
    }

    public HapticEffectRenderResult Render(AudioSampleBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Clear();

        if (!Options.IsEnabled || !Options.Bst1OutputEnabled || !_signal.IsActive)
        {
            Snapshot = CreateSnapshot(_signal, peakLevel: 0f, rmsLevel: 0f);
            return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: false, PeakLevel: 0f);
        }

        var frequencyHz = float.IsFinite(_signal.Bst1FrequencyHz) && _signal.Bst1FrequencyHz > 0f
            ? _signal.Bst1FrequencyHz
            : 0f;
        var noiseAmount = HapticEffectMath.Clamp(_signal.NoiseAmount, 0f, 1f);
        var toneAmount = 1f - noiseAmount;
        var grainDensityHz = Math.Max(
            6f,
            frequencyHz * (0.70f + (_signal.SpeedScale * 1.50f) + (noiseAmount * 0.80f)));
        var amplitude = HapticEffectMath.Clamp(
            _signal.OutputIntensity * Options.Gain,
            0f,
            Options.MaximumAmplitude);

        for (var frame = 0; frame < destination.FrameCount; frame++)
        {
            var tone = Math.Sin(_basePhase) * toneAmount;
            if (_grainHoldFramesRemaining <= 0)
            {
                _heldNoiseSample = (float)HapticEffectMath.DeterministicSignedUnitNoise(
                    _frameCursor,
                    _signal.DominantSurfaceTypeId ?? 0);
                _grainHoldFramesRemaining = Math.Max(
                    1,
                    (int)Math.Round(destination.SampleRate / Math.Max(1f, grainDensityHz)));
            }

            var noise = _heldNoiseSample * noiseAmount;
            var sample = (float)((tone + noise) * amplitude);
            HapticEffectMath.WriteMonoFrame(destination, frame, sample);

            _basePhase = HapticEffectMath.AdvancePhase(_basePhase, frequencyHz, destination.SampleRate);
            _frameCursor++;
            _grainHoldFramesRemaining--;
        }

        var peak = HapticEffectMath.CalculatePeak(destination);
        var rms = CalculateRms(destination);
        Snapshot = CreateSnapshot(_signal, peak, rms);
        return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: peak > 0f, peak);
    }

    private RoadTextureEffectSnapshot CreateSnapshot(
        RoadTextureSignal signal,
        float peakLevel,
        float rmsLevel)
    {
        return new RoadTextureEffectSnapshot(
            Options.IsEnabled,
            Options.Bst1OutputEnabled,
            Options.Bst1OutputEnabled && signal.IsActive && peakLevel > 0f,
            signal.SurfaceClass == RoadTextureSurfaceClass.None ? null : signal.DominantSurfaceTypeId,
            signal.SurfaceName,
            signal.Bst1FrequencyHz,
            Options.Bst1OutputEnabled ? signal.OutputIntensity * Options.Gain : 0f,
            signal.SurfaceMix,
            peakLevel,
            signal,
            rmsLevel);
    }

    private static float CalculateRms(AudioSampleBuffer buffer)
    {
        if (buffer.SampleCount <= 0)
        {
            return 0f;
        }

        var sum = 0d;
        var count = 0;
        foreach (var sample in buffer.Samples)
        {
            if (!float.IsFinite(sample))
            {
                continue;
            }

            sum += sample * sample;
            count++;
        }

        return count == 0 ? 0f : (float)Math.Sqrt(sum / count);
    }
}
