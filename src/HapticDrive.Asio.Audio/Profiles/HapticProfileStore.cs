using System.Text.Json;
using HapticDrive.Asio.Core.Persistence;
using HapticDrive.Asio.Audio.Effects.Registry;

namespace HapticDrive.Asio.Audio.Profiles;

public enum HapticProfileLoadStatus
{
    Success,
    FileNotFound,
    UnsupportedVersion,
    Corrupt,
    Failure
}

public enum HapticProfileSaveStatus
{
    Success,
    Failure
}

public sealed record HapticProfileLoadResult(
    HapticProfileLoadStatus Status,
    HapticDriveProfile? Profile,
    string Message,
    bool WasRepaired,
    IReadOnlyList<string> ValidationMessages)
{
    public bool Succeeded => Status == HapticProfileLoadStatus.Success;

    public static HapticProfileLoadResult Success(
        HapticDriveProfile profile,
        bool wasRepaired,
        IReadOnlyList<string> validationMessages,
        string? message = null)
    {
        return new(
            HapticProfileLoadStatus.Success,
            profile,
            message ?? (wasRepaired ? "Profile loaded with safe repairs." : "Profile loaded."),
            wasRepaired,
            validationMessages);
    }

    public static HapticProfileLoadResult FileNotFound(string message)
    {
        return new(HapticProfileLoadStatus.FileNotFound, null, message, WasRepaired: false, []);
    }

    public static HapticProfileLoadResult UnsupportedVersion(string message)
    {
        return new(HapticProfileLoadStatus.UnsupportedVersion, null, message, WasRepaired: false, []);
    }

    public static HapticProfileLoadResult Corrupt(string message)
    {
        return new(HapticProfileLoadStatus.Corrupt, null, message, WasRepaired: false, []);
    }

    public static HapticProfileLoadResult Failure(string message)
    {
        return new(HapticProfileLoadStatus.Failure, null, message, WasRepaired: false, []);
    }
}

public sealed record HapticProfileSaveResult(
    HapticProfileSaveStatus Status,
    string Path,
    string Message,
    bool WasRepaired,
    IReadOnlyList<string> ValidationMessages)
{
    public bool Succeeded => Status == HapticProfileSaveStatus.Success;

    public static HapticProfileSaveResult Success(
        string path,
        bool wasRepaired,
        IReadOnlyList<string> validationMessages)
    {
        return new(
            HapticProfileSaveStatus.Success,
            path,
            wasRepaired ? "Profile saved with safe repairs." : "Profile saved.",
            wasRepaired,
            validationMessages);
    }

    public static HapticProfileSaveResult Failure(string path, string message)
    {
        return new(HapticProfileSaveStatus.Failure, path, message, WasRepaired: false, []);
    }
}

