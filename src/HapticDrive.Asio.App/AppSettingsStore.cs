using System.IO;
using System.Text.Json;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsPath { get; }

    public AppSettingsStore(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? GetDefaultSettingsPath();
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return AppSettings.Default;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return Sanitize(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return AppSettings.Default with
            {
                LastStatusMessage = $"App settings could not be loaded: {ex.Message}"
            };
        }
    }

    public void Save(AppSettings settings)
    {
        var sanitized = Sanitize(settings);
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(sanitized, SerializerOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public static string GetDefaultSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HapticDrive.Asio",
            "appsettings.json");
    }

    internal static AppSettings Sanitize(AppSettings? settings)
    {
        if (settings is null)
        {
            return AppSettings.Default;
        }

        var destinations = settings.ForwardingDestinations
            .Where(destination => !string.IsNullOrWhiteSpace(destination.Host))
            .Select(destination => destination with
            {
                Name = string.IsNullOrWhiteSpace(destination.Name)
                    ? $"{destination.Host.Trim()}:{destination.Port}"
                    : destination.Name.Trim(),
                Host = destination.Host.Trim(),
                Port = Math.Clamp(destination.Port, 1, 65_535)
            })
            .ToList();

        return settings with
        {
            SelectedGameId = GameTelemetryCatalog.NormalizeGameId(settings.SelectedGameId),
            PreferredOutputMode = settings.PreferredOutputMode is null
                ? null
                : Enum.IsDefined(settings.PreferredOutputMode.Value)
                    ? settings.PreferredOutputMode
                    : null,
            PreferredPhprPedalsEnabled = settings.PreferredPhprPedalsEnabled,
            PreferredPhprPedalsMode = settings.PreferredPhprPedalsMode is null
                ? null
                : Enum.IsDefined(settings.PreferredPhprPedalsMode.Value)
                    ? settings.PreferredPhprPedalsMode
                    : null,
            LastAsioDriverName = string.IsNullOrWhiteSpace(settings.LastAsioDriverName)
                ? null
                : settings.LastAsioDriverName.Trim(),
            LastAsioOutputChannel = settings.LastAsioOutputChannel is >= 0 and <= 63
                ? settings.LastAsioOutputChannel
                : null,
            ReplayTimingPreference = Enum.IsDefined(settings.ReplayTimingPreference)
                ? settings.ReplayTimingPreference
                : ReplayTimingPreference.RealTime,
            ForwardingDestinations = destinations,
            PaddleInputMapping = SanitizePaddleInputMapping(settings.PaddleInputMapping),
            Bst1PaddleGearPulse = SanitizeBst1PaddleGearPulse(settings.Bst1PaddleGearPulse),
            ShiftIntent = SanitizeShiftIntent(settings.ShiftIntent),
            MockGearPulseRouting = SanitizeMockGearPulseRouting(settings.MockGearPulseRouting),
            MockPedalEffectsRouting = SanitizeMockPedalEffectsRouting(settings.MockPedalEffectsRouting),
            RealPhprGearPulseRouting = SanitizeRealPhprGearPulseRouting(settings.RealPhprGearPulseRouting),
            RealPhprRoadVibrationRouting = SanitizeRealPhprRoadVibrationRouting(settings.RealPhprRoadVibrationRouting),
            RealPhprSlipLockRouting = SanitizeRealPhprSlipLockRouting(settings.RealPhprSlipLockRouting)
        };
    }

    private static PaddleInputMappingSetting SanitizePaddleInputMapping(PaddleInputMappingSetting setting)
    {
        var method = Enum.IsDefined(setting.SelectedMethod)
            ? setting.SelectedMethod
            : InputDiscoveryMethod.WindowsGameController;
        var debounce = Math.Clamp(
            setting.DebounceMilliseconds,
            0,
            250);

        return setting with
        {
            SelectedDeviceId = string.IsNullOrWhiteSpace(setting.SelectedDeviceId)
                ? null
                : setting.SelectedDeviceId.Trim(),
            SelectedMethod = method,
            LeftPaddleButtonId = NormalizeButtonId(setting.LeftPaddleButtonId),
            RightPaddleButtonId = NormalizeButtonId(setting.RightPaddleButtonId),
            DebounceMilliseconds = debounce
        };
    }

    private static Bst1PaddleGearPulseSetting SanitizeBst1PaddleGearPulse(Bst1PaddleGearPulseSetting? setting)
    {
        if (setting is null)
        {
            return new Bst1PaddleGearPulseSetting();
        }

        return setting with
        {
            StrengthPercent = float.IsFinite(setting.StrengthPercent)
                ? Math.Clamp(setting.StrengthPercent, 0f, 100f)
                : 50f,
            FrequencyHz = float.IsFinite(setting.FrequencyHz)
                ? Math.Clamp(
                    setting.FrequencyHz,
                    ManualAsioHardwareTestRequest.MinimumFrequencyHz,
                    ManualAsioHardwareTestRequest.MaximumFrequencyHz)
                : 50f,
            CustomDurationMs = Math.Clamp(
                setting.CustomDurationMs,
                ManualAsioHardwareTestRequest.MinimumDurationMilliseconds,
                (int)ManualAsioHardwareTestRequest.MaximumDuration.TotalMilliseconds)
        };
    }

    private static ShiftIntentSetting SanitizeShiftIntent(ShiftIntentSetting? setting)
    {
        if (setting is null)
        {
            return new ShiftIntentSetting();
        }

        var mode = Enum.IsDefined(setting.Mode)
            ? setting.Mode
            : ShiftIntentMode.InstantPaddleOnly;

        return setting with
        {
            Mode = mode
        };
    }

    private static MockGearPulseRoutingSetting SanitizeMockGearPulseRouting(MockGearPulseRoutingSetting? setting)
    {
        if (setting is null)
        {
            return new MockGearPulseRoutingSetting();
        }

        var target = Enum.IsDefined(setting.TargetModule)
            ? setting.TargetModule
            : PHprGearPulseTarget.Both;

        return setting with
        {
            TargetModule = target,
            Strength01 = double.IsFinite(setting.Strength01)
                ? Math.Clamp(setting.Strength01, 0d, 1d)
                : 0.05d,
            FrequencyHz = double.IsFinite(setting.FrequencyHz)
                ? PhprUiValueConverter.ClampFrequencyHz(setting.FrequencyHz)
                : 50d,
            DurationMs = PhprUiValueConverter.ClampDurationMs(setting.DurationMs)
        };
    }

    private static MockPedalEffectsRoutingSetting SanitizeMockPedalEffectsRouting(MockPedalEffectsRoutingSetting? setting)
    {
        if (setting is null)
        {
            return new MockPedalEffectsRoutingSetting();
        }

        return setting with
        {
            RoadVibration = SanitizeMockPedalEffect(setting.RoadVibration, PHprPedalEffectKind.RoadVibration),
            WheelSlip = SanitizeMockPedalEffect(setting.WheelSlip, PHprPedalEffectKind.WheelSlip),
            WheelLock = SanitizeMockPedalEffect(setting.WheelLock, PHprPedalEffectKind.WheelLock)
        };
    }

    private static RealPhprGearPulseRoutingSetting SanitizeRealPhprGearPulseRouting(RealPhprGearPulseRoutingSetting? setting)
    {
        if (setting is null)
        {
            return new RealPhprGearPulseRoutingSetting();
        }

        return setting with
        {
            Brake = SanitizeRealPhprGearPulseSetting(setting.Brake),
            Throttle = SanitizeRealPhprGearPulseSetting(setting.Throttle)
        };
    }

    private static RealPhprGearPulseSetting SanitizeRealPhprGearPulseSetting(RealPhprGearPulseSetting? setting)
    {
        if (setting is null)
        {
            return RealPhprGearPulseSetting.Default;
        }

        var limits = SimagicPhprOutputDevice.DirectControlSafetyLimits;
        var normalized = new PHprRealGearPulseSettings
        {
            IsEnabled = setting.IsEnabled,
            Strength01 = setting.Strength01,
            FrequencyHz = setting.FrequencyHz,
            DurationMs = setting.DurationMs
        }.Normalize(limits);

        return setting with
        {
            IsEnabled = normalized.IsEnabled,
            Strength01 = normalized.Strength01,
            FrequencyHz = normalized.FrequencyHz,
            DurationMs = normalized.DurationMs
        };
    }

    private static RealPhprRoadVibrationRoutingSetting SanitizeRealPhprRoadVibrationRouting(RealPhprRoadVibrationRoutingSetting? setting)
    {
        if (setting is null)
        {
            return new RealPhprRoadVibrationRoutingSetting();
        }

        return setting with
        {
            Brake = SanitizeRealPhprRoadVibrationPedalSetting(setting.Brake),
            Throttle = SanitizeRealPhprRoadVibrationPedalSetting(setting.Throttle)
        };
    }

    private static RealPhprRoadVibrationPedalSetting SanitizeRealPhprRoadVibrationPedalSetting(RealPhprRoadVibrationPedalSetting? setting)
    {
        if (setting is null)
        {
            return RealPhprRoadVibrationPedalSetting.Default;
        }

        var normalized = new PHprRoadVibrationPedalSettings
        {
            IsEnabled = setting.IsEnabled,
            MinimumStrength01 = setting.MinimumStrength01,
            Strength01 = setting.Strength01,
            MinimumFrequencyHz = setting.MinimumFrequencyHz,
            FrequencyHz = setting.FrequencyHz,
            DurationMs = setting.DurationMs
        }.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);

        return setting with
        {
            IsEnabled = normalized.IsEnabled,
            MinimumStrength01 = normalized.MinimumStrength01,
            Strength01 = normalized.Strength01,
            MinimumFrequencyHz = normalized.MinimumFrequencyHz,
            FrequencyHz = normalized.FrequencyHz,
            DurationMs = normalized.DurationMs
        };
    }

    private static RealPhprSlipLockRoutingSetting SanitizeRealPhprSlipLockRouting(RealPhprSlipLockRoutingSetting? setting)
    {
        if (setting is null)
        {
            return new RealPhprSlipLockRoutingSetting();
        }

        return setting with
        {
            WheelSlip = SanitizeRealPhprSlipLockEffectSetting(setting.WheelSlip, PHprPedalEffectKind.WheelSlip),
            WheelLock = SanitizeRealPhprSlipLockEffectSetting(setting.WheelLock, PHprPedalEffectKind.WheelLock)
        };
    }

    private static RealPhprSlipLockEffectSetting SanitizeRealPhprSlipLockEffectSetting(
        RealPhprSlipLockEffectSetting? setting,
        PHprPedalEffectKind kind)
    {
        if (setting is null)
        {
            return RealPhprSlipLockEffectSetting.DefaultFor(kind);
        }

        var normalized = new PHprSlipLockEffectSettings
        {
            IsEnabled = setting.IsEnabled,
            TargetModule = setting.TargetModule,
            MinimumStrength01 = setting.MinimumStrength01,
            Strength01 = setting.Strength01,
            MinimumFrequencyHz = setting.MinimumFrequencyHz,
            FrequencyHz = setting.FrequencyHz,
            TextureCadenceMs = setting.TextureCadenceMs,
            DurationMs = setting.DurationMs
        }.Normalize(kind, SimagicPhprOutputDevice.DirectControlSafetyLimits);

        return setting with
        {
            IsEnabled = normalized.IsEnabled,
            TargetModule = normalized.TargetModule,
            MinimumStrength01 = normalized.MinimumStrength01,
            Strength01 = normalized.Strength01,
            MinimumFrequencyHz = normalized.MinimumFrequencyHz,
            FrequencyHz = normalized.FrequencyHz,
            TextureCadenceMs = normalized.TextureCadenceMs,
            DurationMs = normalized.DurationMs
        };
    }

    private static MockPedalEffectSetting SanitizeMockPedalEffect(
        MockPedalEffectSetting? setting,
        PHprPedalEffectKind kind)
    {
        var defaults = MockPedalEffectSetting.DefaultFor(kind);
        if (setting is null)
        {
            return defaults;
        }

        var target = Enum.IsDefined(setting.TargetModule)
            ? setting.TargetModule
            : defaults.TargetModule;
        var defaultProfile = PHprPedalEffectProfile.DefaultFor(kind);

        return setting with
        {
            TargetModule = target,
            Strength01 = double.IsFinite(setting.Strength01)
                ? Math.Clamp(setting.Strength01, 0d, 1d)
                : defaultProfile.Strength01,
            FrequencyHz = double.IsFinite(setting.FrequencyHz)
                ? PhprUiValueConverter.ClampFrequencyHz(setting.FrequencyHz)
                : defaultProfile.FrequencyHz,
            DurationMs = PhprUiValueConverter.ClampDurationMs(setting.DurationMs)
        };
    }

    private static int? NormalizeButtonId(int? buttonId)
    {
        return buttonId is > 0 and <= 128 ? buttonId : null;
    }
}

