using HapticDrive.Asio.Core.Audio;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal sealed record StartupAsioReadinessPlan(
    AudioOutputDeviceKind SelectedOutputKind,
    string? SelectedAsioDriverName,
    int? SelectedAsioOutputChannel,
    bool ArmAsioPreference,
    string Message);

internal static class StartupReadinessPlanner
{
    public static StartupAsioReadinessPlan BuildAsioSelectionPlan(
        bool hasPersistedOutputModePreference,
        AudioOutputDeviceKind selectedOutputKind,
        string? selectedAsioDriverName,
        int? selectedAsioOutputChannel,
        bool armAsioPreference,
        IReadOnlyList<string> visibleDriverNames)
    {
        ArgumentNullException.ThrowIfNull(visibleDriverNames);

        if (hasPersistedOutputModePreference)
        {
            if (selectedOutputKind == AudioOutputDeviceKind.Asio
                && selectedAsioDriverName is not null
                && !visibleDriverNames.Contains(selectedAsioDriverName, StringComparer.OrdinalIgnoreCase))
            {
                return new StartupAsioReadinessPlan(
                    SelectedOutputKind: selectedOutputKind,
                    SelectedAsioDriverName: null,
                    SelectedAsioOutputChannel: selectedAsioOutputChannel,
                    ArmAsioPreference: false,
                    Message: "Saved ASIO output selection restored, but the saved driver is unavailable. Select a driver and review Arm ASIO before starting haptics.");
            }

            if (selectedOutputKind == AudioOutputDeviceKind.Asio)
            {
                return new StartupAsioReadinessPlan(
                    SelectedOutputKind: selectedOutputKind,
                    SelectedAsioDriverName: selectedAsioDriverName,
                    SelectedAsioOutputChannel: selectedAsioOutputChannel,
                    ArmAsioPreference: armAsioPreference,
                    Message: armAsioPreference
                        ? "Saved ASIO output selection and Arm ASIO readiness restored without starting haptics or output."
                        : "Saved ASIO output selection restored. ASIO remains disarmed until you arm it manually.");
            }

            return new StartupAsioReadinessPlan(
                SelectedOutputKind: selectedOutputKind,
                SelectedAsioDriverName: selectedAsioDriverName,
                SelectedAsioOutputChannel: selectedAsioOutputChannel,
                ArmAsioPreference: armAsioPreference,
                Message: $"Saved output selection restored: {selectedOutputKind}.");
        }

        var selection = Bst1AsioStartupDefaults.Resolve(visibleDriverNames);
        return new StartupAsioReadinessPlan(
            SelectedOutputKind: selection.OutputKind,
            SelectedAsioDriverName: selection.DriverName,
            SelectedAsioOutputChannel: selection.OutputChannel,
            ArmAsioPreference: selection.Armed,
            Message: selection.Message);
    }

    public static PhprDirectAutoReadySelection BuildStartupPhprAutoReadySelection(
        IEnumerable<PHprDirectOutputCandidate> candidates,
        PHprRealOutputOptions currentOptions)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        // Startup may auto-select a preferred candidate for no-output readiness checks,
        // but it must not enable or arm direct control.
        return PhprDirectAutoReadySelector.Select(
            candidates,
            currentOptions,
            enableWhenPreferredPresent: false);
    }
}
