using HapticDrive.Asio.App;

namespace HapticDrive.Asio.App.Tests;

public sealed class ShutdownCleanupPlannerTests
{
    [Fact]
    public void BuildAppShutdownPlan_ReturnsExpectedStopOnlyOrder()
    {
        var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();

        Assert.Equal(ShutdownCleanupContext.AppShutdown, plan.Context);
        Assert.Equal(
            [
                ShutdownCleanupStepKind.DetachUnhandledExceptionAndInputTelemetryHandlers,
                ShutdownCleanupStepKind.StopTelemetryStatusTimer,
                ShutdownCleanupStepKind.StopContinuousPhprRuntime,
                ShutdownCleanupStepKind.StopStandaloneBst1PulseSession,
                ShutdownCleanupStepKind.DisposeTestBench,
                ShutdownCleanupStepKind.DisposePaddleInputSource,
                ShutdownCleanupStepKind.DisposeTelemetryReceiver,
                ShutdownCleanupStepKind.StopRoadAndDisposeRealPhprOutput,
                ShutdownCleanupStepKind.DisposeContinuousPhprRuntime,
                ShutdownCleanupStepKind.DisposeHapticPipeline
            ],
            plan.Steps.Select(step => step.Kind).ToArray());
    }

    [Fact]
    public void BuildAppShutdownPlan_ContainsOnlyDetachStopAndDisposeActions()
    {
        var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();

        Assert.All(
            plan.Steps,
            step => Assert.Contains(
                step.Action,
                new[]
                {
                    ShutdownCleanupActionKind.DetachObserver,
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
    public void BuildAppShutdownPlan_IsDeterministicAcrossCalls()
    {
        var first = ShutdownCleanupPlanner.BuildAppShutdownPlan();
        var second = ShutdownCleanupPlanner.BuildAppShutdownPlan();

        Assert.Same(first, second);
        Assert.Equal(first, second);
    }

    [Fact]
    public void BuildAppShutdownPlan_PreservesExpectedTimeoutsForBoundedCleanupSteps()
    {
        var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();

        Assert.Null(plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.DetachUnhandledExceptionAndInputTelemetryHandlers).Timeout);
        Assert.Null(plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.StopTelemetryStatusTimer).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.StopContinuousPhprRuntime).Timeout);
        Assert.Null(plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.StopStandaloneBst1PulseSession).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.DisposeTestBench).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.DisposePaddleInputSource).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.DisposeTelemetryReceiver).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.StopRoadAndDisposeRealPhprOutput).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.DisposeContinuousPhprRuntime).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(3), plan.Steps.Single(step => step.Kind == ShutdownCleanupStepKind.DisposeHapticPipeline).Timeout);
    }
}
