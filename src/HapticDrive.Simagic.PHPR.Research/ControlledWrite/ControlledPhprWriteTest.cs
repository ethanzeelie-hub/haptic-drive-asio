using System.Globalization;
using HapticDrive.Simagic.PHPR.Abstractions.Coexistence;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Simagic.PHPR.Research.ControlledWrite;

public enum ControlledPhprWriteTarget
{
    Brake = 0,
    Throttle = 1,
    Both = 2,
    Sequence = 3
}

public sealed record ControlledPhprWriteTestOptions
{
    public const string ApprovalPhrase = PHprControlledWriteApproval.Phrase;

    public bool Execute { get; init; }

    public string? ApprovalPhraseText { get; init; }

    public string? DevicePath { get; init; }

    public byte? ReportId { get; init; }

    public int ReportLength { get; init; } = SimHubF1EcRealReportEncoder.PayloadLengthBytes;

    public ControlledPhprWriteTarget Target { get; init; } = ControlledPhprWriteTarget.Sequence;

    public double StrengthPercent { get; init; } = 10d;

    public double FrequencyHz { get; init; } = 50d;

    public int DurationMs { get; init; } = 50;

    public int WriteTimeoutMs { get; init; } = PHprRealOutputOptions.DefaultWriteTimeoutMs;

    public bool HasApprovalPhrase =>
        PHprControlledWriteApproval.IsApproved(ApprovalPhraseText);

    public PHprHidDeviceSelector ToSelector()
    {
        return new PHprHidDeviceSelector(
            DevicePath,
            string.IsNullOrWhiteSpace(DevicePath) ? "No P-HPR HID device selected" : "Controlled P-HPR HID device",
            "controlled-write-test",
            ReportId,
            ReportLength).Normalize();
    }

    public ControlledPhprWriteTestOptions Normalize()
    {
        return this with
        {
            DevicePath = string.IsNullOrWhiteSpace(DevicePath) ? null : DevicePath.Trim(),
            ReportLength = ReportLength <= 0 ? SimHubF1EcRealReportEncoder.PayloadLengthBytes : ReportLength,
            StrengthPercent = Math.Clamp(double.IsFinite(StrengthPercent) ? StrengthPercent : 10d, 0d, 100d),
            FrequencyHz = Math.Clamp(double.IsFinite(FrequencyHz) ? FrequencyHz : 50d, 1d, 50d),
            DurationMs = Math.Clamp(DurationMs, 10, 1_000),
            WriteTimeoutMs = Math.Clamp(WriteTimeoutMs, PHprRealOutputOptions.MinWriteTimeoutMs, PHprRealOutputOptions.MaxWriteTimeoutMs)
        };
    }
}

public sealed record ControlledPhprWritePlannedCommand(
    PHprModuleId TargetModule,
    double Strength01,
    double FrequencyHz,
    int DurationMs);

public sealed record ControlledPhprWriteTestResult(
    bool Executed,
    bool Succeeded,
    string Message,
    ControlledPhprWriteTestOptions Options,
    PHprSoftwareConflictStatus CoexistenceStatus,
    IReadOnlyList<string> Issues,
    IReadOnlyList<ControlledPhprWritePlannedCommand> PlannedCommands,
    IReadOnlyList<PHprCommandResult> CommandResults,
    PHprRealOutputDiagnostics? Diagnostics);

public sealed class ControlledPhprWriteTestRunner
{
    private readonly Func<PHprHidDeviceSelector, IPhprHidReportWriter> _writerFactory;
    private readonly Func<PHprSoftwareCoexistenceSnapshot> _coexistenceScanner;
    private readonly Func<int, CancellationToken, Task> _delayAsync;

    public ControlledPhprWriteTestRunner(
        Func<PHprHidDeviceSelector, IPhprHidReportWriter>? writerFactory = null,
        Func<PHprSoftwareCoexistenceSnapshot>? coexistenceScanner = null,
        Func<int, CancellationToken, Task>? delayAsync = null)
    {
        _writerFactory = writerFactory ?? (selector => new WindowsHidReportWriter(selector));
        _coexistenceScanner = coexistenceScanner
            ?? (() => new PHprSoftwareCoexistenceDetector(new WindowsProcessSnapshotProvider()).Scan());
        _delayAsync = delayAsync ?? ((milliseconds, cancellationToken) => Task.Delay(milliseconds, cancellationToken));
    }

