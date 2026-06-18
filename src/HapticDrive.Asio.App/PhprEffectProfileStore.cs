using System.IO;
using System.Text.Json;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Core.Persistence;

namespace HapticDrive.Asio.App;

internal enum PhprEffectProfileLoadStatus
{
    Success,
    FileNotFound,
    UnsupportedVersion,
    Corrupt,
    Failure
}

internal enum PhprEffectProfileSaveStatus
{
    Success,
    Failure
}

internal sealed record PhprEffectProfile(
    int Version,
    string Name,
    ShiftIntentSetting ShiftIntent,
    MockGearPulseRoutingSetting MockGearPulseRouting,
    MockPedalEffectsRoutingSetting MockPedalEffectsRouting,
    RealPhprGearPulseRoutingSetting RealPhprGearPulseRouting,
    RealPhprRoadVibrationRoutingSetting RealPhprRoadVibrationRouting,
    RealPhprSlipLockRoutingSetting RealPhprSlipLockRouting)
{
    public const int CurrentVersion = 1;

    public static PhprEffectProfile Default { get; } = FromAppSettings(
        "Default P-HPR Effects",
        AppSettings.Default);

    public static PhprEffectProfile FromAppSettings(string name, AppSettings settings)
    {
        var sanitized = AppSettingsStore.Sanitize(settings);
        return new PhprEffectProfile(
            CurrentVersion,
            string.IsNullOrWhiteSpace(name) ? "Default P-HPR Effects" : name.Trim(),
            sanitized.ShiftIntent,
            sanitized.MockGearPulseRouting,
            sanitized.MockPedalEffectsRouting,
            sanitized.RealPhprGearPulseRouting,
            sanitized.RealPhprRoadVibrationRouting,
            sanitized.RealPhprSlipLockRouting);
    }

    public AppSettings ApplyTo(AppSettings baseSettings)
    {
        return AppSettingsStore.Sanitize(baseSettings with
        {
            ShiftIntent = ShiftIntent,
            MockGearPulseRouting = MockGearPulseRouting,
            MockPedalEffectsRouting = MockPedalEffectsRouting,
            RealPhprGearPulseRouting = RealPhprGearPulseRouting,
            RealPhprRoadVibrationRouting = RealPhprRoadVibrationRouting,
            RealPhprSlipLockRouting = RealPhprSlipLockRouting
        });
    }
}

internal sealed record PhprEffectProfileValidationResult(
    PhprEffectProfile Profile,
    bool IsSupportedVersion,
    bool WasRepaired,
    IReadOnlyList<string> Messages);

internal sealed record PhprEffectProfileLoadResult(
    PhprEffectProfileLoadStatus Status,
    PhprEffectProfile? Profile,
    string Message,
    bool WasRepaired,
    IReadOnlyList<string> ValidationMessages)
{
    public bool Succeeded => Status == PhprEffectProfileLoadStatus.Success;

    public static PhprEffectProfileLoadResult Success(
        PhprEffectProfile profile,
        bool wasRepaired,
        IReadOnlyList<string> validationMessages)
    {
        return new(
            PhprEffectProfileLoadStatus.Success,
            profile,
            wasRepaired ? "P-HPR profile loaded with safe repairs." : "P-HPR profile loaded.",
            wasRepaired,
            validationMessages);
    }

    public static PhprEffectProfileLoadResult FileNotFound(string message)
    {
        return new(PhprEffectProfileLoadStatus.FileNotFound, null, message, WasRepaired: false, []);
    }

    public static PhprEffectProfileLoadResult UnsupportedVersion(string message)
    {
        return new(PhprEffectProfileLoadStatus.UnsupportedVersion, null, message, WasRepaired: false, []);
    }

    public static PhprEffectProfileLoadResult Corrupt(string message)
    {
        return new(PhprEffectProfileLoadStatus.Corrupt, null, message, WasRepaired: false, []);
    }

    public static PhprEffectProfileLoadResult Failure(string message)
    {
        return new(PhprEffectProfileLoadStatus.Failure, null, message, WasRepaired: false, []);
    }
}

internal sealed record PhprEffectProfileSaveResult(
    PhprEffectProfileSaveStatus Status,
    string Path,
    string Message,
    bool WasRepaired,
    IReadOnlyList<string> ValidationMessages)
{
    public bool Succeeded => Status == PhprEffectProfileSaveStatus.Success;

    public static PhprEffectProfileSaveResult Success(
        string path,
        bool wasRepaired,
        IReadOnlyList<string> validationMessages)
    {
        return new(
            PhprEffectProfileSaveStatus.Success,
            path,
            wasRepaired ? "P-HPR profile saved with safe repairs." : "P-HPR profile saved.",
            wasRepaired,
            validationMessages);
    }

    public static PhprEffectProfileSaveResult Failure(string path, string message)
    {
        return new(PhprEffectProfileSaveStatus.Failure, path, message, WasRepaired: false, []);
    }
}

internal static class PhprEffectProfileValidator
{
    public static PhprEffectProfileValidationResult Validate(PhprEffectProfile? profile)
    {
        var messages = new List<string>();
        if (profile is null)
        {
            messages.Add("P-HPR profile was missing; safe defaults were used.");
            return new PhprEffectProfileValidationResult(
                PhprEffectProfile.Default,
                IsSupportedVersion: true,
                WasRepaired: true,
                messages);
        }

        if (profile.Version != PhprEffectProfile.CurrentVersion)
        {
            messages.Add($"P-HPR profile version {profile.Version} is not supported.");
            return new PhprEffectProfileValidationResult(
                PhprEffectProfile.Default with { Name = SafeName(profile.Name) },
                IsSupportedVersion: false,
                WasRepaired: true,
                messages);
        }

        var repaired = false;
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            repaired = true;
            messages.Add("P-HPR profile name was missing; a safe name was used.");
        }

