using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Core.Vehicle.Freshness;

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
        HapticRenderFrame renderFrame,
        RoadTextureEvaluationContext? context = null)
    {
        context ??= RoadTextureEvaluationContext.Default;

        RoadTextureEvaluatorOptions options;
        lock (_gate)
        {
            options = _options;
        }

        var now = context.NowUtc;
        var frame = renderFrame.Frame;
        if (!options.IsEnabled)
        {
            return StoreSuppressed(now, "road disabled", options, context, renderFrame);
        }

        if (!context.HapticsRunning)
        {
            return StoreSuppressed(now, "haptics stopped", options, context, renderFrame);
        }

        if (context.TelemetryStale)
        {
            Interlocked.Increment(ref _staleTelemetrySuppressedCount);
            return StoreSuppressed(now, "telemetry stale", options, context, renderFrame);
        }

        if (!context.DrivingArmed && !context.AllowWhenDrivingNotArmed)
        {
            return StoreSuppressed(now, "driving not armed", options, context, renderFrame);
        }

        if (frame.Context.IsPaused || !frame.Context.IsPlayerControlled || !frame.Context.AllowsDrivingOutput)
        {
            return StoreSuppressed(now, "driving state muted", options, context, renderFrame);
        }

        if (!renderFrame.Freshness.Telemetry.IsFresh)
        {
            Interlocked.Increment(ref _staleTelemetrySuppressedCount);
            return StoreSuppressed(now, "telemetry missing or stale", options, context, renderFrame);
        }

        if (frame.Signals.SurfaceTypeIds is null || frame.Signals.SpeedMetersPerSecond is null)
        {
            return StoreSuppressed(now, "missing canonical telemetry", options, context, renderFrame);
        }

        var speedKph = (ushort)Math.Clamp(
            (int)Math.Round(frame.Signals.SpeedMetersPerSecond.Value * 3.6f, MidpointRounding.AwayFromZero),
            0,
            ushort.MaxValue);
        var speedProgress = SpeedProgress(speedKph, options.MinimumSpeedKph, options.FullIntensitySpeedKph);
        if (speedProgress <= 0f)
        {
            return StoreSuppressed(now, "below road minimum speed", options, context, renderFrame);
        }

        var speedScale = SpeedScale(speedProgress);
        var surface = EvaluateSurface(frame.Signals.SurfaceTypeIds, options);
        if (surface.SurfaceMix <= 0f)
        {
            return StoreSuppressed(now, "no supported road surface", options, context, renderFrame);
        }

        var motion = EvaluateMotion(renderFrame, options);
        var bst1FrequencyHz = EvaluateBst1Frequency(surface.Bst1FrequencyHz, speedProgress, options);
        var noiseAmount = EvaluateNoiseAmount(surface.NoiseAmount, speedProgress, motion.RoughnessMetric, options);
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
            frame.Identity.SessionUid,
            frame.Identity.SessionTime,
            frame.Identity.FrameIdentifier,
            frame.Identity.OverallFrameIdentifier,
            options.IsEnabled,
            TelemetryFresh: true,
            context.HapticsRunning,
            context.DrivingArmed || context.AllowWhenDrivingNotArmed,
            speedKph,
            speedScale,
            ToVehicleWheelData(frame.Signals.SurfaceTypeIds),
            ToVehicleWheelData(frame.Signals.SuspensionAcceleration),
            ToVehicleWheelData(frame.Signals.WheelVerticalForce),
            frame.Signals.VerticalG,
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
            (bst1FrequencyHz + surface.PHprFrequencyHz) * 0.5f,
            bst1FrequencyHz,
            surface.PHprFrequencyHz,
            noiseAmount,
            gearDuckingActive,
            duckingGain,
            SuppressedReason: outputIntensity > 0f ? null : "road intensity zero")
        {
            DominantSurfaceTypeId = surface.DominantSurfaceTypeId
        };
        return Store(signal);
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

        if (!IsTelemetryFresh(vehicleState, now, options.MaximumTelemetryFrameLag))
        {
            Interlocked.Increment(ref _staleTelemetrySuppressedCount);
            return StoreSuppressed(now, "telemetry missing or stale", options, context, vehicleState);
        }

        var telemetry = vehicleState.Telemetry!.Value;
        var speedProgress = SpeedProgress(telemetry.SpeedKph, options.MinimumSpeedKph, options.FullIntensitySpeedKph);
        if (speedProgress <= 0f)
        {
            return StoreSuppressed(now, "below road minimum speed", options, context, vehicleState);
        }

        var speedScale = SpeedScale(speedProgress);
        var surface = EvaluateSurface(telemetry.SurfaceTypeIds, options);
        if (surface.SurfaceMix <= 0f)
        {
            return StoreSuppressed(now, "no supported road surface", options, context, vehicleState);
        }

        var motion = EvaluateMotion(vehicleState, options);
        var bst1FrequencyHz = EvaluateBst1Frequency(surface.Bst1FrequencyHz, speedProgress, options);
        var noiseAmount = EvaluateNoiseAmount(surface.NoiseAmount, speedProgress, motion.RoughnessMetric, options);
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
            (bst1FrequencyHz + surface.PHprFrequencyHz) * 0.5f,
            bst1FrequencyHz,
            surface.PHprFrequencyHz,
            noiseAmount,
            gearDuckingActive,
            duckingGain,
            SuppressedReason: outputIntensity > 0f ? null : "road intensity zero")
        {
            DominantSurfaceTypeId = surface.DominantSurfaceTypeId
        };
        return Store(signal);
    }

    private RoadTextureSignal StoreSuppressed(
        DateTimeOffset now,
        string reason,
        RoadTextureEvaluatorOptions options,
        RoadTextureEvaluationContext context,
        HapticRenderFrame renderFrame)
    {
        _smoothedIntensity = Smooth(0f, options);
        var frame = renderFrame.Frame;
        var signal = RoadTextureSignal.Inactive(now, reason) with
        {
            RoadEffectEnabled = options.IsEnabled,
            HapticsRunning = context.HapticsRunning,
            DrivingArmed = context.DrivingArmed || context.AllowWhenDrivingNotArmed,
            TelemetryFresh = !context.TelemetryStale && renderFrame.Freshness.Telemetry.IsPresent,
            SessionUid = frame.Identity.SessionUid,
            SessionTime = frame.Identity.SessionTime,
            FrameIdentifier = frame.Identity.FrameIdentifier,
            OverallFrameIdentifier = frame.Identity.OverallFrameIdentifier,
            SpeedKph = frame.Signals.SpeedMetersPerSecond is null
                ? (ushort)0
                : (ushort)Math.Clamp(
                    (int)Math.Round(frame.Signals.SpeedMetersPerSecond.Value * 3.6f, MidpointRounding.AwayFromZero),
                    0,
                    ushort.MaxValue),
            SurfaceTypeIds = ToVehicleWheelData(frame.Signals.SurfaceTypeIds),
            SuspensionAcceleration = ToVehicleWheelData(frame.Signals.SuspensionAcceleration),
            WheelVertForce = ToVehicleWheelData(frame.Signals.WheelVerticalForce),
            VerticalG = frame.Signals.VerticalG,
            SmoothedIntensity = _smoothedIntensity,
            GearDuckingActive = IsGearDuckingActive(context.LastGearPulseAtUtc, now, options.GearDuckingWindow),
            DuckingGain = IsGearDuckingActive(context.LastGearPulseAtUtc, now, options.GearDuckingWindow) ? options.GearDuckingGain : 1f
        };
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
            dominant.SurfaceTypeId,
            surfaceMix,
            dominant.SurfaceClass,
            dominant.Name,
            bst1Frequency,
            phprFrequency,
            (bst1Frequency + phprFrequency) * 0.5f,
            Clamp(weightedNoise / denominator, 0f, 1f));
    }

    private static SurfaceEvaluation EvaluateSurface(
        HapticWheelSignals<byte>? surfaceTypeIds,
        RoadTextureEvaluatorOptions options)
    {
        return EvaluateSurface(ToVehicleWheelData(surfaceTypeIds), options);
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

    private static MotionEvaluation EvaluateMotion(
        HapticRenderFrame renderFrame,
        RoadTextureEvaluatorOptions options)
    {
        var frame = renderFrame.Frame;
        var suspensionAcceleration = ToVehicleWheelData(frame.Signals.SuspensionAcceleration);
        var wheelVertForce = ToVehicleWheelData(frame.Signals.WheelVerticalForce);
        var suspensionRoughness = AverageMagnitudeOverThreshold(
            suspensionAcceleration,
            options.SuspensionAccelerationThreshold,
            options.SuspensionAccelerationFullScale);
        var verticalForceRoughness = AverageDeltaOverThreshold(
            wheelVertForce,
            options.WheelVertForceDeltaThreshold,
            options.WheelVertForceDeltaFullScale);
        var verticalG = frame.Signals.VerticalG;
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

    private static bool IsTelemetryFresh(
        VehicleState vehicleState,
        DateTimeOffset nowUtc,
        uint maximumFrameLag)
    {
        return VehicleStateFreshness.EvaluateTelemetry(
            vehicleState,
            nowUtc,
            0,
            TimeProvider.System,
            CreateFrameFreshnessPolicy(maximumFrameLag)).IsFresh;
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

    private static float SpeedProgress(float speedKph, float minimumSpeedKph, float fullSpeedKph)
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

    private static float SpeedScale(float speedProgress)
    {
        var clamped = Clamp(speedProgress, 0f, 1f);
        if (clamped <= 0f)
        {
            return 0f;
        }

        return Clamp((0.35f * clamped) + (0.65f * MathF.Sqrt(clamped)), 0f, 1f);
    }

    private static float EvaluateBst1Frequency(
        float surfaceFrequencyHz,
        float speedProgress,
        RoadTextureEvaluatorOptions options)
    {
        var speedTunedFrequency = Interpolate(
            options.Bst1LowSpeedFrequencyHz,
            options.Bst1HighSpeedFrequencyHz,
            speedProgress);
        return Clamp(
            Interpolate(surfaceFrequencyHz, speedTunedFrequency, options.Bst1SpeedFrequencyInfluence),
            15f,
            90f);
    }

    private static float EvaluateNoiseAmount(
        float baseNoiseAmount,
        float speedProgress,
        float roughnessMetric,
        RoadTextureEvaluatorOptions options)
    {
        var speedDrivenNoise = options.Bst1GrainAmount * (0.25f + (0.75f * Clamp(speedProgress, 0f, 1f)));
        var roughnessBoost = Clamp(roughnessMetric * 0.12f, 0f, 0.12f);
        return Clamp(baseNoiseAmount + speedDrivenNoise + roughnessBoost, 0f, 0.85f);
    }

    private static float Interpolate(float start, float end, float amount)
    {
        var clamped = Clamp(amount, 0f, 1f);
        return start + ((end - start) * clamped);
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

    private static VehicleWheelData<byte> ToVehicleWheelData(HapticWheelSignals<byte>? values)
    {
        return values is null
            ? Wheels<byte>(0)
            : new VehicleWheelData<byte>(values.RearLeft, values.RearRight, values.FrontLeft, values.FrontRight);
    }

    private static VehicleWheelData<float> ToVehicleWheelData(HapticWheelSignals<float>? values)
    {
        return values is null
            ? Wheels(0f)
            : new VehicleWheelData<float>(values.RearLeft, values.RearRight, values.FrontLeft, values.FrontRight);
    }

    private static TelemetryFreshnessPolicy CreateFrameFreshnessPolicy(uint maximumFrameLag)
    {
        return new TelemetryFreshnessPolicy(
            TimeSpan.MaxValue,
            TimeSpan.MaxValue,
            TimeSpan.MaxValue,
            TimeSpan.MaxValue,
            TimeSpan.MaxValue,
            maximumFrameLag);
    }

    private sealed record SurfaceEvaluation(
        byte? DominantSurfaceTypeId,
        float SurfaceMix,
        RoadTextureSurfaceClass SurfaceClass,
        string SurfaceName,
        float Bst1FrequencyHz,
        float PHprFrequencyHz,
        float FrequencyHintHz,
        float NoiseAmount)
    {
        public static SurfaceEvaluation Inactive { get; } = new(
            null,
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
