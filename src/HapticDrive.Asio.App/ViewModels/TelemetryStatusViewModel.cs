namespace HapticDrive.Asio.App.ViewModels;

internal sealed class TelemetryStatusViewModel : ObservableObject
{
    private string _listenerStatusText = string.Empty;
    private string _warningText = string.Empty;
    private bool _allowLanTelemetry;
    private string _allowedRemoteAddresses = string.Empty;

    public string ListenerStatusText
    {
        get => _listenerStatusText;
        set => SetProperty(ref _listenerStatusText, value);
    }

    public string WarningText
    {
        get => _warningText;
        set => SetProperty(ref _warningText, value);
    }

    public bool AllowLanTelemetry
    {
        get => _allowLanTelemetry;
        set => SetProperty(ref _allowLanTelemetry, value);
    }

    public string AllowedRemoteAddresses
    {
        get => _allowedRemoteAddresses;
        set => SetProperty(ref _allowedRemoteAddresses, value);
    }
}
