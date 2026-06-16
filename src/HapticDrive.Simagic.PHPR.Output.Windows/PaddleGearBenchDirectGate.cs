using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

internal static class PaddleGearBenchDirectGate
{
    public const byte RequiredReportId = 0xF1;

    public static bool TryGetReady(
        PHprRealOutputOptions options,
        PHprSoftwareConflictStatus coexistenceStatus,
        PHprOutputSnapshot outputSnapshot,
        bool roadVibrationEnabled,
        bool slipLockEnabled,
        out string message)
    {
        var normalized = (options ?? PHprRealOutputOptions.Disabled).Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        var selector = normalized.Selector.Normalize();

        if (!normalized.DirectControlEnabled)
        {
            message = "direct control is disabled";
            return false;
        }

        if (!selector.IsSelected)
        {
            message = "no P-HPR device/interface/report is selected";
            return false;
        }

        if (selector.Transport != PHprHidReportTransport.FeatureReport)
        {
            message = "bench direct gear pulse requires FeatureReport transport";
            return false;
        }

        if (selector.ReportId != RequiredReportId)
        {
            message = "bench direct gear pulse requires report ID 0xF1";
            return false;
        }

        if (selector.ReportLength != SimHubF1EcRealReportEncoder.PayloadLengthBytes)
        {
            message = $"bench direct gear pulse requires {SimHubF1EcRealReportEncoder.PayloadLengthBytes} byte reports";
            return false;
        }

        if (normalized.CandidateIsRawInputOnly || !normalized.CandidateHasOpenableHidPath)
        {
            message = "selected candidate is Raw Input metadata only or has no openable HID device-interface path";
            return false;
        }

        if (!normalized.OpenCheckSucceeded)
        {
            message = "selected candidate has not passed HID open-check";
            return false;
        }

        if (!normalized.AllowsDirectPulseReportShape)
        {
            message = normalized.ReportShapeValidationFailed
                ? $"selected candidate report shape is blocked: {normalized.ReportShapeValidationMessage ?? "validation failed"}"
                : "selected candidate report transport/capability/shape is unavailable";
            return false;
        }

        if (coexistenceStatus != PHprSoftwareConflictStatus.Clear)
        {
            message = $"software coexistence is {coexistenceStatus}";
            return false;
        }

        if (outputSnapshot.IsEmergencyStopActive)
        {
            message = "emergency stop is active";
            return false;
        }

        if (slipLockEnabled)
        {
            message = "real P-HPR slip/lock must be disabled for bench direct gear pulses";
            return false;
        }

        message = "direct bench gear pulse ready";
        return true;
    }
}
