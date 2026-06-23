using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Safety;

namespace HapticDrive.Asio.Audio.Profiles;

public sealed record HapticDriveProfile(
    int Version,
    string Name,
    HapticEffectTuning Effects,
    HapticMixerTuning Mixer,
    HapticSafetyTuning Safety)
{
    public const int CurrentVersion = 2;

    public static HapticDriveProfile Default { get; } = CreateDefault();

    public int SchemaVersion { get; init; } = CurrentVersion;

    public IReadOnlyDictionary<string, EffectSettingsDocument> EffectSettings { get; init; } = new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, EffectSettingsDocument> UnknownEffectSettings { get; init; } = new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase);

    public HapticEffectEngineOptions ToEffectOptions()
    {
        var profile = HapticProfileValidator.Validate(this).Profile;
        return HapticEffectSettingsTranslator.ToEngineOptions(profile.ToEffectSettings());
    }

    public IReadOnlyDictionary<string, EffectSettingsDocument> ToEffectSettings()
    {
        return EffectSettings is { Count: > 0 }
            ? EffectSettings
            : HapticEffectSettingsTranslator.CreateDocumentsFromLegacy(
                Effects ?? HapticDriveProfile.Default.Effects,
                BuiltInHapticEffectRegistry.Instance);
    }

    public AudioMixerSettings ToMixerSettings(bool emergencyMute = false)
    {
        var profile = HapticProfileValidator.Validate(this).Profile;
        return new AudioMixerSettings(
            profile.Mixer.MasterGain,
            profile.Mixer.IsMuted,
            emergencyMute);
    }

    public AudioSafetyProcessorOptions ToSafetyOptions(bool emergencyMute = false)
    {
        var profile = HapticProfileValidator.Validate(this).Profile;
        return new AudioSafetyProcessorOptions(
            profile.Safety.OutputGain,
            profile.Safety.OutputGainCeiling,
            profile.Safety.LimiterEnabled,
            emergencyMute);
    }

    public static HapticDriveProfile FromRuntimeSettings(
        string name,
        HapticEffectEngineOptions effects,
        AudioMixerSettings mixer,
        AudioSafetyProcessorOptions safety)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(mixer);
        ArgumentNullException.ThrowIfNull(safety);

        var registry = BuiltInHapticEffectRegistry.Instance;
        var effectSettings = HapticEffectSettingsTranslator.CreateDocumentsFromOptions(effects, registry);

        return HapticProfileValidator.Validate(new HapticDriveProfile(
            CurrentVersion,
            name,
            HapticEffectSettingsTranslator.ToLegacyTuning(effectSettings),
            new HapticMixerTuning(mixer.MasterGain, mixer.IsMuted),
            new HapticSafetyTuning(
                safety.OutputGain,
                safety.OutputGainCeiling,
                safety.LimiterEnabled))
        {
            SchemaVersion = CurrentVersion,
            EffectSettings = effectSettings
        }).Profile;
    }

    private static HapticDriveProfile CreateDefault()
    {
        var effectOptions = HapticEffectEngineOptions.Default;
        var safeEffects = new HapticEffectTuning(
            new EngineVibrationTuning(
                IsEnabled: false,
                Gain: 0.5f,
                effectOptions.Engine.MinimumFrequencyHz,
                effectOptions.Engine.MaximumFrequencyHz),
            new GearShiftTuning(
                IsEnabled: false,
                Gain: 0.5f,
                effectOptions.GearShift.PulseFrequencyHz,
                (int)Math.Round(effectOptions.GearShift.PulseDuration.TotalMilliseconds)),
            new KerbTuning(
                IsEnabled: false,
                Gain: 0.5f,
                effectOptions.Kerb.BaseFrequencyHz,
                effectOptions.Kerb.MinimumSpeedKph,
                effectOptions.Kerb.FullIntensitySpeedKph),
            new ImpactTuning(
                IsEnabled: false,
                Gain: 0.5f,
                effectOptions.Impact.PulseFrequencyHz,
                (int)Math.Round(effectOptions.Impact.PulseDuration.TotalMilliseconds),
                (int)Math.Round(effectOptions.Impact.CooldownDuration.TotalMilliseconds),
                effectOptions.Impact.VerticalGDeltaThreshold),
            new RoadTextureTuning(
                IsEnabled: true,
                Gain: 1f,
                effectOptions.RoadTexture.MinimumSpeedKph,
                effectOptions.RoadTexture.FullIntensitySpeedKph)
            {
                Bst1OutputEnabled = true,
                LowSpeedFrequencyHz = effectOptions.RoadTexture.Bst1LowSpeedFrequencyHz,
                HighSpeedFrequencyHz = effectOptions.RoadTexture.Bst1HighSpeedFrequencyHz,
                SpeedFrequencyInfluence = effectOptions.RoadTexture.Bst1SpeedFrequencyInfluence,
                GrainAmount = effectOptions.RoadTexture.Bst1GrainAmount
            },
            new SlipTuning(
                IsEnabled: false,
                Gain: 0.5f,
                effectOptions.Slip.WheelSlipFrequencyHz,
                effectOptions.Slip.MinimumSpeedKph,
                effectOptions.Slip.SlipRatioThreshold,
                effectOptions.Slip.SlipAngleThresholdRadians)
            {
                WheelSlipEnabled = false,
                WheelSlipNoiseAmount = effectOptions.Slip.WheelSlipNoiseAmount,
                WheelLockEnabled = false,
                WheelLockGain = 0.5f,
                WheelLockFrequencyHz = effectOptions.Slip.WheelLockFrequencyHz,
                WheelLockNoiseAmount = effectOptions.Slip.WheelLockNoiseAmount,
                WheelLockWheelSpeedRatioThreshold = effectOptions.Slip.BrakeLockWheelSpeedRatioThreshold
            });
        var effectSettings = HapticEffectSettingsTranslator.CreateDocumentsFromLegacy(
            safeEffects,
            BuiltInHapticEffectRegistry.Instance);
        var profile = new HapticDriveProfile(
            CurrentVersion,
            "Current Rig Defaults",
            safeEffects,
            new HapticMixerTuning(
                AudioMixerSettings.Default.MasterGain,
                AudioMixerSettings.Default.IsMuted),
            new HapticSafetyTuning(
                AudioSafetyProcessorOptions.Default.OutputGain,
                AudioSafetyProcessorOptions.Default.OutputGainCeiling,
                AudioSafetyProcessorOptions.Default.LimiterEnabled));

        return profile with
        {
            SchemaVersion = CurrentVersion,
            EffectSettings = effectSettings
        };
    }
}

