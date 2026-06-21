using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.App;

internal enum HapticsDisplayState
{
    Stopped = 0,
    EmergencyMuted = 1,
    TelemetryStaleMuted = 2,
    MixerIdle = 3,
    ActiveEffects = 4
}

internal enum HapticsMuteState
{
    None = 0,
    NormalMute = 1,
    EmergencyMute = 2,
    TelemetryStaleMute = 3
}

internal enum HapticsStartReadinessState
{
    Ready = 0,
    ReadyNullOutput = 1,
    Running = 2,
    OutputUnavailable = 3,
    HardwareNotArmed = 4,
    Faulted = 5
}

internal sealed record HapticsControlStateSnapshot(
    bool HapticsStarted,
    bool PipelineRunning,
    bool EmergencyMuteActive,
    bool NormalMuteActive,
    bool TelemetryTimedOutMuted,
    int ActiveEffectCount,
    float? OutputPeakLevel,
    AudioOutputStatus OutputStatus);

internal sealed record HapticsControlStatePresentation(
    string StartStopButtonText,
    string EmergencyMuteButtonText,
    string HapticsStateText,
    HapticsDisplayState DisplayState,
    HapticsMuteState MuteState,
    HapticsStartReadinessState StartReadinessState,
    string StartReadinessText);

internal static class HapticsControlStatePresenter
{
    public static HapticsControlStatePresentation Build(HapticsControlStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var displayState = BuildDisplayState(snapshot);
        var muteState = BuildMuteState(snapshot);
        var startReadinessState = BuildStartReadinessState(snapshot);

        return new HapticsControlStatePresentation(
            StartStopButtonText: snapshot.HapticsStarted ? "Stop Haptics" : "Start Haptics",
            EmergencyMuteButtonText: snapshot.EmergencyMuteActive ? "Emergency Active" : "Emergency Mute",
            HapticsStateText: BuildStateText(displayState, snapshot),
            DisplayState: displayState,
            MuteState: muteState,
            StartReadinessState: startReadinessState,
            StartReadinessText: BuildStartReadinessText(startReadinessState, snapshot.OutputStatus));
    }

    private static HapticsDisplayState BuildDisplayState(HapticsControlStateSnapshot snapshot)
    {
        if (snapshot.EmergencyMuteActive)
        {
            return HapticsDisplayState.EmergencyMuted;
        }

        if (!snapshot.PipelineRunning)
        {
            return HapticsDisplayState.Stopped;
        }

        if (snapshot.TelemetryTimedOutMuted)
        {
            return HapticsDisplayState.TelemetryStaleMuted;
        }

        return snapshot.OutputPeakLevel is null
            ? HapticsDisplayState.MixerIdle
            : HapticsDisplayState.ActiveEffects;
    }

    private static HapticsMuteState BuildMuteState(HapticsControlStateSnapshot snapshot)
    {
        if (snapshot.EmergencyMuteActive)
        {
            return HapticsMuteState.EmergencyMute;
        }

        if (snapshot.TelemetryTimedOutMuted)
        {
            return HapticsMuteState.TelemetryStaleMute;
        }

        return snapshot.NormalMuteActive
            ? HapticsMuteState.NormalMute
            : HapticsMuteState.None;
    }

    private static HapticsStartReadinessState BuildStartReadinessState(HapticsControlStateSnapshot snapshot)
    {
        if (snapshot.PipelineRunning)
        {
            return HapticsStartReadinessState.Running;
        }

        if (snapshot.OutputStatus.Kind == AudioOutputDeviceKind.Null)
        {
            return HapticsStartReadinessState.ReadyNullOutput;
        }

        if (!snapshot.OutputStatus.IsAvailable)
        {
            return HapticsStartReadinessState.OutputUnavailable;
        }

        if (snapshot.OutputStatus.State == AudioOutputDeviceState.Faulted)
        {
            return HapticsStartReadinessState.Faulted;
        }

        if (snapshot.OutputStatus.RequiresPhysicalHardware && !snapshot.OutputStatus.IsHardwareArmed)
        {
            return HapticsStartReadinessState.HardwareNotArmed;
        }

        return HapticsStartReadinessState.Ready;
    }

    private static string BuildStateText(HapticsDisplayState displayState, HapticsControlStateSnapshot snapshot)
    {
        return displayState switch
        {
            HapticsDisplayState.EmergencyMuted => "Emergency muted",
            HapticsDisplayState.Stopped => "Stopped",
            HapticsDisplayState.TelemetryStaleMuted => "Telemetry stale mute",
            HapticsDisplayState.MixerIdle => "Mixer idle",
            HapticsDisplayState.ActiveEffects => $"{snapshot.ActiveEffectCount} effect(s); peak {snapshot.OutputPeakLevel ?? 0f:0.000}",
            _ => "Stopped"
        };
    }

    private static string BuildStartReadinessText(HapticsStartReadinessState readinessState, AudioOutputStatus outputStatus)
    {
        var outputName = string.IsNullOrWhiteSpace(outputStatus.DisplayName)
            ? "Selected output"
            : outputStatus.DisplayName.Trim();

        return readinessState switch
        {
            HapticsStartReadinessState.Running => $"Haptics running through {outputName}.",
            HapticsStartReadinessState.ReadyNullOutput => "Null output is selected; Start Haptics remains hardware-safe and produces no physical sound.",
            HapticsStartReadinessState.OutputUnavailable => $"Start Haptics is blocked until {outputName} is available. {outputStatus.StatusMessage}",
            HapticsStartReadinessState.HardwareNotArmed => $"{outputName} is selected but not armed; Start Haptics may be blocked until it is armed.",
            HapticsStartReadinessState.Faulted => $"Start Haptics may be blocked: {outputStatus.StatusMessage}",
            HapticsStartReadinessState.Ready => $"{outputName} is ready for Start Haptics.",
            _ => $"{outputName} status unavailable."
        };
    }
}
