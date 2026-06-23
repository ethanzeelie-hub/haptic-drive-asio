using HapticDrive.Asio.App;

namespace HapticDrive.Asio.App.Tests;

public sealed class ShutdownCleanupPlannerTests
{
    [Fact]
    public void ShutdownOrder_TripsInterlockBeforeStoppingOutputs()
    {
        var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();

        Assert.Equal(ShutdownCleanupContext.AppShutdown, plan.Context);
        Assert.Equal(
            [
                ShutdownCleanupStepKind.TripOutputInterlock,
                ShutdownCleanupStepKind.StopUdpReceiver,
                ShutdownCleanupStepKind.CompleteAndDrainIngress,
                ShutdownCleanupStepKind.StopAndFinalizeRecording,
                ShutdownCleanupStepKind.StopReplay,
                ShutdownCleanupStepKind.StopActuatorRuntimes,
                ShutdownCleanupStepKind.StopAndDisposeAudio,
                ShutdownCleanupStepKind.DisposeRemainingServices
            ],
            plan.Steps.Select(step => step.Kind).ToArray());
    }

    [Fact]
    public void ShutdownOrder_DrainsIngressBeforeFinalizingRecording()
    {
        var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();
        var stepKinds = plan.Steps.Select(step => step.Kind).ToArray();

        Assert.True(Array.IndexOf(stepKinds, ShutdownCleanupStepKind.CompleteAndDrainIngress)
                    < Array.IndexOf(stepKinds, ShutdownCleanupStepKind.StopAndFinalizeRecording));
        Assert.True(Array.IndexOf(stepKinds, ShutdownCleanupStepKind.StopAndFinalizeRecording)
                    < Array.IndexOf(stepKinds, ShutdownCleanupStepKind.StopReplay));
    }

    [Fact]
    public void BuildAppShutdownPlan_IsDeterministicAcrossCalls()
    {
        var first = ShutdownCleanupPlanner.BuildAppShutdownPlan();
        var second = ShutdownCleanupPlanner.BuildAppShutdownPlan();

        Assert.Same(first, second);
        Assert.Equal(first, second);
    }

    [Fact]
    public void BuildAppShutdownPlan_ContainsOnlyStopOnlyAndDisposeActions()
    {
        var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();

        Assert.All(
            plan.Steps,
            step => Assert.Contains(
                step.Action,
                new[]
                {
                    ShutdownCleanupActionKind.StopOnly,
                    ShutdownCleanupActionKind.Dispose
                }));
        Assert.DoesNotContain(
            plan.Steps,
            step => step.Description.Contains("start", StringComparison.OrdinalIgnoreCase)
                || step.Description.Contains("arm", StringComparison.OrdinalIgnoreCase)
                || step.Description.Contains("enable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildAppShutdownPlan_PreservesExpectedTimeoutsForBoundedCleanupSteps()
    {
        var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();

        Assert.Null(plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.TripOutputInterlock).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.StopUdpReceiver).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.CompleteAndDrainIngress).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.StopAndFinalizeRecording).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.StopReplay).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.StopActuatorRuntimes).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(3), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.StopAndDisposeAudio).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.DisposeRemainingServices).Timeout);
    }
}
