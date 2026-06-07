using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;
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
    PHprSafetyLimits SafetyLimits,
    string Mode = "MockOnly",
    bool BrakeAvailable = true,
    bool ThrottleAvailable = true,
    long GeneratedFrameCount = 0,
    PHprMockProtocolFrame? LastFrame = null,
    int PendingScheduledStopCount = 0,
    long EmergencyStopCount = 0);
