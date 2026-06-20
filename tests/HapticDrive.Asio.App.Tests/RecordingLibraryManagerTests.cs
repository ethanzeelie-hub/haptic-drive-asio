using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Recording;
using System.IO;
using System.Net;

namespace HapticDrive.Asio.App.Tests;

public sealed class RecordingLibraryManagerTests
{
    [Fact]
    public void ReplayTimingMode_DefaultIsRealTimeAndFastDebugIsExplicit()
    {
        var modes = ReplayTimingModeOption.Defaults;

        Assert.Same(ReplayTimingModeOption.RealTime, modes[0]);
        Assert.True(modes[0].Options.PreserveTiming);
        Assert.False(modes[0].IsFastDebug);
        Assert.Contains("preserves recorded packet timing", modes[0].HelpText);
        Assert.Same(ReplayTimingModeOption.FastDebug, modes[1]);
        Assert.False(modes[1].Options.PreserveTiming);
        Assert.True(modes[1].IsFastDebug);
        Assert.Contains("not suitable for feel/latency testing", modes[1].HelpText);
    }

    [Fact]
    public async Task DeleteSelectedRecording_RemovesFileAndRefreshCanObserveRemoval()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "delete-me.hdrec");
        await CreateRecordingAsync(path);
        Assert.Single(await RecordingLibraryManager.LoadAsync(temp.Path));

        var result = RecordingLibraryManager.DeleteSelected(temp.Path, path, activeRecordingPath: null);
        var refreshed = await RecordingLibraryManager.LoadAsync(temp.Path);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(RecordingLibraryDeleteStatus.Success, result.Status);
        Assert.False(File.Exists(path));
        Assert.Empty(refreshed);
    }

    [Fact]
    public void DeleteSelectedRecording_MissingFileIsGraceful()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "missing.hdrec");

        var result = RecordingLibraryManager.DeleteSelected(temp.Path, path, activeRecordingPath: null);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(RecordingLibraryDeleteStatus.Missing, result.Status);
        Assert.Contains("already missing", result.Message);
    }

    [Fact]
    public async Task DeleteSelectedRecording_LockedFileReportsFailureAndDoesNotCrash()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "locked.hdrec");
        await CreateRecordingAsync(path);

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = RecordingLibraryManager.DeleteSelected(temp.Path, path, activeRecordingPath: null);

        Assert.False(result.Succeeded);
        Assert.Equal(RecordingLibraryDeleteStatus.Failure, result.Status);
        Assert.Contains("could not be deleted", result.Message);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void DeleteSelectedRecording_BlocksFilesOutsideRecordingsDirectory()
    {
        using var temp = TempRecordingDirectory.Create();
        var outsideDirectory = Directory.CreateDirectory(Path.Combine(temp.ParentPath, $"{Path.GetFileName(temp.Path)}-outside"));
        var outsidePath = Path.Combine(outsideDirectory.FullName, "outside.hdrec");
        File.WriteAllBytes(outsidePath, [0x01]);

        var result = RecordingLibraryManager.DeleteSelected(temp.Path, outsidePath, activeRecordingPath: null);

        Assert.False(result.Succeeded);
        Assert.Equal(RecordingLibraryDeleteStatus.Blocked, result.Status);
        Assert.Contains("outside the recordings folder", result.Message);
        Assert.True(File.Exists(outsidePath));
    }

    [Fact]
    public async Task DeleteSelectedRecording_BlocksActiveRecordingOutput()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "active.hdrec");
        await CreateRecordingAsync(path);

        var result = RecordingLibraryManager.DeleteSelected(temp.Path, path, activeRecordingPath: path);

        Assert.False(result.Succeeded);
        Assert.Equal(RecordingLibraryDeleteStatus.Blocked, result.Status);
        Assert.Contains("active recording", result.Message);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void DeleteSelectedRecording_BlocksNonRecordingExtension()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "not-a-recording.txt");
        File.WriteAllText(path, "nope");

        var result = RecordingLibraryManager.DeleteSelected(temp.Path, path, activeRecordingPath: null);

        Assert.False(result.Succeeded);
        Assert.Equal(RecordingLibraryDeleteStatus.Blocked, result.Status);
        Assert.Contains(".hdrec", result.Message);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task RenameSelectedRecording_RenamesFilePreservesContentAndRefreshes()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "original.hdrec");
        await CreateRecordingAsync(path);
        var before = await File.ReadAllBytesAsync(path);

        var result = RecordingLibraryManager.RenameSelected(temp.Path, path, "renamed-session", activeRecordingPath: null);
        var refreshed = await RecordingLibraryManager.LoadAsync(temp.Path);
        var renamedPath = Path.Combine(temp.Path, "renamed-session.hdrec");
        var after = await File.ReadAllBytesAsync(renamedPath);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(RecordingLibraryRenameStatus.Success, result.Status);
        Assert.Equal(renamedPath, result.RenamedPath);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(renamedPath));
        Assert.Equal(before, after);
        Assert.Single(refreshed);
        Assert.Equal(renamedPath, refreshed[0].Path);
    }

    [Fact]
    public async Task LoadAsync_IncludesDurationPayloadAndSequenceHealthInLibraryText()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "session.hdrec");
        await CreateRecordingAsync(
            path,
            [
                new RecordedPacketTemplate(1, [0x01, 0x02], TimeSpan.Zero),
                new RecordedPacketTemplate(2, [0x03, 0x04, 0x05], TimeSpan.FromMilliseconds(15))
            ]);

        var items = await RecordingLibraryManager.LoadAsync(temp.Path);

        var item = Assert.Single(items);
        Assert.Contains("15 ms", item.DisplayText);
        Assert.Contains("payload 5 B", item.DetailText);
        Assert.Contains("sequence 1-2", item.DetailText);
        Assert.Contains("approx 133.3 pkt/s", item.DetailText);
        Assert.Contains("sequence continuous", item.DetailText);
    }

    [Fact]
    public async Task Filter_MatchesFilenameMetadataAndHealthTerms()
    {
        using var temp = TempRecordingDirectory.Create();
        await CreateRecordingAsync(
            Path.Combine(temp.Path, "night-race.hdrec"),
            [
                new RecordedPacketTemplate(10, [0x01, 0x02], TimeSpan.Zero),
                new RecordedPacketTemplate(11, [0x03], TimeSpan.FromMilliseconds(20))
            ],
            metadata: new TelemetryRecordingMetadata(
                new DateTimeOffset(2026, 6, 12, 2, 0, 0, TimeSpan.Zero),
                "F1 25",
                "Night Test",
                "stage-25n-test"));
        await CreateRecordingAsync(
            Path.Combine(temp.Path, "gap-session.hdrec"),
            [
                new RecordedPacketTemplate(1, [0x01], TimeSpan.Zero),
                new RecordedPacketTemplate(4, [0x02], TimeSpan.FromMilliseconds(25))
            ],
            metadata: new TelemetryRecordingMetadata(
                new DateTimeOffset(2026, 6, 12, 3, 0, 0, TimeSpan.Zero),
                "F1 25",
                "Gap Test",
                "stage-25n-test"));

        var items = await RecordingLibraryManager.LoadAsync(temp.Path);

        var filenameAndProfile = RecordingLibraryManager.Filter(items, "night test");
        var health = RecordingLibraryManager.Filter(items, "gaps largest");

        Assert.Single(filenameAndProfile);
        Assert.Contains("night-race.hdrec", filenameAndProfile[0].DisplayText, StringComparison.OrdinalIgnoreCase);
        Assert.Single(health);
        Assert.Contains("gap-session.hdrec", health[0].DisplayText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenameSelectedRecording_EnforcesHdrecExtension()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "source.hdrec");
        await CreateRecordingAsync(path);

        var result = RecordingLibraryManager.RenameSelected(temp.Path, path, "renamed.txt", activeRecordingPath: null);
        var renamedPath = Path.Combine(temp.Path, "renamed.hdrec");

        Assert.True(result.Succeeded, result.Message);
        Assert.True(File.Exists(renamedPath));
        Assert.False(File.Exists(Path.Combine(temp.Path, "renamed.txt")));
    }

    [Fact]
    public async Task RenameSelectedRecording_BlocksEmptyOrInvalidName()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "source.hdrec");
        await CreateRecordingAsync(path);

        var empty = RecordingLibraryManager.RenameSelected(temp.Path, path, "   ", activeRecordingPath: null);
        var invalid = RecordingLibraryManager.RenameSelected(temp.Path, path, "...", activeRecordingPath: null);

        Assert.False(empty.Succeeded);
        Assert.Equal(RecordingLibraryRenameStatus.Blocked, empty.Status);
        Assert.False(invalid.Succeeded);
        Assert.Equal(RecordingLibraryRenameStatus.Blocked, invalid.Status);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task RenameSelectedRecording_BlocksPathTraversal()
    {
        using var temp = TempRecordingDirectory.Create();
        var path = Path.Combine(temp.Path, "source.hdrec");
        await CreateRecordingAsync(path);

        var result = RecordingLibraryManager.RenameSelected(temp.Path, path, "..\\escape", activeRecordingPath: null);

        Assert.False(result.Succeeded);
        Assert.Equal(RecordingLibraryRenameStatus.Blocked, result.Status);
        Assert.Contains("name only", result.Message);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task RenameSelectedRecording_BlocksOverwriteAndActiveRecordingOutput()
    {
        using var temp = TempRecordingDirectory.Create();
        var first = Path.Combine(temp.Path, "first.hdrec");
        var second = Path.Combine(temp.Path, "second.hdrec");
        await CreateRecordingAsync(first);
        await CreateRecordingAsync(second);

        var overwrite = RecordingLibraryManager.RenameSelected(temp.Path, first, "second", activeRecordingPath: null);
        var active = RecordingLibraryManager.RenameSelected(temp.Path, first, "active-rename", activeRecordingPath: first);

        Assert.False(overwrite.Succeeded);
        Assert.Equal(RecordingLibraryRenameStatus.Blocked, overwrite.Status);
        Assert.False(active.Succeeded);
        Assert.Equal(RecordingLibraryRenameStatus.Blocked, active.Status);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    private static async Task CreateRecordingAsync(
        string path,
        IReadOnlyList<RecordedPacketTemplate>? packets = null,
        TelemetryRecordingMetadata? metadata = null)
    {
        var createdAtUtc = new DateTimeOffset(2026, 6, 12, 1, 0, 0, TimeSpan.Zero);
        packets ??=
        [
            new RecordedPacketTemplate(1, [0x01, 0x02], TimeSpan.Zero)
        ];
        metadata ??= new TelemetryRecordingMetadata(createdAtUtc, "F1 25", "App Test", "stage-18p-b");
        createdAtUtc = metadata.CreatedAtUtc;

        await using var recorder = new TelemetryRecordingService();
        Assert.True((await recorder.StartAsync(
            path,
            metadata)).Succeeded);
        foreach (var packet in packets)
        {
            Assert.True(recorder.RecordPacket(new UdpTelemetryPacket(
                packet.SequenceNumber,
                packet.Payload,
                new IPEndPoint(IPAddress.Loopback, 20_778),
                createdAtUtc + packet.RelativeTime)).Succeeded);
        }

        Assert.True((await recorder.StopAsync()).Succeeded);
    }

    private sealed record RecordedPacketTemplate(
        long SequenceNumber,
        byte[] Payload,
        TimeSpan RelativeTime);

    private sealed class TempRecordingDirectory : IDisposable
    {
        private TempRecordingDirectory(string parentPath, string path)
        {
            ParentPath = parentPath;
            Path = path;
        }

        public string Path { get; }

        public string ParentPath { get; }

        public static TempRecordingDirectory Create()
        {
            var parentPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "HapticDrive.Asio.App.Tests",
                Guid.NewGuid().ToString("N"));
            var path = System.IO.Path.Combine(parentPath, "Recordings");
            Directory.CreateDirectory(path);
            return new TempRecordingDirectory(parentPath, path);
        }

        public void Dispose()
        {
            if (Directory.Exists(ParentPath))
            {
                Directory.Delete(ParentPath, recursive: true);
            }
        }
    }
}
