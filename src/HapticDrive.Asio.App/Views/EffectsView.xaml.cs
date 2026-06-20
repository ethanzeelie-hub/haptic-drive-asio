using System.Windows;
using System.Windows.Controls;

namespace HapticDrive.Asio.App.Views;

public partial class EffectsView : UserControl
{
    internal event RoutedEventHandler? TuningControlChanged;
    internal event RoutedEventHandler? PhprPedalsControlChanged;
    internal event RoutedEventHandler? PhprPedalsControlLostFocus;
    internal event RoutedEventHandler? RealPhprDirectControlChanged;
    internal event RoutedEventHandler? RealPhprDirectControlLostFocus;
    internal event RoutedEventHandler? Bst1PaddleGearPulseControlChanged;
    internal event RoutedEventHandler? Bst1PaddleGearPulseControlLostFocus;

    public EffectsView()
    {
        InitializeComponent();
    }

    internal void Apply(EffectsStatusPresentation presentation)
    {
        SharedRoadSignalStatusText.Text = presentation.SharedRoadSignalStatusText;
        EngineEffectStateText.Text = presentation.EngineEffectStateText;
        EngineEffectDetailText.Text = presentation.EngineEffectDetailText;
        EngineEffectDefaultsText.Text = presentation.EngineEffectDefaultsText;
        GearShiftEffectStateText.Text = presentation.GearShiftEffectStateText;
        GearShiftEffectDetailText.Text = presentation.GearShiftEffectDetailText;
        GearShiftEffectDefaultsText.Text = presentation.GearShiftEffectDefaultsText;
        KerbEffectStateText.Text = presentation.KerbEffectStateText;
        KerbEffectDetailText.Text = presentation.KerbEffectDetailText;
        KerbEffectDefaultsText.Text = presentation.KerbEffectDefaultsText;
        ImpactEffectStateText.Text = presentation.ImpactEffectStateText;
        ImpactEffectDetailText.Text = presentation.ImpactEffectDetailText;
        ImpactEffectDefaultsText.Text = presentation.ImpactEffectDefaultsText;
        RoadTextureEffectStateText.Text = presentation.RoadTextureEffectStateText;
        RoadTextureEffectDetailText.Text = presentation.RoadTextureEffectDetailText;
        RoadTextureEffectDefaultsText.Text = presentation.RoadTextureEffectDefaultsText;
        SlipEffectStateText.Text = presentation.SlipEffectStateText;
        SlipEffectDetailText.Text = presentation.SlipEffectDetailText;
        SlipEffectDefaultsText.Text = presentation.SlipEffectDefaultsText;
    }

    internal void ApplyAudioProfileEffectControlValues(Bst1AudioProfileEffectControlValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        SharedRoadSignalEnabledCheckBox.IsChecked = values.SharedRoadSignalEnabled;
        EngineEnabledCheckBox.IsChecked = values.EngineEnabled;
        EngineGainSlider.Value = values.EngineGain;
        EngineMinimumFrequencySlider.Value = values.EngineMinimumFrequencyHz;
        EngineMaximumFrequencySlider.Value = values.EngineMaximumFrequencyHz;
        GearShiftEnabledCheckBox.IsChecked = values.GearShiftEnabled;
        GearShiftGainSlider.Value = values.GearShiftGain;
        GearShiftDurationSlider.Value = values.GearShiftDurationMilliseconds;
        KerbEnabledCheckBox.IsChecked = values.KerbEnabled;
        KerbGainSlider.Value = values.KerbGain;
        KerbBaseFrequencySlider.Value = values.KerbBaseFrequencyHz;
        ImpactEnabledCheckBox.IsChecked = values.ImpactEnabled;
        ImpactGainSlider.Value = values.ImpactGain;
        ImpactDurationSlider.Value = values.ImpactDurationMilliseconds;
        Bst1RoadOutputEnabledCheckBox.IsChecked = values.Bst1RoadOutputEnabled;
        RoadTextureGainSlider.Value = values.RoadTextureGain;
        RoadTextureMinimumSpeedSlider.Value = values.RoadTextureMinimumSpeedKph;
        RoadTextureSpeedReferenceSlider.Value = values.RoadTextureSpeedReferenceKph;
        RoadTextureLowSpeedFrequencySlider.Value = values.RoadTextureLowSpeedFrequencyHz;
        RoadTextureHighSpeedFrequencySlider.Value = values.RoadTextureHighSpeedFrequencyHz;
        RoadTextureSpeedFrequencyInfluenceSlider.Value = values.RoadTextureSpeedFrequencyInfluence;
        RoadTextureGrainAmountSlider.Value = values.RoadTextureGrainAmount;
        SlipWheelSlipEnabledCheckBox.IsChecked = values.SlipWheelSlipEnabled;
        SlipWheelSlipGainSlider.Value = values.SlipWheelSlipGain;
        SlipWheelSlipFrequencySlider.Value = values.SlipWheelSlipFrequencyHz;
        SlipWheelSlipNoiseSlider.Value = values.SlipWheelSlipNoiseAmount;
        SlipWheelLockEnabledCheckBox.IsChecked = values.SlipWheelLockEnabled;
        SlipWheelLockGainSlider.Value = values.SlipWheelLockGain;
        SlipWheelLockFrequencySlider.Value = values.SlipWheelLockFrequencyHz;
        SlipWheelLockNoiseSlider.Value = values.SlipWheelLockNoiseAmount;
        SlipWheelLockSensitivitySlider.Value = values.SlipWheelLockSensitivity;
        SlipThresholdSlider.Value = values.SlipThreshold;
    }

