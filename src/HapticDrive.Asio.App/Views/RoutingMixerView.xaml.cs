using System.Windows;
using System.Windows.Controls;

namespace HapticDrive.Asio.App.Views;

public partial class RoutingMixerView : UserControl, IAudioProfileRoutingMixerViewSync
{
    internal event RoutedEventHandler? TuningControlChanged;

    public RoutingMixerView()
    {
        InitializeComponent();
    }

    internal void Apply(RoutingMixerStatusPresentation presentation)
    {
        MasterGainValueText.Text = presentation.MasterGainValueText;
        SafetyOutputGainValueText.Text = presentation.SafetyOutputGainValueText;
        MixerEmergencyMuteStatusText.Text = presentation.MixerEmergencyMuteStatusText;
        MixerOutputPeakStatusText.Text = presentation.MixerOutputPeakStatusText;
        MixerLimiterActivityStatusText.Text = presentation.MixerLimiterActivityStatusText;
        Bst1RoutingSummaryText.Text = presentation.Bst1RoutingSummaryText;
        Bst1EffectsSummaryText.Text = presentation.Bst1EffectsSummaryText;
        BrakePhprRoutingSummaryText.Text = presentation.BrakePhprRoutingSummaryText;
        BrakePhprEffectsSummaryText.Text = presentation.BrakePhprEffectsSummaryText;
        ThrottlePhprRoutingSummaryText.Text = presentation.ThrottlePhprRoutingSummaryText;
        ThrottlePhprEffectsSummaryText.Text = presentation.ThrottlePhprEffectsSummaryText;
        ActiveEffectsSummaryText.Text = presentation.ActiveEffectsSummaryText;
        PriorityDuckingSummaryText.Text = presentation.PriorityDuckingSummaryText;
    }

    internal void ApplyAudioProfileMixerControlValues(AudioProfileControlValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        MasterGainSlider.Value = values.MasterGain;
        MixerMuteCheckBox.IsChecked = values.MixerMuted;
        SafetyOutputGainSlider.Value = values.SafetyOutputGain;
    }

    internal void ApplyAudioProfileMixerControlText(AudioProfileControlTextValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        MasterGainValueText.Text = values.MasterGainText;
        SafetyOutputGainValueText.Text = values.SafetyOutputGainText;
    }

    internal AudioProfileMixerControlInputs BuildAudioProfileMixerControlInputs()
    {
        return new AudioProfileMixerControlInputs(
            MasterGainValue: MasterGainSlider.Value,
            MixerMuted: MixerMuteCheckBox.IsChecked == true,
            SafetyOutputGainValue: SafetyOutputGainSlider.Value);
    }

    AudioProfileMixerControlInputs IAudioProfileRoutingMixerViewSync.BuildAudioProfileMixerControlInputs()
    {
        return BuildAudioProfileMixerControlInputs();
    }

    void IAudioProfileRoutingMixerViewSync.ApplyAudioProfileMixerControlValues(AudioProfileControlValues values)
    {
        ApplyAudioProfileMixerControlValues(values);
    }

    void IAudioProfileRoutingMixerViewSync.ApplyAudioProfileMixerControlText(AudioProfileControlTextValues values)
    {
        ApplyAudioProfileMixerControlText(values);
    }

    private void TuningControl_Changed(object sender, RoutedEventArgs e)
    {
        TuningControlChanged?.Invoke(sender, e);
    }
}
