using HapticDrive.Actuation.PHpr;
using HapticDrive.Actuation.Shift;
using HapticDrive.Asio.App;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App.Tests;

public sealed class ControlSettingsSnapshotBuilderTests
{
    [Fact]
    public void ShiftReplayAndForwardingBuilders_MapRepresentativeInputs()
    {
        var shift = ControlSettingsSnapshotBuilder.BuildShiftIntentOptions(new ShiftIntentControlInputs(
            IsEnabled: true,
            SelectedMode: ShiftIntentMode.InstantPaddleOnly));
        var replayMode = ControlSettingsSnapshotBuilder.GetSelectedReplayTimingMode(ReplayTimingModeOption.FastDebug);
        var replayPreference = ControlSettingsSnapshotBuilder.GetReplayTimingPreference(ReplayTimingModeOption.FastDebug);

        var forwardingBuilt = ControlSettingsSnapshotBuilder.TryBuildForwardingDestinationSetting(
            new ForwardingDestinationControlInputs(
                NameText: string.Empty,
                HostText: "192.168.0.20",
                PortText: "20778",
                Enabled: true),
            out var forwarding,
            out var message);

        Assert.True(shift.IsEnabled);
        Assert.Equal(ShiftIntentMode.InstantPaddleOnly, shift.Mode);
        Assert.Same(ReplayTimingModeOption.FastDebug, replayMode);
        Assert.Equal(ReplayTimingPreference.FastDebug, replayPreference);
        Assert.True(forwardingBuilt);
        Assert.Equal("192.168.0.20:20778", forwarding.Name);
        Assert.Equal("192.168.0.20", forwarding.Host);
        Assert.Equal(20778, forwarding.Port);
        Assert.True(forwarding.Enabled);
        Assert.Equal("Forwarding destination ready.", message);
    }

    [Fact]
    public void TryBuildPaddleMapping_ParsesRepresentativeInputAndRejectsInvalidButtonIds()
    {
        var built = ControlSettingsSnapshotBuilder.TryBuildPaddleMapping(
            new PaddleMappingControlInputs(
                SelectedDeviceId: "wheel-1",
                FallbackSelectedDeviceId: "fallback",
                LeftButtonText: "14",
                RightButtonText: "13",
                DebounceText: "6"),
            out var mapping,
            out var message);

        Assert.True(built);
        Assert.Equal("wheel-1", mapping.SelectedDeviceId);
        Assert.Equal(InputDiscoveryMethod.WindowsGameController, mapping.SelectedMethod);
        Assert.Equal(14, mapping.LeftPaddleButtonId);
        Assert.Equal(13, mapping.RightPaddleButtonId);
        Assert.Equal(6, mapping.DebounceDuration.TotalMilliseconds);
        Assert.Equal("Paddle input mapping ready.", message);

        var invalid = ControlSettingsSnapshotBuilder.TryBuildPaddleMapping(
            new PaddleMappingControlInputs(
                SelectedDeviceId: null,
                FallbackSelectedDeviceId: "wheel-1",
                LeftButtonText: "0",
                RightButtonText: "13",
                DebounceText: "6"),
            out _,
            out message);

        Assert.False(invalid);
        Assert.Equal("Paddle button IDs must be whole numbers from 1 to 128.", message);
    }

    [Fact]
    public void MockRoutingBuilders_MapRepresentativeGearAndPedalEffectInputs()
    {
        var mockGearBuilt = ControlSettingsSnapshotBuilder.TryBuildMockGearPulseOptions(
            new MockGearPulseControlInputs(
                IsEnabled: true,
                TargetModule: PHprGearPulseTarget.Brake,
                StrengthText: "6",
                FrequencyText: "45",
                DurationText: "60"),
            new PHprGearPulseRouterOptions(),
            out var mockGear,
            out var message);

        var mockPedalBuilt = ControlSettingsSnapshotBuilder.TryBuildMockPedalEffectsOptions(
            new MockPedalEffectsControlInputs(
                IsEnabled: true,
                RoadVibration: new PhprPedalEffectControlInputs(true, PHprGearPulseTarget.Brake, "4", "45", "50"),
                WheelSlip: new PhprPedalEffectControlInputs(true, PHprGearPulseTarget.Throttle, "5", "40", "60"),
                WheelLock: new PhprPedalEffectControlInputs(false, PHprGearPulseTarget.Brake, "7", "50", "70")),
            new PHprPedalEffectsRouterOptions(),
            out var mockPedals,
            out _);

        Assert.True(mockGearBuilt);
        Assert.True(mockGear.IsEnabled);
        Assert.Equal(PHprGearPulseTarget.Brake, mockGear.TargetModule);
        Assert.Equal(0.06d, mockGear.Profile.Strength01, precision: 6);
        Assert.Equal(45d, mockGear.Profile.FrequencyHz, precision: 6);
        Assert.Equal(60, mockGear.Profile.DurationMs);
        Assert.Equal("Mock P-HPR gear pulse routing ready.", message);

        Assert.True(mockPedalBuilt);
        Assert.True(mockPedals.IsEnabled);
        Assert.True(mockPedals.RoadVibration.IsEnabled);
        Assert.Equal(PHprGearPulseTarget.Brake, mockPedals.RoadVibration.TargetModule);
        Assert.Equal(0.04d, mockPedals.RoadVibration.Profile.Strength01, precision: 6);
        Assert.Equal(PHprGearPulseTarget.Throttle, mockPedals.WheelSlip.TargetModule);
        Assert.False(mockPedals.WheelLock.IsEnabled);

        var values = ControlSettingsSnapshotBuilder.BuildMockPedalEffectsControlValues(mockPedals);
        Assert.Equal("4", values.RoadVibration.StrengthText);
        Assert.Equal("45", values.RoadVibration.FrequencyText);
        Assert.Equal("50", values.RoadVibration.DurationText);
    }

