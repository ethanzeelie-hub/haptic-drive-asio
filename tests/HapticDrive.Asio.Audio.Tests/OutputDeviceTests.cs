using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class OutputDeviceTests
{
    [Fact]
    public async Task NullOutputDevice_OpensStartsAndStopsWithoutHardware()
    {
        await using var device = new NullAudioOutputDevice();

        var openResult = await device.OpenAsync(AudioOutputConfiguration.Default);
        var startResult = await device.StartAsync();
        var stopResult = await device.StopAsync();

        Assert.True(openResult.Succeeded);
        Assert.True(startResult.Succeeded);
        Assert.True(stopResult.Succeeded);
        Assert.False(stopResult.Status.RequiresPhysicalHardware);
        Assert.False(stopResult.Status.IsManualDebugOnly);
        Assert.Equal(AudioOutputDeviceState.Stopped, stopResult.Status.State);
        Assert.Equal("NullAudioOutputDevice", stopResult.Status.DeviceName);
    }

    [Fact]
    public async Task WasapiDebugOutputDevice_IsManualDebugOnly()
    {
        await using var device = new WasapiDebugOutputDevice();

        var result = await device.OpenAsync(AudioOutputConfiguration.Default);

        Assert.True(result.Succeeded);
        Assert.True(result.Status.IsManualDebugOnly);
        Assert.False(result.Status.RequiresPhysicalHardware);
        Assert.Contains("manual debug", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsioOutputDevice_FailsGracefullyWhenDriverIsUnavailable()
    {
        await using var device = new AsioAudioOutputDevice(new FakeAsioDriverCatalog([]));

        var result = await device.OpenAsync(AudioOutputConfiguration.Default);

        Assert.False(result.Succeeded);
        Assert.Equal(AudioOutputDeviceState.Faulted, result.Status.State);
        Assert.True(result.Status.RequiresPhysicalHardware);
        Assert.False(result.Status.IsAvailable);
        Assert.Contains("unavailable", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsioOutputDevice_PrefersMAudioDriverWhenAvailable()
    {
        await using var device = new AsioAudioOutputDevice(new FakeAsioDriverCatalog(
        [
            "Other ASIO Driver",
            AsioAudioOutputDevice.PreferredDriverName
        ]));

        var result = await device.OpenAsync(AudioOutputConfiguration.Default);

        Assert.True(result.Succeeded);
        Assert.Equal(AudioOutputDeviceState.Open, result.Status.State);
        Assert.Equal(AsioAudioOutputDevice.PreferredDriverName, result.Status.DeviceName);
    }

    [Fact]
    public async Task AudioOutputDeviceFactory_CreatesRequestedDeviceWithoutFallback()
    {
        var factory = new AudioOutputDeviceFactory(new FakeAsioDriverCatalog([]));

        await using var asioDevice = factory.Create(AudioOutputDeviceKind.Asio);

        Assert.IsType<AsioAudioOutputDevice>(asioDevice);
        Assert.Equal(AudioOutputDeviceKind.Asio, asioDevice.Kind);
    }

    [Fact(Skip = "Manual hardware test: requires a real ASIO driver/interface and must not run in automated test suites.")]
    public async Task Manual_AsioOutputDevice_OpensRealDriverWhenHardwareIsAvailable()
    {
        await using var device = new AsioAudioOutputDevice();
        var result = await device.OpenAsync(AudioOutputConfiguration.Default);
        Assert.True(result.Succeeded);
    }

    private sealed class FakeAsioDriverCatalog : IAsioDriverCatalog
    {
        private readonly IReadOnlyList<string> _driverNames;

        public FakeAsioDriverCatalog(IReadOnlyList<string> driverNames)
        {
            _driverNames = driverNames;
        }

        public ValueTask<IReadOnlyList<string>> GetDriverNamesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_driverNames);
        }
    }
}
