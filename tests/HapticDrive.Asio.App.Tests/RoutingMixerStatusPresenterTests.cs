using HapticDrive.Asio.Audio.Effects;

namespace HapticDrive.Asio.App.Tests;

public sealed class RoutingMixerStatusPresenterTests
{
    [Fact]
    public void Build_WhenRoutingIsIdle_ShowsDefaultSafetyAndIdleSummaries()
    {
        var presentation = RoutingMixerStatusPresenter.Build(CreateSnapshot());

        Assert.Equal("50%", presentation.MasterGainValueText);
        Assert.Equal("80%", presentation.SafetyOutputGainValueText);
        Assert.Equal("Emergency mute: off; normal mute: off.", presentation.MixerEmergencyMuteStatusText);
        Assert.Equal("Output peak: 0.000; mixer peak 0.000.", presentation.MixerOutputPeakStatusText);
        Assert.Equal("Limiter protection stays on automatically to protect the output path.", presentation.MixerLimiterActivityStatusText);
        Assert.Contains("Output mode Null Output; selected driver none; channel none; armed False; readiness Null output selected.", presentation.Bst1RoutingSummaryText, StringComparison.Ordinal);
        Assert.Contains("Effects: gear enabled/idle; road disabled; engine enabled/idle; kerb enabled/idle; impact enabled/idle; slip enabled/idle; lock enabled/idle.", presentation.Bst1EffectsSummaryText, StringComparison.Ordinal);
        Assert.Equal("0 active source(s); engine idle; gear idle; road idle; kerb idle; impact idle; slip/lock idle; output peak 0.000.", presentation.ActiveEffectsSummaryText);
        Assert.Equal("Master 50%; mute off; emergency mute off; output peak 0.000; active effects 0.", presentation.RoutingMixerPageStatusText);
    }

    [Fact]
    public void Build_WhenRoutingIsActive_ShowsLiveRoutingSummaries()
    {
        var presentation = RoutingMixerStatusPresenter.Build(CreateSnapshot(
            masterGain: 0.72,
            safetyOutputGain: 0.91,
            emergencyMuted: true,
            normalMuted: true,
            outputPeakLevel: 0.421f,
            mixerPeakLevel: 0.387f,
            limitedSampleCount: 12,
            selectedOutputModeText: "ASIO",
            selectedAsioDriverNameText: "Driver A",
            selectedAsioOutputChannelText: "2",
            asioArmed: true,
            trueAsioStatusText: "ready",
            bst1GearActive: true,
            bst1RoadEnabled: true,
            bst1RoadActive: true,
            engineActive: true,
            kerbActive: true,
            impactActive: true,
            wheelSlipActive: true,
            phprPedalsModeText: "Direct",
            brakeGearPulseEnabled: true,
            directReadinessText: "direct ready",
            directConnectionStateText: "Connected",
            brakeGearActive: true,
            brakeRoadEnabled: true,
            brakeRoadActive: true,
            brakeLockEnabled: true,
            brakeLockActive: true,
            throttleGearPulseEnabled: true,
            phprSoftwareCoexistenceStatusText: "Clean",
            realEmergencyStopActive: true,
            throttleGearActive: true,
            throttleRoadEnabled: true,
            throttleRoadActive: true,
            throttleSlipEnabled: true,
            throttleSlipActive: true,
            activeEffectCount: 5,
            gearShiftActive: true,
            roadTextureActive: true,
            slipLockActive: true));

        Assert.Equal("72%", presentation.MasterGainValueText);
        Assert.Equal("91%", presentation.SafetyOutputGainValueText);
        Assert.Equal("Emergency mute: on; normal mute: on.", presentation.MixerEmergencyMuteStatusText);
        Assert.Equal("Output peak: 0.421; mixer peak 0.387.", presentation.MixerOutputPeakStatusText);
        Assert.Equal("Limiter protection is active and has reduced peaks during this session.", presentation.MixerLimiterActivityStatusText);
        Assert.Contains("Output mode ASIO; selected driver Driver A; channel 2; armed True; readiness ready.", presentation.Bst1RoutingSummaryText, StringComparison.Ordinal);
        Assert.Contains("Effects: gear enabled/active; road enabled/active; engine enabled/active; kerb enabled/active; impact enabled/active; slip enabled/active; lock enabled/idle.", presentation.Bst1EffectsSummaryText, StringComparison.Ordinal);
        Assert.Equal("Mode Direct; brake pedal output enabled; direct ready; connection Connected.", presentation.BrakePhprRoutingSummaryText);
        Assert.Equal("Effects: gear enabled/active; road enabled/active; lock enabled/active.", presentation.BrakePhprEffectsSummaryText);
        Assert.Equal("Mode Direct; throttle pedal output enabled; coexistence Clean; emergency stop on.", presentation.ThrottlePhprRoutingSummaryText);
        Assert.Equal("Effects: gear enabled/active; road enabled/active; slip enabled/active.", presentation.ThrottlePhprEffectsSummaryText);
        Assert.Equal("5 active source(s); engine active; gear active; road active; kerb active; impact active; slip/lock active; output peak 0.421.", presentation.ActiveEffectsSummaryText);
        Assert.Equal("Master 72%; mute on; emergency mute on; output peak 0.421; active effects 5.", presentation.RoutingMixerPageStatusText);
    }

    [Fact]
    public void Build_WhenGenericActivityItemsExist_UsesThemForActiveSummary()
    {
        var presentation = RoutingMixerStatusPresenter.Build(CreateSnapshot(
            activeEffectCount: 2,
            outputPeakLevel: 0.222f) with
        {
            ActivityItems =
            [
                new HapticEffectActivityItem("engine", "active"),
                new HapticEffectActivityItem("new effect", "warming up")
            ]
        });

        Assert.Equal("2 active source(s); engine active; new effect warming up; output peak 0.222.", presentation.ActiveEffectsSummaryText);
    }

