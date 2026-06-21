using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.App.ViewModels;

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
}
