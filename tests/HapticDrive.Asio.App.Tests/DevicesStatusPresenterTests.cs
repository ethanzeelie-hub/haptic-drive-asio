using HapticDrive.Input.Abstractions.Paddles;

namespace HapticDrive.Asio.App.Tests;

public sealed class DevicesStatusPresenterTests
{
    [Fact]
    public void Build_WhenInputDiscoveryHasNotRun_ShowsSafeReadinessGuidance()
    {
        var presentation = DevicesStatusPresenter.Build(CreateSnapshot());

        Assert.Equal("Input devices have not been refreshed yet. Use Refresh Input Devices before choosing the wheel input.", presentation.InputDiscoveryStatusText);
        Assert.Contains("Refresh is read-only and safe.", presentation.InputDiscoveryItems);
        Assert.False(presentation.StartPaddleInputListenerEnabled);
        Assert.Contains("Blocked: input discovery has not completed", presentation.StartPaddleInputListenerToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WhenPaddleListenerIsRunning_ShowsListeningState()
    {
        var presentation = DevicesStatusPresenter.Build(CreateSnapshot(
            paddleListenerStatus: InputListenerStatus.Listening,
            paddleSelectedText: "GT Neo Wheel (RawInput)",
            paddlePressCount: 12,
            paddleLastMappedText: "Left paddle button 5 at 12:34:56",
            paddleSelectionBlocker: null));

        Assert.Equal("Listening", presentation.PaddleInputBadgeText);
        Assert.Contains("Paddle listener is running on GT Neo Wheel (RawInput); mapped presses 12; last mapped input Left paddle button 5 at 12:34:56.", presentation.PaddleInputStatusText, StringComparison.Ordinal);
        Assert.False(presentation.StartPaddleInputListenerEnabled);
        Assert.True(presentation.StopPaddleInputListenerEnabled);
    }

    [Fact]
    public void Build_WhenHardwareOutputIsSelected_ShowsHardwareReadyPageStatus()
    {
        var presentation = DevicesStatusPresenter.Build(CreateSnapshot(
            outputRequiresPhysicalHardware: true));

        Assert.Equal("Hardware output is selected. Confirm readiness here, use Testing / Validation for checks, and keep haptics stopped until you are ready.", presentation.DevicesPageStatusText);
        Assert.Contains("driver M-Audio; channel 1; armed on.", presentation.AsioReadinessStatusText, StringComparison.Ordinal);
    }

    private static DevicesStatusSnapshot CreateSnapshot(
        string currentOutputDisplayName = "ASIO Output",
        string currentOutputState = "Open",
        string currentOutputStatusMessage = "ASIO output ready.",
        string asioVisibilityMessage = "ASIO is selected and available.",
        string trueAsioStatusText = "Ready for manual BST-1 checks",
        string? selectedAsioDriverName = "M-Audio",
        int? selectedAsioOutputChannel = 1,
        bool asioArmed = true,
        string hardwareChainWarning = "Hardware chain confirmed.",
        bool outputRequiresPhysicalHardware = false,
        bool inputDiscoveryHasRun = false,
        string? inputDiscoveryLocalRefreshText = null,
        int inputDiscoveryDeviceCount = 0,
        bool inputDiscoveryReadOnlyDiscoverySucceeded = true,
        string inputDiscoveryLikelyCandidatesText = "GT Neo wheel",
        string inputDiscoverySavedCandidatesText = "saved GT Neo wheel",
        string inputDiscoveryMethodText = "RawInput, WinMM",
        string inputDiscoveryErrorText = "none",
        InputListenerStatus paddleListenerStatus = InputListenerStatus.Stopped,
        string paddleSelectedText = "none",
        long paddlePressCount = 0,
        string paddleLastMappedText = "none",
        string paddleLeftButtonMappingText = "none",
        string paddleRightButtonMappingText = "none",
        double paddleDebounceMilliseconds = 40,
        string paddleSelectedButtonCountText = "none",
        string paddleLastRawText = "none",
        string paddleErrorText = "none",
        string? paddleSelectionBlocker = "input discovery has not completed",
        bool shiftIntentEnabled = true,
        string shiftIntentModeText = "InstantPaddleOnly",
        bool drivingArmed = false,
        long shiftIntentAcceptedCount = 0,
        long shiftIntentSuppressedCount = 0,
        string shiftIntentTelemetryAgeText = "none",
        bool shiftIntentMenuSafeModeEnabled = true,
        string shiftIntentLastAcceptedText = "none",
        string shiftIntentLastSuppressedText = "none",
        string selectedPhprModeText = "Mock")
    {
        return new DevicesStatusSnapshot(
            CurrentOutputDisplayName: currentOutputDisplayName,
            CurrentOutputState: currentOutputState,
            CurrentOutputStatusMessage: currentOutputStatusMessage,
            AsioVisibilityMessage: asioVisibilityMessage,
            TrueAsioStatusText: trueAsioStatusText,
            SelectedAsioDriverName: selectedAsioDriverName,
            SelectedAsioOutputChannel: selectedAsioOutputChannel,
            AsioArmed: asioArmed,
            HardwareChainWarning: hardwareChainWarning,
            OutputRequiresPhysicalHardware: outputRequiresPhysicalHardware,
            InputDiscoveryHasRun: inputDiscoveryHasRun,
            InputDiscoveryLocalRefreshText: inputDiscoveryLocalRefreshText,
            InputDiscoveryDeviceCount: inputDiscoveryDeviceCount,
            InputDiscoveryReadOnlyDiscoverySucceeded: inputDiscoveryReadOnlyDiscoverySucceeded,
            InputDiscoveryLikelyCandidatesText: inputDiscoveryLikelyCandidatesText,
            InputDiscoverySavedCandidatesText: inputDiscoverySavedCandidatesText,
            InputDiscoveryMethodText: inputDiscoveryMethodText,
            InputDiscoveryErrorText: inputDiscoveryErrorText,
            PaddleListenerStatus: paddleListenerStatus,
            PaddleSelectedText: paddleSelectedText,
            PaddlePressCount: paddlePressCount,
            PaddleLastMappedText: paddleLastMappedText,
            PaddleLeftButtonMappingText: paddleLeftButtonMappingText,
            PaddleRightButtonMappingText: paddleRightButtonMappingText,
            PaddleDebounceMilliseconds: paddleDebounceMilliseconds,
            PaddleSelectedButtonCountText: paddleSelectedButtonCountText,
            PaddleLastRawText: paddleLastRawText,
            PaddleErrorText: paddleErrorText,
            PaddleSelectionBlocker: paddleSelectionBlocker,
            ShiftIntentEnabled: shiftIntentEnabled,
            ShiftIntentModeText: shiftIntentModeText,
            DrivingArmed: drivingArmed,
            ShiftIntentAcceptedCount: shiftIntentAcceptedCount,
            ShiftIntentSuppressedCount: shiftIntentSuppressedCount,
            ShiftIntentTelemetryAgeText: shiftIntentTelemetryAgeText,
            ShiftIntentMenuSafeModeEnabled: shiftIntentMenuSafeModeEnabled,
            ShiftIntentLastAcceptedText: shiftIntentLastAcceptedText,
            ShiftIntentLastSuppressedText: shiftIntentLastSuppressedText,
            SelectedPhprModeText: selectedPhprModeText);
    }
}
