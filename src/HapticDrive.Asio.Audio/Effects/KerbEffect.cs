using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class KerbEffect : IHapticEffectSource
{
    private double _basePhase;
    private double _highPhase;
    private long _frameCursor;
    private float _smoothedAmplitude;
    private KerbEvaluation _evaluation = KerbEvaluation.Inactive;

    public KerbEffect()
        : this(KerbEffectOptions.Default)
    {
    }

    public KerbEffect(KerbEffectOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Snapshot = CreateSnapshot(KerbEvaluation.Inactive, peakLevel: 0f);
    }

    public string Name => "Kerb";

    public KerbEffectOptions Options { get; }

    public KerbEffectSnapshot Snapshot { get; private set; }

    public void Reset()
    {
        _basePhase = 0.0;
        _highPhase = 0.0;
        _frameCursor = 0;
        _smoothedAmplitude = 0f;
        _evaluation = KerbEvaluation.Inactive;
        Snapshot = CreateSnapshot(_evaluation, peakLevel: 0f);
    }

    public void Update(VehicleState vehicleState)
    {
        ArgumentNullException.ThrowIfNull(vehicleState);
        _evaluation = Evaluate(vehicleState, Options);
        Snapshot = CreateSnapshot(_evaluation, peakLevel: 0f);
    }

    public HapticEffectRenderResult Render(AudioSampleBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Clear();

        if (!Options.IsEnabled || !_evaluation.IsActive)
        {
            _smoothedAmplitude = 0f;
            Snapshot = CreateSnapshot(_evaluation with { IsActive = false }, peakLevel: 0f);
            return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: false, PeakLevel: 0f);
        }

        var baseFrequencyHz = SanitizeFrequency(_evaluation.FrequencyHz);
        var highFrequencyHz = SanitizeFrequency(Options.HighFrequencyHz);
        var highGain = Options.HighFrequencyEnabled
            ? HapticEffectMath.Clamp(Options.HighFrequencyGain, 0f, 1f)
            : 0f;
        var noiseAmount = HapticEffectMath.Clamp(Options.NoiseAmount, 0f, 1f);
        var normalizer = 1.0 + highGain + noiseAmount;
        var smoothing = HapticEffectMath.SmoothingCoefficient(destination.SampleRate, Options.ResponseSmoothingTime);

        for (var frame = 0; frame < destination.FrameCount; frame++)
        {
            _smoothedAmplitude += (_evaluation.Amplitude - _smoothedAmplitude) * smoothing;

            var sample = Math.Sin(_basePhase);
            if (highGain > 0f)
            {
                sample += highGain * Math.Sin(_highPhase);
            }

            if (noiseAmount > 0f)
            {
                sample += noiseAmount * HapticEffectMath.DeterministicSignedUnitNoise(
                    _frameCursor,
                    _evaluation.DominantSurfaceTypeId ?? 0);
            }

            HapticEffectMath.WriteMonoFrame(destination, frame, (float)((sample / normalizer) * _smoothedAmplitude));

            _basePhase = HapticEffectMath.AdvancePhase(_basePhase, baseFrequencyHz, destination.SampleRate);
            _highPhase = HapticEffectMath.AdvancePhase(_highPhase, highFrequencyHz, destination.SampleRate);
            _frameCursor++;
        }

        var peak = HapticEffectMath.CalculatePeak(destination);
        Snapshot = CreateSnapshot(_evaluation, peak);
        return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: peak > 0f, peak);
    }

    private static KerbEvaluation Evaluate(VehicleState vehicleState, KerbEffectOptions options)
    {
        if (!options.IsEnabled
            || VehicleStateEffectGuards.ShouldMuteForDrivingState(vehicleState)
            || !VehicleStateEffectGuards.IsFresh(vehicleState, vehicleState.Telemetry, options.MaximumTelemetryFrameLag))
        {
            return KerbEvaluation.Inactive;
        }

        var telemetry = vehicleState.Telemetry!.Value;
        var speedScale = HapticEffectMath.SpeedScale(
            telemetry.SpeedKph,
            options.MinimumSpeedKph,
            options.FullIntensitySpeedKph);
        if (speedScale <= 0f)
        {
            return KerbEvaluation.Inactive;
        }

        var activeWheelCount = 0;
        byte? dominantSurfaceTypeId = null;
        for (var wheel = 0; wheel < 4; wheel++)
        {
            var surfaceTypeId = telemetry.SurfaceTypeIds[wheel];
            if (!IsKerbSurface(surfaceTypeId))
            {
                continue;
            }

            activeWheelCount++;
            dominantSurfaceTypeId ??= surfaceTypeId;
        }

        if (activeWheelCount == 0 || dominantSurfaceTypeId is null)
        {
            return KerbEvaluation.Inactive;
        }

        var contactMultiplier = ResolveContactMultiplier(vehicleState, options);
        var wheelMultiplier = 0.55f + (0.45f * (activeWheelCount / 4f));
        var amplitude = options.Gain * speedScale * wheelMultiplier * contactMultiplier;
        amplitude = HapticEffectMath.Clamp(amplitude, 0f, options.MaximumAmplitude);

        if (amplitude <= 0f)
        {
            return KerbEvaluation.Inactive;
        }

        return new KerbEvaluation(
            IsActive: true,
            dominantSurfaceTypeId,
            VehicleSurfaceTypes.GetName(dominantSurfaceTypeId.Value),
            SanitizeFrequency(options.BaseFrequencyHz),
            amplitude,
            activeWheelCount);
    }

    private static bool IsKerbSurface(byte surfaceTypeId)
    {
        return surfaceTypeId is VehicleSurfaceTypes.RumbleStrip or VehicleSurfaceTypes.Ridged;
    }

    private static float ResolveContactMultiplier(VehicleState vehicleState, KerbEffectOptions options)
    {
        if (!VehicleStateEffectGuards.IsFresh(vehicleState, vehicleState.MotionEx, options.MaximumTelemetryFrameLag))
        {
            return 1f;
        }

        var motionEx = vehicleState.MotionEx!.Value;
        var verticalForce = VehicleStateEffectGuards.CalculateWheelAverage(
            motionEx.WheelVertForce,
            value => value >= 0f && value <= 100_000f ? value : null);
        var suspensionVelocity = VehicleStateEffectGuards.CalculateWheelAverage(
            motionEx.SuspensionVelocity,
            value => float.IsFinite(value) ? Math.Abs(value) : null);
        var suspensionAcceleration = VehicleStateEffectGuards.CalculateWheelAverage(
            motionEx.SuspensionAcceleration,
            value => float.IsFinite(value) ? Math.Abs(value) : null);

        var forceComponent = verticalForce <= 0f
            ? 0.6f
            : HapticEffectMath.Clamp(verticalForce / 12_000f, 0.25f, 1f);
        var velocityComponent = HapticEffectMath.Clamp(suspensionVelocity / 6f, 0f, 0.25f);
        var accelerationComponent = HapticEffectMath.Clamp(suspensionAcceleration / 80f, 0f, 0.2f);

        return HapticEffectMath.Clamp(0.65f + (0.25f * forceComponent) + velocityComponent + accelerationComponent, 0.4f, 1.2f);
    }

    private static float SanitizeFrequency(float frequencyHz)
    {
        return float.IsFinite(frequencyHz) && frequencyHz > 0f ? frequencyHz : 0f;
    }

    private KerbEffectSnapshot CreateSnapshot(KerbEvaluation evaluation, float peakLevel)
    {
        return new KerbEffectSnapshot(
            Options.IsEnabled,
            evaluation.IsActive,
            evaluation.DominantSurfaceTypeId,
            evaluation.DominantSurfaceName,
            evaluation.FrequencyHz,
            evaluation.Amplitude,
            evaluation.ActiveWheelCount,
            peakLevel);
    }

    private sealed record KerbEvaluation(
        bool IsActive,
        byte? DominantSurfaceTypeId,
        string DominantSurfaceName,
        float FrequencyHz,
        float Amplitude,
        int ActiveWheelCount)
    {
        public static KerbEvaluation Inactive { get; } = new(
            IsActive: false,
            DominantSurfaceTypeId: null,
            DominantSurfaceName: "None",
            FrequencyHz: 0f,
            Amplitude: 0f,
            ActiveWheelCount: 0);
    }
}
