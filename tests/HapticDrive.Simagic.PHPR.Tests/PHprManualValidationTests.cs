using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Abstractions.Validation;

namespace HapticDrive.Simagic.PHPR.Tests;

public sealed class PHprManualValidationTests
{
    [Fact]
    public void ChecklistBlocksWhenCoexistenceIsNotClear()
    {
        var readiness = PHprManualValidationReadiness.Evaluate(ReadyChecklist() with
        {
            SoftwareConflictStatus = PHprSoftwareConflictStatus.SimProRunning
        });

        Assert.False(readiness.CanRunBrakePulse);
        Assert.Contains(readiness.Issues, issue => issue.Code == PHprManualValidationIssueCode.SoftwareConflictNotClear);
    }

    [Fact]
    public void ChecklistAllowsManualPulseWhenAllGatesAreReady()
    {
        var readiness = PHprManualValidationReadiness.Evaluate(ReadyChecklist());

        Assert.True(readiness.CanRunBrakePulse);
        Assert.True(readiness.CanRunThrottlePulse);
        Assert.True(readiness.CanRunGearPaddleTest);
        Assert.Empty(readiness.Issues);
    }

    [Fact]
    public void PassDecisionRequiresRequiredManualFields()
    {
        var result = ReadyResult() with
        {
            EmergencyStopResult = "",
            PassFailDecision = "pass"
        };

        var evaluation = result.Evaluate();

        Assert.True(evaluation.PassRequested);
        Assert.False(evaluation.CanMarkPass);
        Assert.Contains(evaluation.Issues, issue => issue.Message.Contains(nameof(PHprManualValidationResult.EmergencyStopResult), StringComparison.Ordinal));
    }

    [Fact]
    public void CannotMarkPassWithoutHardwareConfirmations()
    {
        var result = ReadyResult() with
        {
            P700Connected = false,
            PassFailDecision = "pass"
        };

        var evaluation = result.Evaluate();

        Assert.False(evaluation.CanMarkPass);
        Assert.Contains(evaluation.Issues, issue => issue.Code == PHprManualValidationIssueCode.RequiredHardwareFlagNotConfirmed);
    }

    [Fact]
    public void CompleteResultCanMarkPass()
    {
        var evaluation = ReadyResult().Evaluate();

        Assert.True(evaluation.CanMarkPass);
        Assert.Empty(evaluation.Issues);
    }

    [Fact]
    public void MarkdownExportWarnsAgainstCommittingPrivateData()
    {
        var markdown = PHprManualValidationResultExporter.FormatMarkdown(ReadyResult());

        Assert.Contains("Do not commit", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("private local result", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pass-ready", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportWritesLocalMarkdownFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"phpr-validation-{Guid.NewGuid():N}");
        try
        {
            var exporter = new PHprManualValidationResultExporter();
            var path = exporter.ExportMarkdown(ReadyResult(), directory);

            Assert.True(File.Exists(path));
            Assert.StartsWith(directory, path, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static PHprManualValidationChecklist ReadyChecklist()
    {
        return new PHprManualValidationChecklist(
            UserPhysicallyPresent: true,
            P700Connected: true,
            BrakeModuleInstalled: true,
            ThrottleModuleInstalled: true,
            DirectControlEnabled: true,
            DirectControlArmed: true,
            DeviceInterfaceReportSelected: true,
            SafetyLimitsVisible: true,
            EmergencyStopVisible: true,
            EmergencyStopClear: true,
            BrakeTestPulseAvailable: true,
            ThrottleTestPulseAvailable: true,
            GearPaddleTestPlanned: true,
            SoftwareConflictStatus: PHprSoftwareConflictStatus.Clear);
    }

    private static PHprManualValidationResult ReadyResult()
    {
        return new PHprManualValidationResult(
            CreatedAtUtc: DateTimeOffset.UtcNow,
            AppBranchOrCommit: "test-commit",
            P700Connected: true,
            BrakeModuleInstalled: true,
            ThrottleModuleInstalled: true,
            P700DeviceInfo: "sanitized P700 info",
            SimProStatus: "closed",
            SimHubStatus: "closed",
            SelectedDeviceInterfaceReport: "sanitized manual interface, report 64 bytes",
            BrakeTestResult: "brake pulse observed and stopped",
            ThrottleTestResult: "throttle pulse observed and stopped",
            EmergencyStopResult: "emergency stop stopped both modules",
            PaddleUpshiftResult: "upshift pulse observed",
            PaddleDownshiftResult: "downshift pulse observed",
            WrongPedalBehavior: "none observed",
            SustainedVibrationBehavior: "none observed",
            Notes: "test note",
            PassFailDecision: "pass");
    }
}
