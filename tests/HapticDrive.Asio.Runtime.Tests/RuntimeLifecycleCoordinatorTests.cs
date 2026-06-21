using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Runtime;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class RuntimeLifecycleCoordinatorTests
{
    [Fact]
    public async Task SerializesConcurrentOutputSwitches()
    {
        var coordinator = new RuntimeLifecycleCoordinator();
        var concurrent = 0;
        var maxConcurrent = 0;

        async Task RunSwitchAsync()
        {
            await coordinator.RunSerializedAsync(async (_, _) =>
            {
                var active = Interlocked.Increment(ref concurrent);
                maxConcurrent = Math.Max(maxConcurrent, active);
                await Task.Delay(40);
                Interlocked.Decrement(ref concurrent);
            });
        }

        await Task.WhenAll(RunSwitchAsync(), RunSwitchAsync(), RunSwitchAsync());

        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public void IgnoresStaleGenerationCompletion()
    {
        var coordinator = new RuntimeLifecycleCoordinator();

        var firstGeneration = coordinator.AdvanceGeneration();
        var secondGeneration = coordinator.AdvanceGeneration();

        Assert.False(coordinator.ShouldApply(firstGeneration));
        Assert.True(coordinator.ShouldApply(secondGeneration));
    }

    [Fact]
    public async Task ShutdownTripsInterlockFirst()
    {
        var coordinator = new RuntimeLifecycleCoordinator();
        var interlock = new OutputInterlock();
        Assert.True(interlock.Reset("Ready for shutdown test."));
        var observedLatchedState = false;

        await coordinator.RunShutdownAsync(
            interlock,
            (_, _) =>
            {
                observedLatchedState = interlock.Current.IsLatched
                    && interlock.Current.Reason == OutputInterlockReason.Shutdown;
                return Task.CompletedTask;
            });

        Assert.True(observedLatchedState);
    }

    [Fact]
    public async Task RaceStress_WithConcurrentLifecycleRequests_RemainsSerialized()
    {
        var coordinator = new RuntimeLifecycleCoordinator();
        var concurrent = 0;
        var maxConcurrent = 0;

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => coordinator.RunSerializedAsync(async (_, _) =>
            {
                var active = Interlocked.Increment(ref concurrent);
                maxConcurrent = Math.Max(maxConcurrent, active);
                await Task.Delay(10);
                Interlocked.Decrement(ref concurrent);
            }).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, maxConcurrent);
        Assert.Equal(20, coordinator.CurrentGeneration);
    }
}