    [Fact]
    public void Build_WhenStructuredBst1EffectItemsExist_UsesThemForRoutingSummary()
    {
        var presentation = RoutingMixerStatusPresenter.Build(CreateSnapshot() with
        {
            Bst1Effects =
            [
                new Bst1EffectSummaryItem("engine", "engine", true, false),
                new Bst1EffectSummaryItem("gear", "gear", true, true),
                new Bst1EffectSummaryItem("road", "road", true, false),
                new Bst1EffectSummaryItem("kerb", "kerb", false, false),
                new Bst1EffectSummaryItem("impact", "impact", true, false),
                new Bst1EffectSummaryItem("slip", "slip", true, true),
                new Bst1EffectSummaryItem("lock", "lock", false, false)
            ]
        });

        Assert.Equal("Effects: gear enabled/active; road enabled/idle; engine enabled/idle; kerb disabled; impact enabled/idle; slip enabled/active; lock disabled.", presentation.Bst1EffectsSummaryText);
    }

    private static RoutingMixerStatusSnapshot CreateSnapshot(
        double masterGain = 0.50,
        double safetyOutputGain = 0.80,
        bool emergencyMuted = false,
        bool normalMuted = false,
        float outputPeakLevel = 0f,
        float mixerPeakLevel = 0f,
        int limitedSampleCount = 0,
        int clippedSampleCount = 0,
        string selectedOutputModeText = "Null Output",
        string selectedAsioDriverNameText = "none",
        string selectedAsioOutputChannelText = "none",
        bool asioArmed = false,
        string trueAsioStatusText = "Null output selected",
        bool bst1GearEnabled = true,
        bool bst1GearActive = false,
        bool bst1RoadEnabled = false,
        bool bst1RoadActive = false,
        bool engineEnabled = true,
        bool engineActive = false,
        bool kerbEnabled = true,
        bool kerbActive = false,
        bool impactEnabled = true,
        bool impactActive = false,
        bool wheelSlipEnabled = true,
        bool wheelSlipActive = false,
        bool wheelLockEnabled = true,
        bool wheelLockActive = false,
        string phprPedalsModeText = "Disabled",
        bool brakeGearPulseEnabled = false,
        string directReadinessText = "direct blocked: safety gate",
        string directConnectionStateText = "Disconnected",
        bool brakeGearActive = false,
        bool brakeRoadEnabled = false,
        bool brakeRoadActive = false,
        bool brakeLockEnabled = false,
        bool brakeLockActive = false,
        bool throttleGearPulseEnabled = false,
        string phprSoftwareCoexistenceStatusText = "Unknown",
        bool realEmergencyStopActive = false,
        bool throttleGearActive = false,
        bool throttleRoadEnabled = false,
        bool throttleRoadActive = false,
        bool throttleSlipEnabled = false,
        bool throttleSlipActive = false,
        int activeEffectCount = 0,
        bool gearShiftActive = false,
        bool roadTextureActive = false,
        bool slipLockActive = false)
    {
        return new RoutingMixerStatusSnapshot(
            MasterGain: masterGain,
            SafetyOutputGain: safetyOutputGain,
            EmergencyMuted: emergencyMuted,
            NormalMuted: normalMuted,
            OutputPeakLevel: outputPeakLevel,
            MixerPeakLevel: mixerPeakLevel,
            LimitedSampleCount: limitedSampleCount,
            ClippedSampleCount: clippedSampleCount,
            SelectedOutputModeText: selectedOutputModeText,
            SelectedAsioDriverNameText: selectedAsioDriverNameText,
            SelectedAsioOutputChannelText: selectedAsioOutputChannelText,
            AsioArmed: asioArmed,
            TrueAsioStatusText: trueAsioStatusText,
            Bst1GearEnabled: bst1GearEnabled,
            Bst1GearActive: bst1GearActive,
            Bst1RoadEnabled: bst1RoadEnabled,
            Bst1RoadActive: bst1RoadActive,
            EngineEnabled: engineEnabled,
            EngineActive: engineActive,
            KerbEnabled: kerbEnabled,
            KerbActive: kerbActive,
            ImpactEnabled: impactEnabled,
            ImpactActive: impactActive,
            WheelSlipEnabled: wheelSlipEnabled,
            WheelSlipActive: wheelSlipActive,
            WheelLockEnabled: wheelLockEnabled,
            WheelLockActive: wheelLockActive,
            PhprPedalsModeText: phprPedalsModeText,
            BrakeGearPulseEnabled: brakeGearPulseEnabled,
            DirectReadinessText: directReadinessText,
            DirectConnectionStateText: directConnectionStateText,
            BrakeGearActive: brakeGearActive,
            BrakeRoadEnabled: brakeRoadEnabled,
            BrakeRoadActive: brakeRoadActive,
            BrakeLockEnabled: brakeLockEnabled,
            BrakeLockActive: brakeLockActive,
            ThrottleGearPulseEnabled: throttleGearPulseEnabled,
            PhprSoftwareCoexistenceStatusText: phprSoftwareCoexistenceStatusText,
            RealEmergencyStopActive: realEmergencyStopActive,
            ThrottleGearActive: throttleGearActive,
            ThrottleRoadEnabled: throttleRoadEnabled,
            ThrottleRoadActive: throttleRoadActive,
            ThrottleSlipEnabled: throttleSlipEnabled,
            ThrottleSlipActive: throttleSlipActive,
            ActiveEffectCount: activeEffectCount,
            GearShiftActive: gearShiftActive,
            RoadTextureActive: roadTextureActive,
            SlipLockActive: slipLockActive);
    }
}
