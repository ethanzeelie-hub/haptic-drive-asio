using HapticDrive.Asio.App.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HapticDrive.Asio.App;

public partial class MainWindow : Window
{
    private readonly EffectSettingsListViewModel _effectSettingsViewModel;
    private readonly AppRuntimeSession _runtime;

    public MainWindow()
        : this(new AppCompositionRoot().Services)
    {
    }

    internal MainWindow(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _effectSettingsViewModel = services.EffectSettingsViewModel;

        InitializeComponent();
        EffectsViewControl.DataContext = _effectSettingsViewModel;

        _runtime = new AppRuntimeSession(this, services);
    }

    private void NavigationList_SelectionChanged(object sender, SelectionChangedEventArgs e) => _runtime.NavigationList_SelectionChanged(sender, e);

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e) => _runtime.MainWindow_PreviewKeyDown(sender, e);

    protected override void OnClosing(CancelEventArgs e)
    {
        _runtime.OnHostClosing(e);

        if (!e.Cancel)
        {
            base.OnClosing(e);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _runtime.OnHostClosed();
    }
}
