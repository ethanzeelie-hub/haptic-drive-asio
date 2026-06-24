using System.Runtime.ExceptionServices;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using HapticDrive.Asio.App;

namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowStartupTests
{
    [Fact]
    public void MainWindowConstruction_DoesNotCrash_WhenInitialNavigationSelectionRuns()
    {
        RunOnStaThread(() =>
        {
            if (Application.Current is null)
            {
                _ = new App
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
            }

            EnsureApplicationResourcesLoaded(Application.Current!);

            var root = new AppCompositionRoot();
            var window = new MainWindow(root.Services);
            try
            {
                Assert.Equal(0, window.NavigationList.SelectedIndex);
                Assert.Equal("Dashboard", window.PageTitleText.Text);
                Assert.Equal("Viewing Dashboard", window.FooterStatusText.Text);
                Assert.Equal(Visibility.Visible, window.DashboardViewControl.Visibility);
            }
            finally
            {
                CloseWindowForTest(window);
            }
        });
    }

    [Fact]
    public void NavigationSelectionForwarder_GuardsRuntimeDuringConstruction()
    {
        var source = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs");

        var nullCheckIndex = source.IndexOf("if (_runtime is null)", StringComparison.Ordinal);
        var delegateCallIndex = source.IndexOf("_runtime.NavigationList_SelectionChanged(sender, e);", StringComparison.Ordinal);

        Assert.True(nullCheckIndex >= 0, "Expected a construction-time null guard before forwarding navigation selection.");
        Assert.True(delegateCallIndex > nullCheckIndex, "Expected the runtime forwarding call to appear after the construction-time null guard.");
    }

    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? capturedException = null;
        using var completed = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                capturedException = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(completed.Wait(TimeSpan.FromSeconds(30)), "Timed out waiting for the STA WPF test thread to complete.");
        capturedException?.Throw();
    }

    private static void EnsureApplicationResourcesLoaded(Application application)
    {
        AddMergedDictionaryIfMissing(
            application,
            "pack://application:,,,/HapticDrive.Asio.App;component/Resources/Theme.xaml");
        AddMergedDictionaryIfMissing(
            application,
            "pack://application:,,,/HapticDrive.Asio.App;component/Resources/Styles.xaml");
    }

    private static void AddMergedDictionaryIfMissing(Application application, string source)
    {
        if (application.Resources.MergedDictionaries.Any(dictionary =>
                string.Equals(dictionary.Source?.OriginalString, source, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Absolute)
        });
    }

    private static void CloseWindowForTest(MainWindow window)
    {
        var runtimeField = typeof(MainWindow).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(runtimeField);
        var runtime = runtimeField.GetValue(window);
        Assert.NotNull(runtime);

        var runtimeType = runtime.GetType();
        var cleanupMethod = runtimeType.GetMethod("RunShutdownCleanupBlocking", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cleanupMethod);
        cleanupMethod.Invoke(runtime, []);

        var completedField = runtimeType.GetField("_shutdownCleanupCompleted", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(completedField);
        completedField.SetValue(runtime, true);

        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Close();

        var timeoutUtc = DateTime.UtcNow.AddSeconds(10);
        while (!closed)
        {
            if (DateTime.UtcNow >= timeoutUtc)
            {
                throw new TimeoutException("Timed out waiting for the test window to close.");
            }

            var frame = new DispatcherFrame();
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}
