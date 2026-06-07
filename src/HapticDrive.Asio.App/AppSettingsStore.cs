using System.IO;
using System.Text.Json;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;

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

    private static AppSettings Sanitize(AppSettings? settings)
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
            LastAsioDriverName = string.IsNullOrWhiteSpace(settings.LastAsioDriverName)
                ? null
                : settings.LastAsioDriverName.Trim(),
            LastAsioOutputChannel = settings.LastAsioOutputChannel is >= 0 and <= 63
                ? settings.LastAsioOutputChannel
                : null,
            ForwardingDestinations = destinations,
            PaddleInputMapping = SanitizePaddleInputMapping(settings.PaddleInputMapping),
            ShiftIntent = SanitizeShiftIntent(settings.ShiftIntent),
            MockGearPulseRouting = SanitizeMockGearPulseRouting(settings.MockGearPulseRouting)
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
                ? Math.Clamp(setting.FrequencyHz, 1d, 1_000d)
                : 50d,
            DurationMs = Math.Clamp(setting.DurationMs, 0, 1_000)
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

    public string? LastAsioDriverName { get; init; }

    public int? LastAsioOutputChannel { get; init; }

    public List<ForwardingDestinationSetting> ForwardingDestinations { get; init; } = [];

    public PaddleInputMappingSetting PaddleInputMapping { get; init; } = new();

    public ShiftIntentSetting ShiftIntent { get; init; } = new();

    public MockGearPulseRoutingSetting MockGearPulseRouting { get; init; } = new();

    public string? LastStatusMessage { get; init; }

    public static AppSettings Default { get; } = new();
}

internal sealed record PaddleInputMappingSetting
{
    public string? SelectedDeviceId { get; init; }

    public InputDiscoveryMethod SelectedMethod { get; init; } = InputDiscoveryMethod.WindowsGameController;

    public int? LeftPaddleButtonId { get; init; }

    public int? RightPaddleButtonId { get; init; }

    public int DebounceMilliseconds { get; init; } = (int)WheelPaddleMapping.DefaultDebounceDuration.TotalMilliseconds;
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

internal sealed record ForwardingDestinationSetting
{
    public string Name { get; init; } = "";

    public string Host { get; init; } = "";

    public int Port { get; init; } = 20779;

    public bool Enabled { get; init; } = true;
}
