using HapticDrive.Asio.App.ViewModels;

namespace HapticDrive.Asio.App.Controllers;

internal sealed class RecordingReplayController
{
    public RecordingReplayController(RecordingReplayStatusViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public RecordingReplayStatusViewModel ViewModel { get; }

    public void Publish(
        TelemetryUdpStatusPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        ViewModel.RecordingStatusText = presentation.RecordingsDetailText;
        ViewModel.ReplayStatusText = presentation.ReplayDetailText;
        ViewModel.WarningText = presentation.ForwardingDestinationsSummaryText;
    }
}
