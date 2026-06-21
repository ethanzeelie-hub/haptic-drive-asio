namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowSizeGuardrailTests
{
    [Fact]
    public void MainWindowCodeBehindBelow1700Lines()
    {
        var lineCount = MainWindowSourceTestHelper.ReadMainWindowCodeBehindLineCount();

        Assert.True(
            lineCount < 1700,
            $"Expected MainWindow.xaml.cs to stay below 1700 lines for final production readiness, but found {lineCount}.");
    }
}
