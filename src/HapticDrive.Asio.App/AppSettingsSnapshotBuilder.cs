using HapticDrive.Actuation.PHpr;
using HapticDrive.Actuation.Shift;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal sealed record AppSettingsHydrationSnapshot(
    string? SettingsError,
    bool UseLightTheme,
    bool AdvancedDiagnosticsEnabled,
    bool HasPersistedOutputModePreference,
    AudioOutputDeviceKind SelectedOutputKind,
    string? SelectedAsioDriverName,
    int? SelectedAsioOutputChannel,
    bool ArmAsioPreference,
    ReplayTimingPreference ReplayTimingPreference,
    IReadOnlyList<ForwardingDestinationSetting> ForwardingDestinations,
    WheelPaddleMapping PaddleMapping,
    Bst1PaddleGearPulseSetting Bst1PaddleGearPulse,
    ShiftIntentProcessorOptions ShiftIntentOptions,
    PHprGearPulseRouterOptions MockGearPulseRouterOptions,
    PHprPedalEffectsRouterOptions MockPedalEffectsRouterOptions,
    PHprRealOutputOptions RealPhprOutputOptions,
    PHprRoadVibrationRouterOptions RealRoadVibrationRouterOptions,
    PHprSlipLockRouterOptions RealSlipLockRouterOptions);

internal sealed record AppSettingsSaveInputs(
    bool UseLightTheme,
    bool AdvancedDiagnosticsEnabled,
    AudioOutputDeviceKind SelectedOutputKind,
    string? SelectedAsioDriverName,
    int? SelectedAsioOutputChannel,
    bool ArmAsioPreference,
    ReplayTimingPreference ReplayTimingPreference,
    IReadOnlyList<ForwardingDestinationSetting> ForwardingDestinations,
    WheelPaddleMapping PaddleMapping,
    bool Bst1PaddleGearPulseEnabled,
    float Bst1PaddleGearStrengthPercent,
    float Bst1PaddleGearFrequencyHz,
    bool Bst1PaddleGearUseSharedDuration,
    int Bst1PaddleGearCustomDurationMs,
    bool ShiftIntentEnabled,
    ShiftIntentMode ShiftIntentMode,
    PHprGearPulseRouterOptions MockGearPulseRouterOptions,
    PHprPedalEffectsRouterOptions MockPedalEffectsRouterOptions,
    PHprRealOutputOptions RealPhprOutputOptions,
    PHprRoadVibrationRouterOptions RealRoadVibrationRouterOptions,
    PHprSlipLockRouterOptions RealSlipLockRouterOptions);

internal sealed record PersistedSettingsStatusSnapshot(
    string SettingsPath,
    string? SettingsError,
    bool UseLightTheme,
    string ActiveProfileName,
    AudioOutputDeviceKind SelectedOutputKind,
    string ReplayTimingLabel,
    int ForwardingDestinationCount,
    string? SelectedAsioDriverName,
    int? SelectedAsioOutputChannel,
    bool ArmAsioPreference,
    WheelPaddleMapping PaddleMapping,
    bool Bst1PaddleGearPulseEnabled,
    float Bst1PaddleGearStrengthPercent,
    float Bst1PaddleGearFrequencyHz,
    int EffectiveBst1PaddleGearDurationMs,
    bool ShiftIntentEnabled,
    ShiftIntentMode ShiftIntentMode,
    bool RealDirectControlEnabled,
    bool RealRoadVibrationEnabled,
    bool RealSlipLockEnabled,
    bool MockGearRoutingEnabled,
    PHprGearPulseTarget MockGearRoutingTarget,
    bool MockPedalEffectsEnabled);

internal sealed record PersistedSettingsStatusPresentation(
    string StatusText,
    string PathText,
    string DiagnosticsText);

internal static class AppSettingsSnapshotBuilder
{
    private static readonly PHprSafetyLimits SettingsSafetyLimits = PHprSafetyLimits.Default;

