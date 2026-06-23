using HapticDrive.Asio.App.ViewModels;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Runtime.Telemetry;

namespace HapticDrive.Asio.App.Controllers;

internal sealed class TelemetrySessionController
{
    public TelemetrySessionController(TelemetryStatusViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public TelemetryStatusViewModel ViewModel { get; }

    public void Publish(
        TelemetryUdpStatusPresentation presentation,
        bool allowLanTelemetry,
        IEnumerable<string> allowedRemoteAddresses,
        string? warningText)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(allowedRemoteAddresses);

        ViewModel.ListenerStatusText = presentation.ListenerDetailText;
        ViewModel.AllowLanTelemetry = allowLanTelemetry;
        ViewModel.AllowedRemoteAddresses = string.Join(", ", allowedRemoteAddresses);
        ViewModel.WarningText = warningText ?? string.Empty;
    }

    public void Enqueue(
        TelemetryIngressWorker ingressWorker,
        UdpTelemetryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(ingressWorker);
        ArgumentNullException.ThrowIfNull(packet);

        ingressWorker.ProcessTelemetryPacket(packet);
        ingressWorker.EnqueueForRecording(packet);
        ingressWorker.EnqueueForForwarding(packet);
    }
}
