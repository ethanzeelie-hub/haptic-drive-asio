using System.Text.Json;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.App;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App.Tests;

public sealed class AppSettingsSnapshotBuilderTests
{
    [Fact]
    public void BuildHydrationSnapshot_MapsSafePreferencesWithoutEnablingRuntimeOnlyDirectControl()
    {
        var snapshot = AppSettingsSnapshotBuilder.BuildHydrationSnapshot(new AppSettings
        {
            UseLightTheme = true,
            AdvancedDiagnosticsEnabled = true,
            PreferredOutputMode = AudioOutputDeviceKind.Asio,
            PreferredPhprPedalsEnabled = true,
            PreferredPhprPedalsMode = PhprPedalsModePreference.Direct,
            LastAsioDriverName = "M-Audio",
            LastAsioOutputChannel = 1,
            ArmAsioPreference = true,
            ReplayTimingPreference = ReplayTimingPreference.FastDebug,
            ForwardingDestinations =
            [
                new ForwardingDestinationSetting
                {
                    Name = "Rig",
                    Host = "127.0.0.1",
                    Port = 20779,
                    Enabled = true
                }
            ],
            PaddleInputMapping = new PaddleInputMappingSetting
            {
                SelectedDeviceId = "wheel-1",
                SelectedMethod = InputDiscoveryMethod.WindowsGameController,
                LeftPaddleButtonId = 14,
                RightPaddleButtonId = 13,
                DebounceMilliseconds = 6
            },
            Bst1PaddleGearPulse = new Bst1PaddleGearPulseSetting
            {
                IsEnabled = true,
                StrengthPercent = 55f,
                FrequencyHz = 62.5f,
                UseSharedDuration = false,
                CustomDurationMs = 70
            },
            ShiftIntent = new ShiftIntentSetting
            {
                IsEnabled = true,
                Mode = ShiftIntentMode.InstantPaddleOnly
            },
            MockGearPulseRouting = new MockGearPulseRoutingSetting
            {
                IsEnabled = true,
                TargetModule = PHprGearPulseTarget.Brake,
                Strength01 = 0.06d,
                FrequencyHz = 45d,
                DurationMs = 60
            },
            RealPhprGearPulseRouting = new RealPhprGearPulseRoutingSetting
            {
                Brake = new RealPhprGearPulseSetting
                {
                    IsEnabled = true,
                    Strength01 = 0.07d,
                    FrequencyHz = 50d,
                    DurationMs = 60
                }
            },
            RealPhprRoadVibrationRouting = new RealPhprRoadVibrationRoutingSetting
            {
                IsEnabled = true,
                Brake = new RealPhprRoadVibrationPedalSetting
                {
                    IsEnabled = true,
                    MinimumStrength01 = 0.02d,
                    Strength01 = 0.06d,
                    MinimumFrequencyHz = 30d,
                    FrequencyHz = 50d,
                    DurationMs = 70
                }
            },
            RealPhprSlipLockRouting = new RealPhprSlipLockRoutingSetting
            {
                IsEnabled = true,
                WheelSlip = new RealPhprSlipLockEffectSetting
                {
                    IsEnabled = true,
                    TargetModule = PHprGearPulseTarget.Brake,
                    MinimumStrength01 = 0.02d,
                    Strength01 = 0.06d,
                    MinimumFrequencyHz = 35d,
                    FrequencyHz = 50d,
                    TextureCadenceMs = 65,
                    DurationMs = 70
                }
            }
        });

        Assert.True(snapshot.UseLightTheme);
        Assert.True(snapshot.AdvancedDiagnosticsEnabled);
        Assert.True(snapshot.HasPersistedOutputModePreference);
        Assert.True(snapshot.PhprPedalsEnabledPreference);
        Assert.Equal(PhprPedalsModePreference.Direct, snapshot.PhprPedalsModePreference);
        Assert.Equal(AudioOutputDeviceKind.Asio, snapshot.SelectedOutputKind);
        Assert.Equal("M-Audio", snapshot.SelectedAsioDriverName);
        Assert.Equal(1, snapshot.SelectedAsioOutputChannel);
        Assert.True(snapshot.ArmAsioPreference);
        Assert.Equal(ReplayTimingPreference.FastDebug, snapshot.ReplayTimingPreference);
        Assert.Single(snapshot.ForwardingDestinations);
        Assert.Equal("wheel-1", snapshot.PaddleMapping.SelectedDeviceId);
        Assert.Equal(14, snapshot.PaddleMapping.LeftPaddleButtonId);
        Assert.Equal(13, snapshot.PaddleMapping.RightPaddleButtonId);
        Assert.Equal(6, snapshot.PaddleMapping.DebounceDuration.TotalMilliseconds);
        Assert.True(snapshot.Bst1PaddleGearPulse.IsEnabled);
        Assert.True(snapshot.ShiftIntentOptions.IsEnabled);
        Assert.True(snapshot.MockGearPulseRouterOptions.IsEnabled);
        Assert.Equal(PHprGearPulseTarget.Brake, snapshot.MockGearPulseRouterOptions.TargetModule);
        Assert.False(snapshot.RealPhprOutputOptions.DirectControlEnabled);
        Assert.False(snapshot.RealPhprOutputOptions.DirectControlArmed);
        Assert.False(snapshot.RealPhprOutputOptions.Selector.IsSelected);
        Assert.True(snapshot.RealRoadVibrationRouterOptions.IsEnabled);
        Assert.True(snapshot.RealSlipLockRouterOptions.IsEnabled);
        Assert.Equal(65, snapshot.RealSlipLockRouterOptions.WheelSlip.TextureCadenceMs);
    }

