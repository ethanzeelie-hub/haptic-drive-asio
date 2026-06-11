using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Runtime.Pipeline;

namespace HapticDrive.Asio.App;

internal static class Bst1AsioStatusFormatter
{
    public static string Format(ManualAsioHardwareTestSnapshot snapshot)
    {
        return FormatCompact(snapshot);
    }

    public static string FormatCompact(ManualAsioHardwareTestSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var asioSelected = snapshot.OutputMode == AudioOutputDeviceKind.Asio.ToString();
        var ready = asioSelected
            && snapshot.AsioArmed
            && snapshot.SelectedOutputChannel is >= 0
            && !snapshot.EmergencyMute
            && !snapshot.NormalMute
            && snapshot.LastError is null;

        if (snapshot.AsioRunning && snapshot.AsioCallbackActive)
        {
            return "ASIO ACTIVE - green dot";
        }

        if (ready)
        {
            return "ASIO READY - stream stopped";
        }

        return $"ASIO NOT READY - {GetBlockedReason(snapshot, asioSelected)}";
    }

    public static string FormatDetailed(ManualAsioHardwareTestSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var asioSelected = snapshot.OutputMode == AudioOutputDeviceKind.Asio.ToString();
        var selectedText = FormatYesNo(asioSelected);
        var armedText = FormatYesNo(snapshot.AsioArmed);
        var runningText = FormatYesNo(snapshot.AsioRunning);
        var callbackText = FormatYesNo(snapshot.AsioCallbackActive);
        var lastManualText = FormatYesNo(snapshot.LastManualPulseUsedAsio);
        var lastGearText = FormatYesNo(snapshot.LastGearPulseUsedAsio);
        var channelText = snapshot.SelectedOutputChannel is null ? "none" : snapshot.SelectedOutputChannel.Value.ToString();
        var ready = asioSelected
            && snapshot.AsioArmed
            && snapshot.SelectedOutputChannel is >= 0
            && !snapshot.EmergencyMute
            && !snapshot.NormalMute
            && snapshot.LastError is null;
        var readyText = ready
            ? "ASIO ready: YES - selected, armed, channel valid"
            : $"ASIO ready: NO - {GetBlockedReason(snapshot, asioSelected)}";
        var activeText = snapshot.AsioRunning && snapshot.AsioCallbackActive
            ? "ASIO active: YES - stream running and callback active"
            : "ASIO active: NO - stream stopped";
        var lastPulseText = snapshot.LastPulseUsedAsio
            ? "Last pulse: ASIO hardware path used"
            : "Last pulse: no successful ASIO hardware pulse recorded";

        return $"{readyText}; {activeText}; ASIO selected: {selectedText}; ASIO driver: {snapshot.SelectedAsioDriver}; ASIO armed: {armedText}; ASIO stream running: {runningText}; ASIO callback active: {callbackText}; rendered callbacks: {snapshot.RenderCallbackCount:N0}; submitted frames: {snapshot.SubmittedFrameCount:N0}; dropped frames: {snapshot.DroppedFrameCount:N0}; Last manual pulse used ASIO: {lastManualText}; Last gear pulse used ASIO: {lastGearText}; Selected channel: {channelText}; Last ASIO blocked reason: {snapshot.BlockedReason ?? "none"}; Last ASIO error: {snapshot.LastError ?? "none"}; requested strength {(snapshot.LastStrengthPercent is null ? "none" : $"{snapshot.LastStrengthPercent:0}%")}; output trim {(snapshot.LastOutputTrimPercent is null ? "none" : $"{snapshot.LastOutputTrimPercent:0}%")}; effective pre-limiter amplitude {(snapshot.LastEffectivePreLimiterAmplitude is null ? "none" : $"{snapshot.LastEffectivePreLimiterAmplitude:0.000}")}; effective post-limiter amplitude {(snapshot.LastEffectivePostLimiterAmplitude is null ? "none" : $"{snapshot.LastEffectivePostLimiterAmplitude:0.000}")}; limiter applied {snapshot.LimiterApplied}; {lastPulseText}.";
    }

    private static string GetBlockedReason(ManualAsioHardwareTestSnapshot snapshot, bool asioSelected)
    {
        if (!asioSelected)
        {
            return snapshot.OutputMode == AudioOutputDeviceKind.Null.ToString()
                ? "Null output selected"
                : $"{snapshot.OutputMode} selected";
        }

        if (!snapshot.AsioArmed)
        {
            return "ASIO not armed";
        }

        if (snapshot.SelectedOutputChannel is null)
        {
            return "selected channel missing";
        }

        if (snapshot.SelectedOutputChannel < 0)
        {
            return "selected channel invalid";
        }

        if (snapshot.EmergencyMute)
        {
            return "emergency mute active";
        }

        if (snapshot.NormalMute)
        {
            return "normal mute active";
        }

        return snapshot.BlockedReason ?? "not ready";
    }

    private static string FormatYesNo(bool value)
    {
        return value ? "YES" : "NO";
    }
}
