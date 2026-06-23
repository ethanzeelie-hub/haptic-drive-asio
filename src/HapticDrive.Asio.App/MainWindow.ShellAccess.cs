using HapticDrive.Asio.App.Views;
using System.Windows.Controls;

namespace HapticDrive.Asio.App;

public partial class MainWindow
{
    internal TextBlock TopBarContextTextControl => TopBarContextText;

    internal TextBlock TelemetryStatusTextControl => TelemetryStatusText;

    internal TextBlock SafetyStatusTextControl => SafetyStatusText;

    internal Button StartStopButtonControl => StartStopButton;

    internal Button StartRecordingButtonControl => StartRecordingButton;

    internal Button EmergencyMuteButtonControl => EmergencyMuteButton;

    internal Button ResetOutputInterlockButtonControl => ResetOutputInterlockButton;

    internal Button ThemeButtonControl => ThemeButton;

    internal ListBox NavigationListControl => NavigationList;

    internal TextBlock PageTitleTextControl => PageTitleText;

    internal TextBlock PageSummaryTextControl => PageSummaryText;

    internal TextBlock PageStatusTextControl => PageStatusText;

    internal ItemsControl PageItemsControlControl => PageItemsControl;

    internal TextBlock FooterStatusTextControl => FooterStatusText;

    internal DashboardView DashboardViewControlView => DashboardViewControl;

    internal EffectsView EffectsViewControlView => EffectsViewControl;

    internal RoutingMixerView RoutingMixerViewControlView => RoutingMixerViewControl;

    internal TelemetryUdpView TelemetryUdpViewControlView => TelemetryUdpViewControl;

    internal ProfilesView ProfilesViewControlView => ProfilesViewControl;

    internal TestingValidationView TestingValidationViewControlView => TestingValidationViewControl;

    internal DevicesView DevicesViewControlView => DevicesViewControl;

    internal AdvancedDiagnosticsView AdvancedDiagnosticsViewControlView => AdvancedDiagnosticsViewControl;
}
