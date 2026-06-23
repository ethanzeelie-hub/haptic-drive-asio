using HapticDrive.Actuation.Driving;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Simagic.PHPR.Abstractions.Output;

namespace HapticDrive.Actuation.Tests;

public sealed class ActuationArchitectureGuardrailTests
{
    [Fact]
    public void ContinuousActuation_ConsumesCanonicalFrameAndDrivingContext()
    {
        var routeParameterTypes = typeof(PHprRoadVibrationRouter)
            .GetMethods()
            .Concat(typeof(PHprSlipLockRouter).GetMethods())
            .Where(method =>
                method.DeclaringType == typeof(PHprRoadVibrationRouter)
                || method.DeclaringType == typeof(PHprSlipLockRouter))
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        var runtimeInputPropertyTypes = typeof(PHprContinuousEffectsRuntimeInput)
            .GetProperties()
            .Select(property => property.PropertyType)
            .ToArray();

        Assert.Contains(typeof(HapticFrame), runtimeInputPropertyTypes);
        Assert.Contains(typeof(ActuationDrivingContext), runtimeInputPropertyTypes);
        Assert.DoesNotContain(typeof(VehicleState), routeParameterTypes);
        Assert.DoesNotContain(typeof(VehicleState), runtimeInputPropertyTypes);
    }

    [Fact]
    public async Task OutputSafetyParticipants_SilenceActuationWithoutAppOrAudioOwnership()
    {
        await using var output = new SafetyLimitedPhprOutputDevice(new MockPhprOutputDevice());
        var gearRouter = new PHprGearPulseRouter(output);
        var pedalRouter = new PHprPedalEffectsRouter(output);
        var mockParticipant = new MockPhprOutputSafetyParticipant(gearRouter, pedalRouter);

        await mockParticipant.SilenceAsync(TripSnapshot(), CancellationToken.None);

        Assert.True(mockParticipant.Current.IsSilent);
        Assert.True(gearRouter.GetSnapshot().EmergencyStopActive);
        Assert.True(pedalRouter.GetSnapshot().EmergencyStopActive);
    }

    [Fact]
    public void GearPulseRouting_RemainsSeparatedFromAudioAndVehicleState()
    {
        var constructorParameterNames = typeof(PHprGearPulseRouter)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain("IAudioOutputDevice", constructorParameterNames);
        Assert.DoesNotContain("VehicleState", constructorParameterNames);
        Assert.DoesNotContain("HapticEffectEngine", constructorParameterNames);
    }

    private static OutputInterlockSnapshot TripSnapshot()
    {
        return new OutputInterlockSnapshot(
            IsLatched: true,
            Reason: OutputInterlockReason.UserEmergencyMute,
            Message: "test trip",
            ChangedAtUtc: DateTimeOffset.UtcNow,
            Generation: 1);
    }
}
