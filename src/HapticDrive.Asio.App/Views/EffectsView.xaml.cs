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
