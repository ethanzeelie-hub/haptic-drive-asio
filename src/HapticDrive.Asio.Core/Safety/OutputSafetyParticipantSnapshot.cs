namespace HapticDrive.Asio.Core.Safety;

public sealed record OutputSafetyParticipantSnapshot(
    string Name,
    bool IsSilent,
    bool HasFault,
    string Message);
