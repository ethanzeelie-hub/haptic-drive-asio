using System.IO;
using HapticDrive.Asio.App;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
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
    public void DirectGate_DoesNotRequireArmOrApprovalPhrase()
    {
        var ready = PaddleGearBenchDirectGate.TryGetReady(
            ReadyOptions() with
            {
                DirectControlArmed = false,
                DirectControlApprovalConfirmed = false
            },
            PHprSoftwareConflictStatus.Clear,
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: false,
            slipLockEnabled: false,
            out var message);

        Assert.True(ready, message);
        Assert.Equal("direct bench gear pulse ready", message);
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

    [Fact]
    public void AutoReadySelector_SelectsPreferredFeatureReportCandidateWithoutHardcodedIndex()
    {
        var selected = PhprDirectAutoReadySelector.Select(
            [
                Candidate("generic-output-first", vendorId: 0x1234, productId: 0x5678, outputLength: 64, featureLength: null, featureIds: []),
                Candidate("preferred-second", vendorId: 0x3670, productId: 0x0905, outputLength: null, featureLength: 64, featureIds: [PHprDirectOutputCandidate.F1EcFeatureReportId])
            ],
            PHprRealOutputOptions.Disabled,
            enableWhenPreferredPresent: true);

        Assert.True(selected.HasPreferredCandidate);
        Assert.Equal("preferred-second", selected.Candidate?.CandidateId);
        Assert.Equal(PHprHidReportTransport.FeatureReport, selected.Selector.Transport);
        Assert.Equal(PHprDirectOutputCandidate.F1EcFeatureReportId, selected.Selector.ReportId);
        Assert.Equal(SimHubF1EcRealReportEncoder.PayloadLengthBytes, selected.Selector.ReportLength);
        Assert.True(selected.Options.DirectControlEnabled);
        Assert.True(selected.Options.DirectControlArmed);
        Assert.True(selected.Options.DirectControlApprovalConfirmed);
        Assert.True(selected.Options.ReportShapeValidationSucceeded);
    }

    [Fact]
    public void AutoReadyDryRunCanPassAfterFakeOpenCheckWithoutApprovalPhrase()
    {
        var selected = PhprDirectAutoReadySelector.Select(
            [Candidate("preferred", vendorId: 0x3670, productId: 0x0905, outputLength: null, featureLength: 64, featureIds: [PHprDirectOutputCandidate.F1EcFeatureReportId])],
            PHprRealOutputOptions.Disabled,
            enableWhenPreferredPresent: true);
        var options = selected.Options with
        {
            DirectControlArmed = false,
            DirectControlApprovalConfirmed = false,
            OpenCheckAttempted = true,
            OpenCheckSucceeded = true,
            OpenCheckFailed = false
        };

        var dryRun = PHprDirectOutputDryRunValidator.Validate(
            options,
            PHprSoftwareConflictStatus.Clear,
            emergencyStopActive: false);

        Assert.True(dryRun.CanPulse, string.Join("; ", dryRun.Issues));
        Assert.Empty(dryRun.Issues);
    }

    [Fact]
    public void PaddleBenchDirectPlannerUsesDevicePedalCardValues()
    {
        var shift = ShiftIntentEvent.CreatePaddlePress(
            PaddleSide.Right,
            DrivingArmedState.Armed("bench test"),
            timestampUtc: new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));
        var brake = PHprRealGearPulseSettings.Default with
        {
            Strength01 = 0.11d,
            FrequencyHz = 41d,
            DurationMs = 64
        };
        var throttle = PHprRealGearPulseSettings.Default with
        {
            Strength01 = 0.22d,
            FrequencyHz = 49d,
            DurationMs = 88
        };

        var commands = PaddleGearBenchDirectPulsePlanner.BuildCommands(
            shift,
            PHprGearPulseTarget.Both,
            brake,
            throttle);

        var brakeCommand = Assert.Single(commands, command => command.TargetModule == PHprModuleId.Brake);
        var throttleCommand = Assert.Single(commands, command => command.TargetModule == PHprModuleId.Throttle);
        Assert.Equal(0.11d, brakeCommand.Strength01, precision: 6);
        Assert.Equal(41d, brakeCommand.FrequencyHz, precision: 6);
        Assert.Equal(64, brakeCommand.DurationMs);
        Assert.Equal(0.22d, throttleCommand.Strength01, precision: 6);
        Assert.Equal(49d, throttleCommand.FrequencyHz, precision: 6);
        Assert.Equal(88, throttleCommand.DurationMs);
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

    private static PHprDirectOutputCandidate Candidate(
        string id,
        ushort vendorId,
        ushort productId,
        int? outputLength,
        int? featureLength,
        IReadOnlyList<byte> featureIds)
    {
        return new PHprDirectOutputCandidate
        {
            CandidateId = id,
            DevicePath = $@"\\?\hid#vid_{vendorId:X4}&pid_{productId:X4}#{id}",
            DisplayName = id,
            SourceMethod = PHprDirectOutputCandidateSourceMethod.HidDeviceInterface,
            VendorId = vendorId,
            ProductId = productId,
            OutputReportByteLength = outputLength,
            FeatureReportByteLength = featureLength,
            FeatureReportIds = featureIds
        }.Score();
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