    internal void ApplyAudioProfileEffectControlText(Bst1AudioProfileEffectControlTextValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        EngineGainValueText.Text = values.EngineGainText;
        EngineFrequencyValueText.Text = values.EngineFrequencyText;
        GearShiftGainValueText.Text = values.GearShiftGainText;
        GearShiftDurationValueText.Text = values.GearShiftDurationText;
        KerbGainValueText.Text = values.KerbGainText;
        KerbFrequencyValueText.Text = values.KerbFrequencyText;
        ImpactGainValueText.Text = values.ImpactGainText;
        ImpactDurationValueText.Text = values.ImpactDurationText;
        RoadTextureGainValueText.Text = values.RoadTextureGainText;
        RoadTextureMinimumSpeedValueText.Text = values.RoadTextureMinimumSpeedText;
        RoadTextureSpeedReferenceValueText.Text = values.RoadTextureSpeedReferenceText;
        RoadTextureLowSpeedFrequencyValueText.Text = values.RoadTextureLowSpeedFrequencyText;
        RoadTextureHighSpeedFrequencyValueText.Text = values.RoadTextureHighSpeedFrequencyText;
        RoadTextureSpeedFrequencyInfluenceValueText.Text = values.RoadTextureSpeedFrequencyInfluenceText;
        RoadTextureGrainAmountValueText.Text = values.RoadTextureGrainAmountText;
        SlipWheelSlipGainValueText.Text = values.SlipWheelSlipGainText;
        SlipWheelSlipFrequencyValueText.Text = values.SlipWheelSlipFrequencyText;
        SlipWheelSlipNoiseValueText.Text = values.SlipWheelSlipNoiseText;
        SlipWheelLockGainValueText.Text = values.SlipWheelLockGainText;
        SlipWheelLockFrequencyValueText.Text = values.SlipWheelLockFrequencyText;
        SlipWheelLockNoiseValueText.Text = values.SlipWheelLockNoiseText;
        SlipWheelLockSensitivityValueText.Text = values.SlipWheelLockSensitivityText;
        SlipThresholdValueText.Text = values.SlipThresholdText;
    }

    internal T GetRequiredControl<T>(string name)
        where T : FrameworkElement
    {
        return FindName(name) as T
            ?? throw new InvalidOperationException($"EffectsView control '{name}' was not found.");
    }

    private void TuningControl_Changed(object sender, RoutedEventArgs e)
    {
        TuningControlChanged?.Invoke(sender, e);
    }

    private void PhprPedalsControl_Changed(object sender, RoutedEventArgs e)
    {
        PhprPedalsControlChanged?.Invoke(sender, e);
    }

    private void PhprPedalsControl_LostFocus(object sender, RoutedEventArgs e)
    {
        PhprPedalsControlLostFocus?.Invoke(sender, e);
    }

    private void RealPhprDirectControlCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        RealPhprDirectControlChanged?.Invoke(sender, e);
    }

    private void RealPhprDirectControl_LostFocus(object sender, RoutedEventArgs e)
    {
        RealPhprDirectControlLostFocus?.Invoke(sender, e);
    }

    private void Bst1PaddleGearPulseControl_Changed(object sender, RoutedEventArgs e)
    {
        Bst1PaddleGearPulseControlChanged?.Invoke(sender, e);
    }

    private void Bst1PaddleGearPulseControl_LostFocus(object sender, RoutedEventArgs e)
    {
        Bst1PaddleGearPulseControlLostFocus?.Invoke(sender, e);
    }
}
