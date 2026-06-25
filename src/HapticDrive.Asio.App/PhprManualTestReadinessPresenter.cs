namespace HapticDrive.Asio.App;

internal sealed record PhprManualTestReadinessSnapshot(
    bool CandidateSelected,
    bool OpenCheckPassed,
    bool ReportShapeValid,
    bool DirectControlEnabled,
    bool DirectControlArmed,
    bool SessionAuthorized,
    bool OutputInterlockClear,
    bool CoexistenceClear,
    bool EmergencyStopClear,
    bool DirectConnectionReadyOrOpenable,
    bool StartupCleanupReady,
    bool RecoveryStateClear,
    string RuntimeRecoveryStatus,
    string DirectConnectionState,
    string CoexistenceStatus);

internal sealed record PhprManualTestReadinessPresentation(
    bool IsReady,
    string StatusText,
    string DeviceStatusText,
    IReadOnlyList<string> ChecklistItems);

internal static class PhprManualTestReadinessPresenter
{
    public static PhprManualTestReadinessPresentation Build(PhprManualTestReadinessSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var checklistItems = new[]
        {
            FormatChecklistItem(
                "P-HPR candidate selected",
                snapshot.CandidateSelected,
                "Use Refresh / Select P-HPR Candidate."),
            FormatChecklistItem(
                "HID no-write open-check passed",
                snapshot.OpenCheckPassed,
                "Use Run HID Open-Check."),
            FormatChecklistItem(
                "Report shape valid",
                snapshot.ReportShapeValid,
                "Refresh / Select P-HPR Candidate and review the report details."),
            FormatChecklistItem(
                "Direct control enabled",
                snapshot.DirectControlEnabled,
                "Use Enable Direct Control."),
            FormatChecklistItem(
                "Direct control armed",
                snapshot.DirectControlArmed,
                "Use Arm Direct Control."),
            FormatChecklistItem(
                "Output interlock clear",
                snapshot.OutputInterlockClear,
                "Use Reset Output Interlock."),
            FormatChecklistItem(
                "Coexistence clear",
                snapshot.CoexistenceClear,
                "Close SimPro / SimHub and refresh readiness."),
            FormatChecklistItem(
                "Emergency stop clear",
                snapshot.EmergencyStopClear,
                "Use Clear P-HPR Emergency Stop."),
            FormatChecklistItem(
                "Startup cleanup passed",
                snapshot.StartupCleanupReady,
                "Restart the app or review startup recovery diagnostics."),
            FormatChecklistItem(
                "Recovery hold clear",
                snapshot.RecoveryStateClear,
                snapshot.RuntimeRecoveryStatus),
            FormatChecklistItem(
                "Direct connection ready or openable",
                snapshot.DirectConnectionReadyOrOpenable,
                "Refresh / Select P-HPR Candidate or run the HID open-check.")
        };

        var actions = BuildActions(snapshot);
        var ready = snapshot.SessionAuthorized && actions.Count == 0;
        var deviceStatusText = ready
            ? $"Direct connection {snapshot.DirectConnectionState}; coexistence {snapshot.CoexistenceStatus}; checklist complete. Brake and throttle test buttons may run only when you press them."
            : actions.Count == 0
                ? $"Direct connection {snapshot.DirectConnectionState}; coexistence {snapshot.CoexistenceStatus}; owner-local authorization is paused while safety recovery is active."
                : $"Direct connection {snapshot.DirectConnectionState}; coexistence {snapshot.CoexistenceStatus}; next step: {string.Join("; ", actions)}.";

        return new PhprManualTestReadinessPresentation(
            IsReady: ready,
            StatusText: ready
                ? "Direct P-HPR manual brake and throttle pulses are ready for this session."
                : "Direct P-HPR manual brake and throttle pulses stay disabled until every readiness item below passes.",
            DeviceStatusText: deviceStatusText,
            ChecklistItems: checklistItems);
    }

    private static string FormatChecklistItem(string label, bool passed, string action)
    {
        return passed
            ? $"{label}: YES."
            : $"{label}: NO. {action}";
    }

    private static IReadOnlyList<string> BuildActions(PhprManualTestReadinessSnapshot snapshot)
    {
        var actions = new List<string>();

        AddWhenMissing(actions, snapshot.CandidateSelected, "Refresh / Select P-HPR Candidate");
        AddWhenMissing(actions, snapshot.OpenCheckPassed, "Run HID Open-Check");
        AddWhenMissing(actions, snapshot.ReportShapeValid, "Review the selected report shape");
        AddWhenMissing(actions, snapshot.DirectControlEnabled, "Enable Direct Control");
        AddWhenMissing(actions, snapshot.DirectControlArmed, "Arm Direct Control");
        AddWhenMissing(actions, snapshot.OutputInterlockClear, "Reset Output Interlock");
        AddWhenMissing(actions, snapshot.CoexistenceClear, "Close SimPro / SimHub");
        AddWhenMissing(actions, snapshot.EmergencyStopClear, "Clear P-HPR Emergency Stop");
        AddWhenMissing(actions, snapshot.StartupCleanupReady, "Restart the app or review startup recovery diagnostics");
        AddWhenMissing(actions, snapshot.RecoveryStateClear, snapshot.RuntimeRecoveryStatus);
        AddWhenMissing(actions, snapshot.DirectConnectionReadyOrOpenable, "Refresh the selected candidate or rerun open-check");

        return actions;
    }

    private static void AddWhenMissing(List<string> actions, bool satisfied, string action)
    {
        if (satisfied || actions.Contains(action, StringComparer.Ordinal))
        {
            return;
        }

        actions.Add(action);
    }
}
