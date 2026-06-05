namespace HapticDrive.Simagic.PHPR.Research.Capture;

public sealed record SimagicCaptureValidationMessage(
    SimagicCaptureValidationSeverity Severity,
    string Field,
    string Message);