internal sealed record AppSettings
{
    public bool UseLightTheme { get; init; }

    public bool AdvancedDiagnosticsEnabled { get; init; }

    public string SelectedGameId { get; init; } = GameTelemetryCatalog.DefaultGameId;

    public AudioOutputDeviceKind? PreferredOutputMode { get; init; }

    public bool? PreferredPhprPedalsEnabled { get; init; }

    public PhprPedalsModePreference? PreferredPhprPedalsMode { get; init; }

    public string? LastAsioDriverName { get; init; }

    public int? LastAsioOutputChannel { get; init; }

    public bool ArmAsioPreference { get; init; }

    public ReplayTimingPreference ReplayTimingPreference { get; init; } = ReplayTimingPreference.RealTime;

    public List<ForwardingDestinationSetting> ForwardingDestinations { get; init; } = [];

    public PaddleInputMappingSetting PaddleInputMapping { get; init; } = new();

    public Bst1PaddleGearPulseSetting Bst1PaddleGearPulse { get; init; } = new();

    public ShiftIntentSetting ShiftIntent { get; init; } = new();

    public MockGearPulseRoutingSetting MockGearPulseRouting { get; init; } = new();

    public MockPedalEffectsRoutingSetting MockPedalEffectsRouting { get; init; } = new();

