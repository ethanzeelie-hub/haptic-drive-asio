using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Asio.App;

internal sealed record PhprSafetyContextSnapshot(
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
    public PHprSafetyContext ToSafetyContext()
    {
        return new PHprSafetyContext(
            IsMockOutput,
            IsDeviceConnected,
            BrakeModuleAvailable,
            ThrottleModuleAvailable,
            TelemetryStale,
            HapticsStopped,
            EmergencyMuteActive,
            DrivingArmed,
            EmergencyStopActive,
            SoftwareConflictStatus,
            RequiresRealDeviceWrites);
    }
}

internal static class SafetyContextSnapshotBuilder
{
    public static PhprSafetyContextSnapshot BuildMockRuntimeSnapshot(
        PHprOutputSnapshot outputSnapshot,
        bool telemetryStale,
        bool hapticsStopped,
        bool emergencyMuteActive,
        bool drivingArmed,
        PHprSoftwareConflictStatus softwareConflictStatus)
    {
        ArgumentNullException.ThrowIfNull(outputSnapshot);

        return new PhprSafetyContextSnapshot(
            IsMockOutput: true,
            IsDeviceConnected: outputSnapshot.IsConnected,
            BrakeModuleAvailable: outputSnapshot.BrakeAvailable,
            ThrottleModuleAvailable: outputSnapshot.ThrottleAvailable,
            TelemetryStale: telemetryStale,
            HapticsStopped: hapticsStopped,
            EmergencyMuteActive: emergencyMuteActive,
            DrivingArmed: drivingArmed,
            EmergencyStopActive: outputSnapshot.IsEmergencyStopActive,
            SoftwareConflictStatus: softwareConflictStatus,
            RequiresRealDeviceWrites: false);
    }

    public static PhprSafetyContextSnapshot BuildRealRuntimeSnapshot(
        PHprOutputSnapshot outputSnapshot,
        bool telemetryStale,
        bool hapticsStopped,
        bool emergencyMuteActive,
        bool drivingArmed,
        PHprSoftwareConflictStatus softwareConflictStatus)
    {
        ArgumentNullException.ThrowIfNull(outputSnapshot);

        return new PhprSafetyContextSnapshot(
            IsMockOutput: false,
            IsDeviceConnected: outputSnapshot.IsConnected,
            BrakeModuleAvailable: outputSnapshot.BrakeAvailable,
            ThrottleModuleAvailable: outputSnapshot.ThrottleAvailable,
            TelemetryStale: telemetryStale,
            HapticsStopped: hapticsStopped,
            EmergencyMuteActive: emergencyMuteActive,
            DrivingArmed: drivingArmed,
            EmergencyStopActive: outputSnapshot.IsEmergencyStopActive,
            SoftwareConflictStatus: softwareConflictStatus,
            RequiresRealDeviceWrites: true);
    }

    public static PhprSafetyContextSnapshot BuildManualRealSnapshot(
        bool selectorIsSelected,
        bool emergencyMuteActive,
        bool emergencyStopActive,
        PHprSoftwareConflictStatus softwareConflictStatus)
    {
        return new PhprSafetyContextSnapshot(
            IsMockOutput: false,
            IsDeviceConnected: selectorIsSelected,
            BrakeModuleAvailable: selectorIsSelected,
            ThrottleModuleAvailable: selectorIsSelected,
            TelemetryStale: false,
            HapticsStopped: false,
            EmergencyMuteActive: emergencyMuteActive,
            DrivingArmed: true,
            EmergencyStopActive: emergencyStopActive,
            SoftwareConflictStatus: softwareConflictStatus,
            RequiresRealDeviceWrites: true);
    }

    public static PhprSafetyContextSnapshot BuildBenchMockSnapshot(
        PHprOutputSnapshot outputSnapshot,
        bool emergencyMuteActive)
    {
        ArgumentNullException.ThrowIfNull(outputSnapshot);

        return new PhprSafetyContextSnapshot(
            IsMockOutput: true,
            IsDeviceConnected: outputSnapshot.IsConnected,
            BrakeModuleAvailable: outputSnapshot.BrakeAvailable,
            ThrottleModuleAvailable: outputSnapshot.ThrottleAvailable,
            TelemetryStale: false,
            HapticsStopped: false,
            EmergencyMuteActive: emergencyMuteActive,
            DrivingArmed: true,
            EmergencyStopActive: outputSnapshot.IsEmergencyStopActive,
            SoftwareConflictStatus: PHprSoftwareConflictStatus.Clear,
            RequiresRealDeviceWrites: false);
    }

    public static PhprSafetyContextSnapshot BuildBenchDirectSnapshot(
        PHprOutputSnapshot outputSnapshot,
        bool emergencyMuteActive,
        PHprSoftwareConflictStatus softwareConflictStatus)
    {
        ArgumentNullException.ThrowIfNull(outputSnapshot);

        return new PhprSafetyContextSnapshot(
            IsMockOutput: false,
            IsDeviceConnected: outputSnapshot.IsConnected,
            BrakeModuleAvailable: outputSnapshot.BrakeAvailable,
            ThrottleModuleAvailable: outputSnapshot.ThrottleAvailable,
            TelemetryStale: false,
            HapticsStopped: false,
            EmergencyMuteActive: emergencyMuteActive,
            DrivingArmed: true,
            EmergencyStopActive: outputSnapshot.IsEmergencyStopActive,
            SoftwareConflictStatus: softwareConflictStatus,
            RequiresRealDeviceWrites: true);
    }
}
