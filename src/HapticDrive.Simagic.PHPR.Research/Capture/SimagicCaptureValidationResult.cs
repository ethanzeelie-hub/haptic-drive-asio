namespace HapticDrive.Simagic.PHPR.Research.Capture;

public sealed record SimagicCaptureValidationResult
{
    public IReadOnlyList<SimagicCaptureValidationMessage> Messages { get; init; } = [];

    public bool IsValid => Messages.All(message => message.Severity != SimagicCaptureValidationSeverity.Error);

    public int ErrorCount => Messages.Count(message => message.Severity == SimagicCaptureValidationSeverity.Error);

    public int WarningCount => Messages.Count(message => message.Severity == SimagicCaptureValidationSeverity.Warning);

    public static SimagicCaptureValidationResult FromMessages(IEnumerable<SimagicCaptureValidationMessage> messages)
    {
        return new SimagicCaptureValidationResult
        {
            Messages = messages.ToArray()
        };
    }
}
