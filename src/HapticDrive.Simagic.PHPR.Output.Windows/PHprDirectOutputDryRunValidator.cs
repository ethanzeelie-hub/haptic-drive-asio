using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprDirectOutputDryRunResult(
    bool CanPulse,
    PHprHidDeviceSelector Selector,
    PHprSoftwareConflictStatus CoexistenceStatus,
    bool EmergencyStopActive,
    IReadOnlyList<string> Issues)
{
    public string Summary =>
        $"Direct-output dry run: selected {Selector.IsSelected}; report length {Selector.ReportLength:N0} bytes; can pulse {CanPulse}; coexistence {CoexistenceStatus}; emergency stop {EmergencyStopActive}; issues {Issues.Count:N0}.";
}

public static class PHprDirectOutputDryRunValidator
{
    public static PHprDirectOutputDryRunResult Validate(
        PHprRealOutputOptions options,
        PHprSoftwareConflictStatus coexistenceStatus,
        bool emergencyStopActive)
    {
        var normalized = (options ?? PHprRealOutputOptions.Disabled).Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        var selector = normalized.Selector;
        var issues = new List<string>();

        if (!selector.IsSelected)
        {
            issues.Add("No direct-output HID candidate is selected.");
        }

        if (normalized.CandidateIsRawInputOnly)
        {
            issues.Add("Selected direct-output candidate is Raw Input metadata only; choose a HID device-interface candidate.");
        }

        if (!normalized.CandidateHasOpenableHidPath)
        {
            issues.Add("Selected direct-output candidate does not have an openable HID device-interface path.");
        }

        if (!normalized.OpenCheckSucceeded)
        {
            issues.Add("Selected direct-output candidate has not passed HID open-check.");
        }

        if (selector.ReportLength != SimHubF1EcRealReportEncoder.PayloadLengthBytes)
        {
            issues.Add($"Selected report length {selector.ReportLength:N0} bytes does not match the current {SimHubF1EcRealReportEncoder.PayloadLengthBytes:N0}-byte P-HPR hypothesis.");
        }

        if (!normalized.DirectControlEnabled)
        {
            issues.Add("Direct control is disabled.");
        }

        if (!normalized.DirectControlArmed)
        {
            issues.Add("Direct control is not armed.");
        }

        if (!normalized.DirectControlApprovalConfirmed)
        {
            issues.Add($"Exact approval phrase is not confirmed: {PHprControlledWriteApproval.Phrase}");
        }

        if (coexistenceStatus != PHprSoftwareConflictStatus.Clear)
        {
            issues.Add($"SimPro/SimHub coexistence is {coexistenceStatus}; Clear is required.");
        }

        if (emergencyStopActive)
        {
            issues.Add("Real P-HPR emergency stop is active.");
        }

        return new PHprDirectOutputDryRunResult(
            CanPulse: issues.Count == 0,
            selector,
            coexistenceStatus,
            emergencyStopActive,
            issues);
    }
}
