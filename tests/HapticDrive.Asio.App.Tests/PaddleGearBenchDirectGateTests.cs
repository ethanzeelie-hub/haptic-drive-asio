using System.IO;
using HapticDrive.Asio.App;
using HapticDrive.Asio.Core.Safety;
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
            InterlockSnapshot(),
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: false,
            slipLockEnabled: false,
            AuthorizationSnapshot(),
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
            InterlockSnapshot(),
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: false,
            slipLockEnabled: false,
            AuthorizationSnapshot(),
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
            InterlockSnapshot(),
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: false,
            slipLockEnabled: false,
            AuthorizationSnapshot(),
            out var message);

        Assert.False(ready);
        Assert.Contains("open-check", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DirectGate_RequiresArmAndSessionAuthorization()
    {
        var armBlocked = PaddleGearBenchDirectGate.TryGetReady(
            ReadyOptions() with
            {
                DirectControlArmed = false
            },
            PHprSoftwareConflictStatus.Clear,
            InterlockSnapshot(),
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: false,
            slipLockEnabled: false,
            AuthorizationSnapshot(),
            out var armMessage);

        Assert.False(armBlocked);
        Assert.Contains("not armed", armMessage, StringComparison.OrdinalIgnoreCase);

        var authorizationBlocked = PaddleGearBenchDirectGate.TryGetReady(
            ReadyOptions(),
            PHprSoftwareConflictStatus.Clear,
            InterlockSnapshot(),
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: false,
            slipLockEnabled: false,
            AuthorizationSnapshot(isAuthorized: false),
            out var authorizationMessage);

        Assert.False(authorizationBlocked);
        Assert.Contains("authorization", authorizationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DirectGate_BlocksEmergencyStopAndSlipLockButAllowsRoadCoexistence()
    {
        Assert.False(PaddleGearBenchDirectGate.TryGetReady(
            ReadyOptions(),
            PHprSoftwareConflictStatus.Clear,
            InterlockSnapshot(),
            OutputSnapshot(emergencyStop: true),
            roadVibrationEnabled: false,
            slipLockEnabled: false,
            AuthorizationSnapshot(),
            out var emergencyMessage));
        Assert.Contains("emergency stop", emergencyMessage, StringComparison.OrdinalIgnoreCase);

        Assert.True(PaddleGearBenchDirectGate.TryGetReady(
            ReadyOptions(),
            PHprSoftwareConflictStatus.Clear,
            InterlockSnapshot(),
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: true,
            slipLockEnabled: false,
            AuthorizationSnapshot(),
            out var roadMessage));
        Assert.Equal("direct bench gear pulse ready", roadMessage);

        Assert.False(PaddleGearBenchDirectGate.TryGetReady(
            ReadyOptions(),
            PHprSoftwareConflictStatus.Clear,
            InterlockSnapshot(),
            OutputSnapshot(emergencyStop: false),
            roadVibrationEnabled: false,
            slipLockEnabled: true,
            AuthorizationSnapshot(),
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
        Assert.True(selected.Options.ReportShapeValidationSucceeded);
    }

    [Fact]
    public void AutoReadyDryRunRequiresSessionAuthorization()
    {
        var selected = PhprDirectAutoReadySelector.Select(
            [Candidate("preferred", vendorId: 0x3670, productId: 0x0905, outputLength: null, featureLength: 64, featureIds: [PHprDirectOutputCandidate.F1EcFeatureReportId])],
            PHprRealOutputOptions.Disabled,
            enableWhenPreferredPresent: true);
        var options = selected.Options with
        {
            OpenCheckAttempted = true,
            OpenCheckSucceeded = true,
            OpenCheckFailed = false
        };

        var dryRun = PHprDirectOutputDryRunValidator.Validate(
            options,
            PHprSoftwareConflictStatus.Clear,
            InterlockSnapshot(),
            emergencyStopActive: false,
            authorization: AuthorizationSnapshot(isAuthorized: false));

        Assert.False(dryRun.CanPulse);
        Assert.Contains(dryRun.Issues, issue => issue.Contains("authorization", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeviceCardPulseServiceUsesDevicePedalCardValues()
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

        var brakeCommand = PhprDeviceCardPulseService.CreateDirectPulseCommand(
            PHprModuleId.Brake,
            brake,
            shift.TimestampUtc);
        var throttleCommand = PhprDeviceCardPulseService.CreateDirectPulseCommand(
            PHprModuleId.Throttle,
            throttle,
            shift.TimestampUtc);

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

    private static OutputInterlockSnapshot InterlockSnapshot(bool allowsOutput = true)
    {
        return new OutputInterlockSnapshot(
            IsLatched: !allowsOutput,
            Reason: allowsOutput ? OutputInterlockReason.ConfigurationInvalid : OutputInterlockReason.StartupSafeDefault,
            Message: allowsOutput ? "clear" : "latched",
            ChangedAtUtc: DateTimeOffset.UtcNow,
            Generation: allowsOutput ? 1 : 0);
    }

    private static PHprWriteAuthorizationSnapshot AuthorizationSnapshot(bool isAuthorized = true)
    {
        return new PHprWriteAuthorizationSnapshot(
            IsAuthorized: isAuthorized,
            AuthorizedAtUtc: isAuthorized ? DateTimeOffset.UtcNow : null,
            Generation: isAuthorized ? 1 : 0,
            Reason: isAuthorized ? "Authorized for this session" : "Not authorized");
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
