namespace HapticDrive.Asio.App.ViewModels;

internal sealed class OutputDeviceStatusViewModel : ObservableObject
{
    private string _selectedOutputId = "null";
    private string _statusText = string.Empty;
    private int _sampleRate;
    private int _bufferSize;

    public string SelectedOutputId
    {
        get => _selectedOutputId;
        set => SetProperty(ref _selectedOutputId, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public int SampleRate
    {
        get => _sampleRate;
        set => SetProperty(ref _sampleRate, value);
    }

    public int BufferSize
    {
        get => _bufferSize;
        set => SetProperty(ref _bufferSize, value);
    }
}