    [Fact]
    public void RealPhprBuilders_MapRepresentativeDirectRoadAndSlipInputs()
    {
        var candidate = new PHprDirectOutputCandidate
        {
            CandidateId = "simagic-1",
            DevicePath = @"\\?\hid#vid_3670&pid_0905#1",
            DisplayName = "Simagic Pedals",
            DeviceClass = "HID",
            SourceMethod = PHprDirectOutputCandidateSourceMethod.HidDeviceInterface,
            VendorId = 0x3670,
            ProductId = 0x0905,
            FeatureReportByteLength = 64,
            FeatureReportIds = [0xF1]
        };

        var brakeBuilt = ControlSettingsSnapshotBuilder.TryBuildNormalPhprGearPulseSettings(
            "Brake",
            new NormalPhprGearPulseControlInputs(true, "7", "50"),
            60,
            out var brake,
            out _);
        var throttleBuilt = ControlSettingsSnapshotBuilder.TryBuildNormalPhprGearPulseSettings(
            "Throttle",
            new NormalPhprGearPulseControlInputs(false, "4", "45"),
            60,
            out var throttle,
            out _);
        var directBuilt = ControlSettingsSnapshotBuilder.TryBuildRealPhprOutputOptions(
            new RealPhprDirectControlInputs(
                DirectControlEnabled: true,
                ReportIdText: "0xF1",
                ReportLengthText: "64",
                Transport: PHprHidReportTransport.FeatureReport,
                InterfaceText: "Simagic interface",
                SelectedCandidate: candidate),
            PHprRealOutputOptions.Disabled,
            brake,
            throttle,
            out var direct,
            out var directMessage);
        var roadBrakeBuilt = ControlSettingsSnapshotBuilder.TryBuildRealRoadVibrationPedalSettings(
            "Brake road",
            new RealRoadVibrationPedalControlInputs(true, "2", "6", "30", "50", "70"),
            out var roadBrake,
            out _);
        var roadThrottleBuilt = ControlSettingsSnapshotBuilder.TryBuildRealRoadVibrationPedalSettings(
            "Throttle road",
            new RealRoadVibrationPedalControlInputs(false, "1", "3", "25", "45", "40"),
            out var roadThrottle,
            out _);
        var roadBuilt = ControlSettingsSnapshotBuilder.TryBuildRealRoadVibrationOptions(
            isEnabled: true,
            current: PHprRoadVibrationRouterOptions.Disabled,
            brake: roadBrake,
            throttle: roadThrottle,
            out var road,
            out _);
        var slipBuilt = ControlSettingsSnapshotBuilder.TryBuildRealSlipLockEffectSettings(
            "Wheel slip",
            PHprPedalEffectKind.WheelSlip,
            new RealSlipLockEffectControlInputs(true, PHprGearPulseTarget.Brake, "2", "6", "35", "50", "65", "70"),
            out var slip,
            out _);
        var lockBuilt = ControlSettingsSnapshotBuilder.TryBuildRealSlipLockEffectSettings(
            "Wheel lock",
            PHprPedalEffectKind.WheelLock,
            new RealSlipLockEffectControlInputs(false, PHprGearPulseTarget.Throttle, "4", "10", "50", "50", "55", "60"),
            out var wheelLock,
            out _);
        var slipLockOptionsBuilt = ControlSettingsSnapshotBuilder.TryBuildRealSlipLockOptions(
            isEnabled: true,
            current: PHprSlipLockRouterOptions.Disabled,
            wheelSlip: slip,
            wheelLock: wheelLock,
            out var slipLock,
            out _);

        Assert.True(brakeBuilt);
        Assert.True(throttleBuilt);
        Assert.True(directBuilt);
        Assert.True(direct.DirectControlEnabled);
        Assert.True(direct.DirectControlArmed);
        Assert.Equal(PHprHidReportTransport.FeatureReport, direct.Selector.Transport);
        Assert.Equal((byte)0xF1, direct.Selector.ReportId);
        Assert.Equal(64, direct.Selector.ReportLength);
        Assert.Equal(0.07d, direct.BrakeGearPulse.Strength01, precision: 6);
        Assert.Equal("Real P-HPR direct-control options ready for this session only.", directMessage);

        Assert.True(roadBrakeBuilt);
        Assert.True(roadThrottleBuilt);
        Assert.True(roadBuilt);
        Assert.True(road.IsEnabled);
        Assert.Equal(0.02d, road.Brake.MinimumStrength01, precision: 6);
        Assert.Equal(45d, road.Throttle.FrequencyHz, precision: 6);

        Assert.True(slipBuilt);
        Assert.True(lockBuilt);
        Assert.True(slipLockOptionsBuilt);
        Assert.True(slipLock.IsEnabled);
        Assert.Equal(PHprGearPulseTarget.Brake, slipLock.WheelSlip.TargetModule);
        Assert.Equal(PHprGearPulseTarget.Throttle, slipLock.WheelLock.TargetModule);
        Assert.Equal(65, slipLock.WheelSlip.TextureCadenceMs);
        Assert.Equal(55, slipLock.WheelLock.TextureCadenceMs);

        var values = ControlSettingsSnapshotBuilder.BuildRealPhprControlValues(direct, road, slipLock);
        Assert.Equal("7", values.BrakeGearPulse.StrengthText);
        Assert.Equal("50", values.BrakeGearPulse.FrequencyText);
        Assert.Equal("60", values.BrakeGearPulse.DurationText);
        Assert.Equal("2", values.BrakeRoadVibration.MinimumStrengthText);
        Assert.Equal(PHprGearPulseTarget.Brake, values.WheelSlip.TargetModule);
        Assert.Equal("65", values.WheelSlip.TextureCadenceText);
    }

