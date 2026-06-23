using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Tests;

internal static class EffectTestCompatibilityExtensions
{
    public static void Update(this EngineVibrationEffect effect, VehicleState vehicleState)
    {
        effect.Update(LegacyHapticEffectInputFactory.FromVehicleState(vehicleState));
    }

    public static void Update(this GearShiftEffect effect, VehicleState vehicleState)
    {
        effect.Update(LegacyHapticEffectInputFactory.FromVehicleState(vehicleState));
    }

    public static void Update(this KerbEffect effect, VehicleState vehicleState)
    {
        effect.Update(LegacyHapticEffectInputFactory.FromVehicleState(vehicleState));
    }

    public static void Update(this ImpactEffect effect, VehicleState vehicleState)
    {
        effect.Update(LegacyHapticEffectInputFactory.FromVehicleState(vehicleState));
    }

    public static void Update(this RoadTextureEffect effect, VehicleState vehicleState)
    {
        effect.Update(LegacyHapticEffectInputFactory.FromVehicleState(vehicleState));
    }

    public static void Update(this SlipEffect effect, VehicleState vehicleState)
    {
        effect.Update(LegacyHapticEffectInputFactory.FromVehicleState(vehicleState));
    }

    public static void Update(this HapticEffectEngine engine, VehicleState vehicleState)
    {
        engine.Update(LegacyHapticEffectInputFactory.FromVehicleState(vehicleState));
    }
}
