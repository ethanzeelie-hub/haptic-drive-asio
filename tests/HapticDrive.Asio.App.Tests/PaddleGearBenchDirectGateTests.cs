using System.IO;
using HapticDrive.Asio.App;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App.Tests;

public sealed class PaddleGearBenchDirectGateTests
{
    [Fact]
    public void ReadyGate_RequiresAllValidatedDirectConditions()
    {
        var ready = PaddleGearBenchDirectGate.TryGetReady(
            ReadyOptions(),
            PHprSoftwareConflictStatus.Clear,
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: false,
            slipLockEnabled: false,
            out var message);

        Assert.True(ready, message);
        Assert.Equal("direct bench gear pulse ready", message);
    }

    [Fact]
    public void DirectGate_BlocksWhenFeatureReportShapeIsMissing()
    {
        var options = ReadyOptions() with
        {
            Selector = ReadySelector() with { Transport = PHprHidReportTransport.OutputReport }
        };

        var ready = PaddleGearBenchDirectGate.TryGetReady(
            options,
            PHprSoftwareConflictStatus.Clear,
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: false,
            slipLockEnabled: false,
            out var message);

        Assert.False(ready);
        Assert.Contains("FeatureReport", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectGate_BlocksUnlessDirectGatesAreGreen()
    {
        var options = ReadyOptions() with
        {
            OpenCheckSucceeded = false
        };

        var ready = PaddleGearBenchDirectGate.TryGetReady(
            options,
            PHprSoftwareConflictStatus.Clear,
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: false,
            slipLockEnabled: false,
            out var message);

        Assert.False(ready);
        Assert.Contains("open-check", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DirectGate_BlocksEmergencyStopRoadAndSlipLock()
    {
        Assert.False(PaddleGearBenchDirectGate.TryGetReady(
            ReadyOptions(),
            PHprSoftwareConflictStatus.Clear,
            OutputSnapshot(emergencyStop: true),
            roadVibrationEnabled: false,
            slipLockEnabled: false,
            out var emergencyMessage));
        Assert.Contains("emergency stop", emergencyMessage, StringComparison.OrdinalIgnoreCase);

        Assert.False(PaddleGearBenchDirectGate.TryGetReady(
            ReadyOptions(),
            PHprSoftwareConflictStatus.Clear,
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: true,
            slipLockEnabled: false,
            out var roadMessage));
        Assert.Contains("road vibration", roadMessage, StringComparison.OrdinalIgnoreCase);

        Assert.False(PaddleGearBenchDirectGate.TryGetReady(
            ReadyOptions(),
            PHprSoftwareConflictStatus.Clear,
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: false,
            slipLockEnabled: true,
            out var slipMessage));
        Assert.Contains("slip/lock", slipMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BenchEnableAndArmState_IsNotPersistedInAppSettings()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "appsettings.json");
        var store = new AppSettingsStore(path);

        store.Save(new AppSettings());
        var json = File.ReadAllText(path);

        Assert.DoesNotContain("PaddleGearBench", json, StringComparison.Ordinal);
        Assert.DoesNotContain("BenchTest", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ManualAsioHardwareTest", json, StringComparison.Ordinal);
    }

    private static PHprRealOutputOptions ReadyOptions()
    {
        return PHprRealOutputOptions.Disabled with
        {
            DirectControlEnabled = true,
            DirectControlArmed = true,
            DirectControlApprovalConfirmed = true,
            CandidateIsRawInputOnly = false,
            CandidateHasOpenableHidPath = true,
            CandidateFeatureReportCapabilityKnown = true,
            CandidateOutputReportCapabilityKnown = false,
            ReportShapeValidationAttempted = true,
            ReportShapeValidationSucceeded = true,
            OpenCheckAttempted = true,
            OpenCheckSucceeded = true,
            Selector = ReadySelector()
        };
    }

    private static PHprHidDeviceSelector ReadySelector()
    {
        return new PHprHidDeviceSelector(
            "sanitized-device-path",
            "Synthetic P-HPR",
            "Synthetic feature report interface",
            PaddleGearBenchDirectGate.RequiredReportId,
            SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            PHprHidReportTransport.FeatureReport);
    }

    private static PHprOutputSnapshot OutputSnapshot(bool emergencyStop)
    {
        return new PHprOutputSnapshot(
            IsMock: false,
            IsConnected: true,
            IsEmergencyStopActive: emergencyStop,
            AcceptedCommandCount: 0,
            RejectedCommandCount: 0,
            LastCommand: null,
            LastStatus: null,
            LastMessage: null,
            LastCommandUtc: null,
            SafetyLimits: SimagicPhprOutputDevice.DirectControlSafetyLimits,
            Mode: "RealDirectArmed",
            BrakeAvailable: true,
            ThrottleAvailable: true);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "HapticDrive.Asio.App.Tests",
            Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
