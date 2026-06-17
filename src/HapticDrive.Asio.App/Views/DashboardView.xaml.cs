using System.Windows;
using System.Windows.Controls;

namespace HapticDrive.Asio.App.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    internal void Apply(DashboardStatusPresentation presentation)
    {
        OutputModeValueText.Text = presentation.OutputModeValueText;
        OutputModeDetailText.Text = presentation.OutputModeDetailText;
        HapticsStateText.Text = presentation.HapticsStateText;
        UdpListenerValueText.Text = presentation.UdpListenerValueText;
        UdpListenerDetailText.Text = presentation.UdpListenerDetailText;
        PacketCountValueText.Text = presentation.PacketCountValueText;
        PacketRateDetailText.Text = presentation.PacketRateDetailText;
        ForwardingValueText.Text = presentation.ForwardingValueText;
        ForwardingDetailText.Text = presentation.ForwardingDetailText;
        HeaderParserValueText.Text = presentation.HeaderParserValueText;
        HeaderParserDetailText.Text = presentation.HeaderParserDetailText;
        VehicleStateValueText.Text = presentation.VehicleStateValueText;
        VehicleStateDetailText.Text = presentation.VehicleStateDetailText;
        RecordingValueText.Text = presentation.RecordingValueText;
        RecordingDetailText.Text = presentation.RecordingDetailText;
        DashboardWorkflowStatusText.Text = presentation.WorkflowStatusText;
        DashboardNextStepText.Text = presentation.NextStepText;
        DashboardChecklistItemsControl.ItemsSource = presentation.ChecklistItems;
        DashboardSummaryPanel.Visibility = presentation.ShowWorkflowCard
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
