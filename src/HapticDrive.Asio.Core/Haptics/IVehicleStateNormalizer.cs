using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Core.Vehicle.Freshness;

namespace HapticDrive.Asio.Core.Haptics;

public interface IVehicleStateNormalizer
{
    GameIntegrationId GameId { get; }

    HapticFrame Normalize(
        VehicleState state,
        DateTimeOffset nowUtc,
        long nowTimestamp,
        TimeProvider timeProvider,
        TelemetryFreshnessPolicy freshnessPolicy);
}
