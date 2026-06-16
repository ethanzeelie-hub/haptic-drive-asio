using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class SlipEffect : IHapticEffectSource
{
    private double _basePhase;
    private long _frameCursor;
    private float _smoothedAmplitude;
    private SlipEvaluation _evaluation = SlipEvaluation.Inactive("not evaluated");

    public SlipEffect()
        : this(SlipEffectOptions.Default)
    {
    }

    public SlipEffect(SlipEffectOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Snapshot = CreateSnapshot(_evaluation, peakLevel: 0f);
    }

    public string Name => "Slip";

    public SlipEffectOptions Options { get; }

    public SlipEffectSnapshot Snapshot { get; private set; }

    public void Reset()
    {
        _basePhase = 0.0;
        _frameCursor = 0;
        _smoothedAmplitude = 0f;
        _evaluation = SlipEvaluation.Inactive("reset");
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
            Snapshot = CreateSnapshot(_evaluation with { IsActive = false, CurrentAmplitude = 0f }, peakLevel: 0f);
            return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: false, PeakLevel: 0f);
        }

        var noiseAmount = HapticEffectMath.Clamp(_evaluation.CurrentNoiseAmount, 0f, 1f);
        var toneAmount = 1f - noiseAmount;
        var smoothing = HapticEffectMath.SmoothingCoefficient(destination.SampleRate, Options.ResponseSmoothingTime);

        for (var frame = 0; frame < destination.FrameCount; frame++)
        {
            _smoothedAmplitude += (_evaluation.CurrentAmplitude - _smoothedAmplitude) * smoothing;

            var tone = Math.Sin(_basePhase) * toneAmount;
            var noise = HapticEffectMath.DeterministicSignedUnitNoise(_frameCursor, seed: 31) * noiseAmount;
            var sample = tone + noise;

            HapticEffectMath.WriteMonoFrame(destination, frame, (float)(sample * _smoothedAmplitude));

            _basePhase = HapticEffectMath.AdvancePhase(_basePhase, _evaluation.CurrentFrequencyHz, destination.SampleRate);
            _frameCursor++;
        }

        var peak = HapticEffectMath.CalculatePeak(destination);
        Snapshot = CreateSnapshot(_evaluation, peak);
        return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: peak > 0f, peak);
    }

    private static SlipEvaluation Evaluate(VehicleState vehicleState, SlipEffectOptions options)
    {
        if (!options.IsEnabled)
        {
            return SlipEvaluation.Inactive("effect disabled");
        }

        if (!options.WheelSlipEnabled && !options.WheelLockEnabled)
        {
            return SlipEvaluation.Inactive("slip and lock outputs disabled");
        }

        if (VehicleStateEffectGuards.ShouldMuteForDrivingState(vehicleState))
        {
            return SlipEvaluation.Inactive("driving state muted");
        }

        if (!VehicleStateEffectGuards.IsFresh(vehicleState, vehicleState.Telemetry, options.MaximumTelemetryFrameLag)
            || !VehicleStateEffectGuards.IsFresh(vehicleState, vehicleState.MotionEx, options.MaximumTelemetryFrameLag))
        {
            return SlipEvaluation.Inactive("telemetry stale");
        }

        var telemetry = vehicleState.Telemetry!.Value;
        var motionEx = vehicleState.MotionEx!.Value;
        var speedScale = HapticEffectMath.SpeedScale(
            telemetry.SpeedKph,
            options.MinimumSpeedKph,
            options.FullIntensitySpeedKph);
        if (speedScale <= 0f)
        {
            return SlipEvaluation.Inactive("below minimum speed");
        }

        var throttle = VehicleStateEffectGuards.SanitizeUnit(telemetry.Throttle);
        var brake = VehicleStateEffectGuards.SanitizeUnit(telemetry.Brake);
        var slip = CalculateSlipSignal(motionEx, options);
        var brakeLock = CalculateBrakeLockSignal(motionEx, telemetry.SpeedKph, brake, options);
        var slipIntensity = slip.Intensity;
        var lockIntensity = brakeLock.Intensity;

        if (throttle < options.TriggerThrottle && brake < options.TriggerBrake)
        {
            slipIntensity *= HapticEffectMath.Clamp(options.LowPedalInputMultiplier, 0f, 1f);
        }

        if (vehicleState.CarStatus?.Value.TractionControl is > 0 && throttle >= options.TriggerThrottle)
        {
            slipIntensity *= HapticEffectMath.Clamp(options.AssistedSlipMultiplier, 0f, 1f);
        }

        if (vehicleState.CarStatus?.Value.AntiLockBrakes is > 0 && brake >= options.TriggerBrake)
        {
            lockIntensity *= HapticEffectMath.Clamp(options.AssistedSlipMultiplier, 0f, 1f);
        }

        slipIntensity = HapticEffectMath.Clamp(slipIntensity, 0f, 1f);
        lockIntensity = HapticEffectMath.Clamp(lockIntensity, 0f, 1f);

        var slipAmplitude = options.WheelSlipEnabled
            ? HapticEffectMath.Clamp(options.WheelSlipGain * speedScale * slipIntensity, 0f, options.MaximumAmplitude)
            : 0f;
        var lockAmplitude = options.WheelLockEnabled
            ? HapticEffectMath.Clamp(options.WheelLockGain * speedScale * lockIntensity, 0f, options.MaximumAmplitude)
            : 0f;
        var lockDominant = lockAmplitude > slipAmplitude;
        var currentAmplitude = Math.Max(slipAmplitude, lockAmplitude);

        if (currentAmplitude <= 0f)
        {
            return SlipEvaluation.Inactive("below thresholds") with
            {
                SlipIntensity = slipIntensity,
                LockIntensity = lockIntensity,
                CurrentSlipRatio = slip.MaximumSlipRatio,
                CurrentSlipAngleRadians = slip.MaximumSlipAngleRadians,
                CurrentMinimumWheelSpeedRatio = brakeLock.MinimumWheelSpeedRatio
            };
        }

        return new SlipEvaluation(
            IsActive: true,
            WheelSlipEnabled: options.WheelSlipEnabled,
            WheelLockEnabled: options.WheelLockEnabled,
            SlipIntensity: slipIntensity,
            LockIntensity: lockIntensity,
            CurrentSlipRatio: slip.MaximumSlipRatio,
            CurrentSlipAngleRadians: slip.MaximumSlipAngleRadians,
            CurrentMinimumWheelSpeedRatio: brakeLock.MinimumWheelSpeedRatio,
            CurrentFrequencyHz: SanitizeFrequency(lockDominant ? options.WheelLockFrequencyHz : options.WheelSlipFrequencyHz),
            CurrentNoiseAmount: HapticEffectMath.Clamp(lockDominant ? options.WheelLockNoiseAmount : options.WheelSlipNoiseAmount, 0f, 1f),
            CurrentAmplitude: currentAmplitude,
            ActiveSource: lockDominant ? "Wheel lock" : "Wheel slip",
            ActiveReason: lockDominant ? "wheel lock active" : "wheel slip active");
    }

    private static SlipSignalEvaluation CalculateSlipSignal(VehicleMotionExState motionEx, SlipEffectOptions options)
    {
        var maximum = 0f;
        var maximumRatio = 0f;
        var maximumAngle = 0f;

        for (var wheel = 0; wheel < 4; wheel++)
        {
            var ratio = VehicleStateEffectGuards.SanitizeFiniteMagnitude(motionEx.WheelSlipRatio[wheel], 3f);
            var angle = VehicleStateEffectGuards.SanitizeFiniteMagnitude(motionEx.WheelSlipAngle[wheel], 2f);
            maximumRatio = Math.Max(maximumRatio, ratio);
            maximumAngle = Math.Max(maximumAngle, angle);
            var ratioAmount = AmountOverThreshold(ratio, options.SlipRatioThreshold, options.SlipRatioFullScale);
            var angleAmount = AmountOverThreshold(angle, options.SlipAngleThresholdRadians, options.SlipAngleFullScaleRadians);
            maximum = Math.Max(maximum, Math.Max(ratioAmount, angleAmount));
        }

        return new SlipSignalEvaluation(maximumRatio, maximumAngle, maximum);
    }

    private static BrakeLockSignalEvaluation CalculateBrakeLockSignal(
        VehicleMotionExState motionEx,
        ushort speedKph,
        float brake,
        SlipEffectOptions options)
    {
        if (brake < options.TriggerBrake)
        {
            return new BrakeLockSignalEvaluation(MaximumSlipRatio: 0f, MinimumWheelSpeedRatio: 1f, Intensity: 0f);
        }

        var slipLock = 0f;
        var maximumSlipRatio = 0f;
        var minimumWheelSpeed = float.MaxValue;
        for (var wheel = 0; wheel < 4; wheel++)
        {
            var ratio = VehicleStateEffectGuards.SanitizeFiniteMagnitude(motionEx.WheelSlipRatio[wheel], 3f);
            maximumSlipRatio = Math.Max(maximumSlipRatio, ratio);
            slipLock = Math.Max(slipLock, AmountOverThreshold(ratio, options.BrakeLockSlipRatioThreshold, options.SlipRatioFullScale));

            var wheelSpeed = Math.Abs(motionEx.WheelSpeed[wheel]);
            if (float.IsFinite(wheelSpeed))
            {
                minimumWheelSpeed = Math.Min(minimumWheelSpeed, wheelSpeed);
            }
        }

        var speedMetersPerSecond = speedKph / 3.6f;
        var wheelLock = 0f;
        var minimumWheelSpeedRatio = 1f;
        if (speedMetersPerSecond > 0.1f && minimumWheelSpeed < float.MaxValue)
        {
            minimumWheelSpeedRatio = HapticEffectMath.Clamp(minimumWheelSpeed / speedMetersPerSecond, 0f, 1f);
            if (minimumWheelSpeedRatio < options.BrakeLockWheelSpeedRatioThreshold)
            {
                wheelLock = HapticEffectMath.Clamp(
                    (options.BrakeLockWheelSpeedRatioThreshold - minimumWheelSpeedRatio) / options.BrakeLockWheelSpeedRatioThreshold,
                    0f,
                    1f);
            }
        }

        return new BrakeLockSignalEvaluation(
            maximumSlipRatio,
            minimumWheelSpeedRatio,
            Math.Max(slipLock, wheelLock));
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

        return HapticEffectMath.Clamp((value - threshold) / (fullScale - threshold), 0f, 1f);
    }

    private static float SanitizeFrequency(float frequencyHz)
    {
        return float.IsFinite(frequencyHz) && frequencyHz > 0f ? frequencyHz : 0f;
    }

    private SlipEffectSnapshot CreateSnapshot(SlipEvaluation evaluation, float peakLevel)
    {
        return new SlipEffectSnapshot(
            Options.IsEnabled,
            Options.WheelSlipEnabled,
            Options.WheelLockEnabled,
            evaluation.IsActive,
            evaluation.SlipIntensity,
            evaluation.LockIntensity,
            evaluation.CurrentSlipRatio,
            evaluation.CurrentSlipAngleRadians,
            evaluation.CurrentMinimumWheelSpeedRatio,
            evaluation.CurrentFrequencyHz,
            evaluation.CurrentNoiseAmount,
            evaluation.CurrentAmplitude,
            evaluation.ActiveSource,
            evaluation.ActiveReason,
            peakLevel);
    }

    private sealed record SlipEvaluation(
        bool IsActive,
        bool WheelSlipEnabled,
        bool WheelLockEnabled,
        float SlipIntensity,
        float LockIntensity,
        float CurrentSlipRatio,
        float CurrentSlipAngleRadians,
        float CurrentMinimumWheelSpeedRatio,
        float CurrentFrequencyHz,
        float CurrentNoiseAmount,
        float CurrentAmplitude,
        string ActiveSource,
        string ActiveReason)
    {
        public static SlipEvaluation Inactive(string reason) => new(
            IsActive: false,
            WheelSlipEnabled: false,
            WheelLockEnabled: false,
            SlipIntensity: 0f,
            LockIntensity: 0f,
            CurrentSlipRatio: 0f,
            CurrentSlipAngleRadians: 0f,
            CurrentMinimumWheelSpeedRatio: 1f,
            CurrentFrequencyHz: 0f,
            CurrentNoiseAmount: 0f,
            CurrentAmplitude: 0f,
            ActiveSource: "None",
            ActiveReason: reason);
    }

    private sealed record SlipSignalEvaluation(
        float MaximumSlipRatio,
        float MaximumSlipAngleRadians,
        float Intensity);

    private sealed record BrakeLockSignalEvaluation(
        float MaximumSlipRatio,
        float MinimumWheelSpeedRatio,
        float Intensity);
}
