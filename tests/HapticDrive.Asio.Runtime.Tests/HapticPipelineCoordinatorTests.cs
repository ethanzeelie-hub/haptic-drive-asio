using System.Buffers.Binary;
using System.Net;
using HapticDrive.Actuation.Driving;
using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class HapticPipelineCoordinatorTests
{
    [Fact]
    public async Task Pipeline_StartsStopsAndRestartsWithoutSubmittingWhileStopped()
    {
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: HapticPipelineOptions.ManualRendering);

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
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: HapticPipelineOptions.ManualRendering);

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);

        var snapshot = coordinator.GetSnapshot();
        Assert.Equal(0, snapshot.ParserSuccessCount);
        Assert.Equal(0f, snapshot.NullOutput!.LastPeakLevel);
        Assert.True(snapshot.Audio.HasValue);
        Assert.Equal(0f, snapshot.Audio.Value.OutputPeakLevel);
    }

    [Fact]
    public async Task LiveLikePacket_DrivesParserVehicleStateEffectsMixerSafetyAndNullOutput()
    {
        var profile = HapticDriveProfile.Default with
        {
            Effects = HapticDriveProfile.Default.Effects with
            {
                Engine = HapticDriveProfile.Default.Effects.Engine with { IsEnabled = true }
            }
        };
        await using var coordinator = RuntimeTestPipelineFactory.Create(
            profile: profile,
            options: HapticPipelineOptions.ManualRendering);
        var packet = CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 0.8f, gear: 6));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        var packetResult = await coordinator.OfferLiveTelemetryPacketAsync(packet);
        var renderResult = await coordinator.RenderNextBufferAsync();

        var snapshot = coordinator.GetSnapshot();
        Assert.Equal(TelemetryPacketParseStatus.Success, packetResult.ParseStatus);
        Assert.True(packetResult.VehicleStateUpdated);
        Assert.True(renderResult.Succeeded, renderResult.Message);
        Assert.Equal(HapticPipelineInputSource.LiveUdp, snapshot.InputSource);
        Assert.Equal(1, snapshot.ParserSuccessCount);
        Assert.Equal(1, snapshot.VehicleStateUpdateCount);
        Assert.True(snapshot.Effects.Engine.IsActive);
        Assert.True(snapshot.Audio.HasValue);
        Assert.True(snapshot.Audio.Value.ActiveSourceCount > 0);
        Assert.True(snapshot.NullOutput!.LastPeakLevel > 0f);
        var packetDiagnostics = snapshot.PacketDiagnostics.Single(item => item.PacketId == 6);
        Assert.Equal("Car Telemetry", packetDiagnostics.Name);
        Assert.Equal(1, packetDiagnostics.ObservedCount);
        Assert.NotNull(packetDiagnostics.LastObservedAtUtc);
    }

    [Fact]
    public async Task Pipeline_ForwardsConfiguredDestinationDiagnosticsWithoutStartingHardware()
    {
        var destination = new UdpTelemetryForwardingDestination(
            "local tool",
            new IPEndPoint(IPAddress.Loopback, 20779),
            enabled: false);
        await using var coordinator = RuntimeTestPipelineFactory.Create(forwardingDestinations: [destination]);

        var snapshot = coordinator.GetSnapshot();

        Assert.Equal(1, snapshot.Forwarding.DestinationCount);
        Assert.Equal(0, snapshot.Forwarding.EnabledDestinationCount);
    }

    [Fact]
    public async Task ReplayPacket_DrivesSameParserVehicleStateAndOutputPath()
    {
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: HapticPipelineOptions.ManualRendering);
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
        await using var coordinator = RuntimeTestPipelineFactory.Create(
            telemetryForwarder: forwarder,
            options: HapticPipelineOptions.ManualRendering);

        var result = await coordinator.OfferLiveTelemetryPacketAsync(CreatePacket([1, 2, 3]));
        var snapshot = coordinator.GetSnapshot();

        Assert.Equal(TelemetryPacketParseStatus.Failure, result.ParseStatus);
        Assert.Equal(1, snapshot.ParserFailureCount);
        Assert.Equal(1, forwarder.InputPacketCount);
        Assert.Equal(0, snapshot.VehicleStateUpdateCount);
    }

    [Fact]
    public async Task RecordingStillReceivesMalformedPacketsBeforeParserFailure()
    {
        var recordingPath = Path.Combine(Path.GetTempPath(), $"haptic-stage15-{Guid.NewGuid():N}.hdrec");
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: HapticPipelineOptions.ManualRendering);

        try
        {
            Assert.True((await coordinator.RecordingService.StartAsync(recordingPath)).Succeeded);
            var result = await coordinator.OfferLiveTelemetryPacketAsync(CreatePacket([9, 8, 7]));
            Assert.Equal(TelemetryPacketParseStatus.Failure, result.ParseStatus);
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
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: HapticPipelineOptions.ManualRendering);
        var packet = CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 1f, gear: 7));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.Equal(TelemetryPacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(packet)).ParseStatus);

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
    public async Task InterlockTripZerosRenderedBuffer()
    {
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: HapticPipelineOptions.ManualRendering);
        var packet = CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 1f, gear: 7));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.Equal(TelemetryPacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(packet)).ParseStatus);
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);
        Assert.True(coordinator.GetSnapshot().NullOutput!.LastPeakLevel > 0f);

        coordinator.OutputInterlock.Trip(OutputInterlockReason.UserEmergencyMute, "Trip for test.");
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);

        var snapshot = coordinator.GetSnapshot();
        Assert.True(snapshot.EmergencyMute);
        Assert.True(snapshot.OutputInterlock.IsLatched);
        Assert.Equal(0f, snapshot.NullOutput!.LastPeakLevel);
    }

    [Fact]
    public async Task StartupLatchedInterlockRendersSilenceUntilReset()
    {
        var outputInterlock = new OutputInterlock();
        await using var coordinator = RuntimeTestPipelineFactory.Create(
            options: HapticPipelineOptions.ManualRendering,
            outputInterlock: outputInterlock);
        var packet = CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 1f, gear: 7));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.Equal(TelemetryPacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(packet)).ParseStatus);
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);
        Assert.Equal(0f, coordinator.GetSnapshot().NullOutput!.LastPeakLevel);

        Assert.True(outputInterlock.Reset("Runtime test reset."));
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);
        Assert.True(coordinator.GetSnapshot().NullOutput!.LastPeakLevel > 0f);
    }

    [Fact]
    public async Task DisabledEffectsDoNotProduceOutput()
    {
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: HapticPipelineOptions.ManualRendering);
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
            TelemetryPacketParseStatus.Success,
            (await coordinator.OfferLiveTelemetryPacketAsync(CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 1f, gear: 7)))).ParseStatus);
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);

        var snapshot = coordinator.GetSnapshot();
        Assert.Equal(0, snapshot.Effects.ActiveEffectCount);
        Assert.Equal(0f, snapshot.NullOutput!.LastPeakLevel);
    }

    [Fact]
    public async Task ReplayStopWhileActiveIsSafe()
    {
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: HapticPipelineOptions.ManualRendering);
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
            TelemetryMuteTimeout = TimeSpan.FromMilliseconds(150)
        };
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: options);
        var packet = CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 1f, gear: 7));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.Equal(TelemetryPacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(packet)).ParseStatus);
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
        await using var coordinator = RuntimeTestPipelineFactory.Create();
        var packet = CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 1f, gear: 7));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.Equal(TelemetryPacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(packet)).ParseStatus);
        await WaitUntilAsync(() => coordinator.GetSnapshot().NullOutput?.LastPeakLevel > 0f);

        Assert.True((await coordinator.SetEmergencyMuteAsync(true)).Succeeded);
        await WaitUntilAsync(() => coordinator.GetSnapshot().NullOutput?.LastPeakLevel == 0f);

        var snapshot = coordinator.GetSnapshot();
        Assert.True(snapshot.EmergencyMute);
        Assert.Equal(0f, snapshot.NullOutput!.LastPeakLevel);
        Assert.True(snapshot.Output.RenderCallbackCount > 0);
    }

    [Fact]
    public async Task StaleTelemetryRendersSilenceEvenWhenSessionPacketsContinue()
    {
        var profile = HapticDriveProfile.Default with
        {
            Effects = HapticDriveProfile.Default.Effects with
            {
                Engine = HapticDriveProfile.Default.Effects.Engine with { IsEnabled = true }
            }
        };
        var options = HapticPipelineOptions.ManualRendering with
        {
            TelemetryMuteTimeout = TimeSpan.FromMilliseconds(50)
        };
        await using var coordinator = RuntimeTestPipelineFactory.Create(profile: profile, options: options);

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.Equal(
            TelemetryPacketParseStatus.Success,
            (await coordinator.OfferLiveTelemetryPacketAsync(
                CreatePacket(
                    CreateCarTelemetryDatagram(rpm: 9_000, throttle: 0.9f, gear: 6, frameIdentifier: 10, overallFrameIdentifier: 10),
                    receivedAtUtc: DateTimeOffset.UtcNow,
                    receivedAtTimestamp: TimeProvider.System.GetTimestamp()))).ParseStatus);
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);
        Assert.True(coordinator.GetSnapshot().NullOutput!.LastPeakLevel > 0f);

        await Task.Delay(80);
        Assert.Equal(
            TelemetryPacketParseStatus.Success,
            (await coordinator.OfferLiveTelemetryPacketAsync(
                CreatePacket(
                    CreateSessionDatagram(frameIdentifier: 11, overallFrameIdentifier: 11),
                    receivedAtUtc: DateTimeOffset.UtcNow,
                    receivedAtTimestamp: TimeProvider.System.GetTimestamp()))).ParseStatus);
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);

        var snapshot = coordinator.GetSnapshot();
        Assert.True(snapshot.TelemetryTimedOutMuted);
        Assert.Equal(0f, snapshot.NullOutput!.LastPeakLevel);
        Assert.True(snapshot.TelemetryFreshness.Age >= options.TelemetryMuteTimeout);
        Assert.True(snapshot.SessionFreshness.IsFresh);
        Assert.False(snapshot.TelemetryFreshness.IsFresh);
    }

    [Fact]
    public async Task TelemetryStaleTripsOutputInterlock()
    {
        var profile = HapticDriveProfile.Default with
        {
            Effects = HapticDriveProfile.Default.Effects with
            {
                Engine = HapticDriveProfile.Default.Effects.Engine with { IsEnabled = true }
            }
        };
        var options = HapticPipelineOptions.ManualRendering with
        {
            TelemetryMuteTimeout = TimeSpan.FromMilliseconds(50)
        };
        await using var coordinator = RuntimeTestPipelineFactory.Create(profile: profile, options: options);

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.Equal(
            TelemetryPacketParseStatus.Success,
            (await coordinator.OfferLiveTelemetryPacketAsync(
                CreatePacket(
                    CreateCarTelemetryDatagram(rpm: 9_000, throttle: 0.9f, gear: 6, frameIdentifier: 10, overallFrameIdentifier: 10),
                    receivedAtUtc: DateTimeOffset.UtcNow,
                    receivedAtTimestamp: TimeProvider.System.GetTimestamp()))).ParseStatus);
        await Task.Delay(300);

        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);

        var snapshot = coordinator.GetSnapshot();
        Assert.True(snapshot.OutputInterlock.IsLatched);
        Assert.Equal(OutputInterlockReason.TelemetryStale, snapshot.OutputInterlock.Reason);
        Assert.Equal(0f, snapshot.NullOutput!.LastPeakLevel);
    }

    [Fact]
    public async Task DrivingArmedFalseWhenCriticalTelemetryStale()
    {
        var profile = HapticDriveProfile.Default with
        {
            Effects = HapticDriveProfile.Default.Effects with
            {
                Engine = HapticDriveProfile.Default.Effects.Engine with { IsEnabled = true }
            }
        };
        var options = HapticPipelineOptions.ManualRendering with
        {
            TelemetryMuteTimeout = TimeSpan.FromMilliseconds(50)
        };
        await using var coordinator = RuntimeTestPipelineFactory.Create(profile: profile, options: options);
        var drivingArmed = new DrivingArmedStateService();

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.Equal(TelemetryPacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(
            CreatePacket(CreateSessionDatagram(frameIdentifier: 10, overallFrameIdentifier: 10)))).ParseStatus);
        Assert.Equal(TelemetryPacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(
            CreatePacket(CreateLapDataDatagram(driverStatus: 4, resultStatus: 2, frameIdentifier: 10, overallFrameIdentifier: 10)))).ParseStatus);
        Assert.Equal(TelemetryPacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(
            CreatePacket(CreateCarStatusDatagram(networkPaused: 0, frameIdentifier: 10, overallFrameIdentifier: 10)))).ParseStatus);
        Assert.Equal(TelemetryPacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(
            CreatePacket(CreateCarTelemetryDatagram(rpm: 9_000, throttle: 0.9f, gear: 6, frameIdentifier: 10, overallFrameIdentifier: 10)))).ParseStatus);

        var freshSnapshot = coordinator.GetSnapshot();
        var freshState = drivingArmed.UpdateFromVehicleState(
            freshSnapshot.VehicleState,
            CreateDrivingArmedContext(freshSnapshot));
        Assert.True(freshState.IsArmed, freshState.Reason);

        await Task.Delay(80);
        Assert.Equal(TelemetryPacketParseStatus.Success, (await coordinator.OfferLiveTelemetryPacketAsync(
            CreatePacket(CreateSessionDatagram(frameIdentifier: 11, overallFrameIdentifier: 11)))).ParseStatus);
        Assert.True((await coordinator.RenderNextBufferAsync()).Succeeded);

        var staleSnapshot = coordinator.GetSnapshot();
        var staleState = drivingArmed.UpdateFromVehicleState(
            staleSnapshot.VehicleState,
            CreateDrivingArmedContext(staleSnapshot));
        Assert.False(staleState.IsArmed);
        Assert.Equal(DrivingArmedSuppressionReason.StaleTelemetry, drivingArmed.GetSnapshot().LastSuppressionReason);
    }

    [Fact]
    public async Task InjectedGameTelemetryAdapter_DrivesPacketDiagnosticsWithoutF125Parsing()
    {
        var adapter = new FakeGameTelemetryAdapter(
            "Fake Racer",
            [new TelemetryPacketDescriptor(42, "Synthetic Packet")],
            new TelemetryPacketProcessResult(
                TelemetryPacketParseStatus.Success,
                42,
                "Synthetic packet parsed.",
                TelemetryVehicleStateUpdateResult.Applied(VehicleState.Empty, "Synthetic packet updated VehicleState.")));

        await using var coordinator = RuntimeTestPipelineFactory.Create(
            options: HapticPipelineOptions.ManualRendering,
            telemetryGameAdapter: adapter);

        var result = await coordinator.OfferLiveTelemetryPacketAsync(CreatePacket([1, 2, 3]));
        var snapshot = coordinator.GetSnapshot();

        Assert.Equal(TelemetryPacketParseStatus.Success, result.ParseStatus);
        Assert.True(result.VehicleStateUpdated);
        Assert.Equal("Synthetic packet parsed.", snapshot.LastPacketMessage);
        Assert.Equal("Synthetic packet updated VehicleState.", snapshot.LastVehicleStateMessage);

        var packetDiagnostics = Assert.Single(snapshot.PacketDiagnostics);
        Assert.Equal(42, packetDiagnostics.PacketId);
        Assert.Equal("Synthetic Packet", packetDiagnostics.Name);
        Assert.Equal(1, packetDiagnostics.ObservedCount);
    }

    private static UdpTelemetryPacket CreatePacket(byte[] payload)
    {
        return new UdpTelemetryPacket(
            1,
            payload,
            new IPEndPoint(IPAddress.Loopback, 20778),
            DateTimeOffset.UtcNow,
            TimeProvider.System.GetTimestamp());
    }

    private static UdpTelemetryPacket CreatePacket(
        byte[] payload,
        long sequenceNumber = 1,
        DateTimeOffset? receivedAtUtc = null,
        long? receivedAtTimestamp = null)
    {
        return new UdpTelemetryPacket(
            sequenceNumber,
            payload,
            new IPEndPoint(IPAddress.Loopback, 20778),
            receivedAtUtc ?? DateTimeOffset.UtcNow,
            receivedAtTimestamp ?? TimeProvider.System.GetTimestamp());
    }

    private const int HeaderOffset = F125PacketDefinitions.HeaderSize;

    private static byte[] CreateCarTelemetryDatagram(
        ushort rpm,
        float throttle,
        sbyte gear,
        ulong sessionUid = 123456789,
        uint frameIdentifier = 42,
        uint overallFrameIdentifier = 84)
    {
        var definition = F125PacketDefinitions.All.Single(item => item.Kind == F125PacketKind.CarTelemetry);
        var datagram = new byte[definition.Size];
        WriteHeader(datagram, definition.Id, playerCarIndex: 0, sessionUid, frameIdentifier, overallFrameIdentifier);
        WriteUInt16(datagram, 0, 120);
        WriteSingle(datagram, 2, throttle);
        datagram[HeaderOffset + 15] = unchecked((byte)gear);
        WriteUInt16(datagram, 16, rpm);
        return datagram;
    }

    private static byte[] CreateSessionDatagram(
        ulong sessionUid = 123456789,
        uint frameIdentifier = 42,
        uint overallFrameIdentifier = 84)
    {
        var definition = F125PacketDefinitions.All.Single(item => item.Kind == F125PacketKind.Session);
        var datagram = new byte[definition.Size];
        WriteHeader(datagram, definition.Id, playerCarIndex: 0, sessionUid, frameIdentifier, overallFrameIdentifier);
        datagram[HeaderOffset + 14] = 0;
        datagram[HeaderOffset + 124] = 0;
        return datagram;
    }

    private static byte[] CreateLapDataDatagram(
        byte driverStatus,
        byte resultStatus,
        ulong sessionUid = 123456789,
        uint frameIdentifier = 42,
        uint overallFrameIdentifier = 84)
    {
        var definition = F125PacketDefinitions.All.Single(item => item.Kind == F125PacketKind.LapData);
        var datagram = new byte[definition.Size];
        WriteHeader(datagram, definition.Id, playerCarIndex: 0, sessionUid, frameIdentifier, overallFrameIdentifier);
        datagram[HeaderOffset + 44] = driverStatus;
        datagram[HeaderOffset + 45] = resultStatus;
        return datagram;
    }

    private static byte[] CreateCarStatusDatagram(
        byte networkPaused,
        ulong sessionUid = 123456789,
        uint frameIdentifier = 42,
        uint overallFrameIdentifier = 84)
    {
        var definition = F125PacketDefinitions.All.Single(item => item.Kind == F125PacketKind.CarStatus);
        var datagram = new byte[definition.Size];
        WriteHeader(datagram, definition.Id, playerCarIndex: 0, sessionUid, frameIdentifier, overallFrameIdentifier);
        datagram[HeaderOffset + 54] = networkPaused;
        return datagram;
    }

    private static void WriteHeader(
        byte[] datagram,
        byte packetId,
        byte playerCarIndex,
        ulong sessionUid,
        uint frameIdentifier,
        uint overallFrameIdentifier)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(datagram.AsSpan(0, 2), 2025);
        datagram[2] = 25;
        datagram[3] = 1;
        datagram[4] = 0;
        datagram[5] = 1;
        datagram[6] = packetId;
        BinaryPrimitives.WriteUInt64LittleEndian(datagram.AsSpan(7, 8), sessionUid);
        BinaryPrimitives.WriteInt32LittleEndian(datagram.AsSpan(15, 4), BitConverter.SingleToInt32Bits(12.25f));
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(19, 4), frameIdentifier);
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(23, 4), overallFrameIdentifier);
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
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(3_000));
        while (!condition())
        {
            await Task.Delay(5, timeout.Token);
        }
    }

    private static DrivingArmedEvaluationContext CreateDrivingArmedContext(HapticPipelineSnapshot snapshot)
    {
        return new DrivingArmedEvaluationContext
        {
            HapticsRunning = snapshot.IsRunning,
            EmergencyMute = snapshot.EmergencyMute,
            HasRecentTelemetry = snapshot.VehicleStateUpdateCount > 0,
            LastVehicleStateUpdateAtUtc = snapshot.LastVehicleStateUpdateAtUtc,
            TelemetryAge = snapshot.TelemetryFreshness.Age ?? snapshot.TelemetryAge,
            TelemetryTimedOutMuted = snapshot.TelemetryTimedOutMuted
                || (snapshot.TelemetryFreshness.IsPresent && !snapshot.TelemetryFreshness.IsFresh)
        };
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

    private sealed class FakeGameTelemetryAdapter : IGameTelemetryAdapter
    {
        private readonly TelemetryPacketProcessResult _result;

        public FakeGameTelemetryAdapter(
            string gameName,
            IReadOnlyList<TelemetryPacketDescriptor> packetDescriptors,
            TelemetryPacketProcessResult result)
        {
            GameName = gameName;
            PacketDescriptors = packetDescriptors;
            _result = result;
        }

        public string GameName { get; }

        public VehicleState CurrentVehicleState => _result.VehicleStateUpdate.State;

        public IReadOnlyList<TelemetryPacketDescriptor> PacketDescriptors { get; }

        public TelemetryPacketProcessResult Process(UdpTelemetryPacket packet)
        {
            return _result;
        }

        public void Reset(VehicleStateResetReason reason)
        {
        }
    }
}
