using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Simagic.PHPR.Abstractions.Output;

namespace HapticDrive.Actuation.Tests;

public sealed class OutputSafetyParticipantTests
{
    [Fact]
    public async Task MockPhprParticipant_StopAllOnTrip()
    {
        await using var output = new SafetyLimitedPhprOutputDevice(new MockPhprOutputDevice());
        var gearRouter = new PHprGearPulseRouter(output);
        var pedalRouter = new PHprPedalEffectsRouter(output);
        var participant = new MockPhprOutputSafetyParticipant(gearRouter, pedalRouter);

        await participant.SilenceAsync(TripSnapshot(), CancellationToken.None);

        Assert.True(participant.Current.IsSilent);
        Assert.True(gearRouter.GetSnapshot().EmergencyStopActive);
        Assert.True(pedalRouter.GetSnapshot().EmergencyStopActive);
    }

    [Fact]
    public async Task ContinuousPhprParticipant_StopAllOnTrip()
    {
        await using var output = new MockPhprOutputDevice();
        var roadRouter = new PHprRoadVibrationRouter(output);
        var slipLockRouter = new PHprSlipLockRouter(output);
        await using var runtime = new PHprContinuousEffectsRuntimeCoordinator(
            roadRouter,
            slipLockRouter,
            () => throw new InvalidOperationException("runtime loop should not be required for participant silence test"));
        var participant = new ContinuousPhprOutputSafetyParticipant(runtime, roadRouter, slipLockRouter);

        await participant.SilenceAsync(TripSnapshot(), CancellationToken.None);

        Assert.True(participant.Current.IsSilent);
        Assert.Contains("Global output interlock latched", roadRouter.GetSnapshot().LastRoadStopReason, StringComparison.Ordinal);
        Assert.Contains("Global output interlock latched", slipLockRouter.GetSnapshot().LastSlipLockStopReason, StringComparison.Ordinal);
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