    public RealPhprGearPulseRoutingSetting RealPhprGearPulseRouting { get; init; } = new();

    public RealPhprRoadVibrationRoutingSetting RealPhprRoadVibrationRouting { get; init; } = new();

    public RealPhprSlipLockRoutingSetting RealPhprSlipLockRouting { get; init; } = new();

    public string? LastStatusMessage { get; init; }

    public static AppSettings Default { get; } = new();
}

internal enum PhprPedalsModePreference
{
    Disabled = 0,
    Mock = 1,
    Direct = 2
}

internal enum ReplayTimingPreference
{
    RealTime = 0,
    FastDebug = 1
}

internal sealed record PaddleInputMappingSetting
{
    public string? SelectedDeviceId { get; init; }

    public InputDiscoveryMethod SelectedMethod { get; init; } = InputDiscoveryMethod.WindowsGameController;

    public int? LeftPaddleButtonId { get; init; } = 14;

    public int? RightPaddleButtonId { get; init; } = 13;

    public int DebounceMilliseconds { get; init; } = (int)WheelPaddleMapping.DefaultDebounceDuration.TotalMilliseconds;
}

internal sealed record Bst1PaddleGearPulseSetting
{
    public bool IsEnabled { get; init; } = true;

    public float StrengthPercent { get; init; } = 50f;

