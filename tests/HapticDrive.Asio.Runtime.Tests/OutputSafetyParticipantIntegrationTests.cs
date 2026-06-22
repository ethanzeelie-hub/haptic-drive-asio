using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Runtime.Safety;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class OutputSafetyParticipantIntegrationTests
{
    [Fact]
    public async Task AudioOutputParticipant_SilencesRunningOutputOnTrip()
    {
        await using var coordinator = RuntimeTestPipelineFactory.Create();
        Assert.True((await coordinator.StartAsync()).Succeeded);
        var participant = new AudioOutputSafetyParticipant(coordinator);

        await participant.SilenceAsync(
            new OutputInterlockSnapshot(
                IsLatched: true,
                Reason: OutputInterlockReason.UserEmergencyMute,
                Message: "test trip",
                ChangedAtUtc: DateTimeOffset.UtcNow,
                Generation: 1),
            CancellationToken.None);

        Assert.True(participant.Current.IsSilent);
        Assert.False(coordinator.OutputDevice.GetStatus().IsStreaming);
    }

    [Fact]
    public async Task ManualTestParticipant_SilencesBenchOnTrip()
    {
        await using var bench = new AudioTestBench();
        Assert.True((await bench.StartAsync()).Succeeded);
        var participant = new ManualAudioTestBenchSafetyParticipant(bench);

        await participant.SilenceAsync(
            new OutputInterlockSnapshot(
                IsLatched: true,
                Reason: OutputInterlockReason.UserEmergencyMute,
                Message: "test trip",
                ChangedAtUtc: DateTimeOffset.UtcNow,
                Generation: 1),
            CancellationToken.None);

        Assert.True(participant.Current.IsSilent);
        Assert.True(bench.GetSnapshot().EmergencyMute);
        Assert.False(bench.GetSnapshot().IsActive);
    }
}
