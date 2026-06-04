using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Abstractions.Output;

public sealed record PHprOutputSnapshot(
    bool IsMock,
    bool IsConnected,
    bool IsEmergencyStopActive,
    long AcceptedCommandCount,
    long RejectedCommandCount,
    PHprCommand? LastCommand,
    PHprCommandStatus? LastStatus,
    string? LastMessage,
    DateTimeOffset? LastCommandUtc,
    PHprSafetyLimits SafetyLimits);
