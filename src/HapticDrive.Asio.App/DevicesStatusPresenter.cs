using HapticDrive.Input.Abstractions.Paddles;

namespace HapticDrive.Asio.App;

internal sealed record DevicesStatusSnapshot(
    string CurrentOutputDisplayName,
    string CurrentOutputState,
    string CurrentOutputStatusMessage,
    string AsioVisibilityMessage,
    string TrueAsioStatusText,
    string? SelectedAsioDriverName,
    int? SelectedAsioOutputChannel,
    bool AsioArmed,
    string HardwareChainWarning,
    bool OutputRequiresPhysicalHardware,
    bool InputDiscoveryHasRun,
    string? InputDiscoveryLocalRefreshText,
    int InputDiscoveryDeviceCount,
    bool InputDiscoveryReadOnlyDiscoverySucceeded,
    string InputDiscoveryLikelyCandidatesText,
    string InputDiscoverySavedCandidatesText,
    string InputDiscoveryMethodText,
    string InputDiscoveryErrorText,
    InputListenerStatus PaddleListenerStatus,
    string PaddleSelectedText,
    long PaddlePressCount,
    string PaddleLastMappedText,
    string PaddleLeftButtonMappingText,
    string PaddleRightButtonMappingText,
    double PaddleDebounceMilliseconds,
    string PaddleSelectedButtonCountText,
    string PaddleLastRawText,
    string PaddleErrorText,
    string? PaddleSelectionBlocker,
    bool ShiftIntentEnabled,
    string ShiftIntentModeText,
    bool DrivingArmed,
    long ShiftIntentAcceptedCount,
    long ShiftIntentSuppressedCount,
    string ShiftIntentTelemetryAgeText,
    bool ShiftIntentMenuSafeModeEnabled,
    string ShiftIntentLastAcceptedText,
    string ShiftIntentLastSuppressedText,
    string SelectedPhprModeText);

internal sealed record DevicesStatusPresentation(
    string CurrentOutputStatusText,
    string NullOutputStatusText,
    string WasapiDebugStatusText,
    string AsioStatusText,
    string AsioReadinessStatusText,
    string HardwareChainStatusText,
    string TrueAsioStatusText,
    string InputDiscoveryStatusText,
    IReadOnlyList<string> InputDiscoveryItems,
    bool StartPaddleInputListenerEnabled,
    string StartPaddleInputListenerToolTip,
    bool StopPaddleInputListenerEnabled,
    string PaddleInputBadgeText,
    string PaddleInputStatusText,
    IReadOnlyList<string> PaddleInputItems,
    string ShiftIntentStatusText,
    IReadOnlyList<string> ShiftIntentItems,
    string DevicesPageStatusText);

