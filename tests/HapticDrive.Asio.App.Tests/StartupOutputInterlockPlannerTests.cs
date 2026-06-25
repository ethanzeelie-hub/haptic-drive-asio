using HapticDrive.Asio.App;
using HapticDrive.Asio.Core.Safety;

namespace HapticDrive.Asio.App.Tests;

public sealed class StartupOutputInterlockPlannerTests
{
    [Fact]
    public void CleanStartup_DefaultsToOutputEnabled_WhenNoRecoveryFaultExists()
    {
        var plan = StartupOutputInterlockPlanner.Build(new StartupOutputInterlockSnapshot(
            InterlockIsLatched: true,
            InterlockReason: OutputInterlockReason.StartupSafeDefault,
            CanReset: true,
            ResetBlocker: string.Empty,
            UncleanShutdownMarkerExists: false,
            DisabledAfterUncleanShutdown: false));

        Assert.True(plan.ShouldEnableOutput);
        Assert.Contains("Output enabled", plan.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("no startup output was sent", plan.FooterMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("remain stopped", plan.FooterMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StartupRecoveryFault_StillLatchesWithSpecificReason()
    {
        var plan = StartupOutputInterlockPlanner.Build(new StartupOutputInterlockSnapshot(
            InterlockIsLatched: true,
            InterlockReason: OutputInterlockReason.StartupSafeDefault,
            CanReset: true,
            ResetBlocker: string.Empty,
            UncleanShutdownMarkerExists: true,
            DisabledAfterUncleanShutdown: true));

        Assert.False(plan.ShouldEnableOutput);
        Assert.Contains("unclean shutdown marker", plan.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stop All / Clear Device State", plan.FooterMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupReadinessBlocker_KeepsInterlockLatchedUntilRecoveryCompletes()
    {
        var plan = StartupOutputInterlockPlanner.Build(new StartupOutputInterlockSnapshot(
            InterlockIsLatched: true,
            InterlockReason: OutputInterlockReason.StartupSafeDefault,
            CanReset: false,
            ResetBlocker: "Output interlock reset blocked: audio output still active",
            UncleanShutdownMarkerExists: false,
            DisabledAfterUncleanShutdown: false));

        Assert.False(plan.ShouldEnableOutput);
        Assert.Contains("audio output still active", plan.StatusMessage, StringComparison.Ordinal);
    }
}
