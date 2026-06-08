using System.Diagnostics;

namespace HapticDrive.Simagic.PHPR.Abstractions.Coexistence;

public sealed class WindowsProcessSnapshotProvider : IPHprSoftwareProcessProvider
{
    private readonly Func<bool> _isWindows;

    public WindowsProcessSnapshotProvider(Func<bool>? isWindows = null)
    {
        _isWindows = isWindows ?? OperatingSystem.IsWindows;
    }

    public PHprSoftwareProcessSnapshot GetSnapshot()
    {
        var scannedAt = DateTimeOffset.UtcNow;
        if (!_isWindows())
        {
            return PHprSoftwareProcessSnapshot.Unsupported(
                "Read-only SimPro/SimHub process detection is available on Windows only.",
                scannedAt);
        }

        var processes = new List<PHprDetectedSoftwareProcess>();
        Process[] snapshot;
        try
        {
            snapshot = Process.GetProcesses();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new PHprSoftwareProcessSnapshot(
                [],
                scannedAt,
                IsSupported: true,
                $"Process snapshot failed: {ex.Message}");
        }

        foreach (var process in snapshot)
        {
            using (process)
            {
                try
                {
                    var processName = process.ProcessName;
                    if (string.IsNullOrWhiteSpace(processName))
                    {
                        continue;
                    }

                    string? title = null;
                    try
                    {
                        title = string.IsNullOrWhiteSpace(process.MainWindowTitle)
                            ? null
                            : process.MainWindowTitle.Trim();
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        title = null;
                    }

                    processes.Add(new PHprDetectedSoftwareProcess(
                        processName.Trim(),
                        TryGetProcessId(process),
                        title));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    _ = ex;
                }
            }
        }

        return new PHprSoftwareProcessSnapshot(
            processes,
            scannedAt,
            IsSupported: true);
    }

    private static int? TryGetProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
