using HapticDrive.Asio.Audio.Profiles;

namespace HapticDrive.Asio.Audio.Effects.Registry;

public static class HapticEffectSettingsTranslator
{
    private static readonly HapticEffectTuning LegacyDefaultTuning = CreateLegacyDefaultTuning();

    public static IReadOnlyDictionary<string, EffectSettingsDocument> CreateDefaultDocuments(IHapticEffectRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        return registry.All.ToDictionary(
            descriptor => descriptor.Key,
            descriptor => descriptor.CreateDefaultSettings(),
            StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyDictionary<string, EffectSettingsDocument> CreateDocumentsFromLegacy(HapticEffectTuning tuning, IHapticEffectRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(tuning);

        var defaultEffects = LegacyDefaultTuning;
        var engine = tuning.Engine ?? defaultEffects.Engine;
        var gear = tuning.GearShift ?? defaultEffects.GearShift;
        var kerb = tuning.Kerb ?? defaultEffects.Kerb;
        var impact = tuning.Impact ?? defaultEffects.Impact;
        var road = tuning.RoadTexture ?? defaultEffects.RoadTexture;
        var slip = tuning.Slip ?? defaultEffects.Slip;

        return new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["engine-rpm"] = new(
                "engine-rpm",
                engine.IsEnabled,
                Params(
                    ("gain", engine.Gain),
                    ("minimum-frequency-hz", engine.MinimumFrequencyHz),
                    ("maximum-frequency-hz", engine.MaximumFrequencyHz),
                    ("high-frequency-enabled", EngineVibrationEffectOptions.Default.HighFrequencyEnabled ? 1d : 0d),
                    ("high-frequency-hz", EngineVibrationEffectOptions.Default.HighFrequencyHz),
                    ("high-frequency-gain", EngineVibrationEffectOptions.Default.HighFrequencyGain),
                    ("frequency-jitter-hz", EngineVibrationEffectOptions.Default.FrequencyJitterHz),
                    ("idle-throttle-gain", EngineVibrationEffectOptions.Default.IdleThrottleGain),
                    ("pit-gain-multiplier", EngineVibrationEffectOptions.Default.PitGainMultiplier))),
            ["gear-shift"] = new(
                "gear-shift",
                gear.IsEnabled,
                Params(
                    ("gain", gear.Gain),
                    ("pulse-frequency-hz", gear.PulseFrequencyHz),
                    ("pulse-duration-ms", gear.PulseDurationMilliseconds),
                    ("rpm-modulation-enabled", GearShiftEffectOptions.Default.ModulateGainByRpm ? 1d : 0d))),
            ["kerb"] = new(
                "kerb",
                kerb.IsEnabled,
                Params(
                    ("gain", kerb.Gain),
                    ("base-frequency-hz", kerb.BaseFrequencyHz),
                    ("minimum-speed-kph", kerb.MinimumSpeedKph),
                    ("full-intensity-speed-kph", kerb.FullIntensitySpeedKph),
                    ("high-frequency-enabled", KerbEffectOptions.Default.HighFrequencyEnabled ? 1d : 0d),
                    ("high-frequency-hz", KerbEffectOptions.Default.HighFrequencyHz),
                    ("high-frequency-gain", KerbEffectOptions.Default.HighFrequencyGain),
                    ("noise-amount", KerbEffectOptions.Default.NoiseAmount))),
            ["impact"] = new(
                "impact",
                impact.IsEnabled,
                Params(
                    ("gain", impact.Gain),
                    ("pulse-frequency-hz", impact.PulseFrequencyHz),
                    ("pulse-duration-ms", impact.PulseDurationMilliseconds),
                    ("cooldown-ms", impact.CooldownMilliseconds),
                    ("vertical-g-threshold", impact.VerticalGDeltaThreshold))),
            ["road-texture"] = new(
                "road-texture",
                road.IsEnabled,
                Params(
                    ("gain", road.Gain),
                    ("shared-signal-enabled", road.IsEnabled ? 1d : 0d),
                    ("bst1-output-enabled", (road.Bst1OutputEnabled ?? road.IsEnabled) ? 1d : 0d),
                    ("minimum-speed-kph", road.MinimumSpeedKph),
                    ("full-intensity-speed-kph", road.FullIntensitySpeedKph),
                    ("low-speed-frequency-hz", road.LowSpeedFrequencyHz),
                    ("high-speed-frequency-hz", road.HighSpeedFrequencyHz),
                    ("speed-frequency-influence", road.SpeedFrequencyInfluence),
                    ("grain-amount", road.GrainAmount))),
            ["slip-lock"] = new(
                "slip-lock",
                slip.IsEnabled,
                Params(
                    ("wheel-slip-enabled", (slip.WheelSlipEnabled ?? slip.IsEnabled) ? 1d : 0d),
                    ("wheel-slip-gain", slip.Gain),
                    ("wheel-slip-frequency-hz", slip.BaseFrequencyHz),
                    ("wheel-slip-noise-amount", slip.WheelSlipNoiseAmount ?? SlipEffectOptions.Default.WheelSlipNoiseAmount),
                    ("wheel-lock-enabled", (slip.WheelLockEnabled ?? slip.IsEnabled) ? 1d : 0d),
                    ("wheel-lock-gain", slip.WheelLockGain ?? slip.Gain),
                    ("wheel-lock-frequency-hz", slip.WheelLockFrequencyHz ?? SlipEffectOptions.Default.WheelLockFrequencyHz),
                    ("wheel-lock-noise-amount", slip.WheelLockNoiseAmount ?? SlipEffectOptions.Default.WheelLockNoiseAmount),
                    ("minimum-speed-kph", slip.MinimumSpeedKph),
                    ("slip-ratio-threshold", slip.SlipRatioThreshold),
                    ("slip-angle-threshold-rad", slip.SlipAngleThresholdRadians),
                    ("wheel-lock-wheel-speed-ratio-threshold", slip.WheelLockWheelSpeedRatioThreshold ?? SlipEffectOptions.Default.BrakeLockWheelSpeedRatioThreshold))),
            ["diagnostic-test"] = CreateDiagnosticTestDefaultSettings()
        };
    }

    public static IReadOnlyDictionary<string, EffectSettingsDocument> NormalizeDocuments(
        IReadOnlyDictionary<string, EffectSettingsDocument>? settings,
        IHapticEffectRegistry registry,
        ICollection<string>? messages = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var source = settings ?? new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase);
        var normalized = new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in registry.All)
        {
            if (!source.TryGetValue(descriptor.Key, out var document) || document is null)
            {
                normalized[descriptor.Key] = descriptor.CreateDefaultSettings();
                messages?.Add($"Effect settings '{descriptor.Key}' were missing; descriptor defaults were used.");
                continue;
            }

            var repairedDocument = NormalizeDocument(descriptor, document);
            var validationErrors = descriptor.Validate(repairedDocument);
            if (validationErrors.Count > 0)
            {
                normalized[descriptor.Key] = descriptor.CreateDefaultSettings();
                messages?.Add($"Effect settings '{descriptor.Key}' were invalid; descriptor defaults were used.");
                foreach (var error in validationErrors)
                {
                    messages?.Add(error);
                }

                continue;
            }

            normalized[descriptor.Key] = repairedDocument;
        }

