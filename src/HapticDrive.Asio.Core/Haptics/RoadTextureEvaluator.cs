using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Core.Haptics;

public sealed class RoadTextureEvaluator
{
    private readonly object _gate = new();
    private RoadTextureEvaluatorOptions _options;
    private float _smoothedIntensity;
    private long _staleTelemetrySuppressedCount;
    private RoadTextureSignal _lastSignal = RoadTextureSignal.Inactive(DateTimeOffset.UtcNow, "not evaluated");

    public RoadTextureEvaluator(RoadTextureEvaluatorOptions? options = null)
    {
        _options = (options ?? RoadTextureEvaluatorOptions.Default).Normalize();
    }

    public RoadTextureEvaluatorOptions Options
    {
        get
        {
            lock (_gate)
            {
                return _options;
            }
        }
    }

    public long StaleTelemetrySuppressedCount => Interlocked.Read(ref _staleTelemetrySuppressedCount);

    public void Configure(RoadTextureEvaluatorOptions options)
    {
        lock (_gate)
        {
            _options = (options ?? RoadTextureEvaluatorOptions.Default).Normalize();
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _smoothedIntensity = 0f;
            _lastSignal = RoadTextureSignal.Inactive(DateTimeOffset.UtcNow, "reset");
        }
    }

    public RoadTextureSignal GetLastSignal()
    {
        lock (_gate)
        {
            return _lastSignal;
        }
    }

    public RoadTextureSignal Evaluate(
        VehicleState? vehicleState,
        RoadTextureEvaluationContext? context = null)
    {
        context ??= RoadTextureEvaluationContext.Default;

        RoadTextureEvaluatorOptions options;
        lock (_gate)
        {
            options = _options;
        }

        var now = context.NowUtc;
        if (!options.IsEnabled)
        {
            return StoreSuppressed(now, "road disabled", options, context);
        }

        if (!context.HapticsRunning)
        {
            return StoreSuppressed(now, "haptics stopped", options, context);
        }

        if (context.TelemetryStale)
        {
            Interlocked.Increment(ref _staleTelemetrySuppressedCount);
            return StoreSuppressed(now, "telemetry stale", options, context);
        }

        if (!context.DrivingArmed && !context.AllowWhenDrivingNotArmed)
        {
            return StoreSuppressed(now, "driving not armed", options, context);
        }

        if (vehicleState is null)
        {
            return StoreSuppressed(now, "missing vehicle state", options, context);
        }

        if (ShouldMuteForDrivingState(vehicleState))
        {
            return StoreSuppressed(now, "driving state muted", options, context, vehicleState);
        }

        if (!IsFresh(vehicleState, vehicleState.Telemetry, options.MaximumTelemetryFrameLag))
        {
            Interlocked.Increment(ref _staleTelemetrySuppressedCount);
            return StoreSuppressed(now, "telemetry missing or stale", options, context, vehicleState);
        }

        var telemetry = vehicleState.Telemetry!.Value;
        var speedScale = SpeedScale(telemetry.SpeedKph, options.MinimumSpeedKph, options.FullIntensitySpeedKph);
        if (speedScale <= 0f)
        {
            return StoreSuppressed(now, "below road minimum speed", options, context, vehicleState);
        }

        var surface = EvaluateSurface(telemetry.SurfaceTypeIds, options);
        if (surface.SurfaceMix <= 0f)
        {
            return StoreSuppressed(now, "no supported road surface", options, context, vehicleState);
        }

        var motion = EvaluateMotion(vehicleState, options);
        var rawIntensity = Clamp(
            (surface.SurfaceMix + (motion.RoughnessMetric * options.RoughnessContribution)) * speedScale,
            0f,
            options.MaximumIntensity);

        if (surface.SurfaceClass == RoadTextureSurfaceClass.SmoothTrack)
        {
            rawIntensity = Math.Min(rawIntensity, options.SmoothSurfaceFloor + (motion.RoughnessMetric * 0.08f));
        }

        var gearDuckingActive = IsGearDuckingActive(context.LastGearPulseAtUtc, now, options.GearDuckingWindow);
        var duckingGain = gearDuckingActive ? options.GearDuckingGain : 1f;
        var smoothed = Smooth(rawIntensity, options);
        var outputIntensity = Clamp(smoothed * duckingGain, 0f, options.MaximumIntensity);
        var signal = new RoadTextureSignal(
            now,
            vehicleState.Frame.SessionUid,
            vehicleState.Frame.SessionTime,
            vehicleState.Frame.FrameIdentifier,
            vehicleState.Frame.OverallFrameIdentifier,
            options.IsEnabled,
            TelemetryFresh: true,
            context.HapticsRunning,
            context.DrivingArmed || context.AllowWhenDrivingNotArmed,
            telemetry.SpeedKph,
            speedScale,
            telemetry.SurfaceTypeIds,
            motion.SuspensionAcceleration,
            motion.WheelVertForce,
            vehicleState.Motion?.Value.GForceVertical,
            surface.SurfaceClass,
            surface.SurfaceName,
            surface.SurfaceMix,
            rawIntensity,
            smoothed,
            outputIntensity,
            motion.SuspensionAccelerationContribution,
            motion.WheelVertForceContribution,
            motion.VerticalGContribution,
            motion.RoughnessMetric,
            surface.FrequencyHintHz,
            surface.Bst1FrequencyHz,
            surface.PHprFrequencyHz,
            surface.NoiseAmount,
            gearDuckingActive,
            duckingGain,
            SuppressedReason: outputIntensity > 0f ? null : "road intensity zero");
        return Store(signal);
    }

