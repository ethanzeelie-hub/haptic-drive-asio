namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowSizeGuardrailTests
{
    [Fact]
    public void MainWindowTotalSizeGuard_CountsAllPartials()
    {
        var lineCount = MainWindowSourceTestHelper.ReadMainWindowPartialLineCounts().Values.Sum();

        Assert.True(
            lineCount < 1000,
            $"Expected all MainWindow partials to stay below 1000 total lines for Stage 8, but found {lineCount}.");
    }

    [Fact]
    public void NoMainWindowPartialExceeds500Lines()
    {
        var lineCounts = MainWindowSourceTestHelper.ReadMainWindowPartialLineCounts();

        foreach (var (fileName, lineCount) in lineCounts)
        {
            Assert.True(
                lineCount < 500,
                $"Expected {fileName} to stay below 500 lines for Stage 8, but found {lineCount}.");
        }
    }
}
