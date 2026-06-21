using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class SlipEffect : IHapticEffectSource
{
    private double _basePhase;
    private long _frameCursor;
    private float _smoothedAmplitude;
    private readonly SlipLockEvaluator _slipLockEvaluator;
    private SlipEvaluation _evaluation = SlipEvaluation.Inactive("not evaluated");

    public SlipEffect()
        : this(SlipEffectOptions.Default)
    {
    }

    public SlipEffect(SlipEffectOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _slipLockEvaluator = new SlipLockEvaluator(CreateEvaluatorOptions(Options));
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

    public void Update(HapticEffectInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _evaluation = Evaluate(
            _slipLockEvaluator.Evaluate(
                SlipLockEvaluationInput.FromHapticFrame(
                    input.Frame,
                    input.VehicleState,
                    _slipLockEvaluator.Options,
                    Options.WheelSlipEnabled,
                    Options.WheelLockEnabled)),
            Options);
        Snapshot = CreateSnapshot(_evaluation, peakLevel: 0f);
    }

    public void Update(HapticDrive.Asio.Core.Vehicle.VehicleState vehicleState)
    {
        Update(LegacyHapticEffectInputFactory.FromVehicleState(vehicleState));
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

    private static SlipEvaluation Evaluate(
        SlipLockEvaluationResult signal,
        SlipEffectOptions options)
    {
        if (!options.IsEnabled)
        {
            return SlipEvaluation.Inactive("effect disabled");
        }

        if (!options.WheelSlipEnabled && !options.WheelLockEnabled)
        {
            return SlipEvaluation.Inactive("slip and lock outputs disabled");
        }

        if (signal.DrivingStateMuted)
        {
            return SlipEvaluation.Inactive("driving state muted");
        }

        if (!signal.TelemetryFresh || !signal.MotionExFresh)
        {
            return SlipEvaluation.Inactive("telemetry stale");
        }

        var speedScale = signal.SpeedScale;
        if (speedScale <= 0f)
        {
            return SlipEvaluation.Inactive("below minimum speed");
        }

        var slipIntensity = signal.WheelSlip.Intensity01;
        var lockIntensity = signal.WheelLock.Intensity01;

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
                CurrentSlipRatio = signal.MaximumSlipRatio,
                CurrentSlipAngleRadians = signal.MaximumSlipAngleRadians,
                CurrentMinimumWheelSpeedRatio = GetCurrentMinimumWheelSpeedRatio(signal)
            };
        }

        return new SlipEvaluation(
            IsActive: true,
            WheelSlipEnabled: options.WheelSlipEnabled,
            WheelLockEnabled: options.WheelLockEnabled,
            SlipIntensity: slipIntensity,
            LockIntensity: lockIntensity,
            CurrentSlipRatio: signal.MaximumSlipRatio,
            CurrentSlipAngleRadians: signal.MaximumSlipAngleRadians,
            CurrentMinimumWheelSpeedRatio: GetCurrentMinimumWheelSpeedRatio(signal),
            CurrentFrequencyHz: SanitizeFrequency(lockDominant ? options.WheelLockFrequencyHz : options.WheelSlipFrequencyHz),
            CurrentNoiseAmount: HapticEffectMath.Clamp(lockDominant ? options.WheelLockNoiseAmount : options.WheelSlipNoiseAmount, 0f, 1f),
            CurrentAmplitude: currentAmplitude,
            ActiveSource: lockDominant ? "Wheel lock" : "Wheel slip",
            ActiveReason: lockDominant ? "wheel lock active" : "wheel slip active");
    }

    private static SlipLockEvaluationOptions CreateEvaluatorOptions(SlipEffectOptions options)
    {
        return new SlipLockEvaluationOptions(
            MinimumSpeedKph: options.MinimumSpeedKph,
            FullIntensitySpeedKph: options.FullIntensitySpeedKph,
            SlipRatioThreshold: options.SlipRatioThreshold,
            SlipRatioFullScale: options.SlipRatioFullScale,
            SlipAngleThresholdRadians: options.SlipAngleThresholdRadians,
            SlipAngleFullScaleRadians: options.SlipAngleFullScaleRadians,
            TriggerThrottle: options.TriggerThrottle,
            TriggerBrake: options.TriggerBrake,
            LowPedalInputMultiplier: options.LowPedalInputMultiplier,
            AssistedSlipMultiplier: options.AssistedSlipMultiplier,
            BrakeLockSlipRatioThreshold: options.BrakeLockSlipRatioThreshold,
            BrakeLockWheelSpeedRatioThreshold: options.BrakeLockWheelSpeedRatioThreshold,
            MaximumTelemetryFrameLag: options.MaximumTelemetryFrameLag);
    }

    private static float GetCurrentMinimumWheelSpeedRatio(SlipLockEvaluationResult signal)
    {
        if (signal.WheelLock.SuppressionReason == SlipLockSuppressionReason.BelowBrakeThreshold)
        {
            return 1f;
        }

        return signal.HasMinimumWheelSpeedRatio
            ? signal.MinimumWheelSpeedRatio
            : 1f;
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
}
