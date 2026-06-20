using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Profiles;

namespace HapticDrive.Asio.App;

internal sealed record Bst1AudioProfileEffectControlValues(
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
    float SlipThreshold);

internal sealed record Bst1AudioProfileEffectControlInputs(
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
    double SlipThresholdValue);

internal sealed record Bst1AudioProfileEffectControlTextValues(
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
    string SlipThresholdText);

internal sealed record Bst1AudioProfileEffectControlApplicationSnapshot(
    Bst1AudioProfileEffectControlValues ControlValues,
    Bst1AudioProfileEffectControlTextValues TextValues);

internal static class Bst1AudioProfileEffectControlSnapshotBuilder
{
    public static HapticEffectTuning BuildProfileEffects(
        HapticEffectTuning currentEffects,
        Bst1AudioProfileEffectControlInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(currentEffects);
        ArgumentNullException.ThrowIfNull(inputs);

        var engineMinimumFrequency = (float)inputs.EngineMinimumFrequencyValue;
        var engineMaximumFrequency = Math.Max(engineMinimumFrequency, (float)inputs.EngineMaximumFrequencyValue);

        return currentEffects with
        {
            Engine = currentEffects.Engine with
            {
                IsEnabled = inputs.EngineEnabled,
                Gain = (float)inputs.EngineGainValue,
                MinimumFrequencyHz = engineMinimumFrequency,
                MaximumFrequencyHz = engineMaximumFrequency
            },
            GearShift = currentEffects.GearShift with
            {
                IsEnabled = inputs.GearShiftEnabled,
                Gain = (float)inputs.GearShiftGainValue,
                PulseDurationMilliseconds = (int)Math.Round(inputs.GearShiftDurationValue)
            },
            Kerb = currentEffects.Kerb with
            {
                IsEnabled = inputs.KerbEnabled,
                Gain = (float)inputs.KerbGainValue,
                BaseFrequencyHz = (float)inputs.KerbBaseFrequencyValue
            },
            Impact = currentEffects.Impact with
            {
                IsEnabled = inputs.ImpactEnabled,
                Gain = (float)inputs.ImpactGainValue,
                PulseDurationMilliseconds = (int)Math.Round(inputs.ImpactDurationValue)
            },
            RoadTexture = currentEffects.RoadTexture with
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
            Slip = currentEffects.Slip with
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
        };
    }

    public static Bst1AudioProfileEffectControlApplicationSnapshot BuildApplicationSnapshot(HapticDriveProfile safeProfile)
    {
        ArgumentNullException.ThrowIfNull(safeProfile);

        var slip = safeProfile.Effects.Slip;
        return new Bst1AudioProfileEffectControlApplicationSnapshot(
            ControlValues: new Bst1AudioProfileEffectControlValues(
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
                SlipThreshold: slip.SlipRatioThreshold),
            TextValues: new Bst1AudioProfileEffectControlTextValues(
                EngineGainText: $"{safeProfile.Effects.Engine.Gain:P0}",
                EngineFrequencyText: $"{safeProfile.Effects.Engine.MinimumFrequencyHz:0}-{safeProfile.Effects.Engine.MaximumFrequencyHz:0} Hz",
                GearShiftGainText: $"{safeProfile.Effects.GearShift.Gain:P0}",
                GearShiftDurationText: $"{safeProfile.Effects.GearShift.PulseDurationMilliseconds} ms",
                KerbGainText: $"{safeProfile.Effects.Kerb.Gain:P0}",
                KerbFrequencyText: $"{safeProfile.Effects.Kerb.BaseFrequencyHz:0} Hz",
                ImpactGainText: $"{safeProfile.Effects.Impact.Gain:P0}",
                ImpactDurationText: $"{safeProfile.Effects.Impact.PulseDurationMilliseconds} ms",
                RoadTextureGainText: $"{safeProfile.Effects.RoadTexture.Gain:P0}",
                RoadTextureMinimumSpeedText: $"{safeProfile.Effects.RoadTexture.MinimumSpeedKph:0} km/h",
                RoadTextureSpeedReferenceText: $"{safeProfile.Effects.RoadTexture.FullIntensitySpeedKph:0} km/h",
                RoadTextureLowSpeedFrequencyText: $"{safeProfile.Effects.RoadTexture.LowSpeedFrequencyHz:0} Hz",
                RoadTextureHighSpeedFrequencyText: $"{safeProfile.Effects.RoadTexture.HighSpeedFrequencyHz:0} Hz",
                RoadTextureSpeedFrequencyInfluenceText: $"{safeProfile.Effects.RoadTexture.SpeedFrequencyInfluence:P0}",
                RoadTextureGrainAmountText: $"{safeProfile.Effects.RoadTexture.GrainAmount:P0}",
                SlipWheelSlipGainText: $"{slip.Gain:P0}",
                SlipWheelSlipFrequencyText: $"{slip.BaseFrequencyHz:0} Hz",
                SlipWheelSlipNoiseText: $"{(slip.WheelSlipNoiseAmount ?? SlipEffectOptions.Default.WheelSlipNoiseAmount):P0}",
                SlipWheelLockGainText: $"{(slip.WheelLockGain ?? slip.Gain):P0}",
                SlipWheelLockFrequencyText: $"{(slip.WheelLockFrequencyHz ?? SlipEffectOptions.Default.WheelLockFrequencyHz):0} Hz",
                SlipWheelLockNoiseText: $"{(slip.WheelLockNoiseAmount ?? SlipEffectOptions.Default.WheelLockNoiseAmount):P0}",
                SlipWheelLockSensitivityText: $"{(slip.WheelLockWheelSpeedRatioThreshold ?? SlipEffectOptions.Default.BrakeLockWheelSpeedRatioThreshold):0.00}",
                SlipThresholdText: $"{slip.SlipRatioThreshold:0.00}"));
    }
}
