using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Asio.Core.Haptics;

public sealed record SlipLockEvaluationOptions(
    float MinimumSpeedKph,
    float FullIntensitySpeedKph,
    float SlipRatioThreshold,
    float SlipRatioFullScale,
    float SlipAngleThresholdRadians,
    float SlipAngleFullScaleRadians,
    float TriggerThrottle,
    float TriggerBrake,
    float LowPedalInputMultiplier,
    float AssistedSlipMultiplier,
    float BrakeLockSlipRatioThreshold,
    float BrakeLockWheelSpeedRatioThreshold,
    uint MaximumTelemetryFrameLag)
{
    public static SlipLockEvaluationOptions Default { get; } = new(
        MinimumSpeedKph: 8f,
        FullIntensitySpeedKph: 90f,
        SlipRatioThreshold: 0.08f,
        SlipRatioFullScale: 0.45f,
        SlipAngleThresholdRadians: 0.08f,
        SlipAngleFullScaleRadians: 0.45f,
        TriggerThrottle: 0.1f,
        TriggerBrake: 0.1f,
        LowPedalInputMultiplier: 0.35f,
        AssistedSlipMultiplier: 0.75f,
        BrakeLockSlipRatioThreshold: 0.35f,
        BrakeLockWheelSpeedRatioThreshold: 0.35f,
        MaximumTelemetryFrameLag: 120);

    public SlipLockEvaluationOptions Normalize()
    {
        var minimumSpeedKph = SanitizeFinite(MinimumSpeedKph, Default.MinimumSpeedKph, 0f, 400f);
        var fullIntensitySpeedKph = SanitizeFinite(FullIntensitySpeedKph, Default.FullIntensitySpeedKph, minimumSpeedKph, 400f);
        var slipRatioThreshold = SanitizeFinite(SlipRatioThreshold, Default.SlipRatioThreshold, 0f, 3f);
        var slipRatioFullScale = SanitizeFinite(SlipRatioFullScale, Default.SlipRatioFullScale, slipRatioThreshold, 3f);
        var slipAngleThresholdRadians = SanitizeFinite(SlipAngleThresholdRadians, Default.SlipAngleThresholdRadians, 0f, 2f);
        var slipAngleFullScaleRadians = SanitizeFinite(SlipAngleFullScaleRadians, Default.SlipAngleFullScaleRadians, slipAngleThresholdRadians, 2f);
        var triggerThrottle = SanitizeFinite(TriggerThrottle, Default.TriggerThrottle, 0f, 1f);
        var triggerBrake = SanitizeFinite(TriggerBrake, Default.TriggerBrake, 0f, 1f);
        var lowPedalInputMultiplier = SanitizeFinite(LowPedalInputMultiplier, Default.LowPedalInputMultiplier, 0f, 1f);
        var assistedSlipMultiplier = SanitizeFinite(AssistedSlipMultiplier, Default.AssistedSlipMultiplier, 0f, 1f);
        var brakeLockSlipRatioThreshold = SanitizeFinite(BrakeLockSlipRatioThreshold, Default.BrakeLockSlipRatioThreshold, 0f, 3f);
        var brakeLockWheelSpeedRatioThreshold = SanitizeFinite(BrakeLockWheelSpeedRatioThreshold, Default.BrakeLockWheelSpeedRatioThreshold, 0.0001f, 1f);

        return this with
        {
            MinimumSpeedKph = minimumSpeedKph,
            FullIntensitySpeedKph = Math.Max(minimumSpeedKph, fullIntensitySpeedKph),
            SlipRatioThreshold = slipRatioThreshold,
            SlipRatioFullScale = Math.Max(slipRatioThreshold, slipRatioFullScale),
            SlipAngleThresholdRadians = slipAngleThresholdRadians,
            SlipAngleFullScaleRadians = Math.Max(slipAngleThresholdRadians, slipAngleFullScaleRadians),
            TriggerThrottle = triggerThrottle,
            TriggerBrake = triggerBrake,
            LowPedalInputMultiplier = lowPedalInputMultiplier,
            AssistedSlipMultiplier = assistedSlipMultiplier,
            BrakeLockSlipRatioThreshold = brakeLockSlipRatioThreshold,
            BrakeLockWheelSpeedRatioThreshold = brakeLockWheelSpeedRatioThreshold,
            MaximumTelemetryFrameLag = MaximumTelemetryFrameLag
        };
    }

    private static float SanitizeFinite(float value, float fallback, float minimum, float maximum)
    {
        return float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
    }
}

