using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.Safety;

namespace HapticDrive.Asio.App;

internal sealed record AudioProfileControlInputs(
    string? ProfileName,
    bool EngineEnabled,
    double EngineGainValue,
    double EngineMinimumFrequencyValue,
    double EngineMaximumFrequencyValue,
    bool GearShiftEnabled,
    double GearShiftGainValue,
    double GearShiftDurationValue,
    bool KerbEnabled,
    double KerbGainValue,
    double KerbBaseFrequencyValue,
    bool ImpactEnabled,
    double ImpactGainValue,
    double ImpactDurationValue,
    bool SharedRoadSignalEnabled,
    bool Bst1RoadOutputEnabled,
    double RoadTextureGainValue,
    double RoadTextureMinimumSpeedValue,
    double RoadTextureSpeedReferenceValue,
    double RoadTextureLowSpeedFrequencyValue,
    double RoadTextureHighSpeedFrequencyValue,
    double RoadTextureSpeedFrequencyInfluenceValue,
    double RoadTextureGrainAmountValue,
    bool SlipWheelSlipEnabled,
    double SlipWheelSlipGainValue,
    double SlipWheelSlipFrequencyValue,
    double SlipWheelSlipNoiseValue,
    bool SlipWheelLockEnabled,
    double SlipWheelLockGainValue,
    double SlipWheelLockFrequencyValue,
    double SlipWheelLockNoiseValue,
    double SlipWheelLockSensitivityValue,
    double SlipThresholdValue,
    double MasterGainValue,
    bool MixerMuted,
    double SafetyOutputGainValue);

internal sealed record AudioProfileControlValues(
    string ProfileName,
    bool EngineEnabled,
    float EngineGain,
    float EngineMinimumFrequencyHz,
    float EngineMaximumFrequencyHz,
    bool GearShiftEnabled,
    float GearShiftGain,
    int GearShiftDurationMilliseconds,
    bool KerbEnabled,
    float KerbGain,
    float KerbBaseFrequencyHz,
    bool ImpactEnabled,
    float ImpactGain,
    int ImpactDurationMilliseconds,
    bool SharedRoadSignalEnabled,
    bool Bst1RoadOutputEnabled,
    float RoadTextureGain,
    float RoadTextureMinimumSpeedKph,
    float RoadTextureSpeedReferenceKph,
    float RoadTextureLowSpeedFrequencyHz,
    float RoadTextureHighSpeedFrequencyHz,
    float RoadTextureSpeedFrequencyInfluence,
    float RoadTextureGrainAmount,
    bool SlipWheelSlipEnabled,
    float SlipWheelSlipGain,
    float SlipWheelSlipFrequencyHz,
    float SlipWheelSlipNoiseAmount,
    bool SlipWheelLockEnabled,
    float SlipWheelLockGain,
    float SlipWheelLockFrequencyHz,
    float SlipWheelLockNoiseAmount,
    float SlipWheelLockSensitivity,
    float SlipThreshold,
    float MasterGain,
    bool MixerMuted,
    float SafetyOutputGain);

internal sealed record AudioProfileControlTextValues(
    string EngineGainText,
    string EngineFrequencyText,
    string GearShiftGainText,
    string GearShiftDurationText,
    string KerbGainText,
    string KerbFrequencyText,
    string ImpactGainText,
    string ImpactDurationText,
    string RoadTextureGainText,
    string RoadTextureMinimumSpeedText,
    string RoadTextureSpeedReferenceText,
    string RoadTextureLowSpeedFrequencyText,
    string RoadTextureHighSpeedFrequencyText,
    string RoadTextureSpeedFrequencyInfluenceText,
    string RoadTextureGrainAmountText,
    string SlipWheelSlipGainText,
    string SlipWheelSlipFrequencyText,
    string SlipWheelSlipNoiseText,
    string SlipWheelLockGainText,
    string SlipWheelLockFrequencyText,
    string SlipWheelLockNoiseText,
    string SlipWheelLockSensitivityText,
    string SlipThresholdText,
    string MasterGainText,
    string SafetyOutputGainText);

