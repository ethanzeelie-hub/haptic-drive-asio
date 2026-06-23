namespace HapticDrive.Asio.App.ViewModels;

internal sealed class RecordingReplayStatusViewModel : ObservableObject
{
    private string _recordingStatusText = string.Empty;
    private string _replayStatusText = string.Empty;
    private string _warningText = string.Empty;
    private string _runtimeState = "Stopped";

    public string RecordingStatusText
    {
        get => _recordingStatusText;
        set => SetProperty(ref _recordingStatusText, value);
    }

    public string ReplayStatusText
    {
        get => _replayStatusText;
        set => SetProperty(ref _replayStatusText, value);
    }

    public string WarningText
    {
        get => _warningText;
        set => SetProperty(ref _warningText, value);
    }

    public string RuntimeState
    {
        get => _runtimeState;
        set => SetProperty(ref _runtimeState, value);
    }
}
