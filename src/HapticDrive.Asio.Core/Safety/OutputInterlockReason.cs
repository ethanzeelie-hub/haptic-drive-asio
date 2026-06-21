namespace HapticDrive.Asio.Core.Safety;

public enum OutputInterlockReason
{
    StartupSafeDefault,
    UserEmergencyMute,
    TelemetryStale,
    DeviceFault,
    Shutdown,
    ConfigurationInvalid,
    ManualTestBlocked
}