internal sealed record AudioProfileControlApplicationPlan(
    HapticDriveProfile SafeProfile,
    AudioProfileControlValues ControlValues,
    AudioProfileControlTextValues TextValues);

internal static class AudioProfileControlSnapshotBuilder
{
    public static HapticDriveProfile BuildProfile(
        HapticDriveProfile currentProfile,
        AudioProfileControlInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(currentProfile);
        ArgumentNullException.ThrowIfNull(inputs);

        var name = string.IsNullOrWhiteSpace(inputs.ProfileName)
            ? currentProfile.Name
            : inputs.ProfileName.Trim();
        var effects = currentProfile.Effects;
        var engineMinimumFrequency = (float)inputs.EngineMinimumFrequencyValue;
        var engineMaximumFrequency = Math.Max(engineMinimumFrequency, (float)inputs.EngineMaximumFrequencyValue);

        return HapticProfileValidator.Validate(currentProfile with
        {
            Name = name,
            Effects = effects with
            {
                Engine = effects.Engine with
                {
                    IsEnabled = inputs.EngineEnabled,
                    Gain = (float)inputs.EngineGainValue,
                    MinimumFrequencyHz = engineMinimumFrequency,
                    MaximumFrequencyHz = engineMaximumFrequency
                },
                GearShift = effects.GearShift with
                {
                    IsEnabled = inputs.GearShiftEnabled,
                    Gain = (float)inputs.GearShiftGainValue,
                    PulseDurationMilliseconds = (int)Math.Round(inputs.GearShiftDurationValue)
                },
                Kerb = effects.Kerb with
                {
                    IsEnabled = inputs.KerbEnabled,
                    Gain = (float)inputs.KerbGainValue,
                    BaseFrequencyHz = (float)inputs.KerbBaseFrequencyValue
                },
                Impact = effects.Impact with
                {
                    IsEnabled = inputs.ImpactEnabled,
                    Gain = (float)inputs.ImpactGainValue,
                    PulseDurationMilliseconds = (int)Math.Round(inputs.ImpactDurationValue)
                },
                RoadTexture = effects.RoadTexture with
                {
                    IsEnabled = inputs.SharedRoadSignalEnabled,
                    Bst1OutputEnabled = inputs.Bst1RoadOutputEnabled,
                    Gain = (float)inputs.RoadTextureGainValue,
                    MinimumSpeedKph = (float)inputs.RoadTextureMinimumSpeedValue,
                    FullIntensitySpeedKph = (float)inputs.RoadTextureSpeedReferenceValue,
                    LowSpeedFrequencyHz = (float)inputs.RoadTextureLowSpeedFrequencyValue,
                    HighSpeedFrequencyHz = (float)inputs.RoadTextureHighSpeedFrequencyValue,
                    SpeedFrequencyInfluence = (float)inputs.RoadTextureSpeedFrequencyInfluenceValue,
                    GrainAmount = (float)inputs.RoadTextureGrainAmountValue
                },
                Slip = effects.Slip with
                {
                    IsEnabled = inputs.SlipWheelSlipEnabled || inputs.SlipWheelLockEnabled,
                    Gain = (float)inputs.SlipWheelSlipGainValue,
                    BaseFrequencyHz = (float)inputs.SlipWheelSlipFrequencyValue,
                    SlipRatioThreshold = (float)inputs.SlipThresholdValue,
                    WheelSlipEnabled = inputs.SlipWheelSlipEnabled,
                    WheelSlipNoiseAmount = (float)inputs.SlipWheelSlipNoiseValue,
                    WheelLockEnabled = inputs.SlipWheelLockEnabled,
                    WheelLockGain = (float)inputs.SlipWheelLockGainValue,
                    WheelLockFrequencyHz = (float)inputs.SlipWheelLockFrequencyValue,
                    WheelLockNoiseAmount = (float)inputs.SlipWheelLockNoiseValue,
                    WheelLockWheelSpeedRatioThreshold = (float)inputs.SlipWheelLockSensitivityValue
                }
            },
            Mixer = currentProfile.Mixer with
            {
                MasterGain = (float)inputs.MasterGainValue,
                IsMuted = inputs.MixerMuted
            },
            Safety = currentProfile.Safety with
            {
                OutputGain = (float)inputs.SafetyOutputGainValue,
                OutputGainCeiling = AudioSafetyProcessorOptions.DefaultOutputGainCeiling,
                LimiterEnabled = true
            }
        }).Profile;
    }

