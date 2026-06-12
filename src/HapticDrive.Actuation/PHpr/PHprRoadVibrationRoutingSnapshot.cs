using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprRoadVibrationRoutingSnapshot(
    PHprRoadVibrationRouterOptions Options,
    long EvaluationCount,
    long IgnoredEvaluationCount,
    long RouteCount,
    long SafetyRejectedCount,
    long IntervalSuppressedCount,
    bool LastActive,
    double LastIntensity01,
    RoadTextureSignal LastSignal,
    PHprCommand? LastCommand,
    PHprCommandResult? LastOutputResult,
    PHprRoadVibrationRoutingResult? LastResult,
    PHprOutputSnapshot? OutputSnapshot,
    string? LastError);
