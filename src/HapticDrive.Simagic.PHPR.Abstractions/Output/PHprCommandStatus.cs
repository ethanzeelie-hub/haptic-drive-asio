namespace HapticDrive.Simagic.PHPR.Abstractions.Output;

public enum PHprCommandStatus
{
    Accepted = 0,
    RejectedEmergencyStop = 1,
    RejectedInvalidCommand = 2,
    RejectedSafetyLimit = 3
}