    public float FrequencyHz { get; init; } = 50f;

    public bool UseSharedDuration { get; init; } = true;

    public int CustomDurationMs { get; init; } = Bst1GearPulseDurationSync.DefaultGearDurationMs;
}

internal sealed record ShiftIntentSetting
{
    public bool IsEnabled { get; init; } = true;

    public ShiftIntentMode Mode { get; init; } = ShiftIntentMode.InstantPaddleOnly;
}

internal sealed record MockGearPulseRoutingSetting
{
    public bool IsEnabled { get; init; } = true;

    public PHprGearPulseTarget TargetModule { get; init; } = PHprGearPulseTarget.Both;

    public double Strength01 { get; init; } = 0.05d;

    public double FrequencyHz { get; init; } = 50d;

    public int DurationMs { get; init; } = 50;
}

internal sealed record MockPedalEffectsRoutingSetting
{
    public bool IsEnabled { get; init; } = true;

    public MockPedalEffectSetting RoadVibration { get; init; } =
        MockPedalEffectSetting.DefaultFor(PHprPedalEffectKind.RoadVibration);

    public MockPedalEffectSetting WheelSlip { get; init; } =
        MockPedalEffectSetting.DefaultFor(PHprPedalEffectKind.WheelSlip);

    public MockPedalEffectSetting WheelLock { get; init; } =
        MockPedalEffectSetting.DefaultFor(PHprPedalEffectKind.WheelLock);
}

internal sealed record RealPhprGearPulseRoutingSetting
{
    public RealPhprGearPulseSetting Brake { get; init; } = RealPhprGearPulseSetting.Default;

    public RealPhprGearPulseSetting Throttle { get; init; } = RealPhprGearPulseSetting.Default;
}

internal sealed record RealPhprGearPulseSetting
{
    public static RealPhprGearPulseSetting Default { get; } = From(PHprRealGearPulseSettings.Default);

    public bool IsEnabled { get; init; } = true;

    public double Strength01 { get; init; } = 0.10d;

    public double FrequencyHz { get; init; } = 50d;

    public int DurationMs { get; init; } = 50;

    public static RealPhprGearPulseSetting From(PHprRealGearPulseSettings settings)
    {
        var normalized = settings.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        return new RealPhprGearPulseSetting
        {
            IsEnabled = normalized.IsEnabled,
            Strength01 = normalized.Strength01,
            FrequencyHz = normalized.FrequencyHz,
            DurationMs = normalized.DurationMs
        };
    }
}

