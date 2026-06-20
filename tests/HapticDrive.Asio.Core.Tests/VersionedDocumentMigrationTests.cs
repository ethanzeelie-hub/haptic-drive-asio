using HapticDrive.Asio.Core.Persistence;

namespace HapticDrive.Asio.Core.Tests;

public sealed class VersionedDocumentMigrationTests
{
    [Fact]
    public void Plan_CurrentVersion_ReturnsDocumentWithoutMigration()
    {
        var result = VersionedDocumentMigration.Plan(
            new LegacyDocument(1, "current"),
            sourceVersion: 1,
            currentVersion: 1,
            legacy => legacy with { Version = 1 },
            version => $"Version {version} is not supported.");

        Assert.True(result.IsSupportedVersion);
        Assert.False(result.WasMigrated);
        Assert.NotNull(result.Document);
        Assert.Equal("current", result.Document.Name);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void Plan_VersionZero_MigratesToCurrentVersion()
    {
        var result = VersionedDocumentMigration.Plan(
            new LegacyDocument(0, "legacy"),
            sourceVersion: 0,
            currentVersion: 1,
            legacy => legacy with { Version = 1, Name = $"{legacy.Name}-migrated" },
            version => $"Version {version} is not supported.");

        Assert.True(result.IsSupportedVersion);
        Assert.True(result.WasMigrated);
        Assert.NotNull(result.Document);
        Assert.Equal(1, result.Document.Version);
        Assert.Equal("legacy-migrated", result.Document.Name);
        Assert.Single(result.Messages);
        Assert.Contains("migrated to version 1", result.Messages[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_FutureVersion_IsUnsupported()
    {
        var result = VersionedDocumentMigration.Plan(
            new LegacyDocument(99, "future"),
            sourceVersion: 99,
            currentVersion: 1,
            legacy => legacy with { Version = 1 },
            version => $"Version {version} is not supported.");

        Assert.False(result.IsSupportedVersion);
        Assert.False(result.WasMigrated);
        Assert.Null(result.Document);
        Assert.Single(result.Messages);
        Assert.Equal("Version 99 is not supported.", result.Messages[0]);
    }

    [Fact]
    public void ReadDeclaredVersion_MissingProperty_ReturnsZero()
    {
        var version = VersionedDocumentMigration.ReadDeclaredVersion("""{"Name":"Legacy"}""");

        Assert.Equal(0, version);
    }

    [Fact]
    public void ReadDeclaredVersion_ExplicitVersion_ReturnsDeclaredValue()
    {
        var version = VersionedDocumentMigration.ReadDeclaredVersion("""{"Version":7,"Name":"Current"}""");

        Assert.Equal(7, version);
    }

    private sealed record LegacyDocument(int Version, string Name);
}