    public static AudioProfileControlApplicationPlan BuildApplicationPlan(HapticDriveProfile? profile)
    {
        var safeProfile = HapticProfileValidator.Validate(profile).Profile;
        var slip = safeProfile.Effects.Slip;
        var controlValues = new AudioProfileControlValues(
            ProfileName: safeProfile.Name,
            EngineEnabled: safeProfile.Effects.Engine.IsEnabled,
            EngineGain: safeProfile.Effects.Engine.Gain,
            EngineMinimumFrequencyHz: safeProfile.Effects.Engine.MinimumFrequencyHz,
            EngineMaximumFrequencyHz: safeProfile.Effects.Engine.MaximumFrequencyHz,
            GearShiftEnabled: safeProfile.Effects.GearShift.IsEnabled,
            GearShiftGain: safeProfile.Effects.GearShift.Gain,
            GearShiftDurationMilliseconds: safeProfile.Effects.GearShift.PulseDurationMilliseconds,
            KerbEnabled: safeProfile.Effects.Kerb.IsEnabled,
            KerbGain: safeProfile.Effects.Kerb.Gain,
            KerbBaseFrequencyHz: safeProfile.Effects.Kerb.BaseFrequencyHz,
            ImpactEnabled: safeProfile.Effects.Impact.IsEnabled,
            ImpactGain: safeProfile.Effects.Impact.Gain,
            ImpactDurationMilliseconds: safeProfile.Effects.Impact.PulseDurationMilliseconds,
            SharedRoadSignalEnabled: safeProfile.Effects.RoadTexture.IsEnabled,
            Bst1RoadOutputEnabled: safeProfile.Effects.RoadTexture.Bst1OutputEnabled == true,
            RoadTextureGain: safeProfile.Effects.RoadTexture.Gain,
            RoadTextureMinimumSpeedKph: safeProfile.Effects.RoadTexture.MinimumSpeedKph,
            RoadTextureSpeedReferenceKph: safeProfile.Effects.RoadTexture.FullIntensitySpeedKph,
            RoadTextureLowSpeedFrequencyHz: safeProfile.Effects.RoadTexture.LowSpeedFrequencyHz,
            RoadTextureHighSpeedFrequencyHz: safeProfile.Effects.RoadTexture.HighSpeedFrequencyHz,
            RoadTextureSpeedFrequencyInfluence: safeProfile.Effects.RoadTexture.SpeedFrequencyInfluence,
            RoadTextureGrainAmount: safeProfile.Effects.RoadTexture.GrainAmount,
            SlipWheelSlipEnabled: slip.WheelSlipEnabled ?? slip.IsEnabled,
            SlipWheelSlipGain: slip.Gain,
            SlipWheelSlipFrequencyHz: slip.BaseFrequencyHz,
            SlipWheelSlipNoiseAmount: slip.WheelSlipNoiseAmount ?? SlipEffectOptions.Default.WheelSlipNoiseAmount,
            SlipWheelLockEnabled: slip.WheelLockEnabled ?? slip.IsEnabled,
            SlipWheelLockGain: slip.WheelLockGain ?? slip.Gain,
            SlipWheelLockFrequencyHz: slip.WheelLockFrequencyHz ?? SlipEffectOptions.Default.WheelLockFrequencyHz,
            SlipWheelLockNoiseAmount: slip.WheelLockNoiseAmount ?? SlipEffectOptions.Default.WheelLockNoiseAmount,
            SlipWheelLockSensitivity: slip.WheelLockWheelSpeedRatioThreshold ?? SlipEffectOptions.Default.BrakeLockWheelSpeedRatioThreshold,
            SlipThreshold: slip.SlipRatioThreshold,
            MasterGain: safeProfile.Mixer.MasterGain,
            MixerMuted: safeProfile.Mixer.IsMuted,
            SafetyOutputGain: safeProfile.Safety.OutputGain);

        return new AudioProfileControlApplicationPlan(
            SafeProfile: safeProfile,
            ControlValues: controlValues,
            TextValues: BuildTextValues(safeProfile, slip));
    }