    public async Task<ControlledPhprWriteTestResult> RunAsync(
        ControlledPhprWriteTestOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = (options ?? new ControlledPhprWriteTestOptions()).Normalize();
        var selector = normalized.ToSelector();
        var plannedCommands = BuildPlannedCommands(normalized).ToArray();
        var coexistence = _coexistenceScanner();
        var issues = Validate(normalized, selector, plannedCommands, coexistence).ToArray();

        if (!normalized.Execute)
        {
            return new ControlledPhprWriteTestResult(
                Executed: false,
                Succeeded: true,
                "Dry run complete. No HID writer was opened and no P-HPR report was sent.",
                normalized,
                coexistence.Status,
                issues,
                plannedCommands,
                [],
                null);
        }

        if (issues.Length > 0)
        {
            return new ControlledPhprWriteTestResult(
                Executed: false,
                Succeeded: false,
                "Controlled real P-HPR write test was blocked before opening the HID writer.",
                normalized,
                coexistence.Status,
                issues,
                plannedCommands,
                [],
                null);
        }

        var openCheck = await new PHprHidOpenCheckRunner(_writerFactory).RunAsync(
            selector,
            PHprHidPathSafety.IsAbsoluteWindowsDevicePath(selector.DevicePath),
            candidateIsRawInputOnly: false,
            cancellationToken);
        if (!openCheck.Succeeded)
        {
            return new ControlledPhprWriteTestResult(
                Executed: false,
                Succeeded: false,
                "Controlled real P-HPR write test was blocked because HID open-check failed before any report was sent.",
                normalized,
                coexistence.Status,
                issues
                    .Concat([$"Open-check failed: {openCheck.SanitizedErrorCategory ?? openCheck.OpenStatus?.ToString() ?? "unknown"}"])
                    .ToArray(),
                plannedCommands,
                [],
                null);
        }

        var realOptions = BuildRealOutputOptions(normalized, selector, openCheck);
        await using var output = new SimagicPhprOutputDevice(_writerFactory(selector), realOptions);
        output.SetSafetyContext(PHprSafetyContext.DefaultMock with
        {
            IsMockOutput = false,
            RequiresRealDeviceWrites = true,
            SoftwareConflictStatus = coexistence.Status
        });

        var results = new List<PHprCommandResult>();
        foreach (var plannedCommand in plannedCommands)
        {
            var command = PHprCommand.Create(
                plannedCommand.TargetModule,
                plannedCommand.Strength01,
                plannedCommand.FrequencyHz,
                plannedCommand.DurationMs,
                PHprCommandSource.TestBench,
                priority: 100);
            var result = await output.SendAsync(command, cancellationToken);
            results.Add(result);

            if (!result.Succeeded)
            {
                break;
            }

            await _delayAsync(plannedCommand.DurationMs + 50, cancellationToken);
        }

        await output.EmergencyStopAsync(cancellationToken);
        await output.CloseAsync(cancellationToken);
        var diagnostics = output.GetDiagnostics();
        var succeeded = results.Count == plannedCommands.Length
            && results.All(result => result.Succeeded)
            && diagnostics.FailedReportWriteCount == 0;

        return new ControlledPhprWriteTestResult(
            Executed: true,
            Succeeded: succeeded,
            succeeded
                ? "Controlled real P-HPR write test completed; emergency stop was requested at the end."
                : "Controlled real P-HPR write test attempted but one or more writes failed; emergency stop was requested.",
            normalized,
            coexistence.Status,
            issues,
            plannedCommands,
            results,
            diagnostics);
    }

    private static IEnumerable<string> Validate(
        ControlledPhprWriteTestOptions options,
        PHprHidDeviceSelector selector,
        IReadOnlyList<ControlledPhprWritePlannedCommand> plannedCommands,
        PHprSoftwareCoexistenceSnapshot coexistence)
    {
        if (!options.HasApprovalPhrase)
        {
            yield return $"Exact approval phrase required: {ControlledPhprWriteTestOptions.ApprovalPhrase}";
        }

        if (!selector.IsSelected)
        {
            yield return "No P-HPR HID device path is selected for this run.";
        }

        if (selector.IsSelected && !PHprHidPathSafety.IsAbsoluteWindowsDevicePath(selector.DevicePath))
        {
            yield return "Selected P-HPR HID device path is not an absolute Windows device-interface path.";
        }

        if (selector.ReportLength != SimHubF1EcRealReportEncoder.PayloadLengthBytes)
        {
            yield return $"Report length must be {SimHubF1EcRealReportEncoder.PayloadLengthBytes} bytes for the current SimHub F1 EC hypothesis.";
        }

        if (coexistence.Status != PHprSoftwareConflictStatus.Clear)
        {
            yield return $"SimPro/SimHub coexistence must be clear before direct writes; current status is {coexistence.Status}.";
        }

        if (plannedCommands.Count == 0)
        {
            yield return "No brake or throttle command is planned.";
        }
    }

    private static PHprRealOutputOptions BuildRealOutputOptions(
        ControlledPhprWriteTestOptions options,
        PHprHidDeviceSelector selector,
        PHprHidOpenCheckResult openCheck)
    {
        var settings = PHprRealGearPulseSettings.Default with
        {
            IsEnabled = true,
            Strength01 = options.StrengthPercent / 100d,
            FrequencyHz = options.FrequencyHz,
            DurationMs = options.DurationMs
        };

        return (PHprRealOutputOptions.Disabled with
        {
            DirectControlEnabled = true,
            DirectControlArmed = true,
            DirectControlApprovalConfirmed = options.HasApprovalPhrase,
            CandidateSourceMethod = PHprDirectOutputCandidateSourceMethod.HidDeviceInterface,
            CandidateIsRawInputOnly = false,
            CandidateHasOpenableHidPath = PHprHidPathSafety.IsAbsoluteWindowsDevicePath(selector.DevicePath),
            OpenCheckAttempted = openCheck.Attempted,
            OpenCheckSucceeded = openCheck.Succeeded,
            OpenCheckFailed = openCheck.Failed,
            OpenCheckSanitizedErrorCategory = openCheck.SanitizedErrorCategory,
            Selector = selector,
            WriteTimeoutMs = options.WriteTimeoutMs,
            BrakeGearPulse = settings,
            ThrottleGearPulse = settings
        }).Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
    }

