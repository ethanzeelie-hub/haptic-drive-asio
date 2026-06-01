namespace HapticDrive.Asio.Core.Audio;

public enum AudioOutputDeviceState
{
    Created = 0,
    Open = 1,
    Started = 2,
    Stopped = 3,
    Faulted = 4,
    Disposed = 5
}