    public static AppSettingsHydrationSnapshot BuildHydrationSnapshot(AppSettings? settings)
    {
        var sanitized = AppSettingsStore.Sanitize(settings);
        return new AppSettingsHydrationSnapshot(
            SettingsError: sanitized.LastStatusMessage,
            UseLightTheme: sanitized.UseLightTheme,
            AdvancedDiagnosticsEnabled: sanitized.AdvancedDiagnosticsEnabled,
            HasPersistedOutputModePreference: sanitized.PreferredOutputMode is not null,
            SelectedOutputKind: sanitized.PreferredOutputMode ?? AudioOutputDeviceKind.Null,
            SelectedAsioDriverName: sanitized.LastAsioDriverName,
            SelectedAsioOutputChannel: sanitized.LastAsioOutputChannel,
            ArmAsioPreference: sanitized.ArmAsioPreference,
            ReplayTimingPreference: sanitized.ReplayTimingPreference,
            ForwardingDestinations: sanitized.ForwardingDestinations.ToList(),
            PaddleMapping: CreatePaddleMapping(sanitized.PaddleInputMapping),
            Bst1PaddleGearPulse: sanitized.Bst1PaddleGearPulse,
            ShiftIntentOptions: CreateShiftIntentOptions(sanitized.ShiftIntent),
            MockGearPulseRouterOptions: CreateMockGearPulseRouterOptions(sanitized.MockGearPulseRouting),
            MockPedalEffectsRouterOptions: CreateMockPedalEffectsRouterOptions(sanitized.MockPedalEffectsRouting),
            RealPhprOutputOptions: CreateRealPhprOutputOptions(sanitized.RealPhprGearPulseRouting),
            RealRoadVibrationRouterOptions: CreateRealRoadVibrationRouterOptions(sanitized.RealPhprRoadVibrationRouting),
            RealSlipLockRouterOptions: CreateRealSlipLockRouterOptions(sanitized.RealPhprSlipLockRouting));
    }

    public static AppSettings BuildAppSettings(AppSettingsSaveInputs inputs)
    {
        return new AppSettings
        {
            UseLightTheme = inputs.UseLightTheme,
            AdvancedDiagnosticsEnabled = inputs.AdvancedDiagnosticsEnabled,
            PreferredOutputMode = inputs.SelectedOutputKind,
            LastAsioDriverName = inputs.SelectedAsioDriverName,
            LastAsioOutputChannel = inputs.SelectedAsioOutputChannel,
            ArmAsioPreference = inputs.ArmAsioPreference,
            ReplayTimingPreference = inputs.ReplayTimingPreference,
            ForwardingDestinations = inputs.ForwardingDestinations.ToList(),
            PaddleInputMapping = CreatePaddleInputMappingSetting(inputs.PaddleMapping),
            Bst1PaddleGearPulse = CreateBst1PaddleGearPulseSetting(
                inputs.Bst1PaddleGearPulseEnabled,
                inputs.Bst1PaddleGearStrengthPercent,
                inputs.Bst1PaddleGearFrequencyHz,
                inputs.Bst1PaddleGearUseSharedDuration,
                inputs.Bst1PaddleGearCustomDurationMs),
            ShiftIntent = CreateShiftIntentSetting(inputs.ShiftIntentEnabled, inputs.ShiftIntentMode),
            MockGearPulseRouting = CreateMockGearPulseRoutingSetting(inputs.MockGearPulseRouterOptions),
            MockPedalEffectsRouting = CreateMockPedalEffectsRoutingSetting(inputs.MockPedalEffectsRouterOptions),
            RealPhprGearPulseRouting = CreateRealPhprGearPulseRoutingSetting(inputs.RealPhprOutputOptions),
            RealPhprRoadVibrationRouting = CreateRealPhprRoadVibrationRoutingSetting(inputs.RealRoadVibrationRouterOptions),
            RealPhprSlipLockRouting = CreateRealPhprSlipLockRoutingSetting(inputs.RealSlipLockRouterOptions)
        };
    }

