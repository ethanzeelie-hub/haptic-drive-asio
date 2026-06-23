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

    public static IReadOnlyDictionary<string, EffectSettingsDocument> CreateDocumentsFromLegacy(
        HapticEffectTuning tuning,
        IHapticEffectRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(registry);

        var defaultEffects = LegacyDefaultTuning;
        var engine = tuning.Engine ?? defaultEffects.Engine;
        var gear = tuning.GearShift ?? defaultEffects.GearShift;
        var kerb = tuning.Kerb ?? defaultEffects.Kerb;
        var impact = tuning.Impact ?? defaultEffects.Impact;
        var road = tuning.RoadTexture ?? defaultEffects.RoadTexture;
        var slip = tuning.Slip ?? defaultEffects.Slip;

        return new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["engine-rpm"] = Doc(
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
                    ("pit-gain-multiplier", EngineVibrationEffectOptions.Default.PitGainMultiplier)),
                registry),
            ["gear-shift"] = Doc(
                "gear-shift",
                gear.IsEnabled,
                Params(
                    ("gain", gear.Gain),
                    ("pulse-frequency-hz", gear.PulseFrequencyHz),
                    ("pulse-duration-ms", gear.PulseDurationMilliseconds),
                    ("rpm-modulation-enabled", GearShiftEffectOptions.Default.ModulateGainByRpm ? 1d : 0d)),
                registry),
            ["kerb"] = Doc(
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
                    ("noise-amount", KerbEffectOptions.Default.NoiseAmount)),
                registry),
            ["impact"] = Doc(
                "impact",
                impact.IsEnabled,
                Params(
                    ("gain", impact.Gain),
                    ("pulse-frequency-hz", impact.PulseFrequencyHz),
                    ("pulse-duration-ms", impact.PulseDurationMilliseconds),
                    ("cooldown-ms", impact.CooldownMilliseconds),
                    ("vertical-g-threshold", impact.VerticalGDeltaThreshold)),
                registry),
            ["road-texture"] = Doc(
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
                    ("grain-amount", road.GrainAmount)),
                registry),
            ["slip-lock"] = Doc(
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
                    ("wheel-lock-wheel-speed-ratio-threshold", slip.WheelLockWheelSpeedRatioThreshold ?? SlipEffectOptions.Default.BrakeLockWheelSpeedRatioThreshold)),
                registry)
        };
    }

    public static IReadOnlyDictionary<string, EffectSettingsDocument> CreateDocumentsFromOptions(
        HapticEffectEngineOptions options,
        IHapticEffectRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(registry);

        return new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["engine-rpm"] = Doc(
                "engine-rpm",
                options.Engine.IsEnabled,
                Params(
                    ("gain", options.Engine.Gain),
                    ("minimum-frequency-hz", options.Engine.MinimumFrequencyHz),
                    ("maximum-frequency-hz", options.Engine.MaximumFrequencyHz),
                    ("high-frequency-enabled", options.Engine.HighFrequencyEnabled ? 1d : 0d),
                    ("high-frequency-hz", options.Engine.HighFrequencyHz),
                    ("high-frequency-gain", options.Engine.HighFrequencyGain),
                    ("frequency-jitter-hz", options.Engine.FrequencyJitterHz),
                    ("idle-throttle-gain", options.Engine.IdleThrottleGain),
                    ("pit-gain-multiplier", options.Engine.PitGainMultiplier)),
                registry),
            ["gear-shift"] = Doc(
                "gear-shift",
                options.GearShift.IsEnabled,
                Params(
                    ("gain", options.GearShift.Gain),
                    ("pulse-frequency-hz", options.GearShift.PulseFrequencyHz),
                    ("pulse-duration-ms", options.GearShift.PulseDuration.TotalMilliseconds),
                    ("rpm-modulation-enabled", options.GearShift.ModulateGainByRpm ? 1d : 0d)),
                registry),
            ["kerb"] = Doc(
                "kerb",
                options.Kerb.IsEnabled,
                Params(
                    ("gain", options.Kerb.Gain),
                    ("base-frequency-hz", options.Kerb.BaseFrequencyHz),
                    ("minimum-speed-kph", options.Kerb.MinimumSpeedKph),
                    ("full-intensity-speed-kph", options.Kerb.FullIntensitySpeedKph),
                    ("high-frequency-enabled", options.Kerb.HighFrequencyEnabled ? 1d : 0d),
                    ("high-frequency-hz", options.Kerb.HighFrequencyHz),
                    ("high-frequency-gain", options.Kerb.HighFrequencyGain),
                    ("noise-amount", options.Kerb.NoiseAmount)),
                registry),
            ["impact"] = Doc(
                "impact",
                options.Impact.IsEnabled,
                Params(
                    ("gain", options.Impact.Gain),
                    ("pulse-frequency-hz", options.Impact.PulseFrequencyHz),
                    ("pulse-duration-ms", options.Impact.PulseDuration.TotalMilliseconds),
                    ("cooldown-ms", options.Impact.CooldownDuration.TotalMilliseconds),
                    ("vertical-g-threshold", options.Impact.VerticalGDeltaThreshold)),
                registry),
            ["road-texture"] = Doc(
                "road-texture",
                options.RoadTexture.IsEnabled,
                Params(
                    ("gain", options.RoadTexture.Gain),
                    ("shared-signal-enabled", options.RoadTexture.IsEnabled ? 1d : 0d),
                    ("bst1-output-enabled", options.RoadTexture.Bst1OutputEnabled ? 1d : 0d),
                    ("minimum-speed-kph", options.RoadTexture.MinimumSpeedKph),
                    ("full-intensity-speed-kph", options.RoadTexture.FullIntensitySpeedKph),
                    ("low-speed-frequency-hz", options.RoadTexture.Bst1LowSpeedFrequencyHz),
                    ("high-speed-frequency-hz", options.RoadTexture.Bst1HighSpeedFrequencyHz),
                    ("speed-frequency-influence", options.RoadTexture.Bst1SpeedFrequencyInfluence),
                    ("grain-amount", options.RoadTexture.Bst1GrainAmount)),
                registry),
            ["slip-lock"] = Doc(
                "slip-lock",
                options.Slip.IsEnabled,
                Params(
                    ("wheel-slip-enabled", options.Slip.WheelSlipEnabled ? 1d : 0d),
                    ("wheel-slip-gain", options.Slip.WheelSlipGain),
                    ("wheel-slip-frequency-hz", options.Slip.WheelSlipFrequencyHz),
                    ("wheel-slip-noise-amount", options.Slip.WheelSlipNoiseAmount),
                    ("wheel-lock-enabled", options.Slip.WheelLockEnabled ? 1d : 0d),
                    ("wheel-lock-gain", options.Slip.WheelLockGain),
                    ("wheel-lock-frequency-hz", options.Slip.WheelLockFrequencyHz),
                    ("wheel-lock-noise-amount", options.Slip.WheelLockNoiseAmount),
                    ("minimum-speed-kph", options.Slip.MinimumSpeedKph),
                    ("slip-ratio-threshold", options.Slip.SlipRatioThreshold),
                    ("slip-angle-threshold-rad", options.Slip.SlipAngleThresholdRadians),
                    ("wheel-lock-wheel-speed-ratio-threshold", options.Slip.BrakeLockWheelSpeedRatioThreshold)),
                registry)
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
            source.TryGetValue(descriptor.Key, out var document);
            var repairedDocument = descriptor.Normalize(document, messages);
            var validationErrors = descriptor.Validate(repairedDocument);
            if (validationErrors.Count > 0)
            {
                normalized[descriptor.Key] = descriptor.CreateDefaultSettings();
                messages?.Add($"Effect settings '{descriptor.Key}' could not be repaired safely; descriptor defaults were used.");
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

        var registry = BuiltInHapticEffectRegistry.Instance;
        var normalized = NormalizeDocuments(settings, registry);
        var engine = GetDocumentOrDefault(normalized, registry, "engine-rpm");
        var gear = GetDocumentOrDefault(normalized, registry, "gear-shift");
        var kerb = GetDocumentOrDefault(normalized, registry, "kerb");
        var impact = GetDocumentOrDefault(normalized, registry, "impact");
        var road = GetDocumentOrDefault(normalized, registry, "road-texture");
        var slip = GetDocumentOrDefault(normalized, registry, "slip-lock");

        return new HapticEffectEngineOptions(
            ToEngineVibrationEffectOptions(engine),
            ToGearShiftEffectOptions(gear),
            ToKerbEffectOptions(kerb),
            ToImpactEffectOptions(impact),
            ToRoadTextureEffectOptions(road),
            ToSlipEffectOptions(slip));
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

    internal static EngineVibrationEffectOptions ToEngineVibrationEffectOptions(EffectSettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ToEngineVibrationEffectOptions(settings.Parameters) with { IsEnabled = settings.Enabled };
    }

    internal static EngineVibrationEffectOptions ToEngineVibrationEffectOptions(IReadOnlyDictionary<string, double> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return EngineVibrationEffectOptions.Default with
        {
            IsEnabled = true,
            Gain = Float(parameters, "gain", EngineVibrationEffectOptions.Default.Gain),
            MinimumFrequencyHz = Float(parameters, "minimum-frequency-hz", EngineVibrationEffectOptions.Default.MinimumFrequencyHz),
            MaximumFrequencyHz = Float(parameters, "maximum-frequency-hz", EngineVibrationEffectOptions.Default.MaximumFrequencyHz),
            HighFrequencyEnabled = Bool(parameters, "high-frequency-enabled", EngineVibrationEffectOptions.Default.HighFrequencyEnabled),
            HighFrequencyHz = Float(parameters, "high-frequency-hz", EngineVibrationEffectOptions.Default.HighFrequencyHz),
            HighFrequencyGain = Float(parameters, "high-frequency-gain", EngineVibrationEffectOptions.Default.HighFrequencyGain),
            FrequencyJitterHz = Float(parameters, "frequency-jitter-hz", EngineVibrationEffectOptions.Default.FrequencyJitterHz),
            IdleThrottleGain = Float(parameters, "idle-throttle-gain", EngineVibrationEffectOptions.Default.IdleThrottleGain),
            PitGainMultiplier = Float(parameters, "pit-gain-multiplier", EngineVibrationEffectOptions.Default.PitGainMultiplier)
        };
    }

    internal static GearShiftEffectOptions ToGearShiftEffectOptions(EffectSettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ToGearShiftEffectOptions(settings.Parameters) with { IsEnabled = settings.Enabled };
    }

    internal static GearShiftEffectOptions ToGearShiftEffectOptions(IReadOnlyDictionary<string, double> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return GearShiftEffectOptions.Default with
        {
            IsEnabled = true,
            Gain = Float(parameters, "gain", GearShiftEffectOptions.Default.Gain),
            PulseFrequencyHz = Float(parameters, "pulse-frequency-hz", GearShiftEffectOptions.Default.PulseFrequencyHz),
            PulseDuration = TimeSpan.FromMilliseconds(Float(parameters, "pulse-duration-ms", GearShiftEffectOptions.Default.PulseDuration.TotalMilliseconds)),
            ModulateGainByRpm = Bool(parameters, "rpm-modulation-enabled", GearShiftEffectOptions.Default.ModulateGainByRpm)
        };
    }

    internal static KerbEffectOptions ToKerbEffectOptions(EffectSettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ToKerbEffectOptions(settings.Parameters) with { IsEnabled = settings.Enabled };
    }

    internal static KerbEffectOptions ToKerbEffectOptions(IReadOnlyDictionary<string, double> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return KerbEffectOptions.Default with
        {
            IsEnabled = true,
            Gain = Float(parameters, "gain", KerbEffectOptions.Default.Gain),
            BaseFrequencyHz = Float(parameters, "base-frequency-hz", KerbEffectOptions.Default.BaseFrequencyHz),
            MinimumSpeedKph = Float(parameters, "minimum-speed-kph", KerbEffectOptions.Default.MinimumSpeedKph),
            FullIntensitySpeedKph = Float(parameters, "full-intensity-speed-kph", KerbEffectOptions.Default.FullIntensitySpeedKph),
            HighFrequencyEnabled = Bool(parameters, "high-frequency-enabled", KerbEffectOptions.Default.HighFrequencyEnabled),
            HighFrequencyHz = Float(parameters, "high-frequency-hz", KerbEffectOptions.Default.HighFrequencyHz),
            HighFrequencyGain = Float(parameters, "high-frequency-gain", KerbEffectOptions.Default.HighFrequencyGain),
            NoiseAmount = Float(parameters, "noise-amount", KerbEffectOptions.Default.NoiseAmount)
        };
    }

    internal static ImpactEffectOptions ToImpactEffectOptions(EffectSettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ToImpactEffectOptions(settings.Parameters) with { IsEnabled = settings.Enabled };
    }

    internal static ImpactEffectOptions ToImpactEffectOptions(IReadOnlyDictionary<string, double> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return ImpactEffectOptions.Default with
        {
            IsEnabled = true,
            Gain = Float(parameters, "gain", ImpactEffectOptions.Default.Gain),
            PulseFrequencyHz = Float(parameters, "pulse-frequency-hz", ImpactEffectOptions.Default.PulseFrequencyHz),
            PulseDuration = TimeSpan.FromMilliseconds(Float(parameters, "pulse-duration-ms", ImpactEffectOptions.Default.PulseDuration.TotalMilliseconds)),
            CooldownDuration = TimeSpan.FromMilliseconds(Float(parameters, "cooldown-ms", ImpactEffectOptions.Default.CooldownDuration.TotalMilliseconds)),
            VerticalGDeltaThreshold = Float(parameters, "vertical-g-threshold", ImpactEffectOptions.Default.VerticalGDeltaThreshold)
        };
    }

    internal static RoadTextureEffectOptions ToRoadTextureEffectOptions(EffectSettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ToRoadTextureEffectOptions(settings.Parameters) with
        {
            IsEnabled = settings.Enabled
                && Bool(settings.Parameters, "shared-signal-enabled", settings.Enabled)
        };
    }

    internal static RoadTextureEffectOptions ToRoadTextureEffectOptions(IReadOnlyDictionary<string, double> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return RoadTextureEffectOptions.Default with
        {
            IsEnabled = true,
            Bst1OutputEnabled = Bool(parameters, "bst1-output-enabled", RoadTextureEffectOptions.Default.Bst1OutputEnabled),
            Gain = Float(parameters, "gain", RoadTextureEffectOptions.Default.Gain),
            MinimumSpeedKph = Float(parameters, "minimum-speed-kph", RoadTextureEffectOptions.Default.MinimumSpeedKph),
            FullIntensitySpeedKph = Float(parameters, "full-intensity-speed-kph", RoadTextureEffectOptions.Default.FullIntensitySpeedKph),
            Bst1LowSpeedFrequencyHz = Float(parameters, "low-speed-frequency-hz", RoadTextureEffectOptions.Default.Bst1LowSpeedFrequencyHz),
            Bst1HighSpeedFrequencyHz = Float(parameters, "high-speed-frequency-hz", RoadTextureEffectOptions.Default.Bst1HighSpeedFrequencyHz),
            Bst1SpeedFrequencyInfluence = Float(parameters, "speed-frequency-influence", RoadTextureEffectOptions.Default.Bst1SpeedFrequencyInfluence),
            Bst1GrainAmount = Float(parameters, "grain-amount", RoadTextureEffectOptions.Default.Bst1GrainAmount)
        };
    }

    internal static SlipEffectOptions ToSlipEffectOptions(EffectSettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ToSlipEffectOptions(settings.Parameters) with { IsEnabled = settings.Enabled };
    }

    internal static SlipEffectOptions ToSlipEffectOptions(IReadOnlyDictionary<string, double> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return SlipEffectOptions.Default with
        {
            IsEnabled = true,
            WheelSlipEnabled = Bool(parameters, "wheel-slip-enabled", false),
            WheelSlipGain = Float(parameters, "wheel-slip-gain", SlipEffectOptions.Default.WheelSlipGain),
            WheelSlipFrequencyHz = Float(parameters, "wheel-slip-frequency-hz", SlipEffectOptions.Default.WheelSlipFrequencyHz),
            WheelSlipNoiseAmount = Float(parameters, "wheel-slip-noise-amount", SlipEffectOptions.Default.WheelSlipNoiseAmount),
            WheelLockEnabled = Bool(parameters, "wheel-lock-enabled", false),
            WheelLockGain = Float(parameters, "wheel-lock-gain", SlipEffectOptions.Default.WheelLockGain),
            WheelLockFrequencyHz = Float(parameters, "wheel-lock-frequency-hz", SlipEffectOptions.Default.WheelLockFrequencyHz),
            WheelLockNoiseAmount = Float(parameters, "wheel-lock-noise-amount", SlipEffectOptions.Default.WheelLockNoiseAmount),
            MinimumSpeedKph = Float(parameters, "minimum-speed-kph", SlipEffectOptions.Default.MinimumSpeedKph),
            SlipRatioThreshold = Float(parameters, "slip-ratio-threshold", SlipEffectOptions.Default.SlipRatioThreshold),
            SlipAngleThresholdRadians = Float(parameters, "slip-angle-threshold-rad", SlipEffectOptions.Default.SlipAngleThresholdRadians),
            BrakeLockWheelSpeedRatioThreshold = Float(parameters, "wheel-lock-wheel-speed-ratio-threshold", SlipEffectOptions.Default.BrakeLockWheelSpeedRatioThreshold)
        };
    }

    private static EffectSettingsDocument GetDocumentOrDefault(
        IReadOnlyDictionary<string, EffectSettingsDocument> settings,
        IHapticEffectRegistry registry,
        string key)
    {
        return settings.TryGetValue(key, out var document)
            ? document
            : registry.GetRequired(key).CreateDefaultSettings();
    }

    private static EffectSettingsDocument Doc(
        string key,
        bool enabled,
        Dictionary<string, double> parameters,
        IHapticEffectRegistry registry)
    {
        return registry.GetRequired(key).Normalize(new EffectSettingsDocument(key, enabled, parameters));
    }

    private static Dictionary<string, double> Params(params (string Key, double Value)[] values)
    {
        return values.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static float Float(
        IReadOnlyDictionary<string, double> parameters,
        string key,
        double fallback)
    {
        return parameters.TryGetValue(key, out var value)
            ? (float)value
            : (float)fallback;
    }

    private static bool Bool(
        IReadOnlyDictionary<string, double> parameters,
        string key,
        bool fallback)
    {
        return parameters.TryGetValue(key, out var value)
            ? value >= 0.5d
            : fallback;
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
}
