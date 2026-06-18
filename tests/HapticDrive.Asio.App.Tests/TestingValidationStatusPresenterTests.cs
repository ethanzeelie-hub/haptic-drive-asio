namespace HapticDrive.Asio.App.Tests;

public sealed class TestingValidationStatusPresenterTests
{
    [Fact]
    public void Build_WhenIdle_ShowsReadyBenchStatus()
    {
        var presentation = TestingValidationStatusPresenter.Build(CreateSnapshot());

        Assert.Equal("Start Test Bench", presentation.TestBenchStartStopButtonText);
        Assert.Equal("Idle", presentation.TestBenchStateText);
        Assert.Equal("0.000", presentation.TestBenchPeakText);
        Assert.Equal("0 limited", presentation.TestBenchLimiterText);
        Assert.Equal("Null Output (Stopped)", presentation.TestBenchOutputText);
        Assert.Equal("Physical shaker feel, safe gain, latency, and frequency tuning are not validated until real hardware testing.", presentation.TestBenchWarningText);
        Assert.Equal("Testing tools ready; synthetic bench idle; output Null Output; local exports and manual checks remain available.", presentation.TestingValidationPageStatusText);
    }

    [Fact]
    public void Build_WhenBenchIsActive_ShowsLiveTestingSummary()
    {
        var presentation = TestingValidationStatusPresenter.Build(CreateSnapshot(
            testBenchActive: true,
            testBenchSelectedSignalName: "Sine 50 Hz",
            testBenchOutputPeakLevel: 0.4321,
            testBenchLimitedSampleCount: 12,
            testBenchOutputDisplayName: "ASIO",
            testBenchOutputState: "Running"));

        Assert.Equal("Stop Test Bench", presentation.TestBenchStartStopButtonText);
        Assert.Equal("Active: Sine 50 Hz", presentation.TestBenchStateText);
        Assert.Equal("0.432", presentation.TestBenchPeakText);
        Assert.Equal("12 limited", presentation.TestBenchLimiterText);
        Assert.Equal("ASIO (Running)", presentation.TestBenchOutputText);
        Assert.Equal("Testing tools active; synthetic bench running Sine 50 Hz; output ASIO; emergency mute off.", presentation.TestingValidationPageStatusText);
    }

    [Fact]
    public void Build_WhenEmergencyMuted_PrefersMutedBenchState()
    {
        var presentation = TestingValidationStatusPresenter.Build(CreateSnapshot(
            testBenchActive: true,
            testBenchEmergencyMute: true,
            testBenchSelectedSignalName: "Pink Noise"));

        Assert.Equal("Emergency muted", presentation.TestBenchStateText);
        Assert.Equal("Testing tools active; synthetic bench running Pink Noise; output Null Output; emergency mute on.", presentation.TestingValidationPageStatusText);
    }

    private static TestingValidationStatusSnapshot CreateSnapshot(
        bool testBenchActive = false,
        bool testBenchEmergencyMute = false,
        string testBenchSelectedSignalName = "Sine 40 Hz",
        double testBenchOutputPeakLevel = 0,
        long testBenchLimitedSampleCount = 0,
        string testBenchOutputDisplayName = "Null Output",
        string testBenchOutputState = "Stopped")
    {
        return new TestingValidationStatusSnapshot(
            TestBenchActive: testBenchActive,
            TestBenchEmergencyMute: testBenchEmergencyMute,
            TestBenchSelectedSignalName: testBenchSelectedSignalName,
            TestBenchOutputPeakLevel: testBenchOutputPeakLevel,
            TestBenchLimitedSampleCount: testBenchLimitedSampleCount,
            TestBenchOutputDisplayName: testBenchOutputDisplayName,
            TestBenchOutputState: testBenchOutputState);
    }
}
