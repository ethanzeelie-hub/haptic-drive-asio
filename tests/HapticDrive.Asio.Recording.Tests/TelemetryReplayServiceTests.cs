using System.Net;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Recording;

namespace HapticDrive.Asio.Recording.Tests;

public sealed class TelemetryReplayServiceTests
{
    [Fact]
    public async Task TelemetryReplayService_StopAsyncWaitsForReplayLoopToExit()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 23, 0, 0, 0, TimeSpan.Zero));
        var scheduler = new BlockingDelayScheduler();
        var replay = new TelemetryReplayService(timeProvider, delayScheduler: scheduler);
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(timeProvider.GetUtcNow()),
            [
                new TelemetryRecordedPacket(1, timeProvider.GetUtcNow(), TimeSpan.Zero, [0x01]),
                new TelemetryRecordedPacket(2, timeProvider.GetUtcNow().AddSeconds(5), TimeSpan.FromSeconds(5), [0x02])
            ]);

        var replayTask = replay.ReplayAsync(recording, TelemetryReplayOptions.TimePreserving).AsTask();
        await scheduler.WaitUntilBlockedAsync();

        var stopTask = replay.StopAsync().AsTask();
        Assert.False(stopTask.IsCompleted);
        scheduler.Release();
        await stopTask;

        var result = await replayTask;
        Assert.Equal(TelemetryReplayStatus.Cancelled, result.Status);
        Assert.False(replay.GetSnapshot().IsReplaying);
    }

    [Fact]
    public async Task TelemetryReplayService_SubscriberExceptionIsIsolatedAndReported()
    {
        var replay = new TelemetryReplayService();
        var callbacks = 0;
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(DateTimeOffset.UtcNow),
            [new TelemetryRecordedPacket(1, TimeSpan.Zero, [0x01])]);
        replay.PacketReplayed += (_, _) => throw new InvalidOperationException("subscriber boom");
        replay.PacketReplayed += (_, _) => callbacks++;

        var result = await replay.ReplayAsync(recording, TelemetryReplayOptions.Fast);
        var snapshot = replay.GetSnapshot();

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(1, callbacks);
        Assert.Equal(1, snapshot.SubscriberExceptionCount);
        Assert.Equal("subscriber boom", snapshot.LastSubscriberErrorMessage);
    }

    [Fact]
    public async Task TelemetryReplayService_DefaultModeIsTimePreserving()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 23, 0, 0, 0, TimeSpan.Zero));
        var scheduler = new AdvancingDelayScheduler(timeProvider);
        var replay = new TelemetryReplayService(timeProvider, delayScheduler: scheduler);
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(timeProvider.GetUtcNow()),
            [
                new TelemetryRecordedPacket(1, timeProvider.GetUtcNow(), TimeSpan.Zero, [0x01]),
                new TelemetryRecordedPacket(2, timeProvider.GetUtcNow().AddMilliseconds(20), TimeSpan.FromMilliseconds(20), [0x02])
            ]);

        var result = await replay.ReplayAsync(recording);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal([TimeSpan.FromMilliseconds(20)], scheduler.Delays);
    }

    [Fact]
    public async Task TelemetryReplayService_FastReplayRequiresExplicitSelection()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 23, 0, 0, 0, TimeSpan.Zero));
        var scheduler = new AdvancingDelayScheduler(timeProvider);
        var replay = new TelemetryReplayService(timeProvider, delayScheduler: scheduler);
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(timeProvider.GetUtcNow()),
            [
                new TelemetryRecordedPacket(1, timeProvider.GetUtcNow(), TimeSpan.Zero, [0x01]),
                new TelemetryRecordedPacket(2, timeProvider.GetUtcNow().AddMilliseconds(20), TimeSpan.FromMilliseconds(20), [0x02])
            ]);

        var result = await replay.ReplayAsync(recording, TelemetryReplayOptions.Fast);

        Assert.True(result.Succeeded, result.Message);
        Assert.Empty(scheduler.Delays);
    }

    [Fact]
    public async Task TelemetryReplayService_LargeRecordingStreamingMemoryBounded()
    {
        var path = CreateTempRecordingPath();
        var createdAtUtc = new DateTimeOffset(2026, 6, 23, 0, 0, 0, TimeSpan.Zero);
        var replay = new TelemetryReplayService();
        var packetsSeen = 0;
        replay.PacketReplayed += (_, _) => packetsSeen++;

        try
        {
            await using (var recorder = new TelemetryRecordingService())
            {
                Assert.True((await recorder.StartAsync(path, TelemetryRecordingMetadata.CreateDefault(createdAtUtc))).Succeeded);
                for (var i = 0; i < 2_000; i++)
                {
                    Assert.True(recorder.RecordPacket(CreatePacket(i + 1, new byte[] { 0x01, 0x02, (byte)(i % 255) }, createdAtUtc.AddMilliseconds(i))).Succeeded);
                }

                Assert.True((await recorder.StopAsync()).Succeeded);
            }

            var result = await replay.ReplayFileAsync(path, TelemetryReplayOptions.Fast);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(2_000, packetsSeen);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static UdpTelemetryPacket CreatePacket(long sequenceNumber, byte[] payload, DateTimeOffset receivedAtUtc)
    {
        return new UdpTelemetryPacket(
            sequenceNumber,
            payload,
            new IPEndPoint(IPAddress.Loopback, 20_778),
            receivedAtUtc,
            TimeProvider.System.GetTimestamp());
    }

    private static string CreateTempRecordingPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HapticDrive.Asio.Recording.Tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.hdrec");
    }

    private sealed class BlockingDelayScheduler : ITelemetryReplayDelayScheduler
    {
        private readonly TaskCompletionSource _blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            _blocked.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task WaitUntilBlockedAsync()
        {
            return _blocked.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }

        public void Release()
        {
            _release.TrySetResult();
        }
    }

    private sealed class AdvancingDelayScheduler : ITelemetryReplayDelayScheduler
    {
        private readonly ManualTimeProvider _timeProvider;

        public AdvancingDelayScheduler(ManualTimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public List<TimeSpan> Delays { get; } = [];

        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            _timeProvider.Advance(delay);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _originUtc;
        private long _timestamp;

        public ManualTimeProvider(DateTimeOffset originUtc)
        {
            _originUtc = originUtc;
        }

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return Interlocked.Read(ref _timestamp);
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _originUtc.AddTicks(GetTimestamp());
        }

        public void Advance(TimeSpan amount)
        {
            Interlocked.Add(ref _timestamp, amount.Ticks);
        }
    }
}
