using System.Text.Json;

namespace HapticDrive.Asio.Core.Persistence;

public sealed record VersionedDocumentMigrationResult<T>(
    bool IsSupportedVersion,
    T? Document,
    bool WasMigrated,
    IReadOnlyList<string> Messages);

public static class VersionedDocumentMigration
{
    public static int ReadDeclaredVersion(string json, string propertyName = "Version")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var version)
            ? version
            : 0;
    }

    public static VersionedDocumentMigrationResult<T> Plan<T>(
        T document,
        int sourceVersion,
        int currentVersion,
        Func<T, T> migrateLegacyDocument,
        Func<int, string> unsupportedVersionMessageFactory)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(migrateLegacyDocument);
        ArgumentNullException.ThrowIfNull(unsupportedVersionMessageFactory);

        if (sourceVersion == currentVersion)
        {
            return new VersionedDocumentMigrationResult<T>(
                IsSupportedVersion: true,
                document,
                WasMigrated: false,
                []);
        }

        if (sourceVersion == 0)
        {
            return new VersionedDocumentMigrationResult<T>(
                IsSupportedVersion: true,
                migrateLegacyDocument(document),
                WasMigrated: true,
                [$"Legacy document version 0 was migrated to version {currentVersion}."]);
        }

        return new VersionedDocumentMigrationResult<T>(
            IsSupportedVersion: false,
            Document: default,
            WasMigrated: false,
            [unsupportedVersionMessageFactory(sourceVersion)]);
    }
}
