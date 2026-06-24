using System.IO;
using System.Text;
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
        try
        {
            base.OnStartup(e);

            _compositionRoot = new AppCompositionRoot();
            MainWindow = _compositionRoot.CreateMainWindow();
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            var diagnosticPath = TryWriteStartupDiagnostic(ex);
            TryShowStartupFailure(ex, diagnosticPath);
            Shutdown(-1);
        }
    }

    private static string? TryWriteStartupDiagnostic(Exception ex)
    {
        try
        {
            var directory = GetStartupDiagnosticsDirectory();
            Directory.CreateDirectory(directory);
            var path = Path.Combine(
                directory,
                $"startup-failure-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.log");
            var builder = new StringBuilder();
            builder.AppendLine("Haptic Drive ASIO startup failure");
            builder.AppendLine($"TimestampUtc: {DateTimeOffset.UtcNow:O}");
            builder.AppendLine($"ProcessPath: {Environment.ProcessPath}");
            builder.AppendLine($"BaseDirectory: {AppContext.BaseDirectory}");
            builder.AppendLine();
            builder.AppendLine(ex.ToString());
            File.WriteAllText(path, builder.ToString());
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static void TryShowStartupFailure(Exception ex, string? diagnosticPath)
    {
        try
        {
            var diagnosticMessage = diagnosticPath is null
                ? "No startup diagnostic file could be written."
                : $"Startup diagnostic: {diagnosticPath}";
            MessageBox.Show(
                $"Haptic Drive ASIO failed during startup.{Environment.NewLine}{Environment.NewLine}{diagnosticMessage}{Environment.NewLine}{Environment.NewLine}{ex}",
                "Haptic Drive ASIO Startup Failure",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
        }
    }

    private static string GetStartupDiagnosticsDirectory()
    {
        var repoRoot = FindRepositoryRoot();
        return repoRoot is null
            ? Path.Combine(AppContext.BaseDirectory, "local-validation-results")
            : Path.Combine(repoRoot, "local-validation-results");
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HapticDrive.Asio.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
