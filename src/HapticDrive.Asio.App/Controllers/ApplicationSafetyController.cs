using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.App.ViewModels;
using HapticDrive.Asio.Runtime.Safety;

namespace HapticDrive.Asio.App.Controllers;

internal sealed class ApplicationSafetyController
{
    public ApplicationSafetyController(SafetyStateViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public SafetyStateViewModel ViewModel { get; }

    public void Publish(OutputInterlockSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ViewModel.IsLatched = snapshot.IsLatched;
        ViewModel.Reason = snapshot.Reason.ToString();
        ViewModel.Message = snapshot.Message;
        ViewModel.Generation = snapshot.Generation;
        ViewModel.StatusText = snapshot.IsLatched
            ? $"Latched: {snapshot.Reason}"
            : "Output enabled";
    }

    public bool TryBuildResetBlockedMessage(
        OutputInterlockSupervisor supervisor,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(supervisor);

        if (supervisor.CanReset(out var blocker))
        {
            message = string.Empty;
            return false;
        }

        message = $"Output interlock reset blocked: {blocker}";
        ViewModel.Message = message;
        return true;
    }
}
