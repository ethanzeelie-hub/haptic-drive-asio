using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class RoadTextureEffect : IHapticEffectSource
{
    private double _basePhase;
    private long _frameCursor;
    private float _smoothedAmplitude;
    private RoadTextureEvaluation _evaluation = RoadTextureEvaluation.Inactive;

    public RoadTextureEffect()
        : this(RoadTextureEffectOptions.Default)
    {
    }

    public RoadTextureEffect(RoadTextureEffectOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Snapshot = CreateSnapshot(RoadTextureEvaluation.Inactive, peakLevel: 0f);
    }

    public string Name => "Road texture";

    public RoadTextureEffectOptions Options { get; }

    public RoadTextureEffectSnapshot Snapshot { get; private set; }

    public void Reset()
    {
        _basePhase = 0.0;
        _frameCursor = 0;
        _smoothedAmplitude = 0f;
        _evaluation = RoadTextureEvaluation.Inactive;
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

        var frequencyHz = float.IsFinite(_evaluation.FrequencyHz) && _evaluation.FrequencyHz > 0f
            ? _evaluation.FrequencyHz
            : 0f;
        var noiseAmount = HapticEffectMath.Clamp(_evaluation.NoiseAmount, 0f, 1f);
        var toneAmount = 1f - noiseAmount;
        var smoothing = HapticEffectMath.SmoothingCoefficient(destination.SampleRate, Options.ResponseSmoothingTime);

        for (var frame = 0; frame < destination.FrameCount; frame++)
        {
            _smoothedAmplitude += (_evaluation.Amplitude - _smoothedAmplitude) * smoothing;

            var tone = Math.Sin(_basePhase) * toneAmount;
            var noise = HapticEffectMath.DeterministicSignedUnitNoise(
                _frameCursor,
                _evaluation.DominantSurfaceTypeId ?? 0) * noiseAmount;
            var sample = (float)((tone + noise) * _smoothedAmplitude);
            HapticEffectMath.WriteMonoFrame(destination, frame, sample);

            _basePhase = HapticEffectMath.AdvancePhase(_basePhase, frequencyHz, destination.SampleRate);
            _frameCursor++;
        }

        var peak = HapticEffectMath.CalculatePeak(destination);
        Snapshot = CreateSnapshot(_evaluation, peak);
        return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: peak > 0f, peak);
    }

    private static RoadTextureEvaluation Evaluate(VehicleState vehicleState, RoadTextureEffectOptions options)
    {
        if (!options.IsEnabled
            || VehicleStateEffectGuards.ShouldMuteForDrivingState(vehicleState)
            || !VehicleStateEffectGuards.IsFresh(vehicleState, vehicleState.Telemetry, options.MaximumTelemetryFrameLag))
        {
            return RoadTextureEvaluation.Inactive;
        }

        var telemetry = vehicleState.Telemetry!.Value;
        var speedScale = HapticEffectMath.SpeedScale(
            telemetry.SpeedKph,
            options.MinimumSpeedKph,
            options.FullIntensitySpeedKph);
        if (speedScale <= 0f)
        {
            return RoadTextureEvaluation.Inactive;
        }

        var profileCount = 0;
        var surfaceMix = 0f;
        var weightedFrequency = 0f;
        var weightedNoise = 0f;
        RoadTextureSurfaceProfile? dominantProfile = null;

        for (var wheel = 0; wheel < 4; wheel++)
        {
            var surfaceTypeId = telemetry.SurfaceTypeIds[wheel];
            if (!options.SurfaceProfiles.TryGetValue(surfaceTypeId, out var profile)
                || profile.GainMultiplier <= 0f)
            {
                continue;
            }

            profileCount++;
            surfaceMix += profile.GainMultiplier;
            weightedFrequency += profile.BaseFrequencyHz * profile.GainMultiplier;
            weightedNoise += profile.NoiseAmount * profile.GainMultiplier;

            if (dominantProfile is null || profile.GainMultiplier > dominantProfile.GainMultiplier)
            {
                dominantProfile = profile;
            }
        }

        if (profileCount == 0 || dominantProfile is null || surfaceMix <= 0f)
        {
            return RoadTextureEvaluation.Inactive;
        }

        surfaceMix /= 4f;
        var frequencyHz = weightedFrequency / Math.Max(0.0001f, surfaceMix * 4f);
        var noiseAmount = weightedNoise / Math.Max(0.0001f, surfaceMix * 4f);
        var motionMultiplier = ResolveMotionMultiplier(vehicleState, options);
        var amplitude = options.Gain * speedScale * surfaceMix * motionMultiplier;
        amplitude = HapticEffectMath.Clamp(amplitude, 0f, options.MaximumAmplitude);

        if (amplitude <= 0f)
        {
            return RoadTextureEvaluation.Inactive;
        }

        return new RoadTextureEvaluation(
            IsActive: true,
            dominantProfile.SurfaceTypeId,
            dominantProfile.Name,
            frequencyHz,
            amplitude,
            surfaceMix,
            HapticEffectMath.Clamp(noiseAmount, 0f, 1f));
    }

    private static float ResolveMotionMultiplier(VehicleState vehicleState, RoadTextureEffectOptions options)
    {
        var multiplier = 1f;

        if (VehicleStateEffectGuards.IsFresh(vehicleState, vehicleState.MotionEx, options.MaximumTelemetryFrameLag))
        {
            var suspensionMotion = VehicleStateEffectGuards.CalculateWheelAverage(
                vehicleState.MotionEx!.Value.SuspensionVelocity,
                value => float.IsFinite(value) ? Math.Abs(value) : null);
            multiplier += HapticEffectMath.Clamp(
                suspensionMotion / 8f,
                0f,
                options.SuspensionMotionGain);
        }

        if (VehicleStateEffectGuards.IsFresh(vehicleState, vehicleState.Motion, options.MaximumTelemetryFrameLag))
        {
            var verticalGDeviation = Math.Abs(vehicleState.Motion!.Value.GForceVertical - 1f);
            if (float.IsFinite(verticalGDeviation))
            {
                multiplier += HapticEffectMath.Clamp(
                    verticalGDeviation / 2f,
                    0f,
                    options.VerticalGDeviationGain);
            }
        }

        return HapticEffectMath.Clamp(multiplier, 0.5f, 1.5f);
    }

    private RoadTextureEffectSnapshot CreateSnapshot(RoadTextureEvaluation evaluation, float peakLevel)
    {
        return new RoadTextureEffectSnapshot(
            Options.IsEnabled,
            evaluation.IsActive,
            evaluation.DominantSurfaceTypeId,
            evaluation.DominantSurfaceName,
            evaluation.FrequencyHz,
            evaluation.Amplitude,
            evaluation.SurfaceMix,
            peakLevel);
    }

    private sealed record RoadTextureEvaluation(
        bool IsActive,
        byte? DominantSurfaceTypeId,
        string DominantSurfaceName,
        float FrequencyHz,
        float Amplitude,
        float SurfaceMix,
        float NoiseAmount)
    {
        public static RoadTextureEvaluation Inactive { get; } = new(
            IsActive: false,
            DominantSurfaceTypeId: null,
            DominantSurfaceName: "None",
            FrequencyHz: 0f,
            Amplitude: 0f,
            SurfaceMix: 0f,
            NoiseAmount: 0f);
    }
}
