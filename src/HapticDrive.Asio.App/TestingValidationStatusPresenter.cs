namespace HapticDrive.Asio.App;

internal sealed record TestingValidationStatusSnapshot(
    bool TestBenchActive,
    bool TestBenchEmergencyMute,
    string TestBenchSelectedSignalName,
    double TestBenchOutputPeakLevel,
    long TestBenchLimitedSampleCount,
    string TestBenchOutputDisplayName,
    string TestBenchOutputState);

internal sealed record TestingValidationStatusPresentation(
    string TestBenchStartStopButtonText,
    string TestBenchStateText,
    string TestBenchPeakText,
    string TestBenchLimiterText,
    string TestBenchOutputText,
    string TestBenchWarningText,
    string TestingValidationPageStatusText);

internal static class TestingValidationStatusPresenter
{
    public static TestingValidationStatusPresentation Build(TestingValidationStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new TestingValidationStatusPresentation(
            TestBenchStartStopButtonText: snapshot.TestBenchActive ? "Stop Test Bench" : "Start Test Bench",
            TestBenchStateText: snapshot.TestBenchEmergencyMute
                ? "Emergency muted"
                : snapshot.TestBenchActive
                    ? $"Active: {snapshot.TestBenchSelectedSignalName}"
                    : "Idle",
            TestBenchPeakText: snapshot.TestBenchOutputPeakLevel.ToString("0.000"),
            TestBenchLimiterText: $"{snapshot.TestBenchLimitedSampleCount:N0} limited",
            TestBenchOutputText: $"{snapshot.TestBenchOutputDisplayName} ({snapshot.TestBenchOutputState})",
            TestBenchWarningText: "Physical shaker feel, safe gain, latency, and frequency tuning are not validated until real hardware testing.",
            TestingValidationPageStatusText: snapshot.TestBenchActive
                ? $"Testing tools active; synthetic bench running {snapshot.TestBenchSelectedSignalName}; output {snapshot.TestBenchOutputDisplayName}; emergency mute {(snapshot.TestBenchEmergencyMute ? "on" : "off")}."
                : $"Testing tools ready; synthetic bench idle; output {snapshot.TestBenchOutputDisplayName}; local exports and manual checks remain available.");
    }
}
