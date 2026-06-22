using HapticDrive.Asio.Core.Safety;

namespace HapticDrive.Asio.Runtime.Safety;

public sealed record OutputInterlockSupervisorSnapshot(
    OutputInterlockSnapshot Interlock,
    IReadOnlyList<OutputSafetyParticipantSnapshot> Participants,
    long ProcessedSnapshotCount,
    long ParticipantFailureCount,
    string? LastFailure,
    DateTimeOffset? LastProcessedAtUtc);
