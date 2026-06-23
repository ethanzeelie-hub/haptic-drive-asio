using System.Windows;
using System.Windows.Controls;

namespace HapticDrive.Asio.App.Views;

public partial class TelemetryUdpView : UserControl
{
    internal event SelectionChangedEventHandler? ReplayTimingModeSelectionChanged;
    internal event RoutedEventHandler? StartRecordingClicked;
    internal event RoutedEventHandler? StartReplayClicked;
    internal event RoutedEventHandler? ReplaySelectedRecordingClicked;
    internal event RoutedEventHandler? RefreshRecordingsClicked;
    internal event RoutedEventHandler? DeleteSelectedRecordingClicked;
    internal event RoutedEventHandler? RenameSelectedRecordingClicked;
    internal event RoutedEventHandler? CopySelectedRecordingDetailClicked;
    internal event RoutedEventHandler? ExportSelectedRecordingDetailClicked;
    internal event TextChangedEventHandler? RecordingLibraryFilterTextChanged;
    internal event RoutedEventHandler? ClearRecordingLibraryFilterClicked;
    internal event SelectionChangedEventHandler? RecordingLibrarySelectionChanged;
    internal event RoutedEventHandler? AllowLanTelemetryChanged;
    internal event RoutedEventHandler? AllowedRemoteAddressesLostFocus;
    internal event RoutedEventHandler? SaveForwardingDestinationClicked;
    internal event RoutedEventHandler? RemoveForwardingDestinationClicked;
    internal event RoutedEventHandler? ClearForwardingDestinationClicked;
    internal event SelectionChangedEventHandler? ForwardingDestinationsSelectionChanged;

    public TelemetryUdpView()
    {
        InitializeComponent();
    }

    internal ComboBox ReplayTimingModeComboBoxControl => ReplayTimingModeComboBox;

    internal TextBlock ReplayTimingModeHelpTextControl => ReplayTimingModeHelpText;

    internal CheckBox AllowLanTelemetryCheckBoxControl => AllowLanTelemetryCheckBox;

    internal TextBox AllowedRemoteAddressesTextBoxControl => AllowedRemoteAddressesTextBox;

    internal TextBlock TelemetryListenerStatusTextControl => TelemetryListenerStatusText;

    internal TextBox RecordingRenameTextBoxControl => RecordingRenameTextBox;

    internal TextBox RecordingLibraryFilterTextBoxControl => RecordingLibraryFilterTextBox;

    internal TextBlock RecordingLibraryDetailTextControl => RecordingLibraryDetailText;

    internal ListBox RecordingLibraryListBoxControl => RecordingLibraryListBox;

    internal TextBlock RecordingLibraryStatusTextControl => RecordingLibraryStatusText;

    internal TextBox ForwardingNameTextBoxControl => ForwardingNameTextBox;

    internal TextBox ForwardingHostTextBoxControl => ForwardingHostTextBox;

    internal TextBox ForwardingPortTextBoxControl => ForwardingPortTextBox;

    internal CheckBox ForwardingEnabledCheckBoxControl => ForwardingEnabledCheckBox;

    internal TextBlock ForwardingEditorStatusTextControl => ForwardingEditorStatusText;

    internal ListBox ForwardingDestinationsListBoxControl => ForwardingDestinationsListBox;

    internal TextBlock ForwardingDestinationsSummaryTextControl => ForwardingDestinationsSummaryText;

    internal void Apply(TelemetryUdpStatusPresentation presentation)
    {
        ReplayTimingModeHelpText.Text = presentation.ReplayTimingModeHelpText;
        RecordingsStartStopButton.Content = presentation.RecordingsStartStopButtonText;
        ReplayStartStopButton.Content = presentation.ReplayStartStopButtonText;
        TelemetryListenerStatusText.Text = presentation.ListenerDetailText;
        RecordingsDetailText.Text = presentation.RecordingsDetailText;
        ReplayDetailText.Text = presentation.ReplayDetailText;
        ForwardingDestinationsSummaryText.Text = presentation.ForwardingDestinationsSummaryText;
    }

    private void ReplayTimingModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ReplayTimingModeSelectionChanged?.Invoke(sender, e);
    }

    private void StartRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        StartRecordingClicked?.Invoke(sender, e);
    }

    private void StartReplayButton_Click(object sender, RoutedEventArgs e)
    {
        StartReplayClicked?.Invoke(sender, e);
    }

    private void ReplaySelectedRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        ReplaySelectedRecordingClicked?.Invoke(sender, e);
    }

    private void RefreshRecordingsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRecordingsClicked?.Invoke(sender, e);
    }

    private void DeleteSelectedRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedRecordingClicked?.Invoke(sender, e);
    }

    private void RenameSelectedRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        RenameSelectedRecordingClicked?.Invoke(sender, e);
    }

    private void CopySelectedRecordingDetailButton_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedRecordingDetailClicked?.Invoke(sender, e);
    }

    private void ExportSelectedRecordingDetailButton_Click(object sender, RoutedEventArgs e)
    {
        ExportSelectedRecordingDetailClicked?.Invoke(sender, e);
    }

    private void RecordingLibraryFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RecordingLibraryFilterTextChanged?.Invoke(sender, e);
    }

    private void ClearRecordingLibraryFilterButton_Click(object sender, RoutedEventArgs e)
    {
        ClearRecordingLibraryFilterClicked?.Invoke(sender, e);
    }

    private void RecordingLibraryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RecordingLibrarySelectionChanged?.Invoke(sender, e);
    }

    private void AllowLanTelemetryCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        AllowLanTelemetryChanged?.Invoke(sender, e);
    }

    private void AllowedRemoteAddressesTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        AllowedRemoteAddressesLostFocus?.Invoke(sender, e);
    }

    private void SaveForwardingDestinationButton_Click(object sender, RoutedEventArgs e)
    {
        SaveForwardingDestinationClicked?.Invoke(sender, e);
    }

    private void RemoveForwardingDestinationButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveForwardingDestinationClicked?.Invoke(sender, e);
    }

    private void ClearForwardingDestinationButton_Click(object sender, RoutedEventArgs e)
    {
        ClearForwardingDestinationClicked?.Invoke(sender, e);
    }

    private void ForwardingDestinationsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ForwardingDestinationsSelectionChanged?.Invoke(sender, e);
    }
}
