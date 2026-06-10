namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprHidReportShapeValidationResult(
    bool Attempted,
    bool Succeeded,
    bool Failed,
    string Message,
    string? SanitizedErrorCategory)
{
    public static PHprHidReportShapeValidationResult NotAttempted { get; } = new(
        Attempted: false,
        Succeeded: false,
        Failed: false,
        "No P-HPR HID report-shape validation has been attempted.",
        SanitizedErrorCategory: null);

    public static PHprHidReportShapeValidationResult Success(string message)
    {
        return new PHprHidReportShapeValidationResult(
            Attempted: true,
            Succeeded: true,
            Failed: false,
            message,
            SanitizedErrorCategory: null);
    }

    public static PHprHidReportShapeValidationResult Failure(
        string message,
        string sanitizedErrorCategory)
    {
        return new PHprHidReportShapeValidationResult(
            Attempted: true,
            Succeeded: false,
            Failed: true,
            message,
            sanitizedErrorCategory);
    }
}

public static class PHprHidReportShapeValidator
{
    public static PHprHidReportShapeValidationResult Validate(
        PHprDirectOutputCandidate? candidate,
        PHprHidDeviceSelector selector)
    {
        if (candidate is null)
        {
            return PHprHidReportShapeValidationResult.NotAttempted;
        }

        var normalizedSelector = (selector ?? PHprHidDeviceSelector.None).Normalize();
        if (candidate.IsRawInputOnly)
        {
            return PHprHidReportShapeValidationResult.Failure(
                "Selected candidate is Raw Input metadata only; no output-report shape can be validated.",
                "RawInputOnly");
        }

        if (!candidate.HasOpenableHidPath)
        {
            return PHprHidReportShapeValidationResult.Failure(
                "Selected candidate does not expose an openable HID device-interface path for output-report validation.",
                "NoOpenableHidPath");
        }

        if (!normalizedSelector.IsSelected)
        {
            return PHprHidReportShapeValidationResult.Failure(
                "No selected HID device/interface/report is available for output-report validation.",
                "NotSelected");
        }

        if (candidate.OutputReportByteLength is not > 0)
        {
            var message = candidate.FeatureReportByteLength is > 0
                ? "Selected candidate exposes feature reports, but no HID output-report byte length is available for the current output writer."
                : "Selected candidate HID output-report byte length is unavailable; open-check alone is not enough to permit a real pulse.";
            return PHprHidReportShapeValidationResult.Failure(message, "OutputReportLengthUnavailable");
        }

        if (normalizedSelector.ReportLength != candidate.OutputReportByteLength.Value)
        {
            return PHprHidReportShapeValidationResult.Failure(
                $"Selected report length {normalizedSelector.ReportLength:N0} bytes does not match HID output-report capability {candidate.OutputReportByteLength.Value:N0} bytes.",
                "OutputReportLengthMismatch");
        }

        if (normalizedSelector.ReportLength != SimHubF1EcRealReportEncoder.PayloadLengthBytes)
        {
            return PHprHidReportShapeValidationResult.Failure(
                $"Selected output-report capability is {normalizedSelector.ReportLength:N0} bytes, but the current P-HPR writer only has a {SimHubF1EcRealReportEncoder.PayloadLengthBytes:N0}-byte SimHub F1 EC hypothesis.",
                "UnsupportedOutputReportLength");
        }

        if (candidate.OutputReportIds.Count > 0)
        {
            var selectedReportId = normalizedSelector.ReportId ?? (byte)0;
            if (!candidate.OutputReportIds.Contains(selectedReportId))
            {
                return PHprHidReportShapeValidationResult.Failure(
                    "Selected report ID is not advertised by the HID output-report capabilities.",
                    "OutputReportIdMismatch");
            }
        }

        return PHprHidReportShapeValidationResult.Success(
            "No-command HID report-shape validation succeeded from output-report capability metadata; no P-HPR report was sent.");
    }
}