public sealed record HapticEffectTuning(
    EngineVibrationTuning Engine,
    GearShiftTuning GearShift,
    KerbTuning Kerb,
    ImpactTuning Impact,
    RoadTextureTuning RoadTexture,
    SlipTuning Slip);

public sealed record EngineVibrationTuning(
    bool IsEnabled,
    float Gain,
    float MinimumFrequencyHz,
    float MaximumFrequencyHz);

public sealed record GearShiftTuning(
    bool IsEnabled,
    float Gain,
    float PulseFrequencyHz,
    int PulseDurationMilliseconds);

public sealed record KerbTuning(
    bool IsEnabled,
    float Gain,
    float BaseFrequencyHz,
    float MinimumSpeedKph,
    float FullIntensitySpeedKph);

public sealed record ImpactTuning(
    bool IsEnabled,
    float Gain,
    float PulseFrequencyHz,
    int PulseDurationMilliseconds,
    int CooldownMilliseconds,
    float VerticalGDeltaThreshold);

public sealed record RoadTextureTuning(
    bool IsEnabled,
    float Gain,
    float MinimumSpeedKph,
    float FullIntensitySpeedKph)
{
    public bool? Bst1OutputEnabled { get; init; }

    public float LowSpeedFrequencyHz { get; init; } = RoadTextureEffectOptions.Default.Bst1LowSpeedFrequencyHz;

    public float HighSpeedFrequencyHz { get; init; } = RoadTextureEffectOptions.Default.Bst1HighSpeedFrequencyHz;

    public float SpeedFrequencyInfluence { get; init; } = RoadTextureEffectOptions.Default.Bst1SpeedFrequencyInfluence;

    public float GrainAmount { get; init; } = RoadTextureEffectOptions.Default.Bst1GrainAmount;
}

