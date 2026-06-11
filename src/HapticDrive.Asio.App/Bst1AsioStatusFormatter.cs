using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Runtime.Pipeline;

namespace HapticDrive.Asio.App;

internal static class Bst1AsioStatusFormatter
{
    public static string Format(ManualAsioHardwareTestSnapshot snapshot)
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
            && !snapshot.NormalMute;
        var readyText = ready
            ? "ASIO ready: YES - selected, armed, channel valid"
            : $"ASIO ready: NO - {GetBlockedReason(snapshot, asioSelected)}";
        var activeText = snapshot.AsioRunning
            ? "ASIO active: YES - stream running"
            : "ASIO active: NO - stream stopped";
        var lastPulseText = snapshot.LastPulseUsedAsio
            ? "Last pulse: ASIO hardware path used"
            : "Last pulse: no successful ASIO hardware pulse recorded";

        return $"{readyText}; {activeText}; ASIO selected: {selectedText}; ASIO driver: {snapshot.SelectedAsioDriver}; ASIO armed: {armedText}; ASIO stream running: {runningText}; ASIO callback active: {callbackText}; Last manual pulse used ASIO: {lastManualText}; Last gear pulse used ASIO: {lastGearText}; Selected channel: {channelText}; Last ASIO blocked reason: {snapshot.BlockedReason ?? "none"}; Last ASIO error: {snapshot.LastError ?? "none"}; {lastPulseText}.";
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
