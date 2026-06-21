using HapticDrive.Asio.App.ViewModels;

namespace HapticDrive.Asio.App.Controllers;

internal sealed class PhprOutputController
{
    public PhprOutputController(PhprStatusViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public PhprStatusViewModel ViewModel { get; }

    public void Publish(
        string workflowStatusText,
        string safetyStatusText)
    {
        ViewModel.WorkflowStatusText = workflowStatusText;
        ViewModel.SafetyStatusText = safetyStatusText;
    }
}
