using System.Windows;
using System.Windows.Controls;

namespace HapticDrive.Asio.App.Views;

public partial class RoutingMixerView : UserControl
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

    internal T GetRequiredControl<T>(string name)
        where T : FrameworkElement
    {
        return FindName(name) as T
            ?? throw new InvalidOperationException($"RoutingMixerView control '{name}' was not found.");
    }

    private void TuningControl_Changed(object sender, RoutedEventArgs e)
    {
        TuningControlChanged?.Invoke(sender, e);
    }
}
