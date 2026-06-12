namespace HapticDrive.Actuation.PHpr;

public enum PHprRoadVibrationRoutingStatus
{
    IgnoredDisabled = 0,
    IgnoredMissingVehicleState = 1,
    IgnoredNoActiveRoadVibration = 2,
    IgnoredNoEnabledPedal = 3,
    IgnoredMinimumInterval = 4,
    Routed = 5,
    RejectedBySafety = 6,
    Failed = 7,
    IgnoredGearDucking = 8
}
