namespace HapticDrive.Asio.App;

internal sealed record RoutingMixerStatusSnapshot(
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
    bool Bst1GearActive,
    bool Bst1RoadEnabled,
    bool Bst1RoadActive,
    bool EngineEnabled,
    bool EngineActive,
    bool KerbEnabled,
    bool KerbActive,
    bool ImpactEnabled,
    bool ImpactActive,
    bool WheelSlipEnabled,
    bool WheelSlipActive,
    bool WheelLockEnabled,
    bool WheelLockActive,
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
    bool ThrottleSlipActive,
    int ActiveEffectCount,
    bool GearShiftActive,
    bool RoadTextureActive,
    bool SlipLockActive);

internal sealed record RoutingMixerStatusPresentation(
    string MasterGainValueText,
    string SafetyOutputGainValueText,
    string MixerEmergencyMuteStatusText,
    string MixerOutputPeakStatusText,
    string MixerLimiterActivityStatusText,
    string Bst1RoutingSummaryText,
    string Bst1EffectsSummaryText,
    string BrakePhprRoutingSummaryText,
    string BrakePhprEffectsSummaryText,
    string ThrottlePhprRoutingSummaryText,
    string ThrottlePhprEffectsSummaryText,
    string ActiveEffectsSummaryText,
    string PriorityDuckingSummaryText,
    string RoutingMixerPageStatusText);

internal static class RoutingMixerStatusPresenter
{
    public static RoutingMixerStatusPresentation Build(RoutingMixerStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new RoutingMixerStatusPresentation(
            MasterGainValueText: $"{snapshot.MasterGain:P0}",
            SafetyOutputGainValueText: $"{snapshot.SafetyOutputGain:P0}",
            MixerEmergencyMuteStatusText: $"Emergency mute: {FormatOnOff(snapshot.EmergencyMuted)}; normal mute: {FormatOnOff(snapshot.NormalMuted)}.",
            MixerOutputPeakStatusText: $"Output peak: {snapshot.OutputPeakLevel:0.000}; mixer peak {snapshot.MixerPeakLevel:0.000}.",
            MixerLimiterActivityStatusText: snapshot.LimitedSampleCount > 0 || snapshot.ClippedSampleCount > 0
                ? "Limiter protection is active and has reduced peaks during this session."
                : "Limiter protection stays on automatically to protect the output path.",
            Bst1RoutingSummaryText: $"Output mode {snapshot.SelectedOutputModeText}; selected driver {snapshot.SelectedAsioDriverNameText}; channel {snapshot.SelectedAsioOutputChannelText}; armed {snapshot.AsioArmed}; readiness {snapshot.TrueAsioStatusText}.",
            Bst1EffectsSummaryText: $"Effects: gear {FormatEnabledActive(snapshot.Bst1GearEnabled, snapshot.Bst1GearActive)}; road {FormatEnabledActive(snapshot.Bst1RoadEnabled, snapshot.Bst1RoadActive)}; engine {FormatEnabledActive(snapshot.EngineEnabled, snapshot.EngineActive)}; kerb {FormatEnabledActive(snapshot.KerbEnabled, snapshot.KerbActive)}; impact {FormatEnabledActive(snapshot.ImpactEnabled, snapshot.ImpactActive)}; slip {FormatEnabledActive(snapshot.WheelSlipEnabled, snapshot.WheelSlipActive)}; lock {FormatEnabledActive(snapshot.WheelLockEnabled, snapshot.WheelLockActive)}.",
            BrakePhprRoutingSummaryText: $"Mode {snapshot.PhprPedalsModeText}; brake pedal output {FormatEnabled(snapshot.BrakeGearPulseEnabled)}; {snapshot.DirectReadinessText}; connection {snapshot.DirectConnectionStateText}.",
            BrakePhprEffectsSummaryText: $"Effects: gear {FormatEnabledActive(snapshot.BrakeGearPulseEnabled, snapshot.BrakeGearActive)}; road {FormatEnabledActive(snapshot.BrakeRoadEnabled, snapshot.BrakeRoadActive)}; lock {FormatEnabledActive(snapshot.BrakeLockEnabled, snapshot.BrakeLockActive)}.",
            ThrottlePhprRoutingSummaryText: $"Mode {snapshot.PhprPedalsModeText}; throttle pedal output {FormatEnabled(snapshot.ThrottleGearPulseEnabled)}; coexistence {snapshot.PhprSoftwareCoexistenceStatusText}; emergency stop {FormatOnOff(snapshot.RealEmergencyStopActive)}.",
            ThrottlePhprEffectsSummaryText: $"Effects: gear {FormatEnabledActive(snapshot.ThrottleGearPulseEnabled, snapshot.ThrottleGearActive)}; road {FormatEnabledActive(snapshot.ThrottleRoadEnabled, snapshot.ThrottleRoadActive)}; slip {FormatEnabledActive(snapshot.ThrottleSlipEnabled, snapshot.ThrottleSlipActive)}.",
            ActiveEffectsSummaryText: $"{snapshot.ActiveEffectCount:N0} active source(s); engine {FormatActiveIdle(snapshot.EngineActive)}; gear {FormatActiveIdle(snapshot.GearShiftActive)}; road {FormatActiveIdle(snapshot.RoadTextureActive)}; kerb {FormatActiveIdle(snapshot.KerbActive)}; impact {FormatActiveIdle(snapshot.ImpactActive)}; slip/lock {FormatActiveIdle(snapshot.SlipLockActive)}; output peak {snapshot.OutputPeakLevel:0.000}.",
            PriorityDuckingSummaryText: "Emergency stop and emergency mute override all output. Gear pulses take priority on P-HPR, and continuous pedal effects back off when higher-priority safety or gear activity needs the path.",
            RoutingMixerPageStatusText: $"Master {snapshot.MasterGain:P0}; mute {FormatOnOff(snapshot.NormalMuted)}; emergency mute {FormatOnOff(snapshot.EmergencyMuted)}; output peak {snapshot.OutputPeakLevel:0.000}; active effects {snapshot.ActiveEffectCount:N0}.");
    }

    private static string FormatEnabled(bool enabled)
    {
        return enabled ? "enabled" : "disabled";
    }

    private static string FormatOnOff(bool value)
    {
        return value ? "on" : "off";
    }

    private static string FormatActiveIdle(bool active)
    {
        return active ? "active" : "idle";
    }

    private static string FormatEnabledActive(bool enabled, bool active)
    {
        return enabled
            ? active ? "enabled/active" : "enabled/idle"
            : "disabled";
    }
}