        if (profile.ShiftIntent is null)
        {
            repaired = true;
            messages.Add("P-HPR shift-intent settings were missing; defaults were used.");
        }

        if (profile.MockGearPulseRouting is null)
        {
            repaired = true;
            messages.Add("Mock P-HPR gear settings were missing; defaults were used.");
        }

        if (profile.MockPedalEffectsRouting is null)
        {
            repaired = true;
            messages.Add("Mock P-HPR pedal-effect settings were missing; defaults were used.");
        }

        if (profile.RealPhprGearPulseRouting is null)
        {
            repaired = true;
            messages.Add("Real P-HPR gear settings were missing; defaults were used.");
        }

        if (profile.RealPhprRoadVibrationRouting is null)
        {
            repaired = true;
            messages.Add("Real P-HPR road settings were missing; defaults were used.");
        }

        if (profile.RealPhprSlipLockRouting is null)
        {
            repaired = true;
            messages.Add("Real P-HPR slip/lock settings were missing; defaults were used.");
        }

        var sanitizedSettings = AppSettingsStore.Sanitize(new AppSettings
        {
            ShiftIntent = profile.ShiftIntent ?? new ShiftIntentSetting(),
            MockGearPulseRouting = profile.MockGearPulseRouting ?? new MockGearPulseRoutingSetting(),
            MockPedalEffectsRouting = profile.MockPedalEffectsRouting ?? new MockPedalEffectsRoutingSetting(),
            RealPhprGearPulseRouting = profile.RealPhprGearPulseRouting ?? new RealPhprGearPulseRoutingSetting(),
            RealPhprRoadVibrationRouting = profile.RealPhprRoadVibrationRouting ?? new RealPhprRoadVibrationRoutingSetting(),
            RealPhprSlipLockRouting = profile.RealPhprSlipLockRouting ?? new RealPhprSlipLockRoutingSetting()
        });

        var safeProfile = PhprEffectProfile.FromAppSettings(
            SafeName(profile.Name),
            sanitizedSettings);

        if (!Equals(safeProfile.ShiftIntent, profile.ShiftIntent)
            || !Equals(safeProfile.MockGearPulseRouting, profile.MockGearPulseRouting)
            || !Equals(safeProfile.MockPedalEffectsRouting, profile.MockPedalEffectsRouting)
            || !Equals(safeProfile.RealPhprGearPulseRouting, profile.RealPhprGearPulseRouting)
            || !Equals(safeProfile.RealPhprRoadVibrationRouting, profile.RealPhprRoadVibrationRouting)
            || !Equals(safeProfile.RealPhprSlipLockRouting, profile.RealPhprSlipLockRouting))
        {
            repaired = true;
            messages.Add("P-HPR profile values were clamped to safe software ranges.");
        }

        return new PhprEffectProfileValidationResult(
            safeProfile,
            IsSupportedVersion: true,
            repaired,
            messages);
    }

    private static string SafeName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? "Default P-HPR Effects" : name.Trim();
    }
}

internal sealed class PhprEffectProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string GetDefaultProfilePath()
    {
        return Path.Combine(HapticProfileStore.GetDefaultProfileDirectory(), "p-hpr.hdphprprofile.json");
    }

    public async ValueTask<PhprEffectProfileSaveResult> SaveAsync(
        PhprEffectProfile profile,
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PhprEffectProfileSaveResult.Failure(path, "P-HPR profile path is required.");
        }

        var validation = PhprEffectProfileValidator.Validate(profile);
        if (!validation.IsSupportedVersion)
        {
            return PhprEffectProfileSaveResult.Failure(path, "P-HPR profile version is not supported.");
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            await AtomicFileWriter.WriteAsync(
                    fullPath,
                    async (stream, ct) =>
                    {
                        await JsonSerializer.SerializeAsync(
                                stream,
                                validation.Profile,
                                JsonOptions,
                                ct)
                            .ConfigureAwait(false);
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return PhprEffectProfileSaveResult.Success(
                fullPath,
                validation.WasRepaired,
                validation.Messages);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return PhprEffectProfileSaveResult.Failure(path, $"P-HPR profile could not be saved: {ex.Message}");
        }
    }

    public async ValueTask<PhprEffectProfileLoadResult> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PhprEffectProfileLoadResult.FileNotFound("P-HPR profile path is required.");
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return PhprEffectProfileLoadResult.FileNotFound("P-HPR profile file was not found.");
            }

            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                useAsync: true);
            var profile = await JsonSerializer.DeserializeAsync<PhprEffectProfile>(
                    stream,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            if (profile is null)
            {
                return PhprEffectProfileLoadResult.Corrupt("P-HPR profile file did not contain a profile.");
            }

            if (profile.Version != PhprEffectProfile.CurrentVersion)
            {
                return PhprEffectProfileLoadResult.UnsupportedVersion($"P-HPR profile version {profile.Version} is not supported.");
            }

            var validation = PhprEffectProfileValidator.Validate(profile);
            return PhprEffectProfileLoadResult.Success(
                validation.Profile,
                validation.WasRepaired,
                validation.Messages);
        }
        catch (JsonException ex)
        {
            return PhprEffectProfileLoadResult.Corrupt($"P-HPR profile JSON is corrupt: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return PhprEffectProfileLoadResult.Failure($"P-HPR profile could not be loaded: {ex.Message}");
        }
    }
}
