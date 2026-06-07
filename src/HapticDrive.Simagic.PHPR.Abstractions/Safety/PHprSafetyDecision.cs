using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public enum PHprSafetyDecisionKind
{
    Accepted = 0,
    AcceptedWithClamp = 1,
    Rejected = 2,
    EmergencyStopped = 3,
    IgnoredSafeStop = 4,
    Failed = 5
}

public enum PHprSafetyViolationCode
{
    None = 0,
    StrengthExceeded = 1,
    DurationExceeded = 2,
    FrequencyTooLow = 3,
    FrequencyTooHigh = 4,
    CommandRateExceeded = 5,
    ContinuousDurationExceeded = 6,
    ModuleUnavailable = 7,
    DeviceDisconnected = 8,
    EmergencyStopActive = 9,
    TelemetryStale = 10,
    HapticsStopped = 11,
    EmergencyMuteActive = 12,
    DrivingNotArmed = 13,
    SimProConflict = 14,
    RealWritesNotAllowed = 15,
    InvalidCommand = 16,
    Unknown = 17
}

public sealed record PHprSafetyViolation(
    PHprSafetyViolationCode Code,
    string Message)
{
    public static PHprSafetyViolation None { get; } = new(PHprSafetyViolationCode.None, "No safety violation.");
}

public sealed record PHprSafetyClampDetails(
    bool StrengthClamped,
    bool DurationClamped,
    bool FrequencyClamped,
    double OriginalStrength01,
    double EffectiveStrength01,
    int OriginalDurationMs,
    int EffectiveDurationMs,
    double OriginalFrequencyHz,
    double EffectiveFrequencyHz)
{
    public bool HasClamp => StrengthClamped || DurationClamped || FrequencyClamped;
}

public sealed record PHprSafetyDecision(
    PHprSafetyDecisionKind Kind,
    PHprSafetyViolation Violation,
    IReadOnlyList<PHprSafetyViolation> Violations,
    PHprCommand? OriginalCommand,
    PHprCommand? Command,
    PHprSafetyClampDetails? ClampDetails,
    DateTimeOffset EvaluatedAtUtc,
    string Message)
{
    public bool Accepted => Kind is PHprSafetyDecisionKind.Accepted or PHprSafetyDecisionKind.AcceptedWithClamp;

    public static PHprSafetyDecision AcceptedCommand(
        PHprCommand originalCommand,
        PHprCommand effectiveCommand,
        PHprSafetyClampDetails? clampDetails,
        DateTimeOffset evaluatedAtUtc)
    {
        var kind = clampDetails?.HasClamp == true
            ? PHprSafetyDecisionKind.AcceptedWithClamp
            : PHprSafetyDecisionKind.Accepted;
        var violations = CreateClampViolations(clampDetails);
        var message = kind == PHprSafetyDecisionKind.AcceptedWithClamp
            ? "P-HPR command accepted with conservative safety clamps; no hardware write was performed."
            : "P-HPR command accepted by safety layer; no hardware write was performed.";

        return new PHprSafetyDecision(
            kind,
            violations.FirstOrDefault() ?? PHprSafetyViolation.None,
            violations,
            originalCommand,
            effectiveCommand,
            clampDetails,
            evaluatedAtUtc,
            message);
    }

    public static PHprSafetyDecision RejectedCommand(
        PHprCommand? originalCommand,
        PHprSafetyViolation violation,
        DateTimeOffset evaluatedAtUtc)
    {
        return new PHprSafetyDecision(
            PHprSafetyDecisionKind.Rejected,
            violation,
            [violation],
            originalCommand,
            originalCommand is null
                ? null
                : originalCommand with { SafetyFlags = originalCommand.SafetyFlags | PHprSafetyFlags.Rejected },
            null,
            evaluatedAtUtc,
            violation.Message);
    }

    public static PHprSafetyDecision EmergencyStopped(DateTimeOffset evaluatedAtUtc)
    {
        var violation = new PHprSafetyViolation(
            PHprSafetyViolationCode.None,
            "P-HPR safety emergency stop recorded; no hardware write was performed.");
        return new PHprSafetyDecision(
            PHprSafetyDecisionKind.EmergencyStopped,
            violation,
            [],
            null,
            null,
            null,
            evaluatedAtUtc,
            violation.Message);
    }

    private static IReadOnlyList<PHprSafetyViolation> CreateClampViolations(PHprSafetyClampDetails? clampDetails)
    {
        if (clampDetails?.HasClamp != true)
        {
            return [];
        }

        var violations = new List<PHprSafetyViolation>();
        if (clampDetails.StrengthClamped)
        {
            violations.Add(new PHprSafetyViolation(
                PHprSafetyViolationCode.StrengthExceeded,
                $"Strength clamped from {clampDetails.OriginalStrength01:0.###} to {clampDetails.EffectiveStrength01:0.###}."));
        }

        if (clampDetails.DurationClamped)
        {
            violations.Add(new PHprSafetyViolation(
                PHprSafetyViolationCode.DurationExceeded,
                $"Duration clamped from {clampDetails.OriginalDurationMs} ms to {clampDetails.EffectiveDurationMs} ms."));
        }

        if (clampDetails.FrequencyClamped)
        {
            var code = clampDetails.OriginalFrequencyHz < clampDetails.EffectiveFrequencyHz
                ? PHprSafetyViolationCode.FrequencyTooLow
                : PHprSafetyViolationCode.FrequencyTooHigh;
            violations.Add(new PHprSafetyViolation(
                code,
                $"Frequency clamped from {clampDetails.OriginalFrequencyHz:0.###} Hz to {clampDetails.EffectiveFrequencyHz:0.###} Hz."));
        }

        return violations;
    }
}
