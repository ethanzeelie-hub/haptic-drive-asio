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
    TripOutputInterlock = 0,
    StopUdpReceiver = 1,
    CompleteAndDrainIngress = 2,
    StopAndFinalizeRecording = 3,
    StopReplay = 4,
    StopActuatorRuntimes = 5,
    StopAndDisposeAudio = 6,
    DisposeRemainingServices = 7
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
                ShutdownCleanupStepKind.TripOutputInterlock,
                ShutdownCleanupActionKind.StopOnly,
                "Trip the global output interlock before any shutdown stop or dispose work begins."),
            new(
                ShutdownCleanupStepKind.StopUdpReceiver,
                ShutdownCleanupActionKind.StopOnly,
                "Stop the UDP telemetry receiver before ingress drain and recording finalization.",
                TimeSpan.FromSeconds(2)),
            new(
                ShutdownCleanupStepKind.CompleteAndDrainIngress,
                ShutdownCleanupActionKind.StopOnly,
                "Disable ingress acceptance, complete ingress queues, and drain remaining haptic/forwarding/recording work before recording finalization.",
                TimeSpan.FromSeconds(2)),
            new(
                ShutdownCleanupStepKind.StopAndFinalizeRecording,
                ShutdownCleanupActionKind.StopOnly,
                "Stop recording and write the final footer only after ingress drain completes.",
                TimeSpan.FromSeconds(2)),
            new(
                ShutdownCleanupStepKind.StopReplay,
                ShutdownCleanupActionKind.StopOnly,
                "Stop replay and wait for the replay loop to exit before actuator or audio shutdown.",
                TimeSpan.FromSeconds(2)),
            new(
                ShutdownCleanupStepKind.StopActuatorRuntimes,
                ShutdownCleanupActionKind.StopOnly,
                "Stop continuous and direct actuator runtimes before audio shutdown.",
                TimeSpan.FromSeconds(2)),
            new(
                ShutdownCleanupStepKind.StopAndDisposeAudio,
                ShutdownCleanupActionKind.Dispose,
                "Stop standalone/manual audio activity and dispose the haptic audio pipeline after upstream shutdown work completes.",
                TimeSpan.FromSeconds(3)),
            new(
                ShutdownCleanupStepKind.DisposeRemainingServices,
                ShutdownCleanupActionKind.Dispose,
                "Dispose remaining app-owned services, listeners, observers, and timers after outputs are already stopped.",
                TimeSpan.FromSeconds(2))
        ]);

    public static ShutdownCleanupPlan BuildAppShutdownPlan()
    {
        return AppShutdownPlan;
    }
}