    public static WheelPaddleMapping CreatePaddleMapping(PaddleInputMappingSetting setting)
    {
        return new WheelPaddleMapping
        {
            SelectedDeviceId = setting.SelectedDeviceId,
            SelectedMethod = setting.SelectedMethod,
            LeftPaddleButtonId = setting.LeftPaddleButtonId,
            RightPaddleButtonId = setting.RightPaddleButtonId,
            DebounceDuration = TimeSpan.FromMilliseconds(setting.DebounceMilliseconds)
        }.Normalize();
    }

    public static PaddleInputMappingSetting CreatePaddleInputMappingSetting(WheelPaddleMapping mapping)
    {
        var normalized = mapping.Normalize();
        return new PaddleInputMappingSetting
        {
            SelectedDeviceId = normalized.SelectedDeviceId,
            SelectedMethod = normalized.SelectedMethod,
            LeftPaddleButtonId = normalized.LeftPaddleButtonId,
            RightPaddleButtonId = normalized.RightPaddleButtonId,
            DebounceMilliseconds = (int)normalized.DebounceDuration.TotalMilliseconds
        };
    }

    public static Bst1PaddleGearPulseSetting CreateBst1PaddleGearPulseSetting(
        bool isEnabled,
        float strengthPercent,
        float frequencyHz,
        bool useSharedDuration,
        int customDurationMs)
    {
        return new Bst1PaddleGearPulseSetting
        {
            IsEnabled = isEnabled,
            StrengthPercent = strengthPercent,
            FrequencyHz = frequencyHz,
            UseSharedDuration = useSharedDuration,
            CustomDurationMs = customDurationMs
        };
    }

    public static ShiftIntentProcessorOptions CreateShiftIntentOptions(ShiftIntentSetting setting)
    {
        return new ShiftIntentProcessorOptions
        {
            IsEnabled = setting.IsEnabled,
            Mode = setting.Mode
        }.Normalize();
    }

    public static ShiftIntentSetting CreateShiftIntentSetting(bool isEnabled, ShiftIntentMode mode)
    {
        return new ShiftIntentSetting
        {
            IsEnabled = isEnabled,
            Mode = mode
        };
    }

    public static PHprGearPulseRouterOptions CreateMockGearPulseRouterOptions(MockGearPulseRoutingSetting setting)
    {
        return new PHprGearPulseRouterOptions
        {
            IsEnabled = setting.IsEnabled,
            TargetModule = setting.TargetModule,
            Profile = PHprGearPulseProfile.Default with
            {
                Strength01 = setting.Strength01,
                FrequencyHz = setting.FrequencyHz,
                DurationMs = setting.DurationMs
            }
        }.Normalize();
    }

    public static MockGearPulseRoutingSetting CreateMockGearPulseRoutingSetting(PHprGearPulseRouterOptions options)
    {
        var normalized = options.Normalize();
        return new MockGearPulseRoutingSetting
        {
            IsEnabled = normalized.IsEnabled,
            TargetModule = normalized.TargetModule,
            Strength01 = normalized.Profile.Strength01,
            FrequencyHz = normalized.Profile.FrequencyHz,
            DurationMs = normalized.Profile.DurationMs
        };
    }

    public static PHprPedalEffectsRouterOptions CreateMockPedalEffectsRouterOptions(MockPedalEffectsRoutingSetting setting)
    {
        return new PHprPedalEffectsRouterOptions
        {
            IsEnabled = setting.IsEnabled,
            RoadVibration = CreatePedalEffectState(PHprPedalEffectKind.RoadVibration, setting.RoadVibration),
            WheelSlip = CreatePedalEffectState(PHprPedalEffectKind.WheelSlip, setting.WheelSlip),
            WheelLock = CreatePedalEffectState(PHprPedalEffectKind.WheelLock, setting.WheelLock)
        }.Normalize();
    }

    public static MockPedalEffectsRoutingSetting CreateMockPedalEffectsRoutingSetting(PHprPedalEffectsRouterOptions options)
    {
        var normalized = options.Normalize();
        return new MockPedalEffectsRoutingSetting
        {
            IsEnabled = normalized.IsEnabled,
            RoadVibration = CreateMockPedalEffectSetting(normalized.RoadVibration),
            WheelSlip = CreateMockPedalEffectSetting(normalized.WheelSlip),
            WheelLock = CreateMockPedalEffectSetting(normalized.WheelLock)
        };
    }

