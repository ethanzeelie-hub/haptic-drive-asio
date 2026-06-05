namespace HapticDrive.Simagic.PHPR.Research.Capture;

public static class SimagicCaptureTemplateFactory
{
    public static SimagicCaptureMetadata Create(
        SimagicCaptureScenarioId scenarioId,
        SimagicCaptureTargetModule? targetModule = null,
        DateTimeOffset? captureStartedAtUtc = null)
    {
        var scenario = SimagicCaptureScenarios.Get(scenarioId);
        var startedAtUtc = captureStartedAtUtc ?? DateTimeOffset.UtcNow;
        var target = targetModule ?? scenario.RecommendedTarget;
        var before = CreateSuggestedSettings(scenario);
        var after = CreateSuggestedSettings(scenario);

        return new SimagicCaptureMetadata
        {
            ScenarioId = scenario.Id,
            ScenarioName = scenario.Name,
            CaptureStartedAtUtc = startedAtUtc,
            CaptureDuration = TimeSpan.FromSeconds(10),
            CaptureFileName = SimagicCaptureFilenameBuilder.Build(
                startedAtUtc,
                scenario.SoftwareUnderTest,
                scenario.DeviceName,
                scenario.Id,
                target,
                before,
                after),
            Software = new SimagicCaptureSoftwareContext
            {
                CaptureTool = "USBPcap/Wireshark",
                SoftwareUnderTest = scenario.SoftwareUnderTest,
                SimProRunning = scenario.SoftwareUnderTest.Equals("simpro", StringComparison.OrdinalIgnoreCase),
                SimHubRunning = scenario.SoftwareUnderTest.Equals("simhub", StringComparison.OrdinalIgnoreCase),
                HapticDriveRunning = false
            },
            Device = new SimagicCaptureDeviceContext
            {
                TargetModule = target
            },
            Action = new SimagicCaptureActionContext
            {
                ActionPerformed = scenario.Description,
                SettingBefore = before,
                SettingAfter = after
            },
            Notes = "Template only. Replace placeholder values before Stage 2I capture analysis.",
            RedactionStatus = SimagicCaptureRedactionStatus.NotReviewed,
            RawCapturePath = "captures/private/simagic/<date>/<capture-file-name>.pcapng",
            SanitizedSummaryPath = "capture-metadata/generated/<capture-id>-summary.json"
        };
    }

    private static SimagicCaptureSettingSnapshot CreateSuggestedSettings(SimagicCaptureScenario scenario)
    {
        return new SimagicCaptureSettingSnapshot
        {
            StrengthPercent = scenario.RequiresStrength ? 20d : null,
            FrequencyHz = scenario.RequiresFrequency ? 30d : null,
            DurationMs = scenario.RequiresDuration ? 100 : null
        };
    }
}
