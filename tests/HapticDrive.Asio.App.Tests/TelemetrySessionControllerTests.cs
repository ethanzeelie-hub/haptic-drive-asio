using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;

namespace HapticDrive.Asio.App.Tests;

public sealed class TelemetrySessionControllerTests
{
    [Fact]
    public void Publish_MapsListenerSettingsIntoViewModel()
    {
        var viewModel = new TelemetryStatusViewModel();
        var controller = new TelemetrySessionController(viewModel);
        var presentation = new TelemetryUdpStatusPresentation(
            ReplayTimingModeHelpText: "time-preserving",
            RecordingsStartStopButtonText: "Start Recording",
            ReplayStartStopButtonText: "Replay Latest",
            ListenerDetailText: "Loopback-only telemetry listening on UDP 20777.",
            RecordingsDetailText: "Recording idle.",
            ReplayDetailText: "Replay idle.",
            ForwardingDestinationsSummaryText: "No forwarding destinations configured.",
            TelemetryUdpPageStatusText: "listener idle");

        controller.Publish(
            presentation,
            allowLanTelemetry: true,
            allowedRemoteAddresses: ["192.168.1.10", "192.168.1.20"],
            warningText: "LAN telemetry is enabled.");

        Assert.Equal("Loopback-only telemetry listening on UDP 20777.", viewModel.ListenerStatusText);
        Assert.True(viewModel.AllowLanTelemetry);
        Assert.Equal("192.168.1.10, 192.168.1.20", viewModel.AllowedRemoteAddresses);
        Assert.Equal("LAN telemetry is enabled.", viewModel.WarningText);
    }
}