    public static PHprRealOutputOptions CreateRealPhprOutputOptions(RealPhprGearPulseRoutingSetting setting)
    {
        return PHprRealOutputOptions.Disabled with
        {
            BrakeGearPulse = CreateRealGearPulseSettings(setting.Brake),
            ThrottleGearPulse = CreateRealGearPulseSettings(setting.Throttle)
        };
    }

    public static RealPhprGearPulseRoutingSetting CreateRealPhprGearPulseRoutingSetting(PHprRealOutputOptions options)
    {
        var normalized = options.Normalize(SettingsSafetyLimits);
        return new RealPhprGearPulseRoutingSetting
        {
            Brake = RealPhprGearPulseSetting.From(normalized.BrakeGearPulse),
            Throttle = RealPhprGearPulseSetting.From(normalized.ThrottleGearPulse)
        };
    }

    public static PHprRoadVibrationRouterOptions CreateRealRoadVibrationRouterOptions(RealPhprRoadVibrationRoutingSetting setting)
    {
        return PHprRoadVibrationRouterOptions.Disabled with
        {
            IsEnabled = setting.IsEnabled,
            Brake = CreateRealRoadVibrationPedalSettings(setting.Brake),
            Throttle = CreateRealRoadVibrationPedalSettings(setting.Throttle)
        };
    }

    public static RealPhprRoadVibrationRoutingSetting CreateRealPhprRoadVibrationRoutingSetting(PHprRoadVibrationRouterOptions options)
    {
        var normalized = options.Normalize(SettingsSafetyLimits);
        return new RealPhprRoadVibrationRoutingSetting
        {
            IsEnabled = normalized.IsEnabled,
            Brake = RealPhprRoadVibrationPedalSetting.From(normalized.Brake),
            Throttle = RealPhprRoadVibrationPedalSetting.From(normalized.Throttle)
        };
    }

    public static PHprSlipLockRouterOptions CreateRealSlipLockRouterOptions(RealPhprSlipLockRoutingSetting setting)
    {
        var wheelSlip = CreateRealSlipLockEffectSettings(PHprPedalEffectKind.WheelSlip, setting.WheelSlip);
        var wheelLock = CreateRealSlipLockEffectSettings(PHprPedalEffectKind.WheelLock, setting.WheelLock);
        return PHprSlipLockRouterOptions.Disabled with
        {
            IsEnabled = wheelSlip.IsEnabled || wheelLock.IsEnabled,
            WheelSlip = wheelSlip,
            WheelLock = wheelLock
        };
    }

    public static RealPhprSlipLockRoutingSetting CreateRealPhprSlipLockRoutingSetting(PHprSlipLockRouterOptions options)
    {
        var normalized = options.Normalize(SettingsSafetyLimits);
        return new RealPhprSlipLockRoutingSetting
        {
            IsEnabled = normalized.WheelSlip.IsEnabled || normalized.WheelLock.IsEnabled,
            WheelSlip = RealPhprSlipLockEffectSetting.From(PHprPedalEffectKind.WheelSlip, normalized.WheelSlip),
            WheelLock = RealPhprSlipLockEffectSetting.From(PHprPedalEffectKind.WheelLock, normalized.WheelLock)
        };
    }

    private static PHprRealGearPulseSettings CreateRealGearPulseSettings(RealPhprGearPulseSetting setting)
    {
        return new PHprRealGearPulseSettings
        {
            IsEnabled = setting.IsEnabled,
            Strength01 = setting.Strength01,
            FrequencyHz = setting.FrequencyHz,
            DurationMs = setting.DurationMs
        }.Normalize(SettingsSafetyLimits);
    }

