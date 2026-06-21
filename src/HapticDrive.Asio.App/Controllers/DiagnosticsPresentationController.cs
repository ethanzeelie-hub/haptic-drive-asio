using HapticDrive.Asio.App.ViewModels;

namespace HapticDrive.Asio.App.Controllers;

internal sealed class DiagnosticsPresentationController
{
    public DiagnosticsPresentationController(DiagnosticsSummaryViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public DiagnosticsSummaryViewModel ViewModel { get; }

    public void Publish(string summaryText, string detailsText)
    {
        ViewModel.SummaryText = summaryText ?? string.Empty;
        ViewModel.DetailsText = detailsText ?? string.Empty;
    }
}
