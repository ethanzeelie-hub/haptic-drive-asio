using System.IO;
using System.Text.Json;

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
            ForwardingDestinations = destinations
        };
    }
}

internal sealed record AppSettings
{
    public bool UseLightTheme { get; init; }

    public string? LastAsioDriverName { get; init; }

    public int? LastAsioOutputChannel { get; init; }

    public List<ForwardingDestinationSetting> ForwardingDestinations { get; init; } = [];

    public string? LastStatusMessage { get; init; }

    public static AppSettings Default { get; } = new();
}

internal sealed record ForwardingDestinationSetting
{
    public string Name { get; init; } = "";

    public string Host { get; init; } = "";

    public int Port { get; init; } = 20779;

    public bool Enabled { get; init; } = true;
}
