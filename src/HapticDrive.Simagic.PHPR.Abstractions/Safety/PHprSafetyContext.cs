using HapticDrive.Simagic.PHPR.Abstractions.Output;

namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public enum PHprSoftwareConflictStatus
{
    Unknown = 0,
    Clear = 1,
    SimProRunning = 2,
    SimHubRunning = 3,
    ActiveConflict = 4
}

public sealed record PHprSafetyContext(
    bool IsMockOutput,
    bool IsDeviceConnected,
    bool BrakeModuleAvailable,
    bool ThrottleModuleAvailable,
    bool TelemetryStale,
    bool HapticsStopped,
    bool EmergencyMuteActive,
    bool DrivingArmed,
    bool EmergencyStopActive,
    PHprSoftwareConflictStatus SoftwareConflictStatus,
    bool RequiresRealDeviceWrites)
{
    public static PHprSafetyContext DefaultMock { get; } = new(
        IsMockOutput: true,
        IsDeviceConnected: true,
        BrakeModuleAvailable: true,
        ThrottleModuleAvailable: true,
        TelemetryStale: false,
        HapticsStopped: false,
        EmergencyMuteActive: false,
        DrivingArmed: true,
        EmergencyStopActive: false,
        SoftwareConflictStatus: PHprSoftwareConflictStatus.Clear,
        RequiresRealDeviceWrites: false);

    public static PHprSafetyContext FromOutputSnapshot(
        PHprOutputSnapshot snapshot,
        PHprSafetyContext? baseContext = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var context = baseContext ?? DefaultMock;
        return context with
        {
            IsMockOutput = snapshot.IsMock,
            IsDeviceConnected = context.IsDeviceConnected && snapshot.IsConnected,
            BrakeModuleAvailable = context.BrakeModuleAvailable && snapshot.BrakeAvailable,
            ThrottleModuleAvailable = context.ThrottleModuleAvailable && snapshot.ThrottleAvailable,
            EmergencyStopActive = context.EmergencyStopActive || snapshot.IsEmergencyStopActive
        };
    }
}