    private RoadTextureSignal StoreSuppressed(
        DateTimeOffset now,
        string reason,
        RoadTextureEvaluatorOptions options,
        RoadTextureEvaluationContext context,
        VehicleState? vehicleState = null)
    {
        _smoothedIntensity = Smooth(0f, options);
        var signal = RoadTextureSignal.Inactive(now, reason) with
        {
            RoadEffectEnabled = options.IsEnabled,
            HapticsRunning = context.HapticsRunning,
            DrivingArmed = context.DrivingArmed || context.AllowWhenDrivingNotArmed,
            TelemetryFresh = !context.TelemetryStale && vehicleState?.Telemetry is not null,
            SessionUid = vehicleState?.Frame.SessionUid,
            SessionTime = vehicleState?.Frame.SessionTime,
            FrameIdentifier = vehicleState?.Frame.FrameIdentifier,
            OverallFrameIdentifier = vehicleState?.Frame.OverallFrameIdentifier,
            SpeedKph = vehicleState?.Telemetry?.Value.SpeedKph ?? 0,
            SurfaceTypeIds = vehicleState?.Telemetry?.Value.SurfaceTypeIds ?? Wheels<byte>(0),
            SuspensionAcceleration = vehicleState?.MotionEx?.Value.SuspensionAcceleration ?? Wheels(0f),
            WheelVertForce = vehicleState?.MotionEx?.Value.WheelVertForce ?? Wheels(0f),
            VerticalG = vehicleState?.Motion?.Value.GForceVertical,
            SmoothedIntensity = _smoothedIntensity,
            GearDuckingActive = IsGearDuckingActive(context.LastGearPulseAtUtc, now, options.GearDuckingWindow),
            DuckingGain = IsGearDuckingActive(context.LastGearPulseAtUtc, now, options.GearDuckingWindow) ? options.GearDuckingGain : 1f
        };
        return Store(signal);
    }

    private RoadTextureSignal Store(RoadTextureSignal signal)
    {
        lock (_gate)
        {
            _lastSignal = signal;
        }

        return signal;
    }

    private float Smooth(float target, RoadTextureEvaluatorOptions options)
    {
        var coefficient = target > _smoothedIntensity
            ? options.AttackSmoothing
            : options.ReleaseSmoothing;
        _smoothedIntensity += (target - _smoothedIntensity) * coefficient;
        _smoothedIntensity = Clamp(_smoothedIntensity, 0f, options.MaximumIntensity);
        return _smoothedIntensity;
    }