internal sealed record RealPhprRoadVibrationRoutingSetting
{
    public bool IsEnabled { get; init; }

    public RealPhprRoadVibrationPedalSetting Brake { get; init; } = RealPhprRoadVibrationPedalSetting.Default;

    public RealPhprRoadVibrationPedalSetting Throttle { get; init; } = RealPhprRoadVibrationPedalSetting.Default;
}

internal sealed record RealPhprRoadVibrationPedalSetting
{
    public static RealPhprRoadVibrationPedalSetting Default { get; } = From(PHprRoadVibrationPedalSettings.Default);

    public bool IsEnabled { get; init; } = true;

    public double MinimumStrength01 { get; init; } = 0.01d;

    public double Strength01 { get; init; } = 0.04d;

    public double MinimumFrequencyHz { get; init; } = 25d;

    public double FrequencyHz { get; init; } = 45d;

    public int DurationMs { get; init; } = 50;

    public static RealPhprRoadVibrationPedalSetting From(PHprRoadVibrationPedalSettings settings)
    {
        var normalized = settings.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        return new RealPhprRoadVibrationPedalSetting
        {
            IsEnabled = normalized.IsEnabled,
            MinimumStrength01 = normalized.MinimumStrength01,
            Strength01 = normalized.Strength01,
            MinimumFrequencyHz = normalized.MinimumFrequencyHz,
            FrequencyHz = normalized.FrequencyHz,
            DurationMs = normalized.DurationMs
        };
    }
}

internal sealed record RealPhprSlipLockRoutingSetting
{
    public bool IsEnabled { get; init; }

    public RealPhprSlipLockEffectSetting WheelSlip { get; init; } =
        RealPhprSlipLockEffectSetting.DefaultFor(PHprPedalEffectKind.WheelSlip);

    public RealPhprSlipLockEffectSetting WheelLock { get; init; } =
        RealPhprSlipLockEffectSetting.DefaultFor(PHprPedalEffectKind.WheelLock);
}

internal sealed record RealPhprSlipLockEffectSetting
{
    public bool IsEnabled { get; init; } = true;

    public PHprGearPulseTarget TargetModule { get; init; } = (PHprGearPulseTarget)(-1);

    public double MinimumStrength01 { get; init; } = 0.03d;

    public double Strength01 { get; init; } = 0.08d;

    public double MinimumFrequencyHz { get; init; } = 45d;

    public double FrequencyHz { get; init; } = 75d;

    public int TextureCadenceMs { get; init; }

    public int DurationMs { get; init; } = 50;

    public static RealPhprSlipLockEffectSetting DefaultFor(PHprPedalEffectKind kind)
    {
        return From(kind, PHprSlipLockEffectSettings.DefaultFor(kind));
    }

    public static RealPhprSlipLockEffectSetting From(
        PHprPedalEffectKind kind,
        PHprSlipLockEffectSettings settings)
    {
        var normalized = settings.Normalize(kind, SimagicPhprOutputDevice.DirectControlSafetyLimits);
        return new RealPhprSlipLockEffectSetting
        {
            IsEnabled = normalized.IsEnabled,
            TargetModule = normalized.TargetModule,
            MinimumStrength01 = normalized.MinimumStrength01,
            Strength01 = normalized.Strength01,
            MinimumFrequencyHz = normalized.MinimumFrequencyHz,
            FrequencyHz = normalized.FrequencyHz,
            TextureCadenceMs = normalized.TextureCadenceMs,
            DurationMs = normalized.DurationMs
        };
    }
}

internal sealed record MockPedalEffectSetting
{
    public bool IsEnabled { get; init; } = true;

    public PHprGearPulseTarget TargetModule { get; init; } = PHprGearPulseTarget.Both;

    public double Strength01 { get; init; } = 0.04d;

    public double FrequencyHz { get; init; } = 45d;

    public int DurationMs { get; init; } = 50;

    public static MockPedalEffectSetting DefaultFor(PHprPedalEffectKind kind)
    {
        var state = PHprPedalEffectState.DefaultFor(kind);
        return new MockPedalEffectSetting
        {
            TargetModule = state.TargetModule,
            Strength01 = state.Profile.Strength01,
            FrequencyHz = state.Profile.FrequencyHz,
            DurationMs = state.Profile.DurationMs
        };
    }
}

internal sealed record ForwardingDestinationSetting
{
    public string Name { get; init; } = "";

    public string Host { get; init; } = "";

    public int Port { get; init; } = 20779;

    public bool Enabled { get; init; } = true;
}
