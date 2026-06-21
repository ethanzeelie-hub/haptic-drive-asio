namespace HapticDrive.Asio.Core.Games;

public sealed record GameCapabilities(
    bool ProvidesMotion,
    bool ProvidesSession,
    bool ProvidesLap,
    bool ProvidesParticipants,
    bool ProvidesCarTelemetry,
    bool ProvidesCarStatus,
    bool ProvidesDamage,
    bool ProvidesEvents);
