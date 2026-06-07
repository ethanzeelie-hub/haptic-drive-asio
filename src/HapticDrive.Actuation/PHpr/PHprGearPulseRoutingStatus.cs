namespace HapticDrive.Actuation.PHpr;

public enum PHprGearPulseRoutingStatus
{
    Routed = 0,
    IgnoredDisabled = 1,
    IgnoredMissingShiftIntent = 2,
    IgnoredDrivingNotArmed = 3,
    IgnoredUnknownDirection = 4,
    RejectedBySafety = 5,
    Failed = 6,
    EmergencyStopped = 7
}
