namespace HapticDrive.Asio.App;

internal enum ShutdownCleanupContext
{
    AppShutdown = 0
}

internal enum ShutdownCleanupActionKind
{
    DetachObserver = 0,
    StopOnly = 1,
    Dispose = 2
}

internal enum ShutdownCleanupStepKind
{
    DetachUnhandledExceptionAndInputTelemetryHandlers = 0,
    StopTelemetryStatusTimer = 1,
    StopContinuousPhprRuntime = 2,
    StopStandaloneBst1PulseSession = 3,
    DisposeTestBench = 4,
    DisposePaddleInputSource = 5,
    DisposeTelemetryReceiver = 6,
    StopRoadAndDisposeRealPhprOutput = 7,
    DisposeContinuousPhprRuntime = 8,
    DisposeHapticPipeline = 9
}

internal sealed record ShutdownCleanupStep(
    ShutdownCleanupStepKind Kind,
    ShutdownCleanupActionKind Action,
    string Description,
    TimeSpan? Timeout = null);

internal sealed record ShutdownCleanupPlan(
    ShutdownCleanupContext Context,
    IReadOnlyList<ShutdownCleanupStep> Steps);

internal static class ShutdownCleanupPlanner
{
    private static readonly ShutdownCleanupPlan AppShutdownPlan = new(
        ShutdownCleanupContext.AppShutdown,
        [
            new(
                ShutdownCleanupStepKind.DetachUnhandledExceptionAndInputTelemetryHandlers,
                ShutdownCleanupActionKind.DetachObserver,
                "Detach global exception hooks plus paddle and telemetry event subscriptions before stop/dispose work begins."),
            new(
                ShutdownCleanupStepKind.StopTelemetryStatusTimer,
                ShutdownCleanupActionKind.StopOnly,
                "Stop the telemetry status timer before runtime and device cleanup."),
            new(
                ShutdownCleanupStepKind.StopContinuousPhprRuntime,
                ShutdownCleanupActionKind.StopOnly,
                "Stop the continuous P-HPR road/slip/lock runtime before disposing its routers or output dependencies.",
                TimeSpan.FromSeconds(2)),
            new(
                ShutdownCleanupStepKind.StopStandaloneBst1PulseSession,
                ShutdownCleanupActionKind.StopOnly,
                "Stop the standalone BST-1 local/manual ASIO pulse session before pipeline disposal."),
            new(
                ShutdownCleanupStepKind.DisposeTestBench,
                ShutdownCleanupActionKind.Dispose,
                "Dispose the app-owned test bench after standalone pulse cleanup.",
                TimeSpan.FromSeconds(2)),
            new(
                ShutdownCleanupStepKind.DisposePaddleInputSource,
                ShutdownCleanupActionKind.Dispose,
                "Dispose the read-only paddle listener after detaching event subscriptions.",
                TimeSpan.FromSeconds(2)),
            new(
                ShutdownCleanupStepKind.DisposeTelemetryReceiver,
                ShutdownCleanupActionKind.Dispose,
                "Dispose the UDP telemetry receiver after event detachment and timer stop.",
                TimeSpan.FromSeconds(2)),
            new(
                ShutdownCleanupStepKind.StopRoadAndDisposeRealPhprOutput,
                ShutdownCleanupActionKind.Dispose,
                "Stop real P-HPR road output and then dispose the real P-HPR output backend.",
                TimeSpan.FromSeconds(2)),
            new(
                ShutdownCleanupStepKind.DisposeContinuousPhprRuntime,
                ShutdownCleanupActionKind.Dispose,
                "Dispose the continuous P-HPR runtime coordinator after its runtime loops have been stopped.",
                TimeSpan.FromSeconds(2)),
            new(
                ShutdownCleanupStepKind.DisposeHapticPipeline,
                ShutdownCleanupActionKind.Dispose,
                "Dispose the haptic pipeline last so ASIO/output-owned resources close after upstream app-owned stop work.",
                TimeSpan.FromSeconds(3))
        ]);

    public static ShutdownCleanupPlan BuildAppShutdownPlan()
    {
        return AppShutdownPlan;
    }
}