    [Fact]
    public void Bst1Builders_ParseAndFormatRepresentativeValuesWithSafeClamps()
    {
        var manualBuilt = ControlSettingsSnapshotBuilder.TryBuildBst1ManualPulseSettings(
            new Bst1ManualPulseControlInputs("55", "250", "62.5", "70"),
            out var manual,
            out var message);
        var paddleBuilt = ControlSettingsSnapshotBuilder.TryBuildBst1PaddleGearPulseSettings(
            new Bst1PaddleGearPulseControlInputs(
                IsEnabled: true,
                StrengthText: "55",
                FrequencyText: "62.5",
                UseSharedDuration: false,
                DurationText: "70",
                ExistingCustomDurationMs: 45),
            out var paddle,
            out _);

        Assert.True(manualBuilt);
        Assert.Equal(55f, manual.StrengthPercent, precision: 6);
        Assert.Equal(250f, manual.OutputTrimPercent, precision: 6);
        Assert.Equal(62.5f, manual.FrequencyHz, precision: 6);
        Assert.Equal(70, manual.DurationMs);
        Assert.Equal("BST-1 manual pulse settings ready.", message);

        Assert.True(paddleBuilt);
        Assert.True(paddle.IsEnabled);
        Assert.Equal(55f, paddle.StrengthPercent, precision: 6);
        Assert.Equal(62.5f, paddle.FrequencyHz, precision: 6);
        Assert.Equal(70, paddle.CustomDurationMs);

        var values = ControlSettingsSnapshotBuilder.BuildBst1PulseControlValues(
            manual.StrengthPercent,
            manual.OutputTrimPercent,
            manual.FrequencyHz,
            manual.DurationMs,
            paddle.IsEnabled,
            paddle.StrengthPercent,
            paddle.FrequencyHz,
            paddleGearSyncDuration: true,
            sharedPhprGearPulseDurationMs: 60,
            paddleGearCustomDurationMs: paddle.CustomDurationMs,
            effectiveBst1PaddleGearDurationMs: 60);
        Assert.Equal("55", values.ManualStrengthText);
        Assert.Equal("250", values.OutputTrimText);
        Assert.Equal("62.5", values.ManualFrequencyText);
        Assert.Equal("70", values.ManualDurationText);
        Assert.Equal("60", values.PaddleGearDurationText);
        Assert.False(values.PaddleGearDurationEnabled);
        Assert.Contains("Effective duration: 60 ms (sync)", values.PaddleGearEffectiveDurationText, StringComparison.Ordinal);
    }
}
