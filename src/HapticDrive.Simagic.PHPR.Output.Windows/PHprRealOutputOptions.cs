using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprRealOutputOptions
{
    public static PHprRealOutputOptions Disabled { get; } = new();

    public const int MinWriteTimeoutMs = 25;

    public const int MaxWriteTimeoutMs = 2_000;

    public const int DefaultWriteTimeoutMs = 250;

    public const int DefaultStalePulseDropThresholdMs = 80;

    public bool DirectControlEnabled { get; init; }

    public bool DirectControlArmed { get; init; }

    public bool DirectControlApprovalConfirmed { get; init; }

    public PHprDirectOutputCandidateSourceMethod CandidateSourceMethod { get; init; } = PHprDirectOutputCandidateSourceMethod.Unknown;

    public bool CandidateIsRawInputOnly { get; init; }

    public bool CandidateHasOpenableHidPath { get; init; }

    public bool CandidateOutputReportCapabilityKnown { get; init; }

    public bool CandidateFeatureReportCapabilityKnown { get; init; }

    public bool ReportShapeValidationAttempted { get; init; }

    public bool ReportShapeValidationSucceeded { get; init; }

    public bool ReportShapeValidationFailed { get; init; }

    public string? ReportShapeValidationMessage { get; init; }

    public bool OpenCheckAttempted { get; init; }

    public bool OpenCheckSucceeded { get; init; }

    public bool OpenCheckFailed { get; init; }

    public string? OpenCheckSanitizedErrorCategory { get; init; }

    public int WriteTimeoutMs { get; init; } = DefaultWriteTimeoutMs;

    public int StalePulseDropThresholdMs { get; init; } = DefaultStalePulseDropThresholdMs;

    public PHprGearPulseRetriggerMode GearPulseRetriggerMode { get; init; } = PHprGearPulseRetriggerMode.Conservative;

    public PHprHidDeviceSelector Selector { get; init; } = PHprHidDeviceSelector.None;

    public PHprRealGearPulseSettings BrakeGearPulse { get; init; } = PHprRealGearPulseSettings.Default;

    public PHprRealGearPulseSettings ThrottleGearPulse { get; init; } = PHprRealGearPulseSettings.Default;

    public bool SelectedTransportCapabilityKnown => Selector.Transport switch
    {
        PHprHidReportTransport.FeatureReport => CandidateFeatureReportCapabilityKnown,
        _ => CandidateOutputReportCapabilityKnown
    };

    public bool AllowsDirectPulseReportShape =>
        !ReportShapeValidationFailed
        && ReportShapeValidationSucceeded
        && SelectedTransportCapabilityKnown;

    public PHprRealOutputOptions Normalize(PHprSafetyLimits? limits = null)
    {
        var safetyLimits = limits ?? PHprSafetyLimits.Default;
        return this with
        {
            WriteTimeoutMs = Math.Clamp(WriteTimeoutMs, MinWriteTimeoutMs, MaxWriteTimeoutMs),
            StalePulseDropThresholdMs = Math.Clamp(StalePulseDropThresholdMs, 0, 1_000),
            GearPulseRetriggerMode = Enum.IsDefined(GearPulseRetriggerMode)
                ? GearPulseRetriggerMode
                : PHprGearPulseRetriggerMode.Conservative,
            Selector = (Selector ?? PHprHidDeviceSelector.None).Normalize(),
            BrakeGearPulse = (BrakeGearPulse ?? PHprRealGearPulseSettings.Default).Normalize(safetyLimits),
            ThrottleGearPulse = (ThrottleGearPulse ?? PHprRealGearPulseSettings.Default).Normalize(safetyLimits)
        };
    }
}
