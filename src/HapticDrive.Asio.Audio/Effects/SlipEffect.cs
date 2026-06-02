using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class SlipEffect : IHapticEffectSource
{
    private double _basePhase;
    private double _highPhase;
    private long _frameCursor;
    private float _smoothedAmplitude;
    private SlipEvaluation _evaluation = SlipEvaluation.Inactive;

    public SlipEffect()
        : this(SlipEffectOptions.Default)
    {
    }

    public SlipEffect(SlipEffectOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Snapshot = CreateSnapshot(SlipEvaluation.Inactive, peakLevel: 0f);
    }

    public string Name => "Slip";

    public SlipEffectOptions Options { get; }

    public SlipEffectSnapshot Snapshot { get; private set; }

    public void Reset()
    {
        _basePhase = 0.0;
        _highPhase = 0.0;
        _frameCursor = 0;
        _smoothedAmplitude = 0f;
        _evaluation = SlipEvaluation.Inactive;
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

        var highGain = Options.HighFrequencyEnabled
            ? HapticEffectMath.Clamp(Options.HighFrequencyGain, 0f, 1f)
            : 0f;
        var roughness = HapticEffectMath.Clamp(Options.RoughnessAmount, 0f, 1f);
        var normalizer = 1.0 + highGain + roughness;
        var smoothing = HapticEffectMath.SmoothingCoefficient(destination.SampleRate, Options.ResponseSmoothingTime);

        for (var frame = 0; frame < destination.FrameCount; frame++)
        {
            _smoothedAmplitude += (_evaluation.Amplitude - _smoothedAmplitude) * smoothing;

            var sample = Math.Sin(_basePhase);
            if (highGain > 0f)
            {
                sample += highGain * Math.Sin(_highPhase);
            }

            if (roughness > 0f)
            {
                sample += roughness * HapticEffectMath.DeterministicSignedUnitNoise(_frameCursor, seed: 31);
            }

            HapticEffectMath.WriteMonoFrame(destination, frame, (float)((sample / normalizer) * _smoothedAmplitude));

            _basePhase = HapticEffectMath.AdvancePhase(_basePhase, _evaluation.FrequencyHz, destination.SampleRate);
            _highPhase = HapticEffectMath.AdvancePhase(_highPhase, Options.HighFrequencyHz, destination.SampleRate);
            _frameCursor++;
        }

        var peak = HapticEffectMath.CalculatePeak(destination);
        Snapshot = CreateSnapshot(_evaluation, peak);
        return new HapticEffectRenderResult(Name, Options.IsEnabled, IsActive: peak > 0f, peak);
    }

    private static SlipEvaluation Evaluate(VehicleState vehicleState, SlipEffectOptions options)
    {
        if (!options.IsEnabled
            || VehicleStateEffectGuards.ShouldMuteForDrivingState(vehicleState)
            || !VehicleStateEffectGuards.IsFresh(vehicleState, vehicleState.Telemetry, options.MaximumTelemetryFrameLag)
            || !VehicleStateEffectGuards.IsFresh(vehicleState, vehicleState.MotionEx, options.MaximumTelemetryFrameLag))
        {
            return SlipEvaluation.Inactive;
        }

        var telemetry = vehicleState.Telemetry!.Value;
        var motionEx = vehicleState.MotionEx!.Value;
        var speedScale = HapticEffectMath.SpeedScale(
            telemetry.SpeedKph,
            options.MinimumSpeedKph,
            options.FullIntensitySpeedKph);
        if (speedScale <= 0f)
        {
            return SlipEvaluation.Inactive;
        }

        var throttle = VehicleStateEffectGuards.SanitizeUnit(telemetry.Throttle);
        var brake = VehicleStateEffectGuards.SanitizeUnit(telemetry.Brake);
        var slipIntensity = CalculateSlipIntensity(motionEx, options);
        var lockIntensity = CalculateBrakeLockIntensity(motionEx, telemetry.SpeedKph, brake, options);

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

        var combinedIntensity = Math.Max(slipIntensity, lockIntensity * options.BrakeLockGainMultiplier);
        combinedIntensity = HapticEffectMath.Clamp(combinedIntensity, 0f, 1f);
        var amplitude = HapticEffectMath.Clamp(
            options.Gain * speedScale * combinedIntensity,
            0f,
            options.MaximumAmplitude);

        if (amplitude <= 0f)
        {
            return SlipEvaluation.Inactive with
            {
                SlipIntensity = slipIntensity,
                LockIntensity = lockIntensity
            };
        }

        var frequencyHz = lockIntensity > slipIntensity
            ? options.BrakeLockFrequencyHz
            : options.BaseFrequencyHz;

        return new SlipEvaluation(
            IsActive: true,
            HapticEffectMath.Clamp(slipIntensity, 0f, 1f),
            HapticEffectMath.Clamp(lockIntensity, 0f, 1f),
            SanitizeFrequency(frequencyHz),
            amplitude);
    }

    private static float CalculateSlipIntensity(VehicleMotionExState motionEx, SlipEffectOptions options)
    {
        var maximum = 0f;

        for (var wheel = 0; wheel < 4; wheel++)
        {
            var ratio = VehicleStateEffectGuards.SanitizeFiniteMagnitude(motionEx.WheelSlipRatio[wheel], 3f);
            var angle = VehicleStateEffectGuards.SanitizeFiniteMagnitude(motionEx.WheelSlipAngle[wheel], 2f);
            var ratioAmount = AmountOverThreshold(ratio, options.SlipRatioThreshold, options.SlipRatioFullScale);
            var angleAmount = AmountOverThreshold(angle, options.SlipAngleThresholdRadians, options.SlipAngleFullScaleRadians);
            maximum = Math.Max(maximum, Math.Max(ratioAmount, angleAmount));
        }

        return maximum;
    }

    private static float CalculateBrakeLockIntensity(
        VehicleMotionExState motionEx,
        ushort speedKph,
        float brake,
        SlipEffectOptions options)
    {
        if (brake < options.TriggerBrake)
        {
            return 0f;
        }

        var slipLock = 0f;
        var minimumWheelSpeed = float.MaxValue;
        for (var wheel = 0; wheel < 4; wheel++)
        {
            var ratio = VehicleStateEffectGuards.SanitizeFiniteMagnitude(motionEx.WheelSlipRatio[wheel], 3f);
            slipLock = Math.Max(slipLock, AmountOverThreshold(ratio, options.BrakeLockSlipRatioThreshold, options.SlipRatioFullScale));

            var wheelSpeed = Math.Abs(motionEx.WheelSpeed[wheel]);
            if (float.IsFinite(wheelSpeed))
            {
                minimumWheelSpeed = Math.Min(minimumWheelSpeed, wheelSpeed);
            }
        }

        var speedMetersPerSecond = speedKph / 3.6f;
        var wheelLock = 0f;
        if (speedMetersPerSecond > 0.1f && minimumWheelSpeed < float.MaxValue)
        {
            var wheelSpeedRatio = HapticEffectMath.Clamp(minimumWheelSpeed / speedMetersPerSecond, 0f, 1f);
            if (wheelSpeedRatio < options.BrakeLockWheelSpeedRatioThreshold)
            {
                wheelLock = HapticEffectMath.Clamp(
                    (options.BrakeLockWheelSpeedRatioThreshold - wheelSpeedRatio) / options.BrakeLockWheelSpeedRatioThreshold,
                    0f,
                    1f);
            }
        }

        return Math.Max(slipLock, wheelLock);
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
            evaluation.IsActive,
            evaluation.SlipIntensity,
            evaluation.LockIntensity,
            evaluation.FrequencyHz,
            evaluation.Amplitude,
            peakLevel);
    }

    private sealed record SlipEvaluation(
        bool IsActive,
        float SlipIntensity,
        float LockIntensity,
        float FrequencyHz,
        float Amplitude)
    {
        public static SlipEvaluation Inactive { get; } = new(
            IsActive: false,
            SlipIntensity: 0f,
            LockIntensity: 0f,
            FrequencyHz: 0f,
            Amplitude: 0f);
    }
}