    private static PHprRoadVibrationPedalSettings CreateRealRoadVibrationPedalSettings(RealPhprRoadVibrationPedalSetting setting)
    {
        return new PHprRoadVibrationPedalSettings
        {
            IsEnabled = setting.IsEnabled,
            MinimumStrength01 = setting.MinimumStrength01,
            Strength01 = setting.Strength01,
            MinimumFrequencyHz = setting.MinimumFrequencyHz,
            FrequencyHz = setting.FrequencyHz,
            DurationMs = setting.DurationMs
        }.Normalize(SettingsSafetyLimits);
    }

    private static PHprSlipLockEffectSettings CreateRealSlipLockEffectSettings(
        PHprPedalEffectKind kind,
        RealPhprSlipLockEffectSetting setting)
    {
        return new PHprSlipLockEffectSettings
        {
            IsEnabled = setting.IsEnabled,
            TargetModule = setting.TargetModule,
            MinimumStrength01 = setting.MinimumStrength01,
            Strength01 = setting.Strength01,
            MinimumFrequencyHz = setting.MinimumFrequencyHz,
            FrequencyHz = setting.FrequencyHz,
            TextureCadenceMs = setting.TextureCadenceMs,
            DurationMs = setting.DurationMs
        }.Normalize(kind, SettingsSafetyLimits);
    }

    private static PHprPedalEffectState CreatePedalEffectState(
        PHprPedalEffectKind kind,
        MockPedalEffectSetting setting)
    {
        var defaults = PHprPedalEffectState.DefaultFor(kind);
        return defaults with
        {
            IsEnabled = setting.IsEnabled,
            TargetModule = setting.TargetModule,
            Profile = defaults.Profile with
            {
                Strength01 = setting.Strength01,
                FrequencyHz = setting.FrequencyHz,
                DurationMs = setting.DurationMs
            }
        };
    }

    private static MockPedalEffectSetting CreateMockPedalEffectSetting(PHprPedalEffectState state)
    {
        return new MockPedalEffectSetting
        {
            IsEnabled = state.IsEnabled,
            TargetModule = state.TargetModule,
            Strength01 = state.Profile.Strength01,
            FrequencyHz = state.Profile.FrequencyHz,
            DurationMs = state.Profile.DurationMs
        };
    }
}

