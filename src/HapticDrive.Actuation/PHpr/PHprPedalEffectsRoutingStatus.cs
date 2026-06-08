namespace HapticDrive.Actuation.PHpr;

public enum PHprPedalEffectsRoutingStatus
{
    Routed = 0,
    IgnoredDisabled = 1,
    IgnoredMissingVehicleState = 2,
    IgnoredNoActiveEffect = 3,
    IgnoredMinimumInterval = 4,
    RejectedBySafety = 5,
    Failed = 6,
    EmergencyStopped = 7
}
