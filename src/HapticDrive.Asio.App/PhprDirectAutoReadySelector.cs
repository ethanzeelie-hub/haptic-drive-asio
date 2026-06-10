using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal sealed record PhprDirectAutoReadySelection(
    PHprDirectOutputCandidate? Candidate,
    PHprHidDeviceSelector Selector,
    PHprRealOutputOptions Options,
    string Message)
{
    public bool HasPreferredCandidate => Candidate is not null;
}

internal static class PhprDirectAutoReadySelector
{
    public static PhprDirectAutoReadySelection Select(
        IEnumerable<PHprDirectOutputCandidate> candidates,
        PHprRealOutputOptions currentOptions,
        bool enableWhenPreferredPresent)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var current = (currentOptions ?? PHprRealOutputOptions.Disabled)
            .Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        var preferred = candidates
            .Where(IsPreferredKnownGoodCandidate)
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.SafeDisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (preferred is null)
        {
            var cleared = (current with
            {
                DirectControlEnabled = false,
                DirectControlArmed = false,
                DirectControlApprovalConfirmed = false,
                Selector = PHprHidDeviceSelector.None,
                CandidateSourceMethod = PHprDirectOutputCandidateSourceMethod.Unknown,
                CandidateIsRawInputOnly = false,
                CandidateHasOpenableHidPath = false,
                CandidateOutputReportCapabilityKnown = false,
                CandidateFeatureReportCapabilityKnown = false,
                ReportShapeValidationAttempted = false,
                ReportShapeValidationSucceeded = false,
                ReportShapeValidationFailed = false,
                ReportShapeValidationMessage = null,
                OpenCheckAttempted = false,
                OpenCheckSucceeded = false,
                OpenCheckFailed = false,
                OpenCheckSanitizedErrorCategory = null
            }).Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);

            return new PhprDirectAutoReadySelection(
                null,
                PHprHidDeviceSelector.None,
                cleared,
                "Preferred P-HPR direct candidate was not found.");
        }

        var selector = preferred.ToSelector(
            PHprDirectOutputCandidate.F1EcFeatureReportId,
            PHprHidReportTransport.FeatureReport);
        var reportShape = PHprHidReportShapeValidator.Validate(preferred, selector);
        var options = (current with
        {
            DirectControlEnabled = enableWhenPreferredPresent,
            DirectControlArmed = enableWhenPreferredPresent,
            DirectControlApprovalConfirmed = enableWhenPreferredPresent,
            Selector = selector,
            CandidateSourceMethod = preferred.SourceMethod,
            CandidateIsRawInputOnly = preferred.IsRawInputOnly,
            CandidateHasOpenableHidPath = preferred.HasOpenableHidPath,
            CandidateOutputReportCapabilityKnown = preferred.HasKnownOutputReportCapability,
            CandidateFeatureReportCapabilityKnown = preferred.HasKnownFeatureReportCapability,
            ReportShapeValidationAttempted = reportShape.Attempted,
            ReportShapeValidationSucceeded = reportShape.Succeeded,
            ReportShapeValidationFailed = reportShape.Failed,
            ReportShapeValidationMessage = reportShape.Message,
            OpenCheckAttempted = false,
            OpenCheckSucceeded = false,
            OpenCheckFailed = false,
            OpenCheckSanitizedErrorCategory = null
        }).Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);

        return new PhprDirectAutoReadySelection(
            preferred,
            selector,
            options,
            "Preferred P-HPR direct candidate selected for automatic no-output readiness checks.");
    }

    private static bool IsPreferredKnownGoodCandidate(PHprDirectOutputCandidate candidate)
    {
        return candidate.VendorId == 0x3670
            && candidate.ProductId == 0x0905
            && candidate.SourceMethod == PHprDirectOutputCandidateSourceMethod.HidDeviceInterface
            && !candidate.IsRawInputOnly
            && candidate.HasOpenableHidPath
            && candidate.FeatureReportByteLength == SimHubF1EcRealReportEncoder.PayloadLengthBytes
            && candidate.HasF1EcFeatureReportId
            && candidate.PreferredTransport == PHprHidReportTransport.FeatureReport;
    }
}
