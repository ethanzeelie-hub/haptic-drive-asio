using System.Windows;
using System.Windows.Controls;

namespace HapticDrive.Asio.App.Views;

public partial class TestingValidationView : UserControl
{
    internal event SelectionChangedEventHandler? TestBenchSignalSelectionChanged;
    internal event RoutedEventHandler? TestBenchStartStopClicked;
    internal event RoutedEventHandler? ManualBst1ControlLostFocus;
    internal event RoutedEventHandler? ManualBst1PulseClicked;
    internal event RoutedEventHandler? ManualAsioHardwareTestChannel1Clicked;
    internal event RoutedEventHandler? ManualAsioHardwareTestChannel0Clicked;
    internal event RoutedEventHandler? TestPhprBrakePulseClicked;
    internal event RoutedEventHandler? TestPhprThrottlePulseClicked;
    internal event RoutedEventHandler? LocalGearTestModeChanged;
    internal event RoutedEventHandler? StartGearTestListenerClicked;
    internal event RoutedEventHandler? PaddleGearBenchControlChanged;
    internal event SelectionChangedEventHandler? PaddleGearBenchSelectionChanged;
    internal event RoutedEventHandler? PaddleGearBenchControlLostFocus;
    internal event RoutedEventHandler? ClearPaddleGearBenchCountersClicked;
    internal event RoutedEventHandler? PhprValidationControlChanged;
    internal event RoutedEventHandler? PhprValidationControlLostFocus;
    internal event RoutedEventHandler? RefreshPhprValidationChecklistClicked;
    internal event RoutedEventHandler? ExportPhprValidationResultClicked;

    public TestingValidationView()
    {
        InitializeComponent();
    }

    internal ComboBox TestBenchSignalComboBoxControl => TestBenchSignalComboBox;

    internal Button TestBenchStartStopButtonControl => TestBenchStartStopButton;

    internal TextBlock TestBenchStateTextControl => TestBenchStateText;

    internal TextBlock TestBenchPeakTextControl => TestBenchPeakText;

    internal TextBlock TestBenchLimiterTextControl => TestBenchLimiterText;

    internal TextBlock TestBenchOutputTextControl => TestBenchOutputText;

    internal TextBlock TestBenchWarningTextControl => TestBenchWarningText;

    internal TextBox ManualBst1StrengthTextBoxControl => ManualBst1StrengthTextBox;

    internal TextBox Bst1OutputTrimTextBoxControl => Bst1OutputTrimTextBox;

    internal TextBox ManualBst1FrequencyTextBoxControl => ManualBst1FrequencyTextBox;

    internal TextBox ManualBst1DurationTextBoxControl => ManualBst1DurationTextBox;

    internal TextBlock ManualAsioHardwareStatusTextControl => ManualAsioHardwareStatusText;

    internal TextBlock ManualAsioHardwareBlockedReasonTextControl => ManualAsioHardwareBlockedReasonText;

    internal TextBlock PhprPedalsModeBadgeTextControl => PhprPedalsModeBadgeText;

    internal TextBlock PhprPedalsStatusTextControl => PhprPedalsStatusText;

    internal TextBlock PhprPedalsDeviceStatusTextControl => PhprPedalsDeviceStatusText;

    internal TextBlock PhprPedalsLastResultTextControl => PhprPedalsLastResultText;

    internal Button TestPhprBrakePulseButtonControl => TestPhprBrakePulseButton;

    internal Button TestPhprThrottlePulseButtonControl => TestPhprThrottlePulseButton;

    internal CheckBox LocalGearTestModeCheckBoxControl => LocalGearTestModeCheckBox;

    internal CheckBox LocalGearTestAutoStartListenerCheckBoxControl => LocalGearTestAutoStartListenerCheckBox;

    internal Button StartGearTestListenerButtonControl => StartGearTestListenerButton;

    internal TextBlock LocalGearTestStatusTextControl => LocalGearTestStatusText;

    internal CheckBox PaddleGearBenchEnabledCheckBoxControl => PaddleGearBenchEnabledCheckBox;

    internal CheckBox PaddleGearBenchArmCheckBoxControl => PaddleGearBenchArmCheckBox;

    internal ComboBox PaddleGearBenchOutputModeComboBoxControl => PaddleGearBenchOutputModeComboBox;

    internal ComboBox PaddleGearBenchTargetComboBoxControl => PaddleGearBenchTargetComboBox;

    internal TextBox PaddleGearBenchStrengthTextBoxControl => PaddleGearBenchStrengthTextBox;

    internal TextBox PaddleGearBenchFrequencyTextBoxControl => PaddleGearBenchFrequencyTextBox;

    internal TextBox PaddleGearBenchDurationTextBoxControl => PaddleGearBenchDurationTextBox;

    internal TextBlock PaddleGearBenchStatusTextControl => PaddleGearBenchStatusText;

    internal ItemsControl PaddleGearBenchItemsControlControl => PaddleGearBenchItemsControl;

    internal CheckBox PhprValidationUserPresentCheckBoxControl => PhprValidationUserPresentCheckBox;

    internal CheckBox PhprValidationP700ConnectedCheckBoxControl => PhprValidationP700ConnectedCheckBox;

