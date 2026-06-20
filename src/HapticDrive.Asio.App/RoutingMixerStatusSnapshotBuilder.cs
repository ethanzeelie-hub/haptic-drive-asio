using HapticDrive.Asio.Audio.Effects;

namespace HapticDrive.Asio.App;

internal sealed record RoutingMixerStatusBuildInputs(
    double MasterGain,
    double SafetyOutputGain,
    bool EmergencyMuted,
    bool NormalMuted,
    float OutputPeakLevel,
    float MixerPeakLevel,
    int LimitedSampleCount,
    int ClippedSampleCount,
    string SelectedOutputModeText,
    string SelectedAsioDriverNameText,
    string SelectedAsioOutputChannelText,
    bool AsioArmed,
    string TrueAsioStatusText,
    bool Bst1GearEnabled,
    HapticEffectEngineSnapshot EffectSnapshot,
    string PhprPedalsModeText,
    bool BrakeGearPulseEnabled,
    string DirectReadinessText,
    string DirectConnectionStateText,
    bool BrakeGearActive,
    bool BrakeRoadEnabled,
    bool BrakeRoadActive,
    bool BrakeLockEnabled,
    bool BrakeLockActive,
    bool ThrottleGearPulseEnabled,
    string PhprSoftwareCoexistenceStatusText,
    bool RealEmergencyStopActive,
    bool ThrottleGearActive,
    bool ThrottleRoadEnabled,
    bool ThrottleRoadActive,
    bool ThrottleSlipEnabled,
    bool ThrottleSlipActive);

internal static class RoutingMixerStatusSnapshotBuilder
{
    public static RoutingMixerStatusSnapshot Build(RoutingMixerStatusBuildInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.EffectSnapshot);

        var effectSnapshot = inputs.EffectSnapshot;
        return new RoutingMixerStatusSnapshot(
            MasterGain: inputs.MasterGain,
            SafetyOutputGain: inputs.SafetyOutputGain,
            EmergencyMuted: inputs.EmergencyMuted,
            NormalMuted: inputs.NormalMuted,
            OutputPeakLevel: inputs.OutputPeakLevel,
            MixerPeakLevel: inputs.MixerPeakLevel,
            LimitedSampleCount: inputs.LimitedSampleCount,
            ClippedSampleCount: inputs.ClippedSampleCount,
            SelectedOutputModeText: inputs.SelectedOutputModeText,
            SelectedAsioDriverNameText: inputs.SelectedAsioDriverNameText,
            SelectedAsioOutputChannelText: inputs.SelectedAsioOutputChannelText,
            AsioArmed: inputs.AsioArmed,
            TrueAsioStatusText: inputs.TrueAsioStatusText,
            Bst1GearEnabled: inputs.Bst1GearEnabled,
            Bst1GearActive: effectSnapshot.GearShift.IsActive,
            Bst1RoadEnabled: effectSnapshot.RoadTexture.Bst1OutputEnabled,
            Bst1RoadActive: effectSnapshot.RoadTexture.IsActive,
            EngineEnabled: effectSnapshot.Engine.IsEnabled,
            EngineActive: effectSnapshot.Engine.IsActive,
            KerbEnabled: effectSnapshot.Kerb.IsEnabled,
            KerbActive: effectSnapshot.Kerb.IsActive,
            ImpactEnabled: effectSnapshot.Impact.IsEnabled,
            ImpactActive: effectSnapshot.Impact.IsActive,
            WheelSlipEnabled: effectSnapshot.Slip.WheelSlipEnabled,
            WheelSlipActive: effectSnapshot.Slip.IsActive && string.Equals(effectSnapshot.Slip.ActiveSource, "Wheel slip", StringComparison.Ordinal),
            WheelLockEnabled: effectSnapshot.Slip.WheelLockEnabled,
            WheelLockActive: effectSnapshot.Slip.IsActive && string.Equals(effectSnapshot.Slip.ActiveSource, "Wheel lock", StringComparison.Ordinal),
            PhprPedalsModeText: inputs.PhprPedalsModeText,
            BrakeGearPulseEnabled: inputs.BrakeGearPulseEnabled,
            DirectReadinessText: inputs.DirectReadinessText,
            DirectConnectionStateText: inputs.DirectConnectionStateText,
            BrakeGearActive: inputs.BrakeGearActive,
            BrakeRoadEnabled: inputs.BrakeRoadEnabled,
            BrakeRoadActive: inputs.BrakeRoadActive,
            BrakeLockEnabled: inputs.BrakeLockEnabled,
            BrakeLockActive: inputs.BrakeLockActive,
            ThrottleGearPulseEnabled: inputs.ThrottleGearPulseEnabled,
            PhprSoftwareCoexistenceStatusText: inputs.PhprSoftwareCoexistenceStatusText,
            RealEmergencyStopActive: inputs.RealEmergencyStopActive,
            ThrottleGearActive: inputs.ThrottleGearActive,
            ThrottleRoadEnabled: inputs.ThrottleRoadEnabled,
            ThrottleRoadActive: inputs.ThrottleRoadActive,
            ThrottleSlipEnabled: inputs.ThrottleSlipEnabled,
            ThrottleSlipActive: inputs.ThrottleSlipActive,
            ActiveEffectCount: effectSnapshot.ActiveEffectCount,
            GearShiftActive: effectSnapshot.GearShift.IsActive,
            RoadTextureActive: effectSnapshot.RoadTexture.IsActive,
            SlipLockActive: effectSnapshot.Slip.IsActive)
        {
            ActivityItems = effectSnapshot.ActivityItems,
            Bst1Effects = Bst1EffectSummarySnapshotBuilder.Build(effectSnapshot).Items
        };
    }
}
