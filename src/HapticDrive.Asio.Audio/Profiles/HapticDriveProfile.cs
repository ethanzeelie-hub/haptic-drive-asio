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
    public const int CurrentVersion = 1;

    public static HapticDriveProfile Default { get; } = CreateDefault();

    public HapticEffectEngineOptions ToEffectOptions()
    {
        var profile = HapticProfileValidator.Validate(this).Profile;

        return new HapticEffectEngineOptions(
            EngineVibrationEffectOptions.Default with
            {
                IsEnabled = profile.Effects.Engine.IsEnabled,
                Gain = profile.Effects.Engine.Gain,
                MinimumFrequencyHz = profile.Effects.Engine.MinimumFrequencyHz,
                MaximumFrequencyHz = profile.Effects.Engine.MaximumFrequencyHz
            },
            GearShiftEffectOptions.Default with
            {
                IsEnabled = profile.Effects.GearShift.IsEnabled,
                Gain = profile.Effects.GearShift.Gain,
                PulseFrequencyHz = profile.Effects.GearShift.PulseFrequencyHz,
                PulseDuration = TimeSpan.FromMilliseconds(profile.Effects.GearShift.PulseDurationMilliseconds)
            },
            KerbEffectOptions.Default with
            {
                IsEnabled = profile.Effects.Kerb.IsEnabled,
                Gain = profile.Effects.Kerb.Gain,
                BaseFrequencyHz = profile.Effects.Kerb.BaseFrequencyHz,
                MinimumSpeedKph = profile.Effects.Kerb.MinimumSpeedKph,
                FullIntensitySpeedKph = profile.Effects.Kerb.FullIntensitySpeedKph
            },
            ImpactEffectOptions.Default with
            {
                IsEnabled = profile.Effects.Impact.IsEnabled,
                Gain = profile.Effects.Impact.Gain,
                PulseFrequencyHz = profile.Effects.Impact.PulseFrequencyHz,
                PulseDuration = TimeSpan.FromMilliseconds(profile.Effects.Impact.PulseDurationMilliseconds),
                CooldownDuration = TimeSpan.FromMilliseconds(profile.Effects.Impact.CooldownMilliseconds),
                VerticalGDeltaThreshold = profile.Effects.Impact.VerticalGDeltaThreshold
            },
            RoadTextureEffectOptions.Default with
            {
                IsEnabled = profile.Effects.RoadTexture.IsEnabled,
                Gain = profile.Effects.RoadTexture.Gain,
                MinimumSpeedKph = profile.Effects.RoadTexture.MinimumSpeedKph,
                FullIntensitySpeedKph = profile.Effects.RoadTexture.FullIntensitySpeedKph
            },
            SlipEffectOptions.Default with
            {
                IsEnabled = profile.Effects.Slip.IsEnabled,
                Gain = profile.Effects.Slip.Gain,
                BaseFrequencyHz = profile.Effects.Slip.BaseFrequencyHz,
                MinimumSpeedKph = profile.Effects.Slip.MinimumSpeedKph,
                SlipRatioThreshold = profile.Effects.Slip.SlipRatioThreshold,
                SlipAngleThresholdRadians = profile.Effects.Slip.SlipAngleThresholdRadians
            });
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

        return HapticProfileValidator.Validate(new HapticDriveProfile(
            CurrentVersion,
            name,
            new HapticEffectTuning(
                new EngineVibrationTuning(
                    effects.Engine.IsEnabled,
                    effects.Engine.Gain,
                    effects.Engine.MinimumFrequencyHz,
                    effects.Engine.MaximumFrequencyHz),
                new GearShiftTuning(
                    effects.GearShift.IsEnabled,
                    effects.GearShift.Gain,
                    effects.GearShift.PulseFrequencyHz,
                    (int)Math.Round(effects.GearShift.PulseDuration.TotalMilliseconds)),
                new KerbTuning(
                    effects.Kerb.IsEnabled,
                    effects.Kerb.Gain,
                    effects.Kerb.BaseFrequencyHz,
                    effects.Kerb.MinimumSpeedKph,
                    effects.Kerb.FullIntensitySpeedKph),
                new ImpactTuning(
                    effects.Impact.IsEnabled,
                    effects.Impact.Gain,
                    effects.Impact.PulseFrequencyHz,
                    (int)Math.Round(effects.Impact.PulseDuration.TotalMilliseconds),
                    (int)Math.Round(effects.Impact.CooldownDuration.TotalMilliseconds),
                    effects.Impact.VerticalGDeltaThreshold),
                new RoadTextureTuning(
                    effects.RoadTexture.IsEnabled,
                    effects.RoadTexture.Gain,
                    effects.RoadTexture.MinimumSpeedKph,
                    effects.RoadTexture.FullIntensitySpeedKph),
                new SlipTuning(
                    effects.Slip.IsEnabled,
                    effects.Slip.Gain,
                    effects.Slip.BaseFrequencyHz,
                    effects.Slip.MinimumSpeedKph,
                    effects.Slip.SlipRatioThreshold,
                    effects.Slip.SlipAngleThresholdRadians)),
            new HapticMixerTuning(mixer.MasterGain, mixer.IsMuted),
            new HapticSafetyTuning(
                safety.OutputGain,
                safety.OutputGainCeiling,
                safety.LimiterEnabled))).Profile;
    }

    private static HapticDriveProfile CreateDefault()
    {
        var effects = HapticEffectEngineOptions.Default;
        return new HapticDriveProfile(
            CurrentVersion,
            "Default Conservative",
            new HapticEffectTuning(
                new EngineVibrationTuning(
                    effects.Engine.IsEnabled,
                    effects.Engine.Gain,
                    effects.Engine.MinimumFrequencyHz,
                    effects.Engine.MaximumFrequencyHz),
                new GearShiftTuning(
                    effects.GearShift.IsEnabled,
                    effects.GearShift.Gain,
                    effects.GearShift.PulseFrequencyHz,
                    (int)Math.Round(effects.GearShift.PulseDuration.TotalMilliseconds)),
                new KerbTuning(
                    effects.Kerb.IsEnabled,
                    effects.Kerb.Gain,
                    effects.Kerb.BaseFrequencyHz,
                    effects.Kerb.MinimumSpeedKph,
                    effects.Kerb.FullIntensitySpeedKph),
                new ImpactTuning(
                    effects.Impact.IsEnabled,
                    effects.Impact.Gain,
                    effects.Impact.PulseFrequencyHz,
                    (int)Math.Round(effects.Impact.PulseDuration.TotalMilliseconds),
                    (int)Math.Round(effects.Impact.CooldownDuration.TotalMilliseconds),
                    effects.Impact.VerticalGDeltaThreshold),
                new RoadTextureTuning(
                    effects.RoadTexture.IsEnabled,
                    effects.RoadTexture.Gain,
                    effects.RoadTexture.MinimumSpeedKph,
                    effects.RoadTexture.FullIntensitySpeedKph),
                new SlipTuning(
                    effects.Slip.IsEnabled,
                    effects.Slip.Gain,
                    effects.Slip.BaseFrequencyHz,
                    effects.Slip.MinimumSpeedKph,
                    effects.Slip.SlipRatioThreshold,
                    effects.Slip.SlipAngleThresholdRadians)),
            new HapticMixerTuning(
                AudioMixerSettings.Default.MasterGain,
                AudioMixerSettings.Default.IsMuted),
            new HapticSafetyTuning(
                AudioSafetyProcessorOptions.Default.OutputGain,
                AudioSafetyProcessorOptions.Default.OutputGainCeiling,
                AudioSafetyProcessorOptions.Default.LimiterEnabled));
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
    float FullIntensitySpeedKph);

