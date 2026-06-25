using HapticDrive.Asio.Core.Safety;

namespace HapticDrive.Asio.App;

internal sealed record StartupOutputInterlockSnapshot(
    bool InterlockIsLatched,
    OutputInterlockReason InterlockReason,
    bool CanReset,
    string ResetBlocker,
    bool UncleanShutdownMarkerExists,
    bool DisabledAfterUncleanShutdown);

internal sealed record StartupOutputInterlockPlan(
    bool ShouldEnableOutput,
    string StatusMessage,
    string FooterMessage);

internal static class StartupOutputInterlockPlanner
{
    public static StartupOutputInterlockPlan Build(StartupOutputInterlockSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.InterlockIsLatched)
        {
            const string alreadyEnabledMessage = "Startup readiness checks already left output enabled. Live haptics remain stopped until you press Start Haptics.";
            return new StartupOutputInterlockPlan(
                ShouldEnableOutput: false,
                StatusMessage: alreadyEnabledMessage,
                FooterMessage: alreadyEnabledMessage);
        }

        if (snapshot.InterlockReason != OutputInterlockReason.StartupSafeDefault)
        {
            var message = $"Startup preserved the existing safety latch: {snapshot.InterlockReason}. Clear the real fault or recovery condition before enabling output.";
            return new StartupOutputInterlockPlan(
                ShouldEnableOutput: false,
                StatusMessage: message,
                FooterMessage: message);
        }

        if (snapshot.UncleanShutdownMarkerExists || snapshot.DisabledAfterUncleanShutdown)
        {
            const string message = "Startup kept output latched because a P-HPR unclean shutdown marker is present. Use P-HPR Stop All / Clear Device State before retesting.";
            return new StartupOutputInterlockPlan(
                ShouldEnableOutput: false,
                StatusMessage: message,
                FooterMessage: message);
        }

        if (!snapshot.CanReset)
        {
            var blocker = string.IsNullOrWhiteSpace(snapshot.ResetBlocker)
                ? "another output-safety participant is not ready to reset"
                : snapshot.ResetBlocker.Trim();
            var message = $"Startup kept output latched because readiness checks are still blocked: {blocker}.";
            return new StartupOutputInterlockPlan(
                ShouldEnableOutput: false,
                StatusMessage: message,
                FooterMessage: message);
        }

        const string readyMessage = "Startup readiness checks passed. Output enabled; live haptics remain stopped until you press Start Haptics, and no startup output was sent.";
        return new StartupOutputInterlockPlan(
            ShouldEnableOutput: true,
            StatusMessage: readyMessage,
            FooterMessage: readyMessage);
    }
}
