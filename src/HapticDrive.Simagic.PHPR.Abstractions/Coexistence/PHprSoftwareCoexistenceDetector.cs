using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Abstractions.Coexistence;

public sealed class PHprSoftwareCoexistenceDetector : IPHprSoftwareCoexistenceDetector
{
    private readonly IPHprSoftwareProcessProvider _processProvider;
    private readonly PHprCoexistenceOptions _options;

    public PHprSoftwareCoexistenceDetector(
        IPHprSoftwareProcessProvider processProvider,
        PHprCoexistenceOptions? options = null)
    {
        _processProvider = processProvider ?? throw new ArgumentNullException(nameof(processProvider));
        _options = (options ?? PHprCoexistenceOptions.Default).Normalize();
    }

    public PHprSoftwareCoexistenceSnapshot Scan()
    {
        PHprSoftwareProcessSnapshot processSnapshot;
        try
        {
            processSnapshot = _processProvider.GetSnapshot();
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return new PHprSoftwareCoexistenceSnapshot(
                PHprSoftwareConflictStatus.Unknown,
                SimProRunning: false,
                SimHubRunning: false,
                [],
                [],
                DateTimeOffset.UtcNow,
                IsSupported: true,
                "SimPro/SimHub process detection failed safely; direct control must remain blocked until status is clear.",
                ex.Message);
        }

        if (!processSnapshot.IsSupported)
        {
            return new PHprSoftwareCoexistenceSnapshot(
                PHprSoftwareConflictStatus.Unknown,
                SimProRunning: false,
                SimHubRunning: false,
                [],
                [],
                processSnapshot.ScannedAtUtc,
                IsSupported: false,
                processSnapshot.ErrorMessage ?? "SimPro/SimHub process detection is unsupported on this platform.",
                processSnapshot.ErrorMessage);
        }

        if (!string.IsNullOrWhiteSpace(processSnapshot.ErrorMessage))
        {
            return new PHprSoftwareCoexistenceSnapshot(
                PHprSoftwareConflictStatus.Unknown,
                SimProRunning: false,
                SimHubRunning: false,
                [],
                [],
                processSnapshot.ScannedAtUtc,
                IsSupported: true,
                "SimPro/SimHub process detection completed with errors; direct control must remain blocked until status is clear.",
                processSnapshot.ErrorMessage);
        }

        var simPro = processSnapshot.Processes
            .Where(process => MatchesAny(process.ProcessName, _options.SimProProcessNamePatterns))
            .ToArray();
        var simHub = processSnapshot.Processes
            .Where(process => MatchesAny(process.ProcessName, _options.SimHubProcessNamePatterns))
            .ToArray();
        var status = ResolveStatus(simPro.Length > 0, simHub.Length > 0);

        return new PHprSoftwareCoexistenceSnapshot(
            status,
            SimProRunning: simPro.Length > 0,
            SimHubRunning: simHub.Length > 0,
            simPro,
            simHub,
            processSnapshot.ScannedAtUtc,
            IsSupported: true,
            CreateMessage(status, simPro.Length, simHub.Length));
    }

    private static PHprSoftwareConflictStatus ResolveStatus(bool simProRunning, bool simHubRunning)
    {
        return (simProRunning, simHubRunning) switch
        {
            (true, true) => PHprSoftwareConflictStatus.ActiveConflict,
            (true, false) => PHprSoftwareConflictStatus.SimProRunning,
            (false, true) => PHprSoftwareConflictStatus.SimHubRunning,
            _ => PHprSoftwareConflictStatus.Clear
        };
    }

    private static bool MatchesAny(string processName, IReadOnlyList<string> patterns)
    {
        return patterns.Any(pattern => processName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateMessage(PHprSoftwareConflictStatus status, int simProCount, int simHubCount)
    {
        return status switch
        {
            PHprSoftwareConflictStatus.Clear => "No SimPro Manager or SimHub process was detected.",
            PHprSoftwareConflictStatus.SimProRunning => $"SimPro Manager appears to be running ({simProCount:N0} process match); read-only warning only.",
            PHprSoftwareConflictStatus.SimHubRunning => $"SimHub appears to be running ({simHubCount:N0} process match); read-only warning only.",
            PHprSoftwareConflictStatus.ActiveConflict => $"SimPro Manager and SimHub both appear to be running ({simProCount:N0}/{simHubCount:N0} process matches); direct P-HPR control is blocked.",
            _ => "SimPro/SimHub coexistence status is unknown."
        };
    }
}
