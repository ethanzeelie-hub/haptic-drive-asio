using System.Buffers.Binary;
using System.Net;
using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class HapticPipelineCoordinatorTests
{
    [Fact]
    public async Task Pipeline_StartsStopsAndRestartsWithoutSubmittingWhileStopped()
    {
        await using var coordinator = new HapticPipelineCoordinator(options: HapticPipelineOptions.ManualRendering);

        var stoppedRender = await coordinator.RenderNextBufferAsync();
        Assert.False(stoppedRender.Succeeded);
        Assert.Equal(0, coordinator.GetSnapshot().NullOutput?.SubmittedBufferCount);

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);
        Assert.True((await coordinator.StopAsync()).Succeeded);
        Assert.False(coordinator.GetSnapshot().IsRunning);

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);

        var snapshot = coordinator.GetSnapshot();
        Assert.True(snapshot.IsRunning);
        Assert.Equal(2, snapshot.RenderedBufferCount);
        Assert.Equal(2, snapshot.NullOutput?.SubmittedBufferCount);
    }

    [Fact]
    public async Task Pipeline_NoValidTelemetryRendersSafeSilence()
    {
        await using var coordinator = new HapticPipelineCoordinator(options: HapticPipelineOptions.ManualRendering);

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);

        var snapshot = coordinator.GetSnapshot();
        Assert.Equal(0, snapshot.ParserSuccessCount);
        Assert.Equal(0f, snapshot.NullOutput!.LastPeakLevel);
        Assert.Equal(0f, snapshot.Audio!.OutputPeakLevel);
    }

    [Fact]
    public async Task LiveLikePacket_DrivesParserVehicleStateEffectsMixerSafetyAndNullOutput()
    {
        await using var coordinator = new HapticPipelineCoordinator(options: HapticPipelineOptions.ManualRendering);
        var packet = CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 0.8f, gear: 6));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        var packetResult = await coordinator.OfferLiveTelemetryPacketAsync(packet);
        var renderResult = await coordinator.RenderNextBufferAsync();

        var snapshot = coordinator.GetSnapshot();
        Assert.Equal(F125PacketParseStatus.Success, packetResult.ParseStatus);
        Assert.True(packetResult.VehicleStateUpdated);
        Assert.True(renderResult.Succeeded, renderResult.Message);
        Assert.Equal(HapticPipelineInputSource.LiveUdp, snapshot.InputSource);
        Assert.Equal(1, snapshot.ParserSuccessCount);
        Assert.Equal(1, snapshot.VehicleStateUpdateCount);
        Assert.True(snapshot.Effects.Engine.IsActive);
        Assert.True(snapshot.Audio!.ActiveSourceCount > 0);
        Assert.True(snapshot.NullOutput!.LastPeakLevel > 0f);
    }

    [Fact]
    public async Task ReplayPacket_DrivesSameParserVehicleStateAndOutputPath()
    {
        await using var coordinator = new HapticPipelineCoordinator(options: HapticPipelineOptions.ManualRendering);
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(DateTimeOffset.UtcNow),
            [
                new TelemetryRecordedPacket(
                    10,
                    TimeSpan.Zero,
                    CreateCarTelemetryDatagram(rpm: 8_500, throttle: 0.7f, gear: 5))
            ]);

        Assert.True((await coordinator.StartAsync()).Succeeded);
        var replayResult = await coordinator.ReplayAsync(recording, TelemetryReplayOptions.Fast);
        var renderResult = await coordinator.RenderNextBufferAsync();

        var snapshot = coordinator.GetSnapshot();
        Assert.True(replayResult.Succeeded, replayResult.Message);
        Assert.True(renderResult.Succeeded, renderResult.Message);
        Assert.Equal(HapticPipelineInputSource.Replay, snapshot.InputSource);
        Assert.Equal(1, snapshot.ParserSuccessCount);
        Assert.Equal(1, snapshot.VehicleStateUpdateCount);
        Assert.True(snapshot.NullOutput!.LastPeakLevel > 0f);
    }

    [Fact]
    public async Task MalformedPackets_DoNotCrashOrPreventForwardingDiagnostics()
    {
        var forwarder = new FakeForwarder();
        await using var coordinator = new HapticPipelineCoordinator(
            telemetryForwarder: forwarder,
            options: HapticPipelineOptions.ManualRendering);

        var result = await coordinator.OfferLiveTelemetryPacketAsync(CreatePacket([1, 2, 3]));
        var snapshot = coordinator.GetSnapshot();

        Assert.Equal(F125PacketParseStatus.Failure, result.ParseStatus);
        Assert.Equal(1, snapshot.ParserFailureCount);
        Assert.Equal(1, forwarder.InputPacketCount);
        Assert.Equal(0, snapshot.VehicleStateUpdateCount);
    }

    [Fact]
    public async Task RecordingStillReceivesMalformedPacketsBeforeParserFailure()
    {
        var recordingPath = Path.Combine(Path.GetTempPath(), $"haptic-stage15-{Guid.NewGuid():N}.hdrec");
        await using var coordinator = new HapticPipelineCoordinator(options: HapticPipelineOptions.ManualRendering);

        try
        {
            Assert.True((await coordinator.RecordingService.StartAsync(recordingPath)).Succeeded);
            var result = await coordinator.OfferLiveTelemetryPacketAsync(CreatePacket([9, 8, 7]));
            Assert.Equal(F125PacketParseStatus.Failure, result.ParseStatus);
            Assert.True((await coordinator.RecordingService.StopAsync()).Succeeded);

            var loadResult = await TelemetryRecordingFile.LoadAsync(recordingPath);
            Assert.True(loadResult.Succeeded, loadResult.Message);
            Assert.Single(loadResult.Recording!.Packets);
            Assert.Equal([9, 8, 7], loadResult.Recording.Packets[0].Payload);
        }
        finally
        {
            if (File.Exists(recordingPath))
            {
                File.Delete(recordingPath);
            }
        }
    }

    [Fact]
    public async Task NormalMuteAndEmergencyMuteForceSilenceThroughOutputPath()
    {
        await using var coordinator = new HapticPipelineCoordinator(options: HapticPipelineOptions.ManualRendering);
        var packet = CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 1f, gear: 7));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.Equal(F125PacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(packet)).ParseStatus);

        coordinator.ApplyProfile(HapticDriveProfile.Default with
        {
            Mixer = HapticDriveProfile.Default.Mixer with { IsMuted = true }
        });
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);
        Assert.Equal(0f, coordinator.GetSnapshot().NullOutput!.LastPeakLevel);

        coordinator.ApplyProfile(HapticDriveProfile.Default);
        Assert.True((await coordinator.SetEmergencyMuteAsync(true)).Succeeded);
        Assert.True(coordinator.GetSnapshot().EmergencyMute);
        Assert.Equal(0f, coordinator.GetSnapshot().NullOutput!.LastPeakLevel);
    }

    [Fact]
    public async Task DisabledEffectsDoNotProduceOutput()
    {
        await using var coordinator = new HapticPipelineCoordinator(options: HapticPipelineOptions.ManualRendering);
        var disabledEffects = HapticDriveProfile.Default.Effects with
        {
            Engine = HapticDriveProfile.Default.Effects.Engine with { IsEnabled = false },
            GearShift = HapticDriveProfile.Default.Effects.GearShift with { IsEnabled = false },
            Kerb = HapticDriveProfile.Default.Effects.Kerb with { IsEnabled = false },
            Impact = HapticDriveProfile.Default.Effects.Impact with { IsEnabled = false },
            RoadTexture = HapticDriveProfile.Default.Effects.RoadTexture with { IsEnabled = false },
            Slip = HapticDriveProfile.Default.Effects.Slip with { IsEnabled = false }
        };

        coordinator.ApplyProfile(HapticDriveProfile.Default with { Effects = disabledEffects });
        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.Equal(
            F125PacketParseStatus.Success,
            (await coordinator.OfferLiveTelemetryPacketAsync(CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 1f, gear: 7)))).ParseStatus);
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);

        var snapshot = coordinator.GetSnapshot();
        Assert.Equal(0, snapshot.Effects.ActiveEffectCount);
        Assert.Equal(0f, snapshot.NullOutput!.LastPeakLevel);
    }

    [Fact]
    public async Task ReplayStopWhileActiveIsSafe()
    {
        await using var coordinator = new HapticPipelineCoordinator(options: HapticPipelineOptions.ManualRendering);
        var recording = new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(DateTimeOffset.UtcNow),
            [
                new TelemetryRecordedPacket(1, TimeSpan.Zero, CreateCarTelemetryDatagram(rpm: 8_000, throttle: 0.6f, gear: 4)),
                new TelemetryRecordedPacket(2, TimeSpan.FromSeconds(5), CreateCarTelemetryDatagram(rpm: 9_000, throttle: 0.7f, gear: 5))
            ]);

        var replayTask = coordinator.ReplayAsync(recording, TelemetryReplayOptions.TimePreserving).AsTask();
        await coordinator.StopAsync();
        var result = await replayTask;

        Assert.True(result.Succeeded);
        Assert.False(coordinator.GetSnapshot().Replay.IsReplaying);
    }

    [Fact]
    public async Task OutputOwnedRendering_StaleTelemetryMutesEffectsByWallClockTimeout()
    {
        var options = HapticPipelineOptions.Default with
        {
            TelemetryMuteTimeout = TimeSpan.FromMilliseconds(30)
        };
        await using var coordinator = new HapticPipelineCoordinator(options: options);
        var packet = CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 1f, gear: 7));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.Equal(F125PacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(packet)).ParseStatus);
        await WaitUntilAsync(() => coordinator.GetSnapshot().NullOutput?.LastPeakLevel > 0f);
        await WaitUntilAsync(() =>
        {
            var snapshot = coordinator.GetSnapshot();
            return snapshot.TelemetryTimedOutMuted
                && snapshot.NullOutput?.LastPeakLevel == 0f;
        });

        var staleSnapshot = coordinator.GetSnapshot();
        Assert.True(staleSnapshot.TelemetryAge >= options.TelemetryMuteTimeout);
        Assert.True(staleSnapshot.TelemetryTimedOutMuted);
        Assert.Equal(0f, staleSnapshot.NullOutput!.LastPeakLevel);
    }

    [Fact]
    public async Task OutputOwnedRendering_EmergencyMuteSilencesNextCallback()
    {
        await using var coordinator = new HapticPipelineCoordinator();
        var packet = CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 1f, gear: 7));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.Equal(F125PacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(packet)).ParseStatus);
        await WaitUntilAsync(() => coordinator.GetSnapshot().NullOutput?.LastPeakLevel > 0f);

        Assert.True((await coordinator.SetEmergencyMuteAsync(true)).Succeeded);
        await WaitUntilAsync(() => coordinator.GetSnapshot().NullOutput?.LastPeakLevel == 0f);

        var snapshot = coordinator.GetSnapshot();
        Assert.True(snapshot.EmergencyMute);
        Assert.Equal(0f, snapshot.NullOutput!.LastPeakLevel);
        Assert.True(snapshot.Output.RenderCallbackCount > 0);
    }

    private static UdpTelemetryPacket CreatePacket(byte[] payload)
    {
        return new UdpTelemetryPacket(
            1,
            payload,
            new IPEndPoint(IPAddress.Loopback, 20778),
            DateTimeOffset.UtcNow);
    }

    private const int HeaderOffset = F125PacketDefinitions.HeaderSize;

    private static byte[] CreateCarTelemetryDatagram(ushort rpm, float throttle, sbyte gear)
    {
        var definition = F125PacketDefinitions.All.Single(item => item.Kind == F125PacketKind.CarTelemetry);
        var datagram = new byte[definition.Size];
        WriteHeader(datagram, definition.Id, playerCarIndex: 0);
        WriteUInt16(datagram, 0, 120);
        WriteSingle(datagram, 2, throttle);
        datagram[HeaderOffset + 15] = unchecked((byte)gear);
        WriteUInt16(datagram, 16, rpm);
        return datagram;
    }

    private static void WriteHeader(byte[] datagram, byte packetId, byte playerCarIndex)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(datagram.AsSpan(0, 2), 2025);
        datagram[2] = 25;
        datagram[3] = 1;
        datagram[4] = 0;
        datagram[5] = 1;
        datagram[6] = packetId;
        BinaryPrimitives.WriteUInt64LittleEndian(datagram.AsSpan(7, 8), 123456789);
        BinaryPrimitives.WriteInt32LittleEndian(datagram.AsSpan(15, 4), BitConverter.SingleToInt32Bits(12.25f));
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(19, 4), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(23, 4), 84);
        datagram[27] = playerCarIndex;
        datagram[28] = 255;
    }

    private static void WriteUInt16(byte[] datagram, int bodyOffset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(datagram.AsSpan(HeaderOffset + bodyOffset, sizeof(ushort)), value);
    }

    private static void WriteSingle(byte[] datagram, int bodyOffset, float value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(
            datagram.AsSpan(HeaderOffset + bodyOffset, sizeof(float)),
            BitConverter.SingleToInt32Bits(value));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(1_000));
        while (!condition())
        {
            await Task.Delay(5, timeout.Token);
        }
    }

    private sealed class FakeForwarder : IUdpTelemetryForwarder
    {
        public IReadOnlyList<UdpTelemetryForwardingDestination> Destinations => [];

        public long InputPacketCount { get; private set; }

        public UdpTelemetryForwarderSnapshot GetSnapshot()
        {
            return new UdpTelemetryForwarderSnapshot(
                IsEnabled: false,
                DestinationCount: 0,
                EnabledDestinationCount: 0,
                InputPacketCount,
                ForwardedDatagramCount: 0,
                ForwardedByteCount: 0,
                ErrorCount: 0,
                LastForwardedAtUtc: null,
                LastErrorMessage: null);
        }

        public ValueTask ForwardAsync(UdpTelemetryPacket packet, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InputPacketCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
