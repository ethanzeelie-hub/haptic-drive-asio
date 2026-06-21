using System.Net;
using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.App;

internal sealed record GameTelemetryOption(
    string GameId,
    string DisplayName);

internal static class GameTelemetryCatalog
{
    public const string F125GameId = "f1-25";
    private static readonly AppGameIntegrationRegistry RegistryValue = new();

    public static string DefaultGameId => F125GameId;

    public static IReadOnlyList<GameTelemetryOption> Options => RegistryValue.All
        .Select(descriptor => new GameTelemetryOption(descriptor.Id.Value, descriptor.DisplayName))
        .ToArray();

    public static IGameIntegrationRegistry Registry => RegistryValue;

    public static string NormalizeGameId(string? gameId)
    {
        foreach (var option in Options)
        {
            if (string.Equals(option.GameId, gameId, StringComparison.OrdinalIgnoreCase))
            {
                return option.GameId;
            }
        }

        return DefaultGameId;
    }

    public static string GetDisplayName(string? gameId)
    {
        var normalized = NormalizeGameId(gameId);
        foreach (var option in Options)
        {
            if (string.Equals(option.GameId, normalized, StringComparison.Ordinal))
            {
                return option.DisplayName;
            }
        }

        return "F1 25";
    }

    public static IGameTelemetryAdapter CreateAdapter(string? gameId)
    {
        return RegistryValue.GetRequired(new GameIntegrationId(NormalizeGameId(gameId))).CreateAdapter();
    }

    public static IVehicleStateNormalizer CreateNormalizer(string? gameId)
    {
        return RegistryValue.CreateRequiredNormalizer(new GameIntegrationId(NormalizeGameId(gameId)));
    }

    private interface IAppGameIntegrationRegistry : IGameIntegrationRegistry
    {
        IVehicleStateNormalizer CreateRequiredNormalizer(GameIntegrationId id);
    }

    private sealed class AppGameIntegrationRegistry : IAppGameIntegrationRegistry
    {
        private static readonly GameIntegrationDescriptor F125Descriptor = new(
            new GameIntegrationId(F125GameId),
            "F1 25",
            "F1 25 UDP",
            "v3",
            new GameTelemetryEndpointDefaults(20778, IPAddress.Loopback, AllowLanTelemetry: false),
            new GameCapabilities(
                ProvidesMotion: true,
                ProvidesSession: true,
                ProvidesLap: true,
                ProvidesParticipants: true,
                ProvidesCarTelemetry: true,
                ProvidesCarStatus: true,
                ProvidesDamage: true,
                ProvidesEvents: true),
            F125GameTelemetryAdapter.PacketDescriptors,
            static () => new F125GameTelemetryAdapter());

        private static readonly IReadOnlyList<GameIntegrationDescriptor> AllDescriptors = [F125Descriptor];

        public IReadOnlyList<GameIntegrationDescriptor> All => AllDescriptors;

        public GameIntegrationDescriptor Default => F125Descriptor;

        public GameIntegrationDescriptor GetRequired(GameIntegrationId id)
        {
            ArgumentNullException.ThrowIfNull(id);
            return AllDescriptors.FirstOrDefault(descriptor => string.Equals(descriptor.Id.Value, id.Value, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"No game integration is registered for '{id.Value}'.");
        }

        public IVehicleStateNormalizer CreateRequiredNormalizer(GameIntegrationId id)
        {
            ArgumentNullException.ThrowIfNull(id);
            if (string.Equals(id.Value, F125GameId, StringComparison.Ordinal))
            {
                return new F125VehicleStateNormalizer();
            }

            throw new InvalidOperationException($"No vehicle-state normalizer is registered for '{id.Value}'.");
        }
    }
}
