using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class RoadTextureEffect : IHapticEffectSource
{
    private readonly RoadTextureEvaluator _evaluator;
    private double _basePhase;
    private long _frameCursor;
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
        _lastGearPulseAtUtc = null;
        _evaluator.Reset();
        _signal = RoadTextureSignal.Inactive(DateTimeOffset.UtcNow, "reset");
        Snapshot = CreateSnapshot(_signal, peakLevel: 0f, rmsLevel: 0f);
    }

    public void NotifyGearPulseAccepted(DateTimeOffset? timestampUtc = null)
    {
        _lastGearPulseAtUtc = timestampUtc ?? DateTimeOffset.UtcNow;
    }

    public void Update(VehicleState vehicleState)
    {
        ArgumentNullException.ThrowIfNull(vehicleState);
        _signal = _evaluator.Evaluate(
            vehicleState,
            new RoadTextureEvaluationContext(
                DateTimeOffset.UtcNow,
                HapticsRunning: true,
                DrivingArmed: true,
                AllowWhenDrivingNotArmed: true,
                TelemetryStale: false,
                _lastGearPulseAtUtc));
        Snapshot = CreateSnapshot(_signal, peakLevel: 0f, rmsLevel: 0f);
    }

    public HapticEffectRenderResult Render(AudioSampleBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Clear();

        if (!Options.IsEnabled || !_signal.IsActive)
        {
            Snapshot = CreateSnapshot(_signal, peakLevel: 0f, rmsLevel: 0f);
            return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: false, PeakLevel: 0f);
        }

        var frequencyHz = float.IsFinite(_signal.Bst1FrequencyHz) && _signal.Bst1FrequencyHz > 0f
            ? _signal.Bst1FrequencyHz
            : 0f;
        var noiseAmount = HapticEffectMath.Clamp(_signal.NoiseAmount, 0f, 1f);
        var toneAmount = 1f - noiseAmount;
        var amplitude = HapticEffectMath.Clamp(
            _signal.OutputIntensity * Options.Gain,
            0f,
            Options.MaximumAmplitude);

        for (var frame = 0; frame < destination.FrameCount; frame++)
        {
            var tone = Math.Sin(_basePhase) * toneAmount;
            var noise = HapticEffectMath.DeterministicSignedUnitNoise(
                _frameCursor,
                _signal.SurfaceTypeIds.RearLeft) * noiseAmount;
            var sample = (float)((tone + noise) * amplitude);
            HapticEffectMath.WriteMonoFrame(destination, frame, sample);

            _basePhase = HapticEffectMath.AdvancePhase(_basePhase, frequencyHz, destination.SampleRate);
            _frameCursor++;
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
            signal.IsActive,
            signal.SurfaceClass == RoadTextureSurfaceClass.None ? null : signal.SurfaceTypeIds.RearLeft,
            signal.SurfaceName,
            signal.Bst1FrequencyHz,
            signal.OutputIntensity * Options.Gain,
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
