using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Diagnostics;

public sealed class AsioReadinessDiagnostics
{
    private static readonly string[] MTrackKeywords =
    [
        "m-audio",
        "maudio",
        "m track",
        "m-track"
    ];

    private readonly IAsioDriverCatalog _driverCatalog;

    public AsioReadinessDiagnostics(IAsioDriverCatalog driverCatalog)
    {
        _driverCatalog = driverCatalog ?? throw new ArgumentNullException(nameof(driverCatalog));
    }

    public async ValueTask<AsioReadinessSnapshot> RefreshAsync(
        AudioOutputStatus outputStatus,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outputStatus);

        try
        {
            var drivers = await _driverCatalog.GetDriverNamesAsync(cancellationToken).ConfigureAwait(false);
            var normalizedDrivers = drivers
                .Where(driver => !string.IsNullOrWhiteSpace(driver))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var preferredDriver = normalizedDrivers.FirstOrDefault(driver =>
                string.Equals(driver, AsioAudioOutputDevice.PreferredDriverName, StringComparison.OrdinalIgnoreCase));
            var mTrackDriver = preferredDriver ?? normalizedDrivers.FirstOrDefault(ContainsMTrackKeyword);
            var selectedIsAsio = outputStatus.Kind == AudioOutputDeviceKind.Asio;
            var running = selectedIsAsio && outputStatus.State == AudioOutputDeviceState.Started;
            var message = BuildMessage(normalizedDrivers.Length, mTrackDriver, outputStatus);

            return new AsioReadinessSnapshot(
                Succeeded: true,
                normalizedDrivers,
                AsioAvailable: normalizedDrivers.Length > 0,
                mTrackDriver is not null,
                mTrackDriver,
                selectedIsAsio,
                outputStatus.DeviceName,
                outputStatus.IsHardwareArmed,
                running,
                outputStatus.SampleRate,
                outputStatus.BufferSize,
                outputStatus.DeviceOutputChannelCount,
                outputStatus.SelectedOutputChannel,
                outputStatus.SubmittedBufferCount,
                outputStatus.DroppedBufferCount,
                outputStatus.LastError,
                WindowsSoundOutputVisibilityProvesAsio: false,
                HardwareChainWarning: "M-Audio and Fosi readiness can be checked manually, but Dayton BST-1 physical shaker validation is deferred.",
                message,
                ErrorMessage: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new AsioReadinessSnapshot(
                Succeeded: false,
                Array.Empty<string>(),
                AsioAvailable: false,
                MTrackDriverVisible: false,
                MatchedMTrackDriverName: null,
                SelectedOutputIsAsio: outputStatus.Kind == AudioOutputDeviceKind.Asio,
                SelectedDriverName: outputStatus.DeviceName,
                outputStatus.IsHardwareArmed,
                OutputRunning: false,
                outputStatus.SampleRate,
                outputStatus.BufferSize,
                outputStatus.DeviceOutputChannelCount,
                outputStatus.SelectedOutputChannel,
                outputStatus.SubmittedBufferCount,
                outputStatus.DroppedBufferCount,
                outputStatus.LastError,
                WindowsSoundOutputVisibilityProvesAsio: false,
                HardwareChainWarning: "ASIO readiness diagnostics failed safely; hardware-dependent checks remain manual.",
                Message: $"ASIO readiness diagnostics failed safely: {ex.Message}",
                ErrorMessage: ex.Message);
        }
    }

    private static string BuildMessage(
        int driverCount,
        string? mTrackDriver,
        AudioOutputStatus outputStatus)
    {
        var driverText = driverCount == 0
            ? "No ASIO drivers were reported by the app ASIO catalog."
            : $"{driverCount} ASIO driver(s) reported by the app ASIO catalog.";
        var mTrackText = mTrackDriver is null
            ? "M-Audio / M-Track ASIO driver was not reported."
            : $"M-Audio / M-Track match: '{mTrackDriver}'.";
        var outputText = outputStatus.Kind == AudioOutputDeviceKind.Asio
            ? $"Selected output is ASIO; armed {outputStatus.IsHardwareArmed}; running {outputStatus.State == AudioOutputDeviceState.Started}."
            : $"Selected output is {outputStatus.DisplayName}; Null remains the safe default unless changed deliberately.";

        return $"{driverText} {mTrackText} {outputText} Windows sound output visibility is not proof of ASIO usage.";
    }

    private static bool ContainsMTrackKeyword(string driver)
    {
        foreach (var keyword in MTrackKeywords)
        {
            if (driver.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed record AsioReadinessSnapshot(
    bool Succeeded,
    IReadOnlyList<string> DriverNames,
    bool AsioAvailable,
    bool MTrackDriverVisible,
    string? MatchedMTrackDriverName,
    bool SelectedOutputIsAsio,
    string? SelectedDriverName,
    bool IsArmed,
    bool OutputRunning,
    int SampleRate,
    int BufferSize,
    int? DeviceOutputChannelCount,
    int? SelectedOutputChannel,
    long SubmittedBufferCount,
    long DroppedBufferCount,
    string? LastError,
    bool WindowsSoundOutputVisibilityProvesAsio,
    string HardwareChainWarning,
    string Message,
    string? ErrorMessage)
{
    public static AsioReadinessSnapshot NotChecked { get; } = new(
        Succeeded: false,
        Array.Empty<string>(),
        AsioAvailable: false,
        MTrackDriverVisible: false,
        MatchedMTrackDriverName: null,
        SelectedOutputIsAsio: false,
        SelectedDriverName: null,
        IsArmed: false,
        OutputRunning: false,
        SampleRate: AudioOutputConfiguration.Default.SampleRate,
        BufferSize: AudioOutputConfiguration.Default.BufferSize,
        DeviceOutputChannelCount: null,
        SelectedOutputChannel: null,
        SubmittedBufferCount: 0,
        DroppedBufferCount: 0,
        LastError: null,
        WindowsSoundOutputVisibilityProvesAsio: false,
        HardwareChainWarning: "ASIO readiness diagnostics have not been checked yet.",
        Message: "ASIO readiness diagnostics have not been checked yet.",
        ErrorMessage: null);
}