public sealed record SlipTuning(
    bool IsEnabled,
    float Gain,
    float BaseFrequencyHz,
    float MinimumSpeedKph,
    float SlipRatioThreshold,
    float SlipAngleThresholdRadians);

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

        if (profile.Version != HapticDriveProfile.CurrentVersion)
        {
            messages.Add($"Profile version {profile.Version} is not supported.");
            return new HapticProfileValidationResult(
                HapticDriveProfile.Default with { Name = SafeName(profile.Name) },
                IsSupportedVersion: false,
                WasRepaired: true,
                messages);
        }

        var repaired = false;
        var defaultProfile = HapticDriveProfile.Default;
        var effects = profile.Effects ?? defaultProfile.Effects;
        if (profile.Effects is null)
        {
            repaired = true;
            messages.Add("Effect tuning was missing; defaults were used.");
        }

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

        var repairedEffects = new HapticEffectTuning(
            new EngineVibrationTuning(
                effects.Engine?.IsEnabled ?? defaultProfile.Effects.Engine.IsEnabled,
                Clamp(effects.Engine?.Gain, 0f, 0.4f, defaultProfile.Effects.Engine.Gain, "engine gain", messages, ref repaired),
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
                Clamp(effects.GearShift?.Gain, 0f, 0.4f, defaultProfile.Effects.GearShift.Gain, "gear shift gain", messages, ref repaired),
                Clamp(effects.GearShift?.PulseFrequencyHz, 5f, 120f, defaultProfile.Effects.GearShift.PulseFrequencyHz, "gear shift pulse frequency", messages, ref repaired),
                Clamp(effects.GearShift?.PulseDurationMilliseconds, 10, 250, defaultProfile.Effects.GearShift.PulseDurationMilliseconds, "gear shift pulse duration", messages, ref repaired)),
            new KerbTuning(
                effects.Kerb?.IsEnabled ?? defaultProfile.Effects.Kerb.IsEnabled,
                Clamp(effects.Kerb?.Gain, 0f, 0.4f, defaultProfile.Effects.Kerb.Gain, "kerb gain", messages, ref repaired),
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
                Clamp(effects.Impact?.Gain, 0f, 0.4f, defaultProfile.Effects.Impact.Gain, "impact gain", messages, ref repaired),
                Clamp(effects.Impact?.PulseFrequencyHz, 5f, 120f, defaultProfile.Effects.Impact.PulseFrequencyHz, "impact pulse frequency", messages, ref repaired),
                Clamp(effects.Impact?.PulseDurationMilliseconds, 10, 300, defaultProfile.Effects.Impact.PulseDurationMilliseconds, "impact pulse duration", messages, ref repaired),
                Clamp(effects.Impact?.CooldownMilliseconds, 0, 1000, defaultProfile.Effects.Impact.CooldownMilliseconds, "impact cooldown", messages, ref repaired),
                Clamp(effects.Impact?.VerticalGDeltaThreshold, 0.1f, 5f, defaultProfile.Effects.Impact.VerticalGDeltaThreshold, "impact vertical-G threshold", messages, ref repaired)),
            new RoadTextureTuning(
                effects.RoadTexture?.IsEnabled ?? defaultProfile.Effects.RoadTexture.IsEnabled,
                Clamp(effects.RoadTexture?.Gain, 0f, 0.25f, defaultProfile.Effects.RoadTexture.Gain, "road texture gain", messages, ref repaired),
                Clamp(effects.RoadTexture?.MinimumSpeedKph, 0f, 80f, defaultProfile.Effects.RoadTexture.MinimumSpeedKph, "road texture minimum speed", messages, ref repaired),
                ClampAtLeast(
                    Clamp(effects.RoadTexture?.FullIntensitySpeedKph, 20f, 300f, defaultProfile.Effects.RoadTexture.FullIntensitySpeedKph, "road texture full-intensity speed", messages, ref repaired),
                    minimum: Clamp(effects.RoadTexture?.MinimumSpeedKph, 0f, 80f, defaultProfile.Effects.RoadTexture.MinimumSpeedKph, "road texture minimum speed", messages, ref repaired),
                    fallback: defaultProfile.Effects.RoadTexture.FullIntensitySpeedKph,
                    "road texture full-intensity speed",
                    messages,
                    ref repaired)),
            new SlipTuning(
                effects.Slip?.IsEnabled ?? defaultProfile.Effects.Slip.IsEnabled,
                Clamp(effects.Slip?.Gain, 0f, 0.3f, defaultProfile.Effects.Slip.Gain, "slip gain", messages, ref repaired),
                Clamp(effects.Slip?.BaseFrequencyHz, 5f, 120f, defaultProfile.Effects.Slip.BaseFrequencyHz, "slip base frequency", messages, ref repaired),
                Clamp(effects.Slip?.MinimumSpeedKph, 0f, 120f, defaultProfile.Effects.Slip.MinimumSpeedKph, "slip minimum speed", messages, ref repaired),
                Clamp(effects.Slip?.SlipRatioThreshold, 0.01f, 1f, defaultProfile.Effects.Slip.SlipRatioThreshold, "slip ratio threshold", messages, ref repaired),
                Clamp(effects.Slip?.SlipAngleThresholdRadians, 0.01f, 1f, defaultProfile.Effects.Slip.SlipAngleThresholdRadians, "slip angle threshold", messages, ref repaired)));

        var repairedProfile = new HapticDriveProfile(
            HapticDriveProfile.CurrentVersion,
            SafeName(profile.Name),
            repairedEffects,
            new HapticMixerTuning(
                Clamp(mixer.MasterGain, 0f, 1f, defaultProfile.Mixer.MasterGain, "master gain", messages, ref repaired),
                mixer.IsMuted),
            new HapticSafetyTuning(
                Clamp(safety.OutputGain, 0f, 0.5f, defaultProfile.Safety.OutputGain, "safety output gain", messages, ref repaired),
                Clamp(safety.OutputGainCeiling, 0.05f, AudioSafetyProcessorOptions.DefaultOutputGainCeiling, defaultProfile.Safety.OutputGainCeiling, "safety output ceiling", messages, ref repaired),
                safety.LimiterEnabled));

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
        return string.IsNullOrWhiteSpace(name) ? "Default Conservative" : name.Trim();
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
}
