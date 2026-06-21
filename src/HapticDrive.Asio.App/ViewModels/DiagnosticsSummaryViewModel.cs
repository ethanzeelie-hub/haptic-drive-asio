namespace HapticDrive.Asio.App.ViewModels;

internal sealed class DiagnosticsSummaryViewModel : ObservableObject
{
    private string _summaryText = string.Empty;
    private string _detailsText = string.Empty;

    public string SummaryText
    {
        get => _summaryText;
        set => SetProperty(ref _summaryText, value);
    }

    public string DetailsText
    {
        get => _detailsText;
        set => SetProperty(ref _detailsText, value);
    }
}
