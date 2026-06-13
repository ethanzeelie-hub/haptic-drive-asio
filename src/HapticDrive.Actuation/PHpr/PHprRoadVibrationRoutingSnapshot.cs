using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprRoadVibrationRoutingSnapshot(
    PHprRoadVibrationRouterOptions Options,
    long RouteAttemptCount,
    long EvaluationCount,
    long IgnoredEvaluationCount,
    long RouteCount,
    long SafetyRejectedCount,
    long IntervalSuppressedCount,
    long StaleTelemetrySuppressedCount,
    long GearDuckingSuppressedCount,
    long CommandRateSuppressedCount,
    bool LastActive,
    double LastIntensity01,
    RoadTextureSignal LastSignal,
    PHprCommand? LastCommand,
    PHprCommandResult? LastOutputResult,
    PHprRoadVibrationRoutingResult? LastResult,
    PHprOutputSnapshot? OutputSnapshot,
    DateTimeOffset? FirstRouteAttemptAtUtc,
    DateTimeOffset? LastRouteAttemptAtUtc,
    DateTimeOffset? LastCommandRoutedAtUtc,
    string RuntimeState,
    string ActiveRoadModules,
    DateTimeOffset? LastRoadStartAtUtc,
    DateTimeOffset? LastRoadUpdateAtUtc,
    DateTimeOffset? LastRoadStopAtUtc,
    string LastRoadStopReason,
    long RoadStopCommandCount,
    long WatchdogStopCount,
    string? LastIgnoredReason,
    string? LastError);
