namespace HapticDrive.Actuation.Driving;

public enum DrivingArmedSuppressionReason
{
    None = 0,
    NoTelemetry = 1,
    StaleTelemetry = 2,
    Paused = 3,
    NetworkPaused = 4,
    GarageMenuOrResultState = 5,
    InvalidVehicleState = 6,
    NotMovingAndNotActive = 7,
    EmergencyMute = 8,
    HapticsStopped = 9
}
