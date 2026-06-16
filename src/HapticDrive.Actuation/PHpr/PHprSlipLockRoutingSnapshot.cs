using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprSlipLockTelemetrySnapshot(
    double SpeedKph,
    double Throttle01,
    double Brake01,
    double MaximumSlipRatio,
    double MaximumSlipAngle,
    double MinimumWheelSpeedMetersPerSecond,
    double MinimumWheelSpeedRatio,
    bool TelemetryFresh,
    bool MotionExFresh,
    bool TractionControlActive,
    bool AntiLockBrakesActive);

public sealed record PHprSlipLockEffectRoutingDiagnostics(
    PHprPedalEffectKind Kind,
    PHprSlipLockEffectSettings Settings,
    bool LastActive,
    string LastReason,
    double LastIntensity01,
    double LastComputedStrength01,
    double LastComputedFrequencyHz,
    int LastCommandDurationMs,
    bool LastBelowTactileThreshold,
    PHprSlipLockTelemetrySnapshot? LastTelemetry,
    PHprGearPulseTarget? LastTargetModule,
    long RouteCount,
    long SafetyRejectedCount,
    long IntervalSuppressedCount,
    long StaleTelemetrySuppressedCount,
    long CommandRateSuppressedCount,
    long StopCommandCount,
    PHprCommand? LastCommand,
    PHprCommandResult? LastOutputResult,
    DateTimeOffset? LastRouteAtUtc,
    DateTimeOffset? LastStartAtUtc,
    DateTimeOffset? LastUpdateAtUtc,
    DateTimeOffset? LastStopAtUtc);

public sealed record PHprSlipLockRoutingSnapshot(
    PHprSlipLockRouterOptions Options,
    long RouteAttemptCount,
    long EvaluationCount,
    long IgnoredEvaluationCount,
    long RouteCount,
    long SafetyRejectedCount,
    long IntervalSuppressedCount,
    long StaleTelemetrySuppressedCount,
    long CommandRateSuppressedCount,
    long StopCommandCount,
    PHprSlipLockEffectRoutingDiagnostics WheelSlip,
    PHprSlipLockEffectRoutingDiagnostics WheelLock,
    PHprPedalEffectKind? LastActiveEffect,
    PHprGearPulseTarget? LastTargetModule,
    PHprCommand? LastCommand,
    PHprCommandResult? LastOutputResult,
    PHprSlipLockRoutingResult? LastResult,
    PHprOutputSnapshot? OutputSnapshot,
    DateTimeOffset? FirstRouteAttemptAtUtc,
    DateTimeOffset? LastRouteAttemptAtUtc,
    DateTimeOffset? LastCommandRoutedAtUtc,
    string RuntimeState,
    string ActiveSlipLockModules,
    DateTimeOffset? LastSlipLockStartAtUtc,
    DateTimeOffset? LastSlipLockUpdateAtUtc,
    DateTimeOffset? LastSlipLockStopAtUtc,
    string LastSlipLockStopReason,
    long GearProtectionSuppressedCount,
    long WatchdogStopCount,
    string? LastIgnoredReason,
    string? LastError)
{
    public IReadOnlyList<PHprSlipLockEffectRoutingDiagnostics> Effects => [WheelSlip, WheelLock];
}
