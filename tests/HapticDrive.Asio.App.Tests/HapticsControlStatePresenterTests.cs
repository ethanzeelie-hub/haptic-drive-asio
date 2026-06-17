using HapticDrive.Asio.App;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.App.Tests;

public sealed class HapticsControlStatePresenterTests
{
    [Fact]
    public void Build_MapsStoppedUnmutedNullOutputState()
    {
        var presentation = HapticsControlStatePresenter.Build(CreateSnapshot(
            hapticsStarted: false,
            pipelineRunning: false,
            outputStatus: CreateOutputStatus(AudioOutputDeviceKind.Null, AudioOutputDeviceState.Open, "Null output ready.", requiresPhysicalHardware: false)));

        Assert.Equal("Start Haptics", presentation.StartStopButtonText);
        Assert.Equal("Emergency Mute", presentation.EmergencyMuteButtonText);
        Assert.Equal("Stopped", presentation.HapticsStateText);
        Assert.Equal(HapticsDisplayState.Stopped, presentation.DisplayState);
        Assert.Equal(HapticsMuteState.None, presentation.MuteState);
        Assert.Equal(HapticsStartReadinessState.ReadyNullOutput, presentation.StartReadinessState);
        Assert.Equal(
            "Null output is selected; Start Haptics remains hardware-safe and produces no physical sound.",
            presentation.StartReadinessText);
    }

    [Fact]
    public void Build_MapsActiveUnmutedState()
    {
        var presentation = HapticsControlStatePresenter.Build(CreateSnapshot(
            hapticsStarted: true,
            pipelineRunning: true,
            activeEffectCount: 3,
            outputPeakLevel: 0.3214f,
            outputStatus: CreateOutputStatus(AudioOutputDeviceKind.Asio, AudioOutputDeviceState.Started, "ASIO running.", isHardwareArmed: true)));

        Assert.Equal("Stop Haptics", presentation.StartStopButtonText);
        Assert.Equal("Emergency Mute", presentation.EmergencyMuteButtonText);
        Assert.Equal("3 effect(s); peak 0.321", presentation.HapticsStateText);
        Assert.Equal(HapticsDisplayState.ActiveEffects, presentation.DisplayState);
        Assert.Equal(HapticsStartReadinessState.Running, presentation.StartReadinessState);
        Assert.Equal("Haptics running through ASIO Output.", presentation.StartReadinessText);
    }

    [Fact]
    public void Build_MapsActiveEmergencyMutedState()
    {
        var presentation = HapticsControlStatePresenter.Build(CreateSnapshot(
            hapticsStarted: true,
            pipelineRunning: true,
            emergencyMuteActive: true,
            normalMuteActive: true,
            telemetryTimedOutMuted: true,
            outputStatus: CreateOutputStatus(AudioOutputDeviceKind.Asio, AudioOutputDeviceState.Started, "ASIO running.", isHardwareArmed: true)));

        Assert.Equal("Clear Mute", presentation.EmergencyMuteButtonText);
        Assert.Equal("Emergency muted", presentation.HapticsStateText);
        Assert.Equal(HapticsDisplayState.EmergencyMuted, presentation.DisplayState);
        Assert.Equal(HapticsMuteState.EmergencyMute, presentation.MuteState);
    }

    [Fact]
    public void Build_MapsStoppedButEmergencyMutedState()
    {
        var presentation = HapticsControlStatePresenter.Build(CreateSnapshot(
            hapticsStarted: false,
            pipelineRunning: false,
            emergencyMuteActive: true,
            outputStatus: CreateOutputStatus(AudioOutputDeviceKind.Null, AudioOutputDeviceState.Stopped, "Null output idle.", requiresPhysicalHardware: false)));

        Assert.Equal("Start Haptics", presentation.StartStopButtonText);
        Assert.Equal("Clear Mute", presentation.EmergencyMuteButtonText);
        Assert.Equal("Emergency muted", presentation.HapticsStateText);
        Assert.Equal(HapticsDisplayState.EmergencyMuted, presentation.DisplayState);
        Assert.Equal(HapticsMuteState.EmergencyMute, presentation.MuteState);
    }

    [Fact]
    public void Build_DistinguishesNormalMuteFromEmergencyMute()
    {
        var normalMute = HapticsControlStatePresenter.Build(CreateSnapshot(
            hapticsStarted: true,
            pipelineRunning: true,
            normalMuteActive: true,
            outputStatus: CreateOutputStatus(AudioOutputDeviceKind.Null, AudioOutputDeviceState.Started, "Null output running.", requiresPhysicalHardware: false)));
        var emergencyMute = HapticsControlStatePresenter.Build(CreateSnapshot(
            hapticsStarted: true,
            pipelineRunning: true,
            emergencyMuteActive: true,
            normalMuteActive: true,
            outputStatus: CreateOutputStatus(AudioOutputDeviceKind.Null, AudioOutputDeviceState.Started, "Null output running.", requiresPhysicalHardware: false)));

        Assert.Equal(HapticsMuteState.NormalMute, normalMute.MuteState);
        Assert.Equal(HapticsDisplayState.MixerIdle, normalMute.DisplayState);
        Assert.Equal(HapticsMuteState.EmergencyMute, emergencyMute.MuteState);
        Assert.Equal(HapticsDisplayState.EmergencyMuted, emergencyMute.DisplayState);
        Assert.NotEqual(normalMute.HapticsStateText, emergencyMute.HapticsStateText);
    }