public sealed record SlipLockEvaluationInput(
    bool HasVehicleState,
    bool WheelSlipEnabled,
    bool WheelLockEnabled,
    bool DrivingStateMuted,
    bool TelemetryFresh,
    bool MotionExFresh,
    float SpeedKph,
    float Throttle01,
    float Brake01,
    bool TractionControlActive,
    bool AntiLockBrakesActive,
    VehicleWheelData<float> WheelSlipRatio,
    VehicleWheelData<float> WheelSlipAngle,
    VehicleWheelData<float> WheelSpeedMetersPerSecond)
{
    public static SlipLockEvaluationInput FromHapticRenderFrame(
        HapticRenderFrame renderFrame,
        SlipLockEvaluationOptions? options = null,
        bool wheelSlipEnabled = true,
        bool wheelLockEnabled = true)
    {
        return FromHapticFrame(renderFrame.Frame, renderFrame.Freshness, options, wheelSlipEnabled, wheelLockEnabled);
    }

    public static SlipLockEvaluationInput FromHapticFrame(
        HapticFrame frame,
        HapticFrameFreshnessSnapshot freshness,
        SlipLockEvaluationOptions? options = null,
        bool wheelSlipEnabled = true,
        bool wheelLockEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return new SlipLockEvaluationInput(
            HasVehicleState: true,
            WheelSlipEnabled: wheelSlipEnabled,
            WheelLockEnabled: wheelLockEnabled,
            DrivingStateMuted: !frame.Context.AllowsDrivingOutput,
            TelemetryFresh: freshness.Telemetry.IsFresh,
            MotionExFresh: freshness.MotionEx.IsFresh,
            SpeedKph: (frame.Signals.SpeedMetersPerSecond ?? 0f) * 3.6f,
            Throttle01: frame.Signals.Throttle ?? 0f,
            Brake01: frame.Signals.Brake ?? 0f,
            TractionControlActive: frame.Signals.TractionControlActive ?? false,
            AntiLockBrakesActive: frame.Signals.AntiLockBrakesActive ?? false,
            WheelSlipRatio: ToVehicleWheelData(frame.Signals.TyreSlip),
            WheelSlipAngle: ToVehicleWheelData(frame.Signals.TyreSlipAngle),
            WheelSpeedMetersPerSecond: ToVehicleWheelData(frame.Signals.WheelSpeedMetersPerSecond));
    }

    public static SlipLockEvaluationInput FromVehicleState(
        VehicleState? vehicleState,
        SlipLockEvaluationOptions? options = null,
        bool wheelSlipEnabled = true,
        bool wheelLockEnabled = true)
    {
        var evaluatorOptions = (options ?? SlipLockEvaluationOptions.Default).Normalize();
        if (vehicleState is null)
        {
            return new SlipLockEvaluationInput(
                HasVehicleState: false,
                WheelSlipEnabled: wheelSlipEnabled,
                WheelLockEnabled: wheelLockEnabled,
                DrivingStateMuted: false,
                TelemetryFresh: false,
                MotionExFresh: false,
                SpeedKph: 0f,
                Throttle01: 0f,
                Brake01: 0f,
                TractionControlActive: false,
                AntiLockBrakesActive: false,
                WheelSlipRatio: Wheels(float.NaN),
                WheelSlipAngle: Wheels(float.NaN),
                WheelSpeedMetersPerSecond: Wheels(float.NaN));
        }

        var telemetry = vehicleState.Telemetry?.Value;
        var motionEx = vehicleState.MotionEx?.Value;
        return new SlipLockEvaluationInput(
            HasVehicleState: true,
            WheelSlipEnabled: wheelSlipEnabled,
            WheelLockEnabled: wheelLockEnabled,
            DrivingStateMuted: ShouldMuteForDrivingState(vehicleState),
            TelemetryFresh: IsTelemetryFresh(vehicleState, evaluatorOptions.MaximumTelemetryFrameLag),
            MotionExFresh: IsMotionExFresh(vehicleState, evaluatorOptions.MaximumTelemetryFrameLag),
            SpeedKph: telemetry?.SpeedKph ?? 0f,
            Throttle01: telemetry?.Throttle ?? 0f,
            Brake01: telemetry?.Brake ?? 0f,
            TractionControlActive: vehicleState.CarStatus?.Value.TractionControl is > 0,
            AntiLockBrakesActive: vehicleState.CarStatus?.Value.AntiLockBrakes is > 0,
            WheelSlipRatio: motionEx?.WheelSlipRatio ?? Wheels(float.NaN),
            WheelSlipAngle: motionEx?.WheelSlipAngle ?? Wheels(float.NaN),
            WheelSpeedMetersPerSecond: motionEx?.WheelSpeed ?? Wheels(float.NaN));
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
        uint maximumFrameLag)
    {
        return VehicleStateFreshness.EvaluateTelemetry(
            vehicleState,
            DateTimeOffset.UtcNow,
            0,
            TimeProvider.System,
            CreateFrameFreshnessPolicy(maximumFrameLag)).IsFresh;
    }

    private static bool IsMotionExFresh(
        VehicleState vehicleState,
        uint maximumFrameLag)
    {
        return VehicleStateFreshness.EvaluateMotionEx(
            vehicleState,
            DateTimeOffset.UtcNow,
            0,
            TimeProvider.System,
            CreateFrameFreshnessPolicy(maximumFrameLag)).IsFresh;
    }

    private static VehicleWheelData<float> Wheels(float value)
    {
        return new VehicleWheelData<float>(value, value, value, value);
    }

    private static VehicleWheelData<float> ToVehicleWheelData(HapticWheelSignals<float>? values)
    {
        return values is null
            ? Wheels(float.NaN)
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
}

public enum SlipLockSuppressionReason
{
    None = 0,
    MissingVehicleState,
    EffectDisabled,
    DrivingStateMuted,
    TelemetryStale,
    MotionStale,
    BelowMinimumSpeed,
    BelowSlipThreshold,
    BelowBrakeThreshold,
    BelowLockThreshold
}

public readonly record struct SlipLockWheelContribution(
    float SlipRatio,
    float SlipAngleRadians,
    float? WheelSpeedMetersPerSecond,
    float SlipRatioContribution01,
    float SlipAngleContribution01,
    float WheelLockSlipContribution01,
    float? WheelSpeedRatio,
    float WheelSpeedLockContribution01);

public sealed record SlipLockSignalResult(
    bool IsEnabled,
    bool IsActive,
    float Intensity01,
    bool IsAssistedAttenuated,
    SlipLockSuppressionReason SuppressionReason)
{
    public static SlipLockSignalResult Inactive(bool isEnabled, SlipLockSuppressionReason suppressionReason)
    {
        return new SlipLockSignalResult(
            IsEnabled: isEnabled,
            IsActive: false,
            Intensity01: 0f,
            IsAssistedAttenuated: false,
            SuppressionReason: suppressionReason);
    }

    public static SlipLockSignalResult Active(bool isEnabled, float intensity01, bool isAssistedAttenuated)
    {
        return new SlipLockSignalResult(
            IsEnabled: isEnabled,
            IsActive: intensity01 > 0f,
            Intensity01: Clamp(intensity01, 0f, 1f),
            IsAssistedAttenuated: isAssistedAttenuated,
            SuppressionReason: SlipLockSuppressionReason.None);
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        return float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : minimum;
    }
}

public sealed record SlipLockEvaluationResult(
    bool HasVehicleState,
    bool DrivingStateMuted,
    bool TelemetryFresh,
    bool MotionExFresh,
    bool TractionControlActive,
    bool AntiLockBrakesActive,
    float SpeedKph,
    float SpeedScale,
    float Throttle01,
    float Brake01,
    float MaximumSlipRatio,
    float MaximumSlipAngleRadians,
    float MinimumWheelSpeedMetersPerSecond,
    bool HasMinimumWheelSpeed,
    float MinimumWheelSpeedRatio,
    bool HasMinimumWheelSpeedRatio,
    VehicleWheelData<SlipLockWheelContribution> WheelContributions,
    SlipLockSignalResult WheelSlip,
    SlipLockSignalResult WheelLock)
{
    public float MaximumIntensity01 => Math.Max(WheelSlip.Intensity01, WheelLock.Intensity01);
}

public sealed class SlipLockEvaluator
{
    public SlipLockEvaluator(SlipLockEvaluationOptions? options = null)
    {
        Options = (options ?? SlipLockEvaluationOptions.Default).Normalize();
    }

    public SlipLockEvaluationOptions Options { get; }

    public SlipLockEvaluationResult Evaluate(SlipLockEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var speedKph = SanitizeMagnitude(input.SpeedKph, maximumMagnitude: 400f);
        var throttle = SanitizeUnit(input.Throttle01);
        var brake = SanitizeUnit(input.Brake01);
        var speedScale = SpeedScale(speedKph, Options.MinimumSpeedKph, Options.FullIntensitySpeedKph);
        var speedMetersPerSecond = speedKph / 3.6f;

        var maximumSlipRatio = 0f;
        var maximumSlipAngle = 0f;
        var maximumSlipIntensity = 0f;
        var maximumLockSlipIntensity = 0f;
        var minimumWheelSpeed = float.MaxValue;
        var minimumWheelSpeedRatio = 0f;
        var hasMinimumWheelSpeedRatio = false;
        var contributions = new SlipLockWheelContribution[4];

        for (var wheel = 0; wheel < 4; wheel++)
        {
            var slipRatio = SanitizeMagnitude(input.WheelSlipRatio[wheel], maximumMagnitude: 3f);
            var slipAngle = SanitizeMagnitude(input.WheelSlipAngle[wheel], maximumMagnitude: 2f);
            maximumSlipRatio = Math.Max(maximumSlipRatio, slipRatio);
            maximumSlipAngle = Math.Max(maximumSlipAngle, slipAngle);

            var slipRatioContribution = AmountOverThreshold(
                slipRatio,
                Options.SlipRatioThreshold,
                Options.SlipRatioFullScale);
            var slipAngleContribution = AmountOverThreshold(
                slipAngle,
                Options.SlipAngleThresholdRadians,
                Options.SlipAngleFullScaleRadians);
            var lockSlipContribution = AmountOverThreshold(
                slipRatio,
                Options.BrakeLockSlipRatioThreshold,
                Options.SlipRatioFullScale);

            maximumSlipIntensity = Math.Max(maximumSlipIntensity, Math.Max(slipRatioContribution, slipAngleContribution));
            maximumLockSlipIntensity = Math.Max(maximumLockSlipIntensity, lockSlipContribution);

            var rawWheelSpeed = input.WheelSpeedMetersPerSecond[wheel];
            float? wheelSpeed = null;
            if (float.IsFinite(rawWheelSpeed))
            {
                wheelSpeed = Math.Abs(rawWheelSpeed);
                minimumWheelSpeed = Math.Min(minimumWheelSpeed, wheelSpeed.Value);
            }

            float? wheelSpeedRatio = null;
            var wheelSpeedLockContribution = 0f;
            if (wheelSpeed is not null && speedMetersPerSecond > 0.1f)
            {
                wheelSpeedRatio = Clamp(wheelSpeed.Value / speedMetersPerSecond, 0f, 1f);
                hasMinimumWheelSpeedRatio = true;
                if (wheelSpeedRatio.Value < Options.BrakeLockWheelSpeedRatioThreshold)
                {
                    wheelSpeedLockContribution = Clamp(
                        (Options.BrakeLockWheelSpeedRatioThreshold - wheelSpeedRatio.Value)
                        / Options.BrakeLockWheelSpeedRatioThreshold,
                        0f,
                        1f);
                }
            }

            contributions[wheel] = new SlipLockWheelContribution(
                SlipRatio: slipRatio,
                SlipAngleRadians: slipAngle,
                WheelSpeedMetersPerSecond: wheelSpeed,
                SlipRatioContribution01: slipRatioContribution,
                SlipAngleContribution01: slipAngleContribution,
                WheelLockSlipContribution01: lockSlipContribution,
                WheelSpeedRatio: wheelSpeedRatio,
                WheelSpeedLockContribution01: wheelSpeedLockContribution);
        }

        var hasMinimumWheelSpeed = minimumWheelSpeed < float.MaxValue;
        if (hasMinimumWheelSpeedRatio && hasMinimumWheelSpeed)
        {
            minimumWheelSpeedRatio = Clamp(minimumWheelSpeed / Math.Max(speedMetersPerSecond, 0.0001f), 0f, 1f);
        }

        var wheelContributionData = new VehicleWheelData<SlipLockWheelContribution>(
            contributions[0],
            contributions[1],
            contributions[2],
            contributions[3]);

        var wheelSlip = EvaluateWheelSlip(input, throttle, brake, speedScale, maximumSlipIntensity);
        var wheelLock = EvaluateWheelLock(input, brake, speedScale, maximumLockSlipIntensity, minimumWheelSpeedRatio, hasMinimumWheelSpeedRatio);

        return new SlipLockEvaluationResult(
            HasVehicleState: input.HasVehicleState,
            DrivingStateMuted: input.DrivingStateMuted,
            TelemetryFresh: input.TelemetryFresh,
            MotionExFresh: input.MotionExFresh,
            TractionControlActive: input.TractionControlActive,
            AntiLockBrakesActive: input.AntiLockBrakesActive,
            SpeedKph: speedKph,
            SpeedScale: speedScale,
            Throttle01: throttle,
            Brake01: brake,
            MaximumSlipRatio: maximumSlipRatio,
            MaximumSlipAngleRadians: maximumSlipAngle,
            MinimumWheelSpeedMetersPerSecond: hasMinimumWheelSpeed ? minimumWheelSpeed : 0f,
            HasMinimumWheelSpeed: hasMinimumWheelSpeed,
            MinimumWheelSpeedRatio: minimumWheelSpeedRatio,
            HasMinimumWheelSpeedRatio: hasMinimumWheelSpeedRatio,
            WheelContributions: wheelContributionData,
            WheelSlip: wheelSlip,
            WheelLock: wheelLock);
    }

    private SlipLockSignalResult EvaluateWheelSlip(
        SlipLockEvaluationInput input,
        float throttle,
        float brake,
        float speedScale,
        float maximumSlipIntensity)
    {
        if (!input.HasVehicleState)
        {
            return SlipLockSignalResult.Inactive(input.WheelSlipEnabled, SlipLockSuppressionReason.MissingVehicleState);
        }

        if (!input.WheelSlipEnabled)
        {
            return SlipLockSignalResult.Inactive(false, SlipLockSuppressionReason.EffectDisabled);
        }

        if (input.DrivingStateMuted)
        {
            return SlipLockSignalResult.Inactive(true, SlipLockSuppressionReason.DrivingStateMuted);
        }

        if (!input.TelemetryFresh)
        {
            return SlipLockSignalResult.Inactive(true, SlipLockSuppressionReason.TelemetryStale);
        }

        if (!input.MotionExFresh)
        {
            return SlipLockSignalResult.Inactive(true, SlipLockSuppressionReason.MotionStale);
        }

        if (speedScale <= 0f)
        {
            return SlipLockSignalResult.Inactive(true, SlipLockSuppressionReason.BelowMinimumSpeed);
        }

        var slipIntensity = maximumSlipIntensity;
        if (throttle < Options.TriggerThrottle && brake < Options.TriggerBrake)
        {
            slipIntensity *= Options.LowPedalInputMultiplier;
        }

        var assistedAttenuated = input.TractionControlActive && throttle >= Options.TriggerThrottle;
        if (assistedAttenuated)
        {
            slipIntensity *= Options.AssistedSlipMultiplier;
        }

        var intensity = Clamp(slipIntensity * speedScale, 0f, 1f);
        return intensity <= 0f
            ? SlipLockSignalResult.Inactive(true, SlipLockSuppressionReason.BelowSlipThreshold)
            : SlipLockSignalResult.Active(true, intensity, assistedAttenuated);
    }

    private SlipLockSignalResult EvaluateWheelLock(
        SlipLockEvaluationInput input,
        float brake,
        float speedScale,
        float maximumLockSlipIntensity,
        float minimumWheelSpeedRatio,
        bool hasMinimumWheelSpeedRatio)
    {
        if (!input.HasVehicleState)
        {
            return SlipLockSignalResult.Inactive(input.WheelLockEnabled, SlipLockSuppressionReason.MissingVehicleState);
        }

        if (!input.WheelLockEnabled)
        {
            return SlipLockSignalResult.Inactive(false, SlipLockSuppressionReason.EffectDisabled);
        }

        if (input.DrivingStateMuted)
        {
            return SlipLockSignalResult.Inactive(true, SlipLockSuppressionReason.DrivingStateMuted);
        }

        if (!input.TelemetryFresh)
        {
            return SlipLockSignalResult.Inactive(true, SlipLockSuppressionReason.TelemetryStale);
        }

        if (!input.MotionExFresh)
        {
            return SlipLockSignalResult.Inactive(true, SlipLockSuppressionReason.MotionStale);
        }

        if (brake < Options.TriggerBrake)
        {
            return SlipLockSignalResult.Inactive(true, SlipLockSuppressionReason.BelowBrakeThreshold);
        }

        if (speedScale <= 0f)
        {
            return SlipLockSignalResult.Inactive(true, SlipLockSuppressionReason.BelowMinimumSpeed);
        }

        var wheelLockContribution = 0f;
        if (hasMinimumWheelSpeedRatio && minimumWheelSpeedRatio < Options.BrakeLockWheelSpeedRatioThreshold)
        {
            wheelLockContribution = Clamp(
                (Options.BrakeLockWheelSpeedRatioThreshold - minimumWheelSpeedRatio)
                / Options.BrakeLockWheelSpeedRatioThreshold,
                0f,
                1f);
        }

        var intensity = Math.Max(maximumLockSlipIntensity, wheelLockContribution) * speedScale;
        var assistedAttenuated = input.AntiLockBrakesActive && brake >= Options.TriggerBrake;
        if (assistedAttenuated)
        {
            intensity *= Options.AssistedSlipMultiplier;
        }

        intensity = Clamp(intensity, 0f, 1f);
        return intensity <= 0f
            ? SlipLockSignalResult.Inactive(true, SlipLockSuppressionReason.BelowLockThreshold)
            : SlipLockSignalResult.Active(true, intensity, assistedAttenuated);
    }

    private static float SpeedScale(float speedKph, float minimumSpeedKph, float fullIntensitySpeedKph)
    {
        if (!float.IsFinite(speedKph) || speedKph <= minimumSpeedKph)
        {
            return 0f;
        }

        if (!float.IsFinite(fullIntensitySpeedKph) || fullIntensitySpeedKph <= minimumSpeedKph)
        {
            return 1f;
        }

        return Clamp((speedKph - minimumSpeedKph) / (fullIntensitySpeedKph - minimumSpeedKph), 0f, 1f);
    }

    private static float SanitizeUnit(float value)
    {
        return float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
    }

    private static float SanitizeMagnitude(float value, float maximumMagnitude)
    {
        return float.IsFinite(value) ? Math.Clamp(Math.Abs(value), 0f, maximumMagnitude) : 0f;
    }

    private static float AmountOverThreshold(float value, float threshold, float fullScale)
    {
        if (!float.IsFinite(value) || !float.IsFinite(threshold) || !float.IsFinite(fullScale) || fullScale <= threshold)
        {
            return 0f;
        }

        if (value <= threshold)
        {
            return 0f;
        }

        return Clamp((value - threshold) / (fullScale - threshold), 0f, 1f);
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        return float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : minimum;
    }
}
