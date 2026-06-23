using System.Windows;

namespace HapticDrive.Asio.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private AppCompositionRoot? _compositionRoot;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _compositionRoot = new AppCompositionRoot();
        MainWindow = _compositionRoot.CreateMainWindow();
        MainWindow.Show();
    }
}
