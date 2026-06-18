using System.Windows;
using System.Windows.Controls;

namespace HapticDrive.Asio.App.Views;

public partial class DevicesView : UserControl
{
    internal event SelectionChangedEventHandler? OutputModeSelectionChanged;
    internal event RoutedEventHandler? RefreshAsioClicked;
    internal event SelectionChangedEventHandler? AsioDriverSelectionChanged;
    internal event SelectionChangedEventHandler? AsioOutputChannelSelectionChanged;
    internal event RoutedEventHandler? AsioArmChanged;
    internal event RoutedEventHandler? PhprPedalsControlChanged;
    internal event SelectionChangedEventHandler? PhprPedalsModeSelectionChanged;
    internal event RoutedEventHandler? PhprPedalsEmergencyStopClicked;
    internal event RoutedEventHandler? ClearPhprPedalsEmergencyStopClicked;
    internal event RoutedEventHandler? PhprPedalsStopAllClearDeviceStateClicked;
    internal event RoutedEventHandler? RefreshInputDevicesClicked;
    internal event SelectionChangedEventHandler? PaddleInputDeviceSelectionChanged;
    internal event RoutedEventHandler? StartPaddleInputListenerClicked;
    internal event RoutedEventHandler? StopPaddleInputListenerClicked;
    internal event RoutedEventHandler? PaddleMappingLostFocus;
    internal event RoutedEventHandler? SetLeftPaddleFromLastChangedClicked;
    internal event RoutedEventHandler? SetRightPaddleFromLastChangedClicked;
    internal event RoutedEventHandler? ShiftIntentEnabledChanged;
    internal event SelectionChangedEventHandler? ShiftIntentModeSelectionChanged;
    internal event RoutedEventHandler? ClearShiftIntentDiagnosticsClicked;

    public DevicesView()
    {
        InitializeComponent();
    }

    internal ComboBox OutputModeComboBoxControl => OutputModeComboBox;

    internal ComboBox AsioDriverComboBoxControl => AsioDriverComboBox;

    internal ComboBox AsioOutputChannelComboBoxControl => AsioOutputChannelComboBox;

    internal CheckBox AsioArmCheckBoxControl => AsioArmCheckBox;

    internal CheckBox PhprPedalsMasterEnableCheckBoxControl => PhprPedalsMasterEnableCheckBox;

    internal ComboBox PhprPedalsModeComboBoxControl => PhprPedalsModeComboBox;

    internal ComboBox PaddleInputDeviceComboBoxControl => PaddleInputDeviceComboBox;

    internal Button StartPaddleInputListenerButtonControl => StartPaddleInputListenerButton;

    internal Button StopPaddleInputListenerButtonControl => StopPaddleInputListenerButton;

    internal TextBox LeftPaddleButtonTextBoxControl => LeftPaddleButtonTextBox;

    internal TextBox RightPaddleButtonTextBoxControl => RightPaddleButtonTextBox;

    internal TextBox PaddleDebounceTextBoxControl => PaddleDebounceTextBox;

    internal CheckBox ShiftIntentEnabledCheckBoxControl => ShiftIntentEnabledCheckBox;

    internal ComboBox ShiftIntentModeComboBoxControl => ShiftIntentModeComboBox;

    internal TextBlock InputDiscoveryStatusTextBlock => InputDiscoveryStatusText;

    internal ItemsControl InputDiscoveryItemsControlView => InputDiscoveryItemsControl;

    internal TextBlock PaddleInputStatusTextBlock => PaddleInputStatusText;

