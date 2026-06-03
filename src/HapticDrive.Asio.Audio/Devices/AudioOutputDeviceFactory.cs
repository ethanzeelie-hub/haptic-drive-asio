using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Devices;

public sealed class AudioOutputDeviceFactory
{
    private readonly IAsioDriverCatalog _asioDriverCatalog;

    public AudioOutputDeviceFactory()
        : this(new WindowsRegistryAsioDriverCatalog())
    {
    }

    public AudioOutputDeviceFactory(IAsioDriverCatalog asioDriverCatalog)
    {
        _asioDriverCatalog = asioDriverCatalog;
    }

    public IAudioOutputDevice Create(AudioOutputDeviceKind kind)
    {
        return kind switch
        {
            AudioOutputDeviceKind.Null => new NullAudioOutputDevice(),
            AudioOutputDeviceKind.WasapiDebug => new WasapiDebugOutputDevice(),
            AudioOutputDeviceKind.Asio => new AsioAudioOutputDevice(_asioDriverCatalog),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown audio output device kind.")
        };
    }
}
