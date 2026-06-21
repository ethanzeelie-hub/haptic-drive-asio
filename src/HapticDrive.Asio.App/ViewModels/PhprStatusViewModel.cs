namespace HapticDrive.Asio.App.ViewModels;

internal sealed class PhprStatusViewModel : ObservableObject
{
    private string _workflowStatusText = string.Empty;
    private string _safetyStatusText = string.Empty;

    public string WorkflowStatusText
    {
        get => _workflowStatusText;
        set => SetProperty(ref _workflowStatusText, value);
    }

    public string SafetyStatusText
    {
        get => _safetyStatusText;
        set => SetProperty(ref _safetyStatusText, value);
    }
}