    private static SurfaceEvaluation EvaluateSurface(
        VehicleWheelData<byte> surfaceTypeIds,
        RoadTextureEvaluatorOptions options)
    {
        var surfaceMix = 0f;
        var weightedBst1 = 0f;
        var weightedPHpr = 0f;
        var weightedNoise = 0f;
        RoadTextureSurfaceProfile? dominant = null;

        for (var wheel = 0; wheel < 4; wheel++)
        {
            if (!options.SurfaceProfiles.TryGetValue(surfaceTypeIds[wheel], out var profile))
            {
                profile = new RoadTextureSurfaceProfile(
                    surfaceTypeIds[wheel],
                    $"Unknown {surfaceTypeIds[wheel]}",
                    RoadTextureSurfaceClass.Unknown,
                    0.18f,
                    34f,
                    30f,
                    0.18f);
            }

            var gain = Clamp(profile.BaseGain, 0f, 1f);
            surfaceMix += gain;
            weightedBst1 += profile.Bst1FrequencyHz * gain;
            weightedPHpr += profile.PHprFrequencyHz * gain;
            weightedNoise += profile.NoiseAmount * gain;
            if (dominant is null || gain > dominant.BaseGain)
            {
                dominant = profile;
            }
        }

        surfaceMix = Clamp(surfaceMix / 4f, 0f, 1f);
        if (dominant is null || surfaceMix <= 0f)
        {
            return SurfaceEvaluation.Inactive;
        }

        var denominator = Math.Max(0.0001f, surfaceMix * 4f);
        var bst1Frequency = Clamp(weightedBst1 / denominator, 15f, 90f);
        var phprFrequency = Clamp(weightedPHpr / denominator, 1f, 50f);
        return new SurfaceEvaluation(
            surfaceMix,
            dominant.SurfaceClass,
            dominant.Name,
            bst1Frequency,
            phprFrequency,
            (bst1Frequency + phprFrequency) * 0.5f,
            Clamp(weightedNoise / denominator, 0f, 1f));
    }

    private static MotionEvaluation EvaluateMotion(VehicleState vehicleState, RoadTextureEvaluatorOptions options)
    {
        var suspensionAcceleration = vehicleState.MotionEx?.Value.SuspensionAcceleration ?? Wheels(0f);
        var wheelVertForce = vehicleState.MotionEx?.Value.WheelVertForce ?? Wheels(0f);
        var suspensionRoughness = AverageMagnitudeOverThreshold(
            suspensionAcceleration,
            options.SuspensionAccelerationThreshold,
            options.SuspensionAccelerationFullScale);
        var verticalForceRoughness = AverageDeltaOverThreshold(
            wheelVertForce,
            options.WheelVertForceDeltaThreshold,
            options.WheelVertForceDeltaFullScale);
        var verticalG = vehicleState.Motion?.Value.GForceVertical;
        var verticalGRoughness = verticalG is null || !float.IsFinite(verticalG.Value)
            ? 0f
            : Clamp((Math.Abs(verticalG.Value - 1f) - 0.12f) / 1.25f, 0f, 1f);

        return new MotionEvaluation(
            suspensionAcceleration,
            wheelVertForce,
            suspensionRoughness,
            verticalForceRoughness,
            verticalGRoughness,
            Clamp(Math.Max(Math.Max(suspensionRoughness, verticalForceRoughness), verticalGRoughness), 0f, 1f));
    }

    private static float AverageMagnitudeOverThreshold(
        VehicleWheelData<float> values,
        float threshold,
        float fullScale)
    {
        var sum = 0f;
        var count = 0;
        for (var wheel = 0; wheel < 4; wheel++)
        {
            var value = values[wheel];
            if (!float.IsFinite(value))
            {
                continue;
            }

            sum += Clamp((Math.Abs(value) - threshold) / Math.Max(0.0001f, fullScale - threshold), 0f, 1f);
            count++;
        }

        return count == 0 ? 0f : sum / count;
    }