    [Fact]
    public void Build_MapsOutputUnavailableState()
    {
        var presentation = HapticsControlStatePresenter.Build(CreateSnapshot(
            hapticsStarted: false,
            pipelineRunning: false,
            outputStatus: CreateOutputStatus(
                AudioOutputDeviceKind.Asio,
                AudioOutputDeviceState.Faulted,
                "ASIO driver unavailable.",
                isAvailable: false,
                isHardwareArmed: false)));

        Assert.Equal(HapticsStartReadinessState.OutputUnavailable, presentation.StartReadinessState);
        Assert.Equal(
            "Start Haptics is blocked until ASIO Output is available. ASIO driver unavailable.",
            presentation.StartReadinessText);
    }

    [Fact]
    public void Build_MapsAsioSelectedButNotReadyState()
    {
        var presentation = HapticsControlStatePresenter.Build(CreateSnapshot(
            hapticsStarted: false,
            pipelineRunning: false,
            outputStatus: CreateOutputStatus(
                AudioOutputDeviceKind.Asio,
                AudioOutputDeviceState.Open,
                "ASIO opened for manual readiness.",
                isAvailable: true,
                isHardwareArmed: false)));

        Assert.Equal(HapticsStartReadinessState.HardwareNotArmed, presentation.StartReadinessState);
        Assert.Equal(
            "ASIO Output is selected but not armed; Start Haptics may be blocked until it is armed.",
            presentation.StartReadinessText);
    }

    [Fact]
    public void Build_MapsAsioSelectedAndReadyState()
    {
        var presentation = HapticsControlStatePresenter.Build(CreateSnapshot(
            hapticsStarted: false,
            pipelineRunning: false,
            outputStatus: CreateOutputStatus(
                AudioOutputDeviceKind.Asio,
                AudioOutputDeviceState.Open,
                "ASIO opened and armed.",
                isAvailable: true,
                isHardwareArmed: true)));

        Assert.Equal(HapticsStartReadinessState.Ready, presentation.StartReadinessState);
        Assert.Equal("ASIO Output is ready for Start Haptics.", presentation.StartReadinessText);
    }

    [Fact]
    public void Build_MapsTelemetryStaleMuteState()
    {
        var presentation = HapticsControlStatePresenter.Build(CreateSnapshot(
            hapticsStarted: true,
            pipelineRunning: true,
            telemetryTimedOutMuted: true,
            outputStatus: CreateOutputStatus(AudioOutputDeviceKind.Null, AudioOutputDeviceState.Started, "Null output running.", requiresPhysicalHardware: false)));

        Assert.Equal("Telemetry stale mute", presentation.HapticsStateText);
        Assert.Equal(HapticsDisplayState.TelemetryStaleMuted, presentation.DisplayState);
        Assert.Equal(HapticsMuteState.TelemetryStaleMute, presentation.MuteState);
    }

    [Fact]
    public void Build_IsDeterministicForEquivalentSnapshots()
    {
        var snapshot = CreateSnapshot(
            hapticsStarted: true,
            pipelineRunning: true,
            activeEffectCount: 2,
            outputPeakLevel: 0.125f,
            outputStatus: CreateOutputStatus(AudioOutputDeviceKind.Asio, AudioOutputDeviceState.Started, "ASIO running.", isHardwareArmed: true));

        var first = HapticsControlStatePresenter.Build(snapshot);
        var second = HapticsControlStatePresenter.Build(snapshot);

        Assert.Equal(first, second);
    }

    private static HapticsControlStateSnapshot CreateSnapshot(
        bool hapticsStarted,
        bool pipelineRunning,
        bool emergencyMuteActive = false,
        bool normalMuteActive = false,
        bool telemetryTimedOutMuted = false,
        int activeEffectCount = 0,
        float? outputPeakLevel = null,
        AudioOutputStatus? outputStatus = null)
    {
        return new HapticsControlStateSnapshot(
            HapticsStarted: hapticsStarted,
            PipelineRunning: pipelineRunning,
            EmergencyMuteActive: emergencyMuteActive,
            NormalMuteActive: normalMuteActive,
            TelemetryTimedOutMuted: telemetryTimedOutMuted,
            ActiveEffectCount: activeEffectCount,
            OutputPeakLevel: outputPeakLevel,
            OutputStatus: outputStatus ?? CreateOutputStatus(AudioOutputDeviceKind.Null, AudioOutputDeviceState.Open, "Null output ready.", requiresPhysicalHardware: false));
    }

    private static AudioOutputStatus CreateOutputStatus(
        AudioOutputDeviceKind kind,
        AudioOutputDeviceState state,
        string statusMessage,
        bool requiresPhysicalHardware = true,
        bool isAvailable = true,
        bool isHardwareArmed = false)
    {
        return new AudioOutputStatus(
            Kind: kind,
            State: state,
            DisplayName: kind == AudioOutputDeviceKind.Null ? "Null Output" : "ASIO Output",
            StatusMessage: statusMessage,
            DeviceName: null,
            SampleRate: 48000,
            ChannelCount: 1,
            BufferSize: 256,
            RequiresPhysicalHardware: requiresPhysicalHardware,
            IsManualDebugOnly: false,
            IsAvailable: isAvailable,
            IsHardwareArmed: isHardwareArmed);
    }
}