    [Fact]
    public void BuildAppSettings_PersistsSafePreferencesAndDropsRuntimeOnlyDirectControlFields()
    {
        var settings = AppSettingsSnapshotBuilder.BuildAppSettings(new AppSettingsSaveInputs(
            UseLightTheme: true,
            AdvancedDiagnosticsEnabled: true,
            SelectedGameId: GameTelemetryCatalog.DefaultGameId,
            AllowLanTelemetry: true,
            AllowedTelemetryRemoteAddresses:
            [
                "192.168.0.10",
                "192.168.0.11"
            ],
            SelectedOutputKind: AudioOutputDeviceKind.Asio,
            PhprPedalsEnabledPreference: true,
            PhprPedalsModePreference: PhprPedalsModePreference.Direct,
            SelectedAsioDriverName: "M-Audio",
            SelectedAsioOutputChannel: 1,
            ArmAsioPreference: true,
            ReplayTimingPreference: ReplayTimingPreference.FastDebug,
            ForwardingDestinations:
            [
                new ForwardingDestinationSetting
                {
                    Name = "Rig",
                    Host = "127.0.0.1",
                    Port = 20779,
                    Enabled = true
                }
            ],
            PaddleMapping: new WheelPaddleMapping
            {
                SelectedDeviceId = "wheel-1",
                SelectedMethod = InputDiscoveryMethod.WindowsGameController,
                LeftPaddleButtonId = 14,
                RightPaddleButtonId = 13,
                DebounceDuration = TimeSpan.FromMilliseconds(6)
            },
            Bst1PaddleGearPulseEnabled: true,
            Bst1PaddleGearStrengthPercent: 55f,
            Bst1PaddleGearFrequencyHz: 62.5f,
            Bst1PaddleGearUseSharedDuration: false,
            Bst1PaddleGearCustomDurationMs: 70,
            ShiftIntentEnabled: true,
            ShiftIntentMode: ShiftIntentMode.InstantPaddleOnly,
            MockGearPulseRouterOptions: new PHprGearPulseRouterOptions
            {
                IsEnabled = true,
                TargetModule = PHprGearPulseTarget.Brake,
                Profile = PHprGearPulseProfile.Default with
                {
                    Strength01 = 0.06d,
                    FrequencyHz = 45d,
                    DurationMs = 60
                }
            }.Normalize(),
            MockPedalEffectsRouterOptions: new PHprPedalEffectsRouterOptions
            {
                IsEnabled = true,
                RoadVibration = PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.RoadVibration) with
                {
                    Profile = PHprPedalEffectProfile.DefaultFor(PHprPedalEffectKind.RoadVibration) with
                    {
                        Strength01 = 0.04d,
                        FrequencyHz = 45d,
                        DurationMs = 50
                    }
                }
            }.Normalize(),
            RealPhprOutputOptions: PHprRealOutputOptions.Disabled with
            {
                DirectControlEnabled = true,
                DirectControlArmed = true,
                Selector = new PHprHidDeviceSelector("private-device-path", "Selected device", "HID", 0xF1, 64, PHprHidReportTransport.FeatureReport),
                BrakeGearPulse = new PHprRealGearPulseSettings
                {
                    IsEnabled = true,
                    Strength01 = 0.07d,
                    FrequencyHz = 50d,
                    DurationMs = 60
                }
            },
            RealRoadVibrationRouterOptions: PHprRoadVibrationRouterOptions.Disabled with
            {
                IsEnabled = true,
                Brake = new PHprRoadVibrationPedalSettings
                {
                    IsEnabled = true,
                    MinimumStrength01 = 0.02d,
                    Strength01 = 0.06d,
                    MinimumFrequencyHz = 30d,
                    FrequencyHz = 50d,
                    DurationMs = 70
                }
            },
            RealSlipLockRouterOptions: PHprSlipLockRouterOptions.Disabled with
            {
                IsEnabled = true,
                WheelSlip = new PHprSlipLockEffectSettings
                {
                    IsEnabled = true,
                    TargetModule = PHprGearPulseTarget.Brake,
                    MinimumStrength01 = 0.02d,
                    Strength01 = 0.06d,
                    MinimumFrequencyHz = 35d,
                    FrequencyHz = 50d,
                    TextureCadenceMs = 65,
                    DurationMs = 70
                }
            }));

