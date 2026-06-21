namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowSizeGuardrailTests
{
    [Fact]
    public void MainWindowCodeBehindBelow3000Lines()
    {
        var lineCount = MainWindowSourceTestHelper.ReadMainWindowCodeBehindLineCount();

        Assert.True(
            lineCount < 3000,
            $"Expected MainWindow.xaml.cs to stay below 3000 lines for Stage 8, but found {lineCount}.");
    }
}
