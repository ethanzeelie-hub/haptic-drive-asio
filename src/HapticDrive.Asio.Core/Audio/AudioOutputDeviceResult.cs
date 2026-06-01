namespace HapticDrive.Asio.Core.Audio;

public sealed record AudioOutputDeviceResult(
    bool Succeeded,
    string Message,
    AudioOutputStatus Status)
{
    public static AudioOutputDeviceResult Success(string message, AudioOutputStatus status)
    {
        return new AudioOutputDeviceResult(true, message, status);
    }

    public static AudioOutputDeviceResult Failure(string message, AudioOutputStatus status)
    {
        return new AudioOutputDeviceResult(false, message, status);
    }
}