public sealed record SlipTuning(
    bool IsEnabled,
    float Gain,
    float BaseFrequencyHz,
    float MinimumSpeedKph,
    float SlipRatioThreshold,
    float SlipAngleThresholdRadians)
{
    public bool? WheelSlipEnabled { get; init; }

    public float? WheelSlipNoiseAmount { get; init; }

    public bool? WheelLockEnabled { get; init; }

    public float? WheelLockGain { get; init; }

    public float? WheelLockFrequencyHz { get; init; }

    public float? WheelLockNoiseAmount { get; init; }

    public float? WheelLockWheelSpeedRatioThreshold { get; init; }
}

public sealed record HapticMixerTuning(
    float MasterGain,
    bool IsMuted);

public sealed record HapticSafetyTuning(
    float OutputGain,
    float OutputGainCeiling,
    bool LimiterEnabled);

public sealed record HapticProfileValidationResult(
    HapticDriveProfile Profile,
    bool IsSupportedVersion,
    bool WasRepaired,
    IReadOnlyList<string> Messages);

public static class HapticProfileValidator
{
    public static HapticProfileValidationResult Validate(HapticDriveProfile? profile)
    {
        var messages = new List<string>();
        if (profile is null)
        {
            messages.Add("Profile was missing; conservative defaults were used.");
            return new HapticProfileValidationResult(
                HapticDriveProfile.Default,
                IsSupportedVersion: true,
                WasRepaired: true,
                messages);
        }

        if (profile.Version != HapticDriveProfile.CurrentVersion && profile.SchemaVersion != HapticDriveProfile.CurrentVersion)
        {
            messages.Add($"Profile version {Math.Max(profile.Version, profile.SchemaVersion)} is not supported.");
            return new HapticProfileValidationResult(
                HapticDriveProfile.Default with { Name = SafeName(profile.Name) },
                IsSupportedVersion: false,
                WasRepaired: true,
                messages);
        }

        var repaired = false;
        var defaultProfile = HapticDriveProfile.Default;
        var registry = BuiltInHapticEffectRegistry.Instance;
        IReadOnlyDictionary<string, EffectSettingsDocument> normalizedEffectSettings;
        if (profile.EffectSettings is { Count: > 0 })
        {
            var messageCountBeforeNormalize = messages.Count;
            var normalizedFromProfile = HapticEffectSettingsTranslator.NormalizeDocuments(
                profile.EffectSettings,
                registry,
                messages);
            repaired |= messages.Count > messageCountBeforeNormalize;

            if (profile.Effects is not null)
            {
                var normalizedFromLegacy = HapticEffectSettingsTranslator.CreateDocumentsFromLegacy(profile.Effects, registry);
                if (!EffectSettingsMatch(normalizedFromProfile, normalizedFromLegacy))
                {
                    repaired = true;
                    messages.Add("Effect settings were resynchronized from legacy effect tuning changes.");
                    normalizedEffectSettings = normalizedFromLegacy;
                }
                else
                {
                    normalizedEffectSettings = normalizedFromProfile;
                }
            }
            else
            {
                normalizedEffectSettings = normalizedFromProfile;
            }
        }
        else if (profile.Effects is not null)
        {
            repaired = true;
            messages.Add("Effect settings were missing; they were rebuilt from legacy effect tuning.");
            normalizedEffectSettings = HapticEffectSettingsTranslator.CreateDocumentsFromLegacy(profile.Effects, registry);
        }
        else
        {
            repaired = true;
            messages.Add("Effect settings were missing; descriptor defaults were used.");
            normalizedEffectSettings = HapticEffectSettingsTranslator.CreateDefaultDocuments(registry);
        }

        var effects = HapticEffectSettingsTranslator.ToLegacyTuning(normalizedEffectSettings);

        var mixer = profile.Mixer ?? defaultProfile.Mixer;
        if (profile.Mixer is null)
        {
            repaired = true;
            messages.Add("Mixer tuning was missing; defaults were used.");
        }

        var safety = profile.Safety ?? defaultProfile.Safety;
        if (profile.Safety is null)
        {
            repaired = true;
            messages.Add("Safety tuning was missing; defaults were used.");
        }

        var slipWheelSlipEnabled = effects.Slip?.WheelSlipEnabled
            ?? effects.Slip?.IsEnabled
            ?? defaultProfile.Effects.Slip.WheelSlipEnabled
            ?? defaultProfile.Effects.Slip.IsEnabled;
        var slipWheelLockEnabled = effects.Slip?.WheelLockEnabled
            ?? effects.Slip?.IsEnabled
            ?? defaultProfile.Effects.Slip.WheelLockEnabled
            ?? defaultProfile.Effects.Slip.IsEnabled;

        var repairedEffects = new HapticEffectTuning(
            new EngineVibrationTuning(
                effects.Engine?.IsEnabled ?? defaultProfile.Effects.Engine.IsEnabled,
                Clamp(effects.Engine?.Gain, 0f, 1f, defaultProfile.Effects.Engine.Gain, "engine gain", messages, ref repaired),
                Clamp(effects.Engine?.MinimumFrequencyHz, 15f, 80f, defaultProfile.Effects.Engine.MinimumFrequencyHz, "engine minimum frequency", messages, ref repaired),
                ClampAtLeast(
                    Clamp(effects.Engine?.MaximumFrequencyHz, 20f, 120f, defaultProfile.Effects.Engine.MaximumFrequencyHz, "engine maximum frequency", messages, ref repaired),
                    minimum: Clamp(effects.Engine?.MinimumFrequencyHz, 15f, 80f, defaultProfile.Effects.Engine.MinimumFrequencyHz, "engine minimum frequency", messages, ref repaired),
                    fallback: defaultProfile.Effects.Engine.MaximumFrequencyHz,
                    "engine maximum frequency",
                    messages,
                    ref repaired)),
            new GearShiftTuning(
                effects.GearShift?.IsEnabled ?? defaultProfile.Effects.GearShift.IsEnabled,
                Clamp(effects.GearShift?.Gain, 0f, 1f, defaultProfile.Effects.GearShift.Gain, "gear shift gain", messages, ref repaired),
                Clamp(effects.GearShift?.PulseFrequencyHz, 5f, 120f, defaultProfile.Effects.GearShift.PulseFrequencyHz, "gear shift pulse frequency", messages, ref repaired),
                Clamp(effects.GearShift?.PulseDurationMilliseconds, 10, 250, defaultProfile.Effects.GearShift.PulseDurationMilliseconds, "gear shift pulse duration", messages, ref repaired)),
            new KerbTuning(
                effects.Kerb?.IsEnabled ?? defaultProfile.Effects.Kerb.IsEnabled,
                Clamp(effects.Kerb?.Gain, 0f, 1f, defaultProfile.Effects.Kerb.Gain, "kerb gain", messages, ref repaired),
                Clamp(effects.Kerb?.BaseFrequencyHz, 5f, 120f, defaultProfile.Effects.Kerb.BaseFrequencyHz, "kerb base frequency", messages, ref repaired),
                Clamp(effects.Kerb?.MinimumSpeedKph, 0f, 80f, defaultProfile.Effects.Kerb.MinimumSpeedKph, "kerb minimum speed", messages, ref repaired),
                ClampAtLeast(
                    Clamp(effects.Kerb?.FullIntensitySpeedKph, 20f, 300f, defaultProfile.Effects.Kerb.FullIntensitySpeedKph, "kerb full-intensity speed", messages, ref repaired),
                    minimum: Clamp(effects.Kerb?.MinimumSpeedKph, 0f, 80f, defaultProfile.Effects.Kerb.MinimumSpeedKph, "kerb minimum speed", messages, ref repaired),
                    fallback: defaultProfile.Effects.Kerb.FullIntensitySpeedKph,
                    "kerb full-intensity speed",
                    messages,
                    ref repaired)),
            new ImpactTuning(
                effects.Impact?.IsEnabled ?? defaultProfile.Effects.Impact.IsEnabled,
                Clamp(effects.Impact?.Gain, 0f, 1f, defaultProfile.Effects.Impact.Gain, "impact gain", messages, ref repaired),
                Clamp(effects.Impact?.PulseFrequencyHz, 5f, 120f, defaultProfile.Effects.Impact.PulseFrequencyHz, "impact pulse frequency", messages, ref repaired),
                Clamp(effects.Impact?.PulseDurationMilliseconds, 10, 300, defaultProfile.Effects.Impact.PulseDurationMilliseconds, "impact pulse duration", messages, ref repaired),
                Clamp(effects.Impact?.CooldownMilliseconds, 0, 1000, defaultProfile.Effects.Impact.CooldownMilliseconds, "impact cooldown", messages, ref repaired),
                Clamp(effects.Impact?.VerticalGDeltaThreshold, 0.1f, 5f, defaultProfile.Effects.Impact.VerticalGDeltaThreshold, "impact vertical-G threshold", messages, ref repaired)),
            new RoadTextureTuning(
                effects.RoadTexture?.IsEnabled ?? defaultProfile.Effects.RoadTexture.IsEnabled,
                Clamp(effects.RoadTexture?.Gain, 0f, 1f, defaultProfile.Effects.RoadTexture.Gain, "BST-1 / ASIO road output gain", messages, ref repaired),
                Clamp(effects.RoadTexture?.MinimumSpeedKph, 0f, 80f, defaultProfile.Effects.RoadTexture.MinimumSpeedKph, "road texture minimum speed", messages, ref repaired),
                ClampAtLeast(
                    Clamp(effects.RoadTexture?.FullIntensitySpeedKph, 20f, 360f, defaultProfile.Effects.RoadTexture.FullIntensitySpeedKph, "road texture speed reference", messages, ref repaired),
                    minimum: Clamp(effects.RoadTexture?.MinimumSpeedKph, 0f, 80f, defaultProfile.Effects.RoadTexture.MinimumSpeedKph, "road texture minimum speed", messages, ref repaired),
                    fallback: defaultProfile.Effects.RoadTexture.FullIntensitySpeedKph,
                    "road texture speed reference",
                    messages,
                    ref repaired))
            {
                Bst1OutputEnabled = effects.RoadTexture?.Bst1OutputEnabled
                    ?? effects.RoadTexture?.IsEnabled
                    ?? defaultProfile.Effects.RoadTexture.Bst1OutputEnabled
                    ?? defaultProfile.Effects.RoadTexture.IsEnabled
                ,
                LowSpeedFrequencyHz = Clamp(
                    effects.RoadTexture?.LowSpeedFrequencyHz,
                    20f,
                    70f,
                    defaultProfile.Effects.RoadTexture.LowSpeedFrequencyHz,
                    "road texture low-speed frequency",
                    messages,
                    ref repaired),
                HighSpeedFrequencyHz = ClampAtLeast(
                    Clamp(
                        effects.RoadTexture?.HighSpeedFrequencyHz,
                        30f,
                        90f,
                        defaultProfile.Effects.RoadTexture.HighSpeedFrequencyHz,
                        "road texture high-speed frequency",
                        messages,
                        ref repaired),
                    minimum: Clamp(
                        effects.RoadTexture?.LowSpeedFrequencyHz,
                        20f,
                        70f,
                        defaultProfile.Effects.RoadTexture.LowSpeedFrequencyHz,
                        "road texture low-speed frequency",
                        messages,
                        ref repaired),
                    fallback: defaultProfile.Effects.RoadTexture.HighSpeedFrequencyHz,
                    "road texture high-speed frequency",
                    messages,
                    ref repaired),
                SpeedFrequencyInfluence = Clamp(
                    effects.RoadTexture?.SpeedFrequencyInfluence,
                    0f,
                    1f,
                    defaultProfile.Effects.RoadTexture.SpeedFrequencyInfluence,
                    "road texture speed-frequency influence",
                    messages,
                    ref repaired),
                GrainAmount = Clamp(
                    effects.RoadTexture?.GrainAmount,
                    0f,
                    0.6f,
                    defaultProfile.Effects.RoadTexture.GrainAmount,
                    "road texture grain amount",
                    messages,
                    ref repaired)
            },
            new SlipTuning(
                slipWheelSlipEnabled || slipWheelLockEnabled,
                Clamp(effects.Slip?.Gain, 0f, 1f, defaultProfile.Effects.Slip.Gain, "slip gain", messages, ref repaired),
                Clamp(effects.Slip?.BaseFrequencyHz, 5f, 120f, defaultProfile.Effects.Slip.BaseFrequencyHz, "slip base frequency", messages, ref repaired),
                Clamp(effects.Slip?.MinimumSpeedKph, 0f, 120f, defaultProfile.Effects.Slip.MinimumSpeedKph, "slip minimum speed", messages, ref repaired),
                Clamp(effects.Slip?.SlipRatioThreshold, 0.01f, 1f, defaultProfile.Effects.Slip.SlipRatioThreshold, "slip ratio threshold", messages, ref repaired),
                Clamp(effects.Slip?.SlipAngleThresholdRadians, 0.01f, 1f, defaultProfile.Effects.Slip.SlipAngleThresholdRadians, "slip angle threshold", messages, ref repaired))
            {
                WheelSlipEnabled = slipWheelSlipEnabled,
                WheelSlipNoiseAmount = Clamp(
                    effects.Slip?.WheelSlipNoiseAmount,
                    0f,
                    1f,
                    defaultProfile.Effects.Slip.WheelSlipNoiseAmount ?? SlipEffectOptions.Default.WheelSlipNoiseAmount,
                    "slip roughness",
                    messages,
                    ref repaired),
                WheelLockEnabled = slipWheelLockEnabled,
                WheelLockGain = Clamp(
                    effects.Slip?.WheelLockGain ?? effects.Slip?.Gain,
                    0f,
                    1f,
                    defaultProfile.Effects.Slip.WheelLockGain ?? defaultProfile.Effects.Slip.Gain,
                    "wheel lock gain",
                    messages,
                    ref repaired),
                WheelLockFrequencyHz = Clamp(
                    effects.Slip?.WheelLockFrequencyHz,
                    5f,
                    120f,
                    defaultProfile.Effects.Slip.WheelLockFrequencyHz ?? SlipEffectOptions.Default.WheelLockFrequencyHz,
                    "wheel lock frequency",
                    messages,
                    ref repaired),
                WheelLockNoiseAmount = Clamp(
                    effects.Slip?.WheelLockNoiseAmount,
                    0f,
                    1f,
                    defaultProfile.Effects.Slip.WheelLockNoiseAmount ?? SlipEffectOptions.Default.WheelLockNoiseAmount,
                    "wheel lock roughness",
                    messages,
                    ref repaired),
                WheelLockWheelSpeedRatioThreshold = Clamp(
                    effects.Slip?.WheelLockWheelSpeedRatioThreshold,
                    0.05f,
                    1f,
                    defaultProfile.Effects.Slip.WheelLockWheelSpeedRatioThreshold ?? SlipEffectOptions.Default.BrakeLockWheelSpeedRatioThreshold,
                    "wheel lock wheel-speed ratio threshold",
                    messages,
                    ref repaired)
            });

        var repairedProfile = new HapticDriveProfile(
            HapticDriveProfile.CurrentVersion,
            SafeName(profile.Name),
            repairedEffects,
            new HapticMixerTuning(
                Clamp(mixer.MasterGain, 0f, 1f, defaultProfile.Mixer.MasterGain, "master gain", messages, ref repaired),
                mixer.IsMuted),
            new HapticSafetyTuning(
                Clamp(safety.OutputGain, 0f, 1f, defaultProfile.Safety.OutputGain, "safety output gain", messages, ref repaired),
                NormalizeSafetyOutputCeiling(safety.OutputGainCeiling, messages, ref repaired),
                NormalizeLimiterEnabled(safety.LimiterEnabled, messages, ref repaired)))
        {
            SchemaVersion = HapticDriveProfile.CurrentVersion,
            EffectSettings = normalizedEffectSettings,
            UnknownEffectSettings = profile.UnknownEffectSettings ?? defaultProfile.UnknownEffectSettings
        };

        if (repairedProfile.Name != profile.Name)
        {
            repaired = true;
            messages.Add("Profile name was missing; a safe name was used.");
        }

        return new HapticProfileValidationResult(
            repairedProfile,
            IsSupportedVersion: true,
            repaired,
            messages);
    }