internal static class DevicesStatusPresenter
{
    public static DevicesStatusPresentation Build(DevicesStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var inputDiscoveryStatus = snapshot.InputDiscoveryHasRun
            ? $"Input devices refreshed {snapshot.InputDiscoveryLocalRefreshText}; {snapshot.InputDiscoveryDeviceCount:N0} device(s) found; status {(snapshot.InputDiscoveryReadOnlyDiscoverySucceeded ? "ready" : "warnings")}."
            : "Input devices have not been refreshed yet. Use Refresh Input Devices before choosing the wheel input.";
        var inputDiscoveryItems = snapshot.InputDiscoveryHasRun
            ? new[]
            {
                $"Wheel input options: {snapshot.InputDiscoveryLikelyCandidatesText}",
                $"Saved selection candidates: {snapshot.InputDiscoverySavedCandidatesText}",
                $"Refresh methods {snapshot.InputDiscoveryMethodText}; errors {snapshot.InputDiscoveryErrorText}"
            }
            : new[]
            {
                "Refresh is read-only and safe.",
                "Choose the wheel input after refresh.",
                "Start the listener only when you want live paddle input."
            };
        var startPaddleInputListenerEnabled = snapshot.PaddleListenerStatus is not InputListenerStatus.Listening
            and not InputListenerStatus.Starting
            && snapshot.PaddleSelectionBlocker is null;
        var startPaddleInputListenerToolTip = snapshot.PaddleSelectionBlocker is null
            ? "Start the read-only Windows game-controller paddle listener."
            : $"Blocked: {snapshot.PaddleSelectionBlocker}";
        var stopPaddleInputListenerEnabled = snapshot.PaddleListenerStatus is InputListenerStatus.Listening
            or InputListenerStatus.Starting
            or InputListenerStatus.Error
            or InputListenerStatus.Disconnected;
        var paddleInputBadgeText = snapshot.PaddleListenerStatus is InputListenerStatus.Listening
            ? "Listening"
            : "Listener stopped";
        var paddleInputStatusText = snapshot.PaddleListenerStatus is InputListenerStatus.Listening
            ? $"Paddle listener is running on {snapshot.PaddleSelectedText}; mapped presses {snapshot.PaddlePressCount:N0}; last mapped input {snapshot.PaddleLastMappedText}."
            : $"Paddle listener is stopped; selected {snapshot.PaddleSelectedText}; {(snapshot.PaddleSelectionBlocker is null ? "ready to start" : $"blocked: {snapshot.PaddleSelectionBlocker}")}.";
        var devicesPageStatusText = snapshot.OutputRequiresPhysicalHardware
            ? "Hardware output is selected. Confirm readiness here, use Testing / Validation for checks, and keep haptics stopped until you are ready."
            : $"Safe startup mode active; output {snapshot.CurrentOutputDisplayName}; input devices {(snapshot.InputDiscoveryHasRun ? snapshot.InputDiscoveryDeviceCount.ToString("N0") : "not refreshed")}; paddle listener {snapshot.PaddleListenerStatus}; P-HPR mode {snapshot.SelectedPhprModeText}.";

        return new DevicesStatusPresentation(
            CurrentOutputStatusText: $"Current output: {snapshot.CurrentOutputDisplayName} ({snapshot.CurrentOutputState}); {snapshot.CurrentOutputStatusMessage}",
            NullOutputStatusText: "Null output: the safe default when you are not ready to drive hardware.",
            WasapiDebugStatusText: "WASAPI debug: manual fallback only. It is never selected automatically.",
            AsioStatusText: snapshot.AsioVisibilityMessage,
            AsioReadinessStatusText: $"{snapshot.TrueAsioStatusText}; driver {Normalize(snapshot.SelectedAsioDriverName, "none")}; channel {(snapshot.SelectedAsioOutputChannel is null ? "none" : snapshot.SelectedAsioOutputChannel)}; armed {FormatOnOff(snapshot.AsioArmed)}.",
            HardwareChainStatusText: snapshot.HardwareChainWarning,
            TrueAsioStatusText: snapshot.TrueAsioStatusText,
            InputDiscoveryStatusText: inputDiscoveryStatus,
            InputDiscoveryItems: inputDiscoveryItems,
            StartPaddleInputListenerEnabled: startPaddleInputListenerEnabled,
            StartPaddleInputListenerToolTip: startPaddleInputListenerToolTip,
            StopPaddleInputListenerEnabled: stopPaddleInputListenerEnabled,
            PaddleInputBadgeText: paddleInputBadgeText,
            PaddleInputStatusText: paddleInputStatusText,
            PaddleInputItems:
            [
                $"Mappings: left {snapshot.PaddleLeftButtonMappingText}, right {snapshot.PaddleRightButtonMappingText}.",
                $"Debounce {snapshot.PaddleDebounceMilliseconds:0} ms; usable buttons {snapshot.PaddleSelectedButtonCountText}.",
                $"Last raw input {snapshot.PaddleLastRawText}; error {snapshot.PaddleErrorText}"
            ],
            ShiftIntentStatusText: $"Shift intent {(snapshot.ShiftIntentEnabled ? "enabled" : "disabled")}; mode {snapshot.ShiftIntentModeText}; driving state {(snapshot.DrivingArmed ? "armed" : "not armed")}; accepted {snapshot.ShiftIntentAcceptedCount:N0}; suppressed {snapshot.ShiftIntentSuppressedCount:N0}.",
            ShiftIntentItems:
            [
                "Mapped paddle presses can become local gear-shift haptics when enabled.",
                $"Driving state {(snapshot.DrivingArmed ? "armed" : "not armed")}; telemetry age {snapshot.ShiftIntentTelemetryAgeText}; menu safe {snapshot.ShiftIntentMenuSafeModeEnabled}.",
                $"Last accepted {snapshot.ShiftIntentLastAcceptedText}; last blocked {snapshot.ShiftIntentLastSuppressedText}"
            ],
            DevicesPageStatusText: devicesPageStatusText);
    }

    private static string FormatOnOff(bool value)
    {
        return value ? "on" : "off";
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value;
    }
}
