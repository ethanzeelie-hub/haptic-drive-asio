namespace HapticDrive.Actuation.PHpr;

public enum PHprSlipLockRoutingStatus
{
    IgnoredDisabled = 0,
    IgnoredMissingVehicleState = 1,
    IgnoredNoActiveEffect = 2,
    IgnoredMinimumInterval = 3,
    Routed = 4,
    RejectedBySafety = 5,
    Failed = 6
}