    private static IEnumerable<ControlledPhprWritePlannedCommand> BuildPlannedCommands(
        ControlledPhprWriteTestOptions options)
    {
        var strength01 = options.StrengthPercent / 100d;
        return options.Target switch
        {
            ControlledPhprWriteTarget.Brake =>
                [new ControlledPhprWritePlannedCommand(PHprModuleId.Brake, strength01, options.FrequencyHz, options.DurationMs)],
            ControlledPhprWriteTarget.Throttle =>
                [new ControlledPhprWritePlannedCommand(PHprModuleId.Throttle, strength01, options.FrequencyHz, options.DurationMs)],
            ControlledPhprWriteTarget.Both =>
                [new ControlledPhprWritePlannedCommand(PHprModuleId.Both, strength01, options.FrequencyHz, options.DurationMs)],
            ControlledPhprWriteTarget.Sequence =>
                [
                    new ControlledPhprWritePlannedCommand(PHprModuleId.Brake, strength01, options.FrequencyHz, options.DurationMs),
                    new ControlledPhprWritePlannedCommand(PHprModuleId.Throttle, strength01, options.FrequencyHz, options.DurationMs)
                ],
            _ => []
        };
    }
}

public static class ControlledPhprWriteTestFormatter
{
    public const string SafetyBanner =
        "CONTROLLED REAL P-HPR WRITE TEST\n"
        + "Real vibration is only attempted with --execute, a selected HID path, clear SimPro/SimHub coexistence,\n"
        + "and the exact Phase 2 controlled-write approval phrase. Console output intentionally hides the device path.";

    public static string FormatConsole(ControlledPhprWriteTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var lines = new List<string>
        {
            SafetyBanner,
            "",
            result.Message,
            $"Mode: {(result.Executed ? "EXECUTED REAL WRITE PATH" : "dry run only")}",
            $"Approval phrase: {(result.Options.HasApprovalPhrase ? "present" : "missing")}",
            $"Selected device path: {(result.Options.ToSelector().IsSelected ? "configured for this run" : "none")}",
            $"Report ID: {(result.Options.ReportId is null ? "none" : result.Options.ReportId.Value.ToString(CultureInfo.InvariantCulture))}; report length {result.Options.ReportLength.ToString(CultureInfo.InvariantCulture)} bytes",
            $"Coexistence: {result.CoexistenceStatus}",
            $"Pulse plan: target {result.Options.Target}; strength {result.Options.StrengthPercent.ToString("0.##", CultureInfo.InvariantCulture)}%; frequency {result.Options.FrequencyHz.ToString("0.##", CultureInfo.InvariantCulture)} Hz; duration {result.Options.DurationMs.ToString(CultureInfo.InvariantCulture)} ms; timeout {result.Options.WriteTimeoutMs.ToString(CultureInfo.InvariantCulture)} ms"
        };

        lines.Add($"Planned commands: {result.PlannedCommands.Count}");
        foreach (var planned in result.PlannedCommands)
        {
            lines.Add($"- {planned.TargetModule}: {planned.Strength01:P0}, {planned.FrequencyHz:0.##} Hz, {planned.DurationMs} ms");
        }

        if (result.Issues.Count > 0)
        {
            lines.Add("Execution blockers:");
            lines.AddRange(result.Issues.Select(issue => $"- {issue}"));
        }
        else
        {
            lines.Add("Execution blockers: none");
        }

        if (result.CommandResults.Count > 0)
        {
            lines.Add("Command results:");
            lines.AddRange(result.CommandResults.Select(commandResult =>
                $"- {commandResult.Command?.TargetModule.ToString() ?? "unknown"}: {commandResult.Status}; {commandResult.Message}"));
        }

        if (result.Diagnostics is not null)
        {
            lines.Add(
                $"Diagnostics: writes {result.Diagnostics.ReportWriteCount}; failures {result.Diagnostics.FailedReportWriteCount}; connection {result.Diagnostics.Connection.State}; emergency stop {result.Diagnostics.Output.IsEmergencyStopActive}; stop reports {result.Diagnostics.Connection.StopReportWriteCount}.");
            lines.Add(
                $"Last report: {result.Diagnostics.LastReportState?.ToString() ?? "none"} {result.Diagnostics.LastTarget?.ToString() ?? "none"}; last write {result.Diagnostics.Connection.LastWriteStatus?.ToString() ?? "none"}; last stop {result.Diagnostics.Connection.LastStopStatus?.ToString() ?? "none"}; last error {result.Diagnostics.LastError ?? "none"}.");
        }

        lines.Add("Do not commit private HID paths, raw captures, serial numbers, or local validation exports.");
        return string.Join(Environment.NewLine, lines);
    }
}