    internal CheckBox PhprValidationBrakeInstalledCheckBoxControl => PhprValidationBrakeInstalledCheckBox;

    internal CheckBox PhprValidationThrottleInstalledCheckBoxControl => PhprValidationThrottleInstalledCheckBox;

    internal CheckBox PhprValidationGearPaddlePlannedCheckBoxControl => PhprValidationGearPaddlePlannedCheckBox;

    internal TextBox PhprValidationDeviceInfoTextBoxControl => PhprValidationDeviceInfoTextBox;

    internal TextBox PhprValidationPassFailDecisionTextBoxControl => PhprValidationPassFailDecisionTextBox;

    internal TextBox PhprValidationBrakeResultTextBoxControl => PhprValidationBrakeResultTextBox;

    internal TextBox PhprValidationThrottleResultTextBoxControl => PhprValidationThrottleResultTextBox;

    internal TextBox PhprValidationEmergencyStopResultTextBoxControl => PhprValidationEmergencyStopResultTextBox;

    internal TextBox PhprValidationUpshiftResultTextBoxControl => PhprValidationUpshiftResultTextBox;

    internal TextBox PhprValidationDownshiftResultTextBoxControl => PhprValidationDownshiftResultTextBox;

    internal TextBox PhprValidationWrongPedalTextBoxControl => PhprValidationWrongPedalTextBox;

    internal TextBox PhprValidationSustainedVibrationTextBoxControl => PhprValidationSustainedVibrationTextBox;

    internal TextBox PhprValidationNotesTextBoxControl => PhprValidationNotesTextBox;

    internal TextBlock PhprValidationStatusTextControl => PhprValidationStatusText;

    internal ItemsControl PhprValidationItemsControlControl => PhprValidationItemsControl;

    internal void Apply(TestingValidationStatusPresentation presentation)
    {
        TestBenchStartStopButton.Content = presentation.TestBenchStartStopButtonText;
        TestBenchStateText.Text = presentation.TestBenchStateText;
        TestBenchPeakText.Text = presentation.TestBenchPeakText;
        TestBenchLimiterText.Text = presentation.TestBenchLimiterText;
        TestBenchOutputText.Text = presentation.TestBenchOutputText;
        TestBenchWarningText.Text = presentation.TestBenchWarningText;
    }

    private void TestBenchSignalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        TestBenchSignalSelectionChanged?.Invoke(sender, e);
    }

    private void TestBenchStartStopButton_Click(object sender, RoutedEventArgs e)
    {
        TestBenchStartStopClicked?.Invoke(sender, e);
    }

    private void ManualBst1Control_LostFocus(object sender, RoutedEventArgs e)
    {
        ManualBst1ControlLostFocus?.Invoke(sender, e);
    }

    private void ManualBst1PulseButton_Click(object sender, RoutedEventArgs e)
    {
        ManualBst1PulseClicked?.Invoke(sender, e);
    }

    private void ManualAsioHardwareTestChannel1Button_Click(object sender, RoutedEventArgs e)
    {
        ManualAsioHardwareTestChannel1Clicked?.Invoke(sender, e);
    }

    private void ManualAsioHardwareTestChannel0Button_Click(object sender, RoutedEventArgs e)
    {
        ManualAsioHardwareTestChannel0Clicked?.Invoke(sender, e);
    }

    private void TestPhprBrakePulseButton_Click(object sender, RoutedEventArgs e)
    {
        TestPhprBrakePulseClicked?.Invoke(sender, e);
    }

    private void TestPhprThrottlePulseButton_Click(object sender, RoutedEventArgs e)
    {
        TestPhprThrottlePulseClicked?.Invoke(sender, e);
    }

    private void LocalGearTestModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        LocalGearTestModeChanged?.Invoke(sender, e);
    }

    private void StartGearTestListenerButton_Click(object sender, RoutedEventArgs e)
    {
        StartGearTestListenerClicked?.Invoke(sender, e);
    }

    private void PaddleGearBenchControl_Changed(object sender, RoutedEventArgs e)
    {
        PaddleGearBenchControlChanged?.Invoke(sender, e);
    }

    private void PaddleGearBenchControl_Changed(object sender, SelectionChangedEventArgs e)
    {
        PaddleGearBenchSelectionChanged?.Invoke(sender, e);
    }

    private void PaddleGearBenchControl_LostFocus(object sender, RoutedEventArgs e)
    {
        PaddleGearBenchControlLostFocus?.Invoke(sender, e);
    }

    private void ClearPaddleGearBenchCountersButton_Click(object sender, RoutedEventArgs e)
    {
        ClearPaddleGearBenchCountersClicked?.Invoke(sender, e);
    }

    private void PhprValidationControl_Changed(object sender, RoutedEventArgs e)
    {
        PhprValidationControlChanged?.Invoke(sender, e);
    }

    private void PhprValidationControl_LostFocus(object sender, RoutedEventArgs e)
    {
        PhprValidationControlLostFocus?.Invoke(sender, e);
    }

    private void RefreshPhprValidationChecklistButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshPhprValidationChecklistClicked?.Invoke(sender, e);
    }

    private void ExportPhprValidationResultButton_Click(object sender, RoutedEventArgs e)
    {
        ExportPhprValidationResultClicked?.Invoke(sender, e);
    }
}