public sealed class HapticProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string GetDefaultProfileDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HapticDrive.Asio",
            "Profiles");
    }

    public static string GetDefaultProfilePath()
    {
        return Path.Combine(GetDefaultProfileDirectory(), "default.hdprofile.json");
    }

    public async ValueTask<HapticProfileSaveResult> SaveAsync(
        HapticDriveProfile profile,
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return HapticProfileSaveResult.Failure(path, "Profile path is required.");
        }

        var validation = HapticProfileValidator.Validate(profile);
        if (!validation.IsSupportedVersion)
        {
            return HapticProfileSaveResult.Failure(path, "Profile version is not supported.");
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var document = HapticDriveProfileDocumentV2.FromProfile(validation.Profile);
            await AtomicFileWriter.WriteAsync(
                    fullPath,
                    async (stream, ct) =>
                    {
                        await JsonSerializer.SerializeAsync(
                                stream,
                                document,
                                JsonOptions,
                                ct)
                            .ConfigureAwait(false);
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            _ = DocumentBackupFile.TryRefreshFromPrimary(fullPath);
            _ = DocumentBackupHistory.TryRefreshFromPrimary(fullPath);

            return HapticProfileSaveResult.Success(
                fullPath,
                validation.WasRepaired,
                validation.Messages);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return HapticProfileSaveResult.Failure(path, $"Profile could not be saved: {ex.Message}");
        }
    }

    public async ValueTask<HapticProfileLoadResult> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var primary = await LoadSingleAsync(path, cancellationToken).ConfigureAwait(false);
        if (primary.Succeeded)
        {
            return primary;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return primary;
        }

        var backupPath = DocumentBackupFile.GetBackupPath(path);
        var backup = File.Exists(backupPath)
            ? await LoadSingleAsync(backupPath, cancellationToken).ConfigureAwait(false)
            : HapticProfileLoadResult.FileNotFound("Backup profile file was not found.");
        if (!backup.Succeeded || backup.Profile is null)
        {
            foreach (var historyPath in DocumentBackupHistory.GetHistoryPathsNewestFirst(path))
            {
                var history = await LoadSingleAsync(historyPath, cancellationToken).ConfigureAwait(false);
                if (!history.Succeeded || history.Profile is null)
                {
                    continue;
                }

                var historyMessages = new List<string>(history.ValidationMessages)
                {
                    $"Recovered profile from backup history snapshot {Path.GetFileName(historyPath)} after the primary file could not be used."
                };
                return HapticProfileLoadResult.Success(
                    history.Profile,
                    wasRepaired: true,
                    historyMessages,
                    "Profile recovered from backup history snapshot.");
            }

            return backup.Status == HapticProfileLoadStatus.FileNotFound
                ? primary
                : HapticProfileLoadResult.Failure(
                    $"{primary.Message} Backup profile could not be loaded: {backup.Message}");
        }

        var messages = new List<string>(backup.ValidationMessages)
        {
            $"Recovered profile from backup snapshot {Path.GetFileName(backupPath)} after the primary file could not be used."
        };
        return HapticProfileLoadResult.Success(
            backup.Profile,
            wasRepaired: true,
            messages,
            "Profile recovered from backup snapshot.");
    }

    private static async ValueTask<HapticProfileLoadResult> LoadSingleAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return HapticProfileLoadResult.FileNotFound("Profile path is required.");
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return HapticProfileLoadResult.FileNotFound("Profile file was not found.");
            }

            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                useAsync: true);
            var json = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            var sourceVersion = VersionedDocumentMigration.ReadDeclaredVersion(json, "SchemaVersion");
            sourceVersion = sourceVersion == 0
                ? VersionedDocumentMigration.ReadDeclaredVersion(json, "Version")
                : sourceVersion;

            if (sourceVersion > HapticDriveProfile.CurrentVersion)
            {
                return HapticProfileLoadResult.UnsupportedVersion($"Profile version {sourceVersion} is not supported.");
            }

            HapticDriveProfile? profile;
            List<string> migrationMessages = [];
            var registry = BuiltInHapticEffectRegistry.Instance;
            if (sourceVersion >= HapticDriveProfile.CurrentVersion)
            {
                var document = JsonSerializer.Deserialize<HapticDriveProfileDocumentV2>(json, JsonOptions);
                profile = document?.ToProfile(registry, migrationMessages);
            }
            else
            {
                profile = await JsonSerializer.DeserializeAsync<HapticDriveProfile>(
                        stream,
                        JsonOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (profile is null)
                {
                    return HapticProfileLoadResult.Corrupt("Profile file did not contain a profile.");
                }

                var migratedEffectSettings = HapticEffectSettingsTranslator.CreateDocumentsFromLegacy(
                    profile.Effects ?? HapticDriveProfile.Default.Effects,
                    registry);
                profile = profile with
                {
                    Version = HapticDriveProfile.CurrentVersion,
                    SchemaVersion = HapticDriveProfile.CurrentVersion,
                    EffectSettings = migratedEffectSettings
                };

                migrationMessages.Add(sourceVersion switch
                {
                    0 => $"Legacy document version 0 was migrated to version {HapticDriveProfile.CurrentVersion}.",
                    1 => $"Profile schema version 1 was migrated to version {HapticDriveProfile.CurrentVersion}.",
                    _ => $"Profile schema version {sourceVersion} was migrated to version {HapticDriveProfile.CurrentVersion}."
                });
            }

            if (profile is null)
            {
                return HapticProfileLoadResult.Corrupt("Profile file did not contain a profile.");
            }

            var validation = HapticProfileValidator.Validate(profile);
            var messages = migrationMessages.Concat(validation.Messages).ToArray();
            return HapticProfileLoadResult.Success(
                validation.Profile,
                migrationMessages.Count > 0 || validation.WasRepaired,
                messages);
        }
        catch (JsonException ex)
        {
            return HapticProfileLoadResult.Corrupt($"Profile JSON is corrupt: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return HapticProfileLoadResult.Failure($"Profile could not be loaded: {ex.Message}");
        }
    }
}

internal sealed record HapticDriveProfileDocumentV2(
    int SchemaVersion,
    string Name,
    IReadOnlyDictionary<string, EffectSettingsDocument> Effects,
    HapticMixerTuning Mixer,
    HapticSafetyTuning Safety)
{
    public static HapticDriveProfileDocumentV2 FromProfile(HapticDriveProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var known = profile.EffectSettings ?? HapticEffectSettingsTranslator.CreateDocumentsFromLegacy(profile.Effects, BuiltInHapticEffectRegistry.Instance);
        var merged = new Dictionary<string, EffectSettingsDocument>(known, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in profile.UnknownEffectSettings ?? new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase))
        {
            merged[pair.Key] = pair.Value;
        }

        return new HapticDriveProfileDocumentV2(
            SchemaVersion: HapticDriveProfile.CurrentVersion,
            Name: profile.Name,
            Effects: merged,
            Mixer: profile.Mixer,
            Safety: profile.Safety);
    }

    public HapticDriveProfile ToProfile(
        IHapticEffectRegistry registry,
        ICollection<string>? messages = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var known = new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase);
        var unknown = new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in Effects ?? new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase))
        {
            if (registry.All.Any(descriptor => string.Equals(descriptor.Key, pair.Key, StringComparison.OrdinalIgnoreCase)))
            {
                known[pair.Key] = pair.Value;
            }
            else
            {
                unknown[pair.Key] = pair.Value;
            }
        }

        var normalizedKnown = HapticEffectSettingsTranslator.NormalizeDocuments(
            known.Count > 0 ? known : null,
            registry,
            messages);
        var legacyTuning = HapticEffectSettingsTranslator.ToLegacyTuning(normalizedKnown);
        return new HapticDriveProfile(
            Version: HapticDriveProfile.CurrentVersion,
            Name: Name,
            Effects: legacyTuning,
            Mixer: Mixer,
            Safety: Safety)
        {
            SchemaVersion = SchemaVersion,
            EffectSettings = normalizedKnown,
            UnknownEffectSettings = unknown
        };
    }
}
