namespace HapticDrive.Simagic.PHPR.Abstractions.Validation;

public enum PHprManualValidationIssueCode
{
    UserNotPresent = 0,
    P700NotConnected = 1,
    BrakeModuleNotInstalled = 2,
    ThrottleModuleNotInstalled = 3,
    DirectControlDisabled = 4,
    DirectControlNotArmed = 5,
    DeviceInterfaceReportNotSelected = 6,
    SafetyLimitsNotVisible = 7,
    EmergencyStopNotVisible = 8,
    EmergencyStopLatched = 9,
    SoftwareConflictNotClear = 10,
    BrakePulseUnavailable = 11,
    ThrottlePulseUnavailable = 12,
    GearPaddleTestNotPlanned = 13,
    MissingRequiredResultField = 14,
    RequiredHardwareFlagNotConfirmed = 15
}
