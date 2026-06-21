namespace HapticDrive.Asio.App.ViewModels;

internal sealed class SafetyStateViewModel : ObservableObject
{
    private bool _isLatched;
    private string _reason = string.Empty;
    private string _message = string.Empty;
    private long _generation;
    private string _statusText = "Startup safe default";

    public bool IsLatched
    {
        get => _isLatched;
        set => SetProperty(ref _isLatched, value);
    }

    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public long Generation
    {
        get => _generation;
        set => SetProperty(ref _generation, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
}
