using System.Windows;
using System.Windows.Controls;

namespace HapticDrive.Asio.App.Views;

public partial class AdvancedDiagnosticsView : UserControl
{
    internal event RoutedEventHandler? AdvancedDiagnosticsEnabledChanged;
    internal event RoutedEventHandler? RealPhprDirectControlChanged;
    internal event SelectionChangedEventHandler? RealPhprDirectControlSelectionChanged;
    internal event RoutedEventHandler? RefreshRealPhprCandidatesClicked;
    internal event RoutedEventHandler? DryRunRealPhprSelectionClicked;
    internal event RoutedEventHandler? OpenCheckRealPhprSelectionClicked;
    internal event SelectionChangedEventHandler? RealPhprCandidateSelectionChanged;
    internal event RoutedEventHandler? ApplyRealPhprSelectionClicked;
    internal event RoutedEventHandler? RealPhprDirectControlLostFocus;
    internal event RoutedEventHandler? TestRealPhprBrakePulseClicked;
    internal event RoutedEventHandler? TestRealPhprThrottlePulseClicked;
    internal event RoutedEventHandler? RealPhprEmergencyStopClicked;
    internal event RoutedEventHandler? ClearRealPhprEmergencyStopClicked;
    internal event RoutedEventHandler? MockGearPulseControlChanged;
    internal event SelectionChangedEventHandler? MockGearPulseControlSelectionChanged;
    internal event RoutedEventHandler? MockGearPulseControlLostFocus;
    internal event RoutedEventHandler? ClearMockGearPulseDiagnosticsClicked;
    internal event RoutedEventHandler? MockGearPulseEmergencyStopClicked;
    internal event RoutedEventHandler? ClearMockGearPulseEmergencyStopClicked;
    internal event RoutedEventHandler? MockPedalEffectsControlChanged;
    internal event SelectionChangedEventHandler? MockPedalEffectsControlSelectionChanged;
    internal event RoutedEventHandler? MockPedalEffectsControlLostFocus;
    internal event RoutedEventHandler? ClearMockPedalEffectsDiagnosticsClicked;
    internal event RoutedEventHandler? MockPedalEffectsEmergencyStopClicked;
    internal event RoutedEventHandler? ClearMockPedalEffectsEmergencyStopClicked;
    internal event RoutedEventHandler? ThemeSettingChanged;
    internal event RoutedEventHandler? ResetProfileClicked;
    internal event RoutedEventHandler? RefreshDiagnosticsClicked;
    internal event RoutedEventHandler? CopyDiagnosticsClicked;
    internal event RoutedEventHandler? RoadTextureFlightRecorderChanged;

    public AdvancedDiagnosticsView()
    {
        InitializeComponent();
    }

    internal void Apply(DiagnosticsStatusPresentation presentation)
    {
        RoadTextureFlightRecorderStatusText.Text = presentation.RoadRecorderStatusText;
        DiagnosticsSummaryText.Text = presentation.SummaryText;
        DiagnosticsItemsControl.ItemsSource = presentation.Items;
    }

    internal T GetRequiredControl<T>(string name)
        where T : FrameworkElement
    {
        return FindName(name) as T
            ?? throw new InvalidOperationException($"AdvancedDiagnosticsView control '{name}' was not found.");
    }

    private void AdvancedDiagnosticsEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        AdvancedDiagnosticsEnabledChanged?.Invoke(sender, e);
    }

    private void RealPhprDirectControlCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        RealPhprDirectControlChanged?.Invoke(sender, e);
    }

    private void RealPhprDirectControlCheckBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        RealPhprDirectControlSelectionChanged?.Invoke(sender, e);
    }

    private void RefreshRealPhprCandidatesButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRealPhprCandidatesClicked?.Invoke(sender, e);
    }

    private void DryRunRealPhprSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        DryRunRealPhprSelectionClicked?.Invoke(sender, e);
    }

    private void OpenCheckRealPhprSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        OpenCheckRealPhprSelectionClicked?.Invoke(sender, e);
    }

    private void RealPhprCandidateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RealPhprCandidateSelectionChanged?.Invoke(sender, e);
    }

    private void ApplyRealPhprSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyRealPhprSelectionClicked?.Invoke(sender, e);
    }

    private void RealPhprDirectControl_LostFocus(object sender, RoutedEventArgs e)
    {
        RealPhprDirectControlLostFocus?.Invoke(sender, e);
    }

    private void TestRealPhprBrakePulseButton_Click(object sender, RoutedEventArgs e)
    {
        TestRealPhprBrakePulseClicked?.Invoke(sender, e);
    }

    private void TestRealPhprThrottlePulseButton_Click(object sender, RoutedEventArgs e)
    {
        TestRealPhprThrottlePulseClicked?.Invoke(sender, e);
    }

    private void RealPhprEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        RealPhprEmergencyStopClicked?.Invoke(sender, e);
    }

    private void ClearRealPhprEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        ClearRealPhprEmergencyStopClicked?.Invoke(sender, e);
    }

    private void MockGearPulseControl_Changed(object sender, RoutedEventArgs e)
    {
        MockGearPulseControlChanged?.Invoke(sender, e);
    }

    private void MockGearPulseControl_Changed(object sender, SelectionChangedEventArgs e)
    {
        MockGearPulseControlSelectionChanged?.Invoke(sender, e);
    }

    private void MockGearPulseControl_LostFocus(object sender, RoutedEventArgs e)
    {
        MockGearPulseControlLostFocus?.Invoke(sender, e);
    }

    private void ClearMockGearPulseDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        ClearMockGearPulseDiagnosticsClicked?.Invoke(sender, e);
    }

    private void MockGearPulseEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        MockGearPulseEmergencyStopClicked?.Invoke(sender, e);
    }

    private void ClearMockGearPulseEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        ClearMockGearPulseEmergencyStopClicked?.Invoke(sender, e);
    }

    private void MockPedalEffectsControl_Changed(object sender, RoutedEventArgs e)
    {
        MockPedalEffectsControlChanged?.Invoke(sender, e);
    }

    private void MockPedalEffectsControl_Changed(object sender, SelectionChangedEventArgs e)
    {
        MockPedalEffectsControlSelectionChanged?.Invoke(sender, e);
    }

    private void MockPedalEffectsControl_LostFocus(object sender, RoutedEventArgs e)
    {
        MockPedalEffectsControlLostFocus?.Invoke(sender, e);
    }

    private void ClearMockPedalEffectsDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        ClearMockPedalEffectsDiagnosticsClicked?.Invoke(sender, e);
    }

    private void MockPedalEffectsEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        MockPedalEffectsEmergencyStopClicked?.Invoke(sender, e);
    }

    private void ClearMockPedalEffectsEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        ClearMockPedalEffectsEmergencyStopClicked?.Invoke(sender, e);
    }

    private void ThemeSettingCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        ThemeSettingChanged?.Invoke(sender, e);
    }

    private void ResetProfileButton_Click(object sender, RoutedEventArgs e)
    {
        ResetProfileClicked?.Invoke(sender, e);
    }

    private void RefreshDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshDiagnosticsClicked?.Invoke(sender, e);
    }

    private void CopyDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        CopyDiagnosticsClicked?.Invoke(sender, e);
    }

    private void RoadTextureFlightRecorderCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        RoadTextureFlightRecorderChanged?.Invoke(sender, e);
    }
}