        var json = JsonSerializer.Serialize(settings);

        Assert.True(settings.UseLightTheme);
        Assert.True(settings.AdvancedDiagnosticsEnabled);
        Assert.True(settings.AllowLanTelemetry);
        Assert.Equal(["192.168.0.10", "192.168.0.11"], settings.AllowedTelemetryRemoteAddresses);
        Assert.Equal(AudioOutputDeviceKind.Asio, settings.PreferredOutputMode);
        Assert.True(settings.PreferredPhprPedalsEnabled);
        Assert.Equal(PhprPedalsModePreference.Direct, settings.PreferredPhprPedalsMode);
        Assert.Equal("M-Audio", settings.LastAsioDriverName);
        Assert.Equal(1, settings.LastAsioOutputChannel);
        Assert.True(settings.ArmAsioPreference);
        Assert.Equal(ReplayTimingPreference.FastDebug, settings.ReplayTimingPreference);
        Assert.Equal("wheel-1", settings.PaddleInputMapping.SelectedDeviceId);
        Assert.Equal(14, settings.PaddleInputMapping.LeftPaddleButtonId);
        Assert.Equal(13, settings.PaddleInputMapping.RightPaddleButtonId);
        Assert.Equal(6, settings.PaddleInputMapping.DebounceMilliseconds);
        Assert.Equal(0.07d, settings.RealPhprGearPulseRouting.Brake.Strength01);
        Assert.Equal(65, settings.RealPhprSlipLockRouting.WheelSlip.TextureCadenceMs);
        Assert.DoesNotContain("DirectControlEnabled", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectControlArmed", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-device-path", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DevicePath", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PersistedSettingsStatusPresenter_BuildsEquivalentSafeStatusAndDiagnosticsText()
    {
        var presentation = PersistedSettingsStatusPresenter.Build(new PersistedSettingsStatusSnapshot(
            SettingsPath: @"C:\Users\ethan\AppData\Local\HapticDrive.Asio\appsettings.json",
            SettingsError: null,
            UseLightTheme: false,
            ActiveProfileName: "Race",
            SelectedGameId: GameTelemetryCatalog.DefaultGameId,
            SelectedGameDisplayName: GameTelemetryCatalog.GetDisplayName(GameTelemetryCatalog.DefaultGameId),
            SelectedOutputKind: AudioOutputDeviceKind.Asio,
            PhprPedalsEnabledPreference: true,
            PhprPedalsModePreference: PhprPedalsModePreference.Direct,
            ReplayTimingLabel: "Real time",
            ForwardingDestinationCount: 2,
            SelectedAsioDriverName: "M-Audio",
            SelectedAsioOutputChannel: 1,
            ArmAsioPreference: true,
            PaddleMapping: new WheelPaddleMapping
            {
                SelectedDeviceId = "wheel-1",
                SelectedMethod = InputDiscoveryMethod.WindowsGameController,
                LeftPaddleButtonId = 14,
                RightPaddleButtonId = 13,
                DebounceDuration = TimeSpan.FromMilliseconds(6)
            },
            Bst1PaddleGearPulseEnabled: true,
            Bst1PaddleGearStrengthPercent: 55f,
            Bst1PaddleGearFrequencyHz: 62.5f,
            EffectiveBst1PaddleGearDurationMs: 70,
            ShiftIntentEnabled: true,
            ShiftIntentMode: ShiftIntentMode.InstantPaddleOnly,
            RealDirectControlEnabled: false,
            RealRoadVibrationEnabled: true,
            RealSlipLockEnabled: true,
            MockGearRoutingEnabled: true,
            MockGearRoutingTarget: PHprGearPulseTarget.Brake,
            MockPedalEffectsEnabled: true));

        Assert.Contains("Theme: Dark. Active profile: Race.", presentation.StatusText, StringComparison.Ordinal);
        Assert.Contains("Saved game F1 25 (f1-25).", presentation.StatusText, StringComparison.Ordinal);
        Assert.Contains("Saved ASIO driver M-Audio; channel 1; Arm ASIO preference True.", presentation.StatusText, StringComparison.Ordinal);
        Assert.Contains("Saved P-HPR pedals enabled in Direct mode.", presentation.StatusText, StringComparison.Ordinal);
        Assert.Contains("Real P-HPR direct control disabled runtime-only.", presentation.StatusText, StringComparison.Ordinal);
        Assert.Contains("manual ASIO test active state are not saved.", presentation.StatusText, StringComparison.Ordinal);
        Assert.Equal(
            @"App settings path: C:\Users\ethan\AppData\Local\HapticDrive.Asio\appsettings.json",
            presentation.PathText);
        Assert.Contains("game F1 25 (f1-25)", presentation.DiagnosticsText, StringComparison.Ordinal);
        Assert.Contains("persisted P-HPR pedals enabled mode Direct", presentation.DiagnosticsText, StringComparison.Ordinal);
        Assert.Contains("persisted paddle mapping device wheel-1 left button 14 right button 13 debounce 6 ms", presentation.DiagnosticsText, StringComparison.Ordinal);
        Assert.Contains("flight-recorder history, and mock histories are not persisted.", presentation.DiagnosticsText, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\?\hid", presentation.DiagnosticsText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("serial", presentation.DiagnosticsText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildHydrationSnapshot_RepairsMissingOrInvalidPhprPreferenceFieldsSafely()
    {
        var snapshot = AppSettingsSnapshotBuilder.BuildHydrationSnapshot(new AppSettings
        {
            PreferredPhprPedalsEnabled = null,
            PreferredPhprPedalsMode = (PhprPedalsModePreference)999,
            MockGearPulseRouting = new MockGearPulseRoutingSetting
            {
                IsEnabled = false
            },
            MockPedalEffectsRouting = new MockPedalEffectsRoutingSetting
            {
                IsEnabled = false
            }
        });

        Assert.False(snapshot.PhprPedalsEnabledPreference);
        Assert.Equal(PhprPedalsModePreference.Disabled, snapshot.PhprPedalsModePreference);
        Assert.False(snapshot.RealPhprOutputOptions.DirectControlEnabled);
        Assert.False(snapshot.RealPhprOutputOptions.DirectControlArmed);
    }
}
