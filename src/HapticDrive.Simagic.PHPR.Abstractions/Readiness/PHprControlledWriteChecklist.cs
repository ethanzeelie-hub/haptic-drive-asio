using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Abstractions.Readiness;

public sealed record PHprControlledWriteChecklist(
    bool UserPhysicallyPresent,
    bool SimProClosed,
    bool SimHubClosed,
    bool P700Connected,
    bool PHprModulesInstalled,
    bool HapticDriveRunning,
    bool EmergencyStopVisible,
    bool BrakeModuleKnown,
    bool ThrottleModuleKnown,
    bool DeviceInterfaceReportSelected,
    bool RealWritesDefaultOff,
    bool DirectControlModeEnabled,
    bool DirectControlArmed,
    PHprSoftwareConflictStatus SoftwareConflictStatus)
{
    public static PHprControlledWriteChecklist Stage2PNoWriteDefault { get; } = new(
        UserPhysicallyPresent: false,
        SimProClosed: false,
        SimHubClosed: false,
        P700Connected: false,
        PHprModulesInstalled: false,
        HapticDriveRunning: true,
        EmergencyStopVisible: true,
        BrakeModuleKnown: false,
        ThrottleModuleKnown: false,
        DeviceInterfaceReportSelected: false,
        RealWritesDefaultOff: true,
        DirectControlModeEnabled: false,
        DirectControlArmed: false,
        SoftwareConflictStatus: PHprSoftwareConflictStatus.Unknown);
}
