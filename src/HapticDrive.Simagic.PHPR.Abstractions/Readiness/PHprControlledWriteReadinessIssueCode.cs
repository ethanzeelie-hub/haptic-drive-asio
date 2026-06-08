namespace HapticDrive.Simagic.PHPR.Abstractions.Readiness;

public enum PHprControlledWriteReadinessIssueCode
{
    StageIsNoWrite = 0,
    DirectControlDisabled = 1,
    DirectControlNotArmed = 2,
    UserNotPresent = 3,
    SimProNotClosed = 4,
    SimHubNotClosed = 5,
    SoftwareConflictNotClear = 6,
    P700NotConfirmed = 7,
    PHprModulesNotConfirmed = 8,
    EmergencyStopNotVisible = 9,
    BrakeModuleUnknown = 10,
    ThrottleModuleUnknown = 11,
    DeviceInterfaceReportNotSelected = 12,
    RealWritesNotDefaultOff = 13
}