internal static class PersistedSettingsStatusPresenter
{
    public static PersistedSettingsStatusPresentation Build(PersistedSettingsStatusSnapshot? snapshot)
    {
        var resolved = snapshot ?? CreateDefaultSnapshot();
        return new PersistedSettingsStatusPresentation(
            StatusText: $"Theme: {(resolved.UseLightTheme ? "Light" : "Dark")}. Active profile: {Normalize(resolved.ActiveProfileName, "Default")}. Saved output mode {resolved.SelectedOutputKind}; replay {Normalize(resolved.ReplayTimingLabel, "Real time")}; forwarding destinations {resolved.ForwardingDestinationCount}. Saved ASIO driver {Normalize(resolved.SelectedAsioDriverName, "none")}; channel {(resolved.SelectedAsioOutputChannel is null ? "none" : resolved.SelectedAsioOutputChannel)}; Arm ASIO preference {resolved.ArmAsioPreference}. Paddle mapping left {FormatButtonMapping(resolved.PaddleMapping.LeftPaddleButtonId)}, right {FormatButtonMapping(resolved.PaddleMapping.RightPaddleButtonId)}, debounce {resolved.PaddleMapping.DebounceDuration.TotalMilliseconds:0} ms. BST-1 local gear {(resolved.Bst1PaddleGearPulseEnabled ? "enabled" : "disabled")} at {resolved.Bst1PaddleGearStrengthPercent:0}% / {resolved.Bst1PaddleGearFrequencyHz:0.#} Hz / {resolved.EffectiveBst1PaddleGearDurationMs} ms. Shift intent {(resolved.ShiftIntentEnabled ? "enabled" : "disabled")} mode {resolved.ShiftIntentMode}. Real P-HPR direct control {(resolved.RealDirectControlEnabled ? "enabled" : "disabled")} runtime-only. Real slip/lock {(resolved.RealSlipLockEnabled ? "enabled" : "disabled")}. Mock gear routing {(resolved.MockGearRoutingEnabled ? "enabled" : "disabled")} target {resolved.MockGearRoutingTarget}. Mock pedal effects {(resolved.MockPedalEffectsEnabled ? "enabled" : "disabled")}. Haptics running, emergency mute, active pulses, pending stops, direct enable/arm/private device, paddle bench enable, and manual ASIO test active state are not saved. {resolved.SettingsError ?? string.Empty}".Trim(),
            PathText: $"App settings path: {Normalize(resolved.SettingsPath, "unknown")}",
            DiagnosticsText: $"{Normalize(resolved.SettingsPath, "unknown")}; {Normalize(resolved.SettingsError, "loaded")}; theme {(resolved.UseLightTheme ? "light" : "dark")}; output mode {resolved.SelectedOutputKind}; replay {Normalize(resolved.ReplayTimingLabel, "Real time")}; persisted ASIO driver {Normalize(resolved.SelectedAsioDriverName, "none")}; persisted ASIO channel {(resolved.SelectedAsioOutputChannel is null ? "none" : resolved.SelectedAsioOutputChannel)}; persisted Arm ASIO preference {resolved.ArmAsioPreference}; persisted paddle mapping device {Normalize(resolved.PaddleMapping.SelectedDeviceId, "none")} left {FormatButtonMapping(resolved.PaddleMapping.LeftPaddleButtonId)} right {FormatButtonMapping(resolved.PaddleMapping.RightPaddleButtonId)} debounce {resolved.PaddleMapping.DebounceDuration.TotalMilliseconds:0} ms; shift intent {(resolved.ShiftIntentEnabled ? "enabled" : "disabled")} mode {resolved.ShiftIntentMode}; BST-1 local gear {(resolved.Bst1PaddleGearPulseEnabled ? "enabled" : "disabled")} {resolved.Bst1PaddleGearStrengthPercent:0}% {resolved.Bst1PaddleGearFrequencyHz:0.#} Hz {resolved.EffectiveBst1PaddleGearDurationMs} ms; mock gear routing {(resolved.MockGearRoutingEnabled ? "enabled" : "disabled")} target {resolved.MockGearRoutingTarget}; mock pedal effects {(resolved.MockPedalEffectsEnabled ? "enabled" : "disabled")}; real road vibration {(resolved.RealRoadVibrationEnabled ? "enabled" : "disabled")}; real slip/lock {(resolved.RealSlipLockEnabled ? "enabled" : "disabled")}; haptics running state, emergency mute, active pulses, pending stops, P-HPR real direct-control enabled/selected private device, P-HPR emergency stop state, safety latch state, paddle bench enable state, manual ASIO test active state, flight-recorder history, and mock histories are not persisted.");
    }

    private static PersistedSettingsStatusSnapshot CreateDefaultSnapshot()
    {
        return new PersistedSettingsStatusSnapshot(
            SettingsPath: "unknown",
            SettingsError: null,
            UseLightTheme: false,
            ActiveProfileName: "Default",
            SelectedOutputKind: AudioOutputDeviceKind.Null,
            ReplayTimingLabel: "Real time",
            ForwardingDestinationCount: 0,
            SelectedAsioDriverName: null,
            SelectedAsioOutputChannel: null,
            ArmAsioPreference: false,
            PaddleMapping: WheelPaddleMapping.Default,
            Bst1PaddleGearPulseEnabled: true,
            Bst1PaddleGearStrengthPercent: 50f,
            Bst1PaddleGearFrequencyHz: 50f,
            EffectiveBst1PaddleGearDurationMs: Bst1GearPulseDurationSync.DefaultGearDurationMs,
            ShiftIntentEnabled: true,
            ShiftIntentMode: ShiftIntentMode.InstantPaddleOnly,
            RealDirectControlEnabled: false,
            RealRoadVibrationEnabled: false,
            RealSlipLockEnabled: false,
            MockGearRoutingEnabled: false,
            MockGearRoutingTarget: PHprGearPulseTarget.Brake,
            MockPedalEffectsEnabled: false);
    }

    private static string FormatButtonMapping(int? buttonId)
    {
        return buttonId is null ? "unmapped" : $"button {buttonId}";
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }
}
