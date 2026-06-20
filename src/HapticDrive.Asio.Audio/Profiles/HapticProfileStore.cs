using System.Text.Json;
using HapticDrive.Asio.Core.Persistence;

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
            _ = DocumentBackupFile.TryRefreshFromPrimary(fullPath);

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
        if (!File.Exists(backupPath))
        {
            return primary;
        }

        var backup = await LoadSingleAsync(backupPath, cancellationToken).ConfigureAwait(false);
        if (!backup.Succeeded || backup.Profile is null)
        {
            return HapticProfileLoadResult.Failure(
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
            var sourceVersion = VersionedDocumentMigration.ReadDeclaredVersion(await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false));
            var profile = await JsonSerializer.DeserializeAsync<HapticDriveProfile>(
                    stream,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            if (profile is null)
            {
                return HapticProfileLoadResult.Corrupt("Profile file did not contain a profile.");
            }

            var migration = VersionedDocumentMigration.Plan(
                profile,
                sourceVersion,
                HapticDriveProfile.CurrentVersion,
                legacy => legacy with { Version = HapticDriveProfile.CurrentVersion },
                version => $"Profile version {version} is not supported.");

            if (!migration.IsSupportedVersion || migration.Document is null)
            {
                return HapticProfileLoadResult.UnsupportedVersion(migration.Messages[0]);
            }

            var validation = HapticProfileValidator.Validate(migration.Document);
            var messages = migration.Messages.Concat(validation.Messages).ToArray();
            return HapticProfileLoadResult.Success(
                validation.Profile,
                migration.WasMigrated || validation.WasRepaired,
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