    private static string SafeName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? "Current Rig Defaults" : name.Trim();
    }

    private static bool EffectSettingsMatch(
        IReadOnlyDictionary<string, EffectSettingsDocument> left,
        IReadOnlyDictionary<string, EffectSettingsDocument> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var other))
            {
                return false;
            }

            if (!string.Equals(pair.Value.EffectKey, other.EffectKey, StringComparison.OrdinalIgnoreCase)
                || pair.Value.Enabled != other.Enabled
                || pair.Value.Parameters.Count != other.Parameters.Count)
            {
                return false;
            }

            foreach (var parameter in pair.Value.Parameters)
            {
                if (!other.Parameters.TryGetValue(parameter.Key, out var otherValue)
                    || Math.Abs(parameter.Value - otherValue) > 0.0001d)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static float Clamp(
        float? value,
        float minimum,
        float maximum,
        float fallback,
        string name,
        ICollection<string> messages,
        ref bool repaired)
    {
        if (value is null || !float.IsFinite(value.Value))
        {
            repaired = true;
            messages.Add($"{name} was missing or invalid; defaulted to {fallback:0.###}.");
            return fallback;
        }

        if (value.Value < minimum || value.Value > maximum)
        {
            repaired = true;
            var clamped = Math.Clamp(value.Value, minimum, maximum);
            messages.Add($"{name} was clamped to {clamped:0.###}.");
            return clamped;
        }

        return value.Value;
    }

    private static int Clamp(
        int? value,
        int minimum,
        int maximum,
        int fallback,
        string name,
        ICollection<string> messages,
        ref bool repaired)
    {
        if (value is null)
        {
            repaired = true;
            messages.Add($"{name} was missing; defaulted to {fallback}.");
            return fallback;
        }

        if (value.Value < minimum || value.Value > maximum)
        {
            repaired = true;
            var clamped = Math.Clamp(value.Value, minimum, maximum);
            messages.Add($"{name} was clamped to {clamped}.");
            return clamped;
        }

        return value.Value;
    }

    private static float ClampAtLeast(
        float value,
        float minimum,
        float fallback,
        string name,
        ICollection<string> messages,
        ref bool repaired)
    {
        if (value >= minimum)
        {
            return value;
        }

        repaired = true;
        var repairedValue = Math.Max(minimum, fallback);
        messages.Add($"{name} was raised to {repairedValue:0.###}.");
        return repairedValue;
    }

    private static float NormalizeSafetyOutputCeiling(
        float value,
        ICollection<string> messages,
        ref bool repaired)
    {
        var normalized = AudioSafetyProcessorOptions.DefaultOutputGainCeiling;
        if (!float.IsFinite(value) || Math.Abs(value - normalized) > 0.0001f)
        {
            repaired = true;
            messages.Add($"safety output ceiling is now internal; normalized to {normalized:0.###}.");
        }

        return normalized;
    }

    private static bool NormalizeLimiterEnabled(
        bool value,
        ICollection<string> messages,
        ref bool repaired)
    {
        if (!value)
        {
            repaired = true;
            messages.Add("limiter remains internally enabled for safety.");
        }

        return true;
    }
}