    private static AudioProfileControlTextValues BuildTextValues(HapticDriveProfile profile, SlipTuning slip)
    {
        return new AudioProfileControlTextValues(
            EngineGainText: $"{profile.Effects.Engine.Gain:P0}",
            EngineFrequencyText: $"{profile.Effects.Engine.MinimumFrequencyHz:0}-{profile.Effects.Engine.MaximumFrequencyHz:0} Hz",
            GearShiftGainText: $"{profile.Effects.GearShift.Gain:P0}",
            GearShiftDurationText: $"{profile.Effects.GearShift.PulseDurationMilliseconds} ms",
            KerbGainText: $"{profile.Effects.Kerb.Gain:P0}",
            KerbFrequencyText: $"{profile.Effects.Kerb.BaseFrequencyHz:0} Hz",
            ImpactGainText: $"{profile.Effects.Impact.Gain:P0}",
            ImpactDurationText: $"{profile.Effects.Impact.PulseDurationMilliseconds} ms",
            RoadTextureGainText: $"{profile.Effects.RoadTexture.Gain:P0}",
            RoadTextureMinimumSpeedText: $"{profile.Effects.RoadTexture.MinimumSpeedKph:0} km/h",
            RoadTextureSpeedReferenceText: $"{profile.Effects.RoadTexture.FullIntensitySpeedKph:0} km/h",
            RoadTextureLowSpeedFrequencyText: $"{profile.Effects.RoadTexture.LowSpeedFrequencyHz:0} Hz",
            RoadTextureHighSpeedFrequencyText: $"{profile.Effects.RoadTexture.HighSpeedFrequencyHz:0} Hz",
            RoadTextureSpeedFrequencyInfluenceText: $"{profile.Effects.RoadTexture.SpeedFrequencyInfluence:P0}",
            RoadTextureGrainAmountText: $"{profile.Effects.RoadTexture.GrainAmount:P0}",
            SlipWheelSlipGainText: $"{slip.Gain:P0}",
            SlipWheelSlipFrequencyText: $"{slip.BaseFrequencyHz:0} Hz",
            SlipWheelSlipNoiseText: $"{(slip.WheelSlipNoiseAmount ?? SlipEffectOptions.Default.WheelSlipNoiseAmount):P0}",
            SlipWheelLockGainText: $"{(slip.WheelLockGain ?? slip.Gain):P0}",
            SlipWheelLockFrequencyText: $"{(slip.WheelLockFrequencyHz ?? SlipEffectOptions.Default.WheelLockFrequencyHz):0} Hz",
            SlipWheelLockNoiseText: $"{(slip.WheelLockNoiseAmount ?? SlipEffectOptions.Default.WheelLockNoiseAmount):P0}",
            SlipWheelLockSensitivityText: $"{(slip.WheelLockWheelSpeedRatioThreshold ?? SlipEffectOptions.Default.BrakeLockWheelSpeedRatioThreshold):0.00}",
            SlipThresholdText: $"{slip.SlipRatioThreshold:0.00}",
            MasterGainText: $"{profile.Mixer.MasterGain:P0}",
            SafetyOutputGainText: $"{profile.Safety.OutputGain:P0}");
    }
}
