using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Abstractions.Validation;

public sealed record PHprManualValidationChecklist(
    bool UserPhysicallyPresent,
    bool P700Connected,
    bool BrakeModuleInstalled,
    bool ThrottleModuleInstalled,
    bool DirectControlEnabled,
    bool DirectControlArmed,
    bool DeviceInterfaceReportSelected,
    bool SafetyLimitsVisible,
    bool EmergencyStopVisible,
    bool EmergencyStopClear,
    bool BrakeTestPulseAvailable,
    bool ThrottleTestPulseAvailable,
    bool GearPaddleTestPlanned,
    PHprSoftwareConflictStatus SoftwareConflictStatus)
{
    public static PHprManualValidationChecklist Default { get; } = new(
        UserPhysicallyPresent: false,
        P700Connected: false,
        BrakeModuleInstalled: false,
        ThrottleModuleInstalled: false,
        DirectControlEnabled: false,
        DirectControlArmed: false,
        DeviceInterfaceReportSelected: false,
        SafetyLimitsVisible: true,
        EmergencyStopVisible: true,
        EmergencyStopClear: true,
        BrakeTestPulseAvailable: false,
        ThrottleTestPulseAvailable: false,
        GearPaddleTestPlanned: false,
        SoftwareConflictStatus: PHprSoftwareConflictStatus.Unknown);
}