        return normalized;
    }

    public static HapticEffectEngineOptions ToEngineOptions(
        IReadOnlyDictionary<string, EffectSettingsDocument> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var engine = settings.TryGetValue("engine-rpm", out var engineDoc)
            ? engineDoc
            : BuiltInHapticEffectRegistry.Instance.GetRequired("engine-rpm").CreateDefaultSettings();
        var gear = settings.TryGetValue("gear-shift", out var gearDoc)
            ? gearDoc
            : BuiltInHapticEffectRegistry.Instance.GetRequired("gear-shift").CreateDefaultSettings();
        var kerb = settings.TryGetValue("kerb", out var kerbDoc)
            ? kerbDoc
            : BuiltInHapticEffectRegistry.Instance.GetRequired("kerb").CreateDefaultSettings();
        var impact = settings.TryGetValue("impact", out var impactDoc)
            ? impactDoc
            : BuiltInHapticEffectRegistry.Instance.GetRequired("impact").CreateDefaultSettings();
        var road = settings.TryGetValue("road-texture", out var roadDoc)
            ? roadDoc
            : BuiltInHapticEffectRegistry.Instance.GetRequired("road-texture").CreateDefaultSettings();
        var slip = settings.TryGetValue("slip-lock", out var slipDoc)
            ? slipDoc
            : BuiltInHapticEffectRegistry.Instance.GetRequired("slip-lock").CreateDefaultSettings();

        return new HapticEffectEngineOptions(
            EngineVibrationEffectOptions.Default with
            {
                IsEnabled = engine.Enabled,
                Gain = Float(engine, "gain", 0.5),
                MinimumFrequencyHz = Float(engine, "minimum-frequency-hz", EngineVibrationEffectOptions.Default.MinimumFrequencyHz),
                MaximumFrequencyHz = Float(engine, "maximum-frequency-hz", EngineVibrationEffectOptions.Default.MaximumFrequencyHz),
                HighFrequencyEnabled = Bool(engine, "high-frequency-enabled", EngineVibrationEffectOptions.Default.HighFrequencyEnabled),
                HighFrequencyHz = Float(engine, "high-frequency-hz", EngineVibrationEffectOptions.Default.HighFrequencyHz),
                HighFrequencyGain = Float(engine, "high-frequency-gain", EngineVibrationEffectOptions.Default.HighFrequencyGain),
                FrequencyJitterHz = Float(engine, "frequency-jitter-hz", EngineVibrationEffectOptions.Default.FrequencyJitterHz),
                IdleThrottleGain = Float(engine, "idle-throttle-gain", EngineVibrationEffectOptions.Default.IdleThrottleGain),
                PitGainMultiplier = Float(engine, "pit-gain-multiplier", EngineVibrationEffectOptions.Default.PitGainMultiplier)
            },
            GearShiftEffectOptions.Default with
            {
                IsEnabled = gear.Enabled,
                Gain = Float(gear, "gain", 0.5),
                PulseFrequencyHz = Float(gear, "pulse-frequency-hz", GearShiftEffectOptions.Default.PulseFrequencyHz),
                PulseDuration = TimeSpan.FromMilliseconds(Float(gear, "pulse-duration-ms", GearShiftEffectOptions.Default.PulseDuration.TotalMilliseconds)),
                ModulateGainByRpm = Bool(gear, "rpm-modulation-enabled", GearShiftEffectOptions.Default.ModulateGainByRpm)
            },
            KerbEffectOptions.Default with
            {
                IsEnabled = kerb.Enabled,
                Gain = Float(kerb, "gain", 0.5),
                BaseFrequencyHz = Float(kerb, "base-frequency-hz", KerbEffectOptions.Default.BaseFrequencyHz),
                MinimumSpeedKph = Float(kerb, "minimum-speed-kph", KerbEffectOptions.Default.MinimumSpeedKph),
                FullIntensitySpeedKph = Float(kerb, "full-intensity-speed-kph", KerbEffectOptions.Default.FullIntensitySpeedKph),
                HighFrequencyEnabled = Bool(kerb, "high-frequency-enabled", KerbEffectOptions.Default.HighFrequencyEnabled),
                HighFrequencyHz = Float(kerb, "high-frequency-hz", KerbEffectOptions.Default.HighFrequencyHz),
                HighFrequencyGain = Float(kerb, "high-frequency-gain", KerbEffectOptions.Default.HighFrequencyGain),
                NoiseAmount = Float(kerb, "noise-amount", KerbEffectOptions.Default.NoiseAmount)
            },
            ImpactEffectOptions.Default with
            {
                IsEnabled = impact.Enabled,
                Gain = Float(impact, "gain", 0.5),
                PulseFrequencyHz = Float(impact, "pulse-frequency-hz", ImpactEffectOptions.Default.PulseFrequencyHz),
                PulseDuration = TimeSpan.FromMilliseconds(Float(impact, "pulse-duration-ms", ImpactEffectOptions.Default.PulseDuration.TotalMilliseconds)),
                CooldownDuration = TimeSpan.FromMilliseconds(Float(impact, "cooldown-ms", ImpactEffectOptions.Default.CooldownDuration.TotalMilliseconds)),
                VerticalGDeltaThreshold = Float(impact, "vertical-g-threshold", ImpactEffectOptions.Default.VerticalGDeltaThreshold)
            },
            RoadTextureEffectOptions.Default with
            {
                IsEnabled = Bool(road, "shared-signal-enabled", road.Enabled),
                Bst1OutputEnabled = Bool(road, "bst1-output-enabled", RoadTextureEffectOptions.Default.Bst1OutputEnabled),
                Gain = Float(road, "gain", 1),
                MinimumSpeedKph = Float(road, "minimum-speed-kph", RoadTextureEffectOptions.Default.MinimumSpeedKph),
                FullIntensitySpeedKph = Float(road, "full-intensity-speed-kph", RoadTextureEffectOptions.Default.FullIntensitySpeedKph),
                Bst1LowSpeedFrequencyHz = Float(road, "low-speed-frequency-hz", RoadTextureEffectOptions.Default.Bst1LowSpeedFrequencyHz),
                Bst1HighSpeedFrequencyHz = Float(road, "high-speed-frequency-hz", RoadTextureEffectOptions.Default.Bst1HighSpeedFrequencyHz),
                Bst1SpeedFrequencyInfluence = Float(road, "speed-frequency-influence", RoadTextureEffectOptions.Default.Bst1SpeedFrequencyInfluence),
                Bst1GrainAmount = Float(road, "grain-amount", RoadTextureEffectOptions.Default.Bst1GrainAmount)
            },
            SlipEffectOptions.Default with
            {
                IsEnabled = slip.Enabled,
                WheelSlipEnabled = Bool(slip, "wheel-slip-enabled", false),
                WheelSlipGain = Float(slip, "wheel-slip-gain", 0.5),
                WheelSlipFrequencyHz = Float(slip, "wheel-slip-frequency-hz", SlipEffectOptions.Default.WheelSlipFrequencyHz),
                WheelSlipNoiseAmount = Float(slip, "wheel-slip-noise-amount", SlipEffectOptions.Default.WheelSlipNoiseAmount),
                WheelLockEnabled = Bool(slip, "wheel-lock-enabled", false),
                WheelLockGain = Float(slip, "wheel-lock-gain", 0.5),
                WheelLockFrequencyHz = Float(slip, "wheel-lock-frequency-hz", SlipEffectOptions.Default.WheelLockFrequencyHz),
                WheelLockNoiseAmount = Float(slip, "wheel-lock-noise-amount", SlipEffectOptions.Default.WheelLockNoiseAmount),
                MinimumSpeedKph = Float(slip, "minimum-speed-kph", SlipEffectOptions.Default.MinimumSpeedKph),
                SlipRatioThreshold = Float(slip, "slip-ratio-threshold", SlipEffectOptions.Default.SlipRatioThreshold),
                SlipAngleThresholdRadians = Float(slip, "slip-angle-threshold-rad", SlipEffectOptions.Default.SlipAngleThresholdRadians),
                BrakeLockWheelSpeedRatioThreshold = Float(slip, "wheel-lock-wheel-speed-ratio-threshold", SlipEffectOptions.Default.BrakeLockWheelSpeedRatioThreshold)
            });
    }

    public static HapticEffectTuning ToLegacyTuning(
        IReadOnlyDictionary<string, EffectSettingsDocument> settings)
    {
        var options = ToEngineOptions(settings);
        return new HapticEffectTuning(
            new EngineVibrationTuning(
                options.Engine.IsEnabled,
                options.Engine.Gain,
                options.Engine.MinimumFrequencyHz,
                options.Engine.MaximumFrequencyHz),
            new GearShiftTuning(
                options.GearShift.IsEnabled,
                options.GearShift.Gain,
                options.GearShift.PulseFrequencyHz,
                (int)Math.Round(options.GearShift.PulseDuration.TotalMilliseconds)),
            new KerbTuning(
                options.Kerb.IsEnabled,
                options.Kerb.Gain,
                options.Kerb.BaseFrequencyHz,
                options.Kerb.MinimumSpeedKph,
                options.Kerb.FullIntensitySpeedKph),
            new ImpactTuning(
                options.Impact.IsEnabled,
                options.Impact.Gain,
                options.Impact.PulseFrequencyHz,
                (int)Math.Round(options.Impact.PulseDuration.TotalMilliseconds),
                (int)Math.Round(options.Impact.CooldownDuration.TotalMilliseconds),
                options.Impact.VerticalGDeltaThreshold),
            new RoadTextureTuning(
                IsEnabled: options.RoadTexture.IsEnabled,
                Gain: options.RoadTexture.Gain,
                MinimumSpeedKph: options.RoadTexture.MinimumSpeedKph,
                FullIntensitySpeedKph: options.RoadTexture.FullIntensitySpeedKph)
            {
                Bst1OutputEnabled = options.RoadTexture.Bst1OutputEnabled,
                LowSpeedFrequencyHz = options.RoadTexture.Bst1LowSpeedFrequencyHz,
                HighSpeedFrequencyHz = options.RoadTexture.Bst1HighSpeedFrequencyHz,
                SpeedFrequencyInfluence = options.RoadTexture.Bst1SpeedFrequencyInfluence,
                GrainAmount = options.RoadTexture.Bst1GrainAmount
            },
            new SlipTuning(
                options.Slip.WheelSlipEnabled || options.Slip.WheelLockEnabled,
                options.Slip.WheelSlipGain,
                options.Slip.WheelSlipFrequencyHz,
                options.Slip.MinimumSpeedKph,
                options.Slip.SlipRatioThreshold,
                options.Slip.SlipAngleThresholdRadians)
            {
                WheelSlipEnabled = options.Slip.WheelSlipEnabled,
                WheelSlipNoiseAmount = options.Slip.WheelSlipNoiseAmount,
                WheelLockEnabled = options.Slip.WheelLockEnabled,
                WheelLockGain = options.Slip.WheelLockGain,
                WheelLockFrequencyHz = options.Slip.WheelLockFrequencyHz,
                WheelLockNoiseAmount = options.Slip.WheelLockNoiseAmount,
                WheelLockWheelSpeedRatioThreshold = options.Slip.BrakeLockWheelSpeedRatioThreshold
            });
    }

    private static Dictionary<string, double> Params(params (string Key, double Value)[] values)
    {
        return values.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static float Float(EffectSettingsDocument settings, string key, double fallback)
    {
        return settings.Parameters.TryGetValue(key, out var value)
            ? (float)value
            : (float)fallback;
    }

    private static bool Bool(EffectSettingsDocument settings, string key, bool fallback)
    {
        return settings.Parameters.TryGetValue(key, out var value)
            ? value >= 0.5d
            : fallback;
    }

    private static EffectSettingsDocument NormalizeDocument(
        IHapticEffectDescriptor descriptor,
        EffectSettingsDocument document)
    {
        var defaultSettings = descriptor.CreateDefaultSettings();
        var sourceParameters = document.Parameters ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var normalizedParameters = defaultSettings.Parameters.ToDictionary(
            parameter => parameter.Key,
            parameter => sourceParameters.TryGetValue(parameter.Key, out var value) ? value : parameter.Value,
            StringComparer.OrdinalIgnoreCase);

        return new EffectSettingsDocument(
            descriptor.Key,
            document.Enabled,
            normalizedParameters);
    }

    private static HapticEffectTuning CreateLegacyDefaultTuning()
    {
        var effects = HapticEffectEngineOptions.Default;
        return new HapticEffectTuning(
            new EngineVibrationTuning(
                IsEnabled: false,
                Gain: 0.5f,
                effects.Engine.MinimumFrequencyHz,
                effects.Engine.MaximumFrequencyHz),
            new GearShiftTuning(
                IsEnabled: false,
                Gain: 0.5f,
                effects.GearShift.PulseFrequencyHz,
                (int)Math.Round(effects.GearShift.PulseDuration.TotalMilliseconds)),
            new KerbTuning(
                IsEnabled: false,
                Gain: 0.5f,
                effects.Kerb.BaseFrequencyHz,
                effects.Kerb.MinimumSpeedKph,
                effects.Kerb.FullIntensitySpeedKph),
            new ImpactTuning(
                IsEnabled: false,
                Gain: 0.5f,
                effects.Impact.PulseFrequencyHz,
                (int)Math.Round(effects.Impact.PulseDuration.TotalMilliseconds),
                (int)Math.Round(effects.Impact.CooldownDuration.TotalMilliseconds),
                effects.Impact.VerticalGDeltaThreshold),
            new RoadTextureTuning(
                IsEnabled: true,
                Gain: 1f,
                effects.RoadTexture.MinimumSpeedKph,
                effects.RoadTexture.FullIntensitySpeedKph)
            {
                Bst1OutputEnabled = true,
                LowSpeedFrequencyHz = effects.RoadTexture.Bst1LowSpeedFrequencyHz,
                HighSpeedFrequencyHz = effects.RoadTexture.Bst1HighSpeedFrequencyHz,
                SpeedFrequencyInfluence = effects.RoadTexture.Bst1SpeedFrequencyInfluence,
                GrainAmount = effects.RoadTexture.Bst1GrainAmount
            },
            new SlipTuning(
                IsEnabled: false,
                Gain: 0.5f,
                effects.Slip.WheelSlipFrequencyHz,
                effects.Slip.MinimumSpeedKph,
                effects.Slip.SlipRatioThreshold,
                effects.Slip.SlipAngleThresholdRadians)
            {
                WheelSlipEnabled = false,
                WheelSlipNoiseAmount = effects.Slip.WheelSlipNoiseAmount,
                WheelLockEnabled = false,
                WheelLockGain = 0.5f,
                WheelLockFrequencyHz = effects.Slip.WheelLockFrequencyHz,
                WheelLockNoiseAmount = effects.Slip.WheelLockNoiseAmount,
                WheelLockWheelSpeedRatioThreshold = effects.Slip.BrakeLockWheelSpeedRatioThreshold
            });
    }

    private static EffectSettingsDocument CreateDiagnosticTestDefaultSettings()
    {
        return new EffectSettingsDocument(
            "diagnostic-test",
            Enabled: false,
            Params(
                ("gain", 0.1d),
                ("frequency-hz", 40d),
                ("pulse-duration-ms", 100d)));
    }
}