    internal void Apply(DevicesStatusPresentation presentation)
    {
        CurrentOutputStatusText.Text = presentation.CurrentOutputStatusText;
        NullOutputStatusText.Text = presentation.NullOutputStatusText;
        WasapiDebugStatusText.Text = presentation.WasapiDebugStatusText;
        AsioStatusText.Text = presentation.AsioStatusText;
        AsioReadinessStatusText.Text = presentation.AsioReadinessStatusText;
        HardwareChainStatusText.Text = presentation.HardwareChainStatusText;
        TrueAsioStatusText.Text = presentation.TrueAsioStatusText;
        InputDiscoveryStatusText.Text = presentation.InputDiscoveryStatusText;
        InputDiscoveryItemsControl.ItemsSource = presentation.InputDiscoveryItems;
        StartPaddleInputListenerButton.IsEnabled = presentation.StartPaddleInputListenerEnabled;
        StartPaddleInputListenerButton.ToolTip = presentation.StartPaddleInputListenerToolTip;
        StopPaddleInputListenerButton.IsEnabled = presentation.StopPaddleInputListenerEnabled;
        PaddleInputBadgeText.Text = presentation.PaddleInputBadgeText;
        PaddleInputStatusText.Text = presentation.PaddleInputStatusText;
        PaddleInputItemsControl.ItemsSource = presentation.PaddleInputItems;
        ShiftIntentStatusText.Text = presentation.ShiftIntentStatusText;
        ShiftIntentItemsControl.ItemsSource = presentation.ShiftIntentItems;
    }

    private void OutputModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OutputModeSelectionChanged?.Invoke(sender, e);
    }

    private void RefreshAsioButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshAsioClicked?.Invoke(sender, e);
    }

    private void AsioDriverComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        AsioDriverSelectionChanged?.Invoke(sender, e);
    }

    private void AsioOutputChannelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        AsioOutputChannelSelectionChanged?.Invoke(sender, e);
    }

    private void AsioArmCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        AsioArmChanged?.Invoke(sender, e);
    }

    private void PhprPedalsControl_Changed(object sender, RoutedEventArgs e)
    {
        PhprPedalsControlChanged?.Invoke(sender, e);
    }

    private void PhprPedalsModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PhprPedalsModeSelectionChanged?.Invoke(sender, e);
    }

    private void PhprPedalsEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        PhprPedalsEmergencyStopClicked?.Invoke(sender, e);
    }

    private void ClearPhprPedalsEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        ClearPhprPedalsEmergencyStopClicked?.Invoke(sender, e);
    }

    private void PhprPedalsStopAllClearDeviceStateButton_Click(object sender, RoutedEventArgs e)
    {
        PhprPedalsStopAllClearDeviceStateClicked?.Invoke(sender, e);
    }

    private void RefreshInputDevicesButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshInputDevicesClicked?.Invoke(sender, e);
    }

    private void PaddleInputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PaddleInputDeviceSelectionChanged?.Invoke(sender, e);
    }

    private void StartPaddleInputListenerButton_Click(object sender, RoutedEventArgs e)
    {
        StartPaddleInputListenerClicked?.Invoke(sender, e);
    }

    private void StopPaddleInputListenerButton_Click(object sender, RoutedEventArgs e)
    {
        StopPaddleInputListenerClicked?.Invoke(sender, e);
    }

    private void PaddleMappingControl_LostFocus(object sender, RoutedEventArgs e)
    {
        PaddleMappingLostFocus?.Invoke(sender, e);
    }

    private void SetLeftPaddleFromLastChangedButton_Click(object sender, RoutedEventArgs e)
    {
        SetLeftPaddleFromLastChangedClicked?.Invoke(sender, e);
    }

    private void SetRightPaddleFromLastChangedButton_Click(object sender, RoutedEventArgs e)
    {
        SetRightPaddleFromLastChangedClicked?.Invoke(sender, e);
    }

    private void ShiftIntentEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        ShiftIntentEnabledChanged?.Invoke(sender, e);
    }

    private void ShiftIntentModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShiftIntentModeSelectionChanged?.Invoke(sender, e);
    }

    private void ClearShiftIntentDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        ClearShiftIntentDiagnosticsClicked?.Invoke(sender, e);
    }
}