    private static float AverageDeltaOverThreshold(
        VehicleWheelData<float> values,
        float threshold,
        float fullScale)
    {
        var finite = new List<float>(capacity: 4);
        for (var wheel = 0; wheel < 4; wheel++)
        {
            if (float.IsFinite(values[wheel]))
            {
                finite.Add(values[wheel]);
            }
        }

        if (finite.Count == 0)
        {
            return 0f;
        }

        var average = finite.Average();
        var delta = finite.Sum(value => Math.Abs(value - average)) / finite.Count;
        return Clamp((delta - threshold) / Math.Max(0.0001f, fullScale - threshold), 0f, 1f);
    }

    private static bool ShouldMuteForDrivingState(VehicleState vehicleState)
    {
        if (vehicleState.Session?.Value.GamePaused is > 0)
        {
            return true;
        }

        if (vehicleState.CarStatus?.Value.NetworkPaused is > 0)
        {
            return true;
        }

        if (vehicleState.Lap?.Value.DriverStatus == 0)
        {
            return true;
        }

        return vehicleState.Lap?.Value.ResultStatus is 0 or 1;
    }

    private static bool IsFresh<T>(
        VehicleState vehicleState,
        VehicleStateSample<T>? sample,
        uint maximumFrameLag)
    {
        if (sample is null)
        {
            return false;
        }

        var currentFrame = vehicleState.Frame.OverallFrameIdentifier;
        if (currentFrame is null || maximumFrameLag == 0)
        {
            return true;
        }

        var sampleFrame = sample.Stamp.OverallFrameIdentifier;
        if (sampleFrame > currentFrame.Value)
        {
            return true;
        }

        return currentFrame.Value - sampleFrame <= maximumFrameLag;
    }

    private static bool IsGearDuckingActive(
        DateTimeOffset? lastGearPulseAtUtc,
        DateTimeOffset nowUtc,
        TimeSpan duckingWindow)
    {
        return lastGearPulseAtUtc is not null
            && duckingWindow > TimeSpan.Zero
            && nowUtc >= lastGearPulseAtUtc.Value
            && nowUtc - lastGearPulseAtUtc.Value <= duckingWindow;
    }

    private static float SpeedScale(float speedKph, float minimumSpeedKph, float fullSpeedKph)
    {
        if (!float.IsFinite(speedKph) || speedKph <= minimumSpeedKph)
        {
            return 0f;
        }

        if (!float.IsFinite(fullSpeedKph) || fullSpeedKph <= minimumSpeedKph)
        {
            return 1f;
        }

        return Clamp((speedKph - minimumSpeedKph) / (fullSpeedKph - minimumSpeedKph), 0f, 1f);
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        if (!float.IsFinite(value))
        {
            return minimum;
        }

        return Math.Clamp(value, minimum, maximum);
    }

    private static VehicleWheelData<T> Wheels<T>(T value)
    {
        return new VehicleWheelData<T>(value, value, value, value);
    }

    private sealed record SurfaceEvaluation(
        float SurfaceMix,
        RoadTextureSurfaceClass SurfaceClass,
        string SurfaceName,
        float Bst1FrequencyHz,
        float PHprFrequencyHz,
        float FrequencyHintHz,
        float NoiseAmount)
    {
        public static SurfaceEvaluation Inactive { get; } = new(
            0f,
            RoadTextureSurfaceClass.None,
            "None",
            0f,
            0f,
            0f,
            0f);
    }

    private sealed record MotionEvaluation(
        VehicleWheelData<float> SuspensionAcceleration,
        VehicleWheelData<float> WheelVertForce,
        float SuspensionAccelerationContribution,
        float WheelVertForceContribution,
        float VerticalGContribution,
        float RoughnessMetric);
}
