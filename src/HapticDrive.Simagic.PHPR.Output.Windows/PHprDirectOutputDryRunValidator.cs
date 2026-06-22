using HapticDrive.Asio.Core.Safety;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprDirectOutputDryRunResult(
    bool CanPulse,
    PHprHidDeviceSelector Selector,
    bool OutputReportCapabilityKnown,
    bool FeatureReportCapabilityKnown,
    bool ReportShapeValidationSucceeded,
    string? ReportShapeValidationMessage,
    string? ExpectedFirstBytes,
    PHprSoftwareConflictStatus CoexistenceStatus,
    bool InterlockAllowsOutput,
    bool EmergencyStopActive,
    PHprWriteAuthorizationSnapshot Authorization,
    IReadOnlyList<string> Issues)
{
    public string Summary =>
        $"Direct-output dry run: selected {Selector.IsSelected}; transport {Selector.Transport}; report ID {FormatReportId(Selector.ReportId)}; report length {Selector.ReportLength:N0} bytes; output report known {OutputReportCapabilityKnown}; feature report known {FeatureReportCapabilityKnown}; expected first bytes {ExpectedFirstBytes ?? "unavailable"}; report-shape validated {ReportShapeValidationSucceeded}; can pulse {CanPulse}; coexistence {CoexistenceStatus}; interlock {InterlockAllowsOutput}; emergency stop {EmergencyStopActive}; authorized {Authorization.IsAuthorized}; issues {Issues.Count:N0}.";

    private static string FormatReportId(byte? reportId)
    {
        return reportId is null ? "none" : $"0x{reportId.Value:X2}";
    }
}

public static class PHprDirectOutputDryRunValidator
{
    public static PHprDirectOutputDryRunResult Validate(
        PHprRealOutputOptions options,
        PHprSoftwareConflictStatus coexistenceStatus,
        OutputInterlockSnapshot interlockSnapshot,
        bool emergencyStopActive,
        PHprWriteAuthorizationSnapshot authorization)
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

        if (!normalized.AllowsDirectPulseReportShape)
        {
            issues.Add(normalized.ReportShapeValidationFailed
                ? $"Selected HID report shape is not validated for real pulses: {normalized.ReportShapeValidationMessage ?? "validation failed"}"
                : "Selected HID report transport/capability/shape is not validated; open-check alone cannot permit a real pulse.");
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

        if (!interlockSnapshot.AllowsOutput)
        {
            issues.Add("Global output interlock is latched.");
        }

        if (coexistenceStatus != PHprSoftwareConflictStatus.Clear)
        {
            issues.Add($"SimPro/SimHub coexistence is {coexistenceStatus}; Clear is required.");
        }

        if (emergencyStopActive)
        {
            issues.Add("Real P-HPR emergency stop is active.");
        }

        if (!authorization.IsAuthorized)
        {
            issues.Add($"Session authorization is required before non-stop controlled writes: {authorization.Reason}.");
        }

        return new PHprDirectOutputDryRunResult(
            CanPulse: issues.Count == 0,
            selector,
            normalized.CandidateOutputReportCapabilityKnown,
            normalized.CandidateFeatureReportCapabilityKnown,
            normalized.ReportShapeValidationSucceeded,
            normalized.ReportShapeValidationMessage,
            selector.IsSelected ? PHprHidReportShapeValidationResult.ExpectedF1EcStartFirstBytes : null,
            coexistenceStatus,
            interlockSnapshot.AllowsOutput,
            emergencyStopActive,
            authorization,
            issues);
    }
}
