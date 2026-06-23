using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Telemetry.F1_25;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Linq;
using System.Net;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class HapticPipelineCoordinatorPerformanceTests
{
    [Fact]
    [Trait("Category", "Performance")]
    public async Task RenderSteadyState_UsesFreshCanonicalTelemetryWithoutMuteBranch()
    {
        foreach (var bufferSize in new[] { 64, 128, 256 })
        {
            var configuration = AudioOutputConfiguration.Default with
            {
                SampleRate = 48_000,
                ChannelCount = 1,
                BufferSize = bufferSize
            };
            var profile = HapticDriveProfile.Default with
            {
                Effects = HapticDriveProfile.Default.Effects with
                {
                    Engine = HapticDriveProfile.Default.Effects.Engine with { IsEnabled = true }
                }
            };

            await using var coordinator = RuntimeTestPipelineFactory.Create(
                configuration: configuration,
                profile: profile,
                options: HapticPipelineOptions.ManualRendering);
            var outputBuffer = AudioSampleBuffer.Allocate(coordinator.Format);
            var durations = new long[10_000];

            Assert.True((await coordinator.StartAsync()).Succeeded);
            Assert.True((await OfferDrivingTelemetryAsync(coordinator, rpm: 9_000, throttle: 0.9f, gear: 6, frameIdentifierBase: 100)).VehicleStateUpdated);

            var initialRender = coordinator.RenderIntoBufferForTesting(outputBuffer, DateTimeOffset.UtcNow);
            Assert.True(initialRender.Succeeded);
            Assert.False(initialRender.TelemetryTimedOut);
            Assert.True(coordinator.GetSnapshot().Effects.ActiveEffectCount > 0);

            for (var i = 0; i < 2_000; i++)
            {
                var warmupResult = coordinator.RenderIntoBufferForTesting(outputBuffer, DateTimeOffset.UtcNow);
                Assert.True(warmupResult.Succeeded);
                Assert.False(warmupResult.TelemetryTimedOut);
            }

            var hadFailedRender = false;
            var hadTimedOutRender = false;
            for (var i = 0; i < durations.Length; i++)
            {
                var started = Stopwatch.GetTimestamp();
                var renderResult = coordinator.RenderIntoBufferForTesting(outputBuffer, DateTimeOffset.UtcNow);
                durations[i] = Stopwatch.GetTimestamp() - started;
                hadFailedRender |= !renderResult.Succeeded;
                hadTimedOutRender |= renderResult.TelemetryTimedOut;
            }

            Assert.False(hadFailedRender);
            Assert.False(hadTimedOutRender);

            var p99 = ToTimeSpan(PercentileTicks(durations, 0.99d));
            var budget = TimeSpan.FromSeconds((double)bufferSize / configuration.SampleRate * 0.25d);
            Assert.True(
                p99 < budget,
                $"Expected p99 render time below {budget.TotalMilliseconds:0.###} ms for buffer size {bufferSize}, observed {p99.TotalMilliseconds:0.###} ms.");
        }
    }

    private static async Task<HapticPipelinePacketResult> OfferDrivingTelemetryAsync(
        HapticPipelineCoordinator coordinator,
        ushort rpm,
        float throttle,
        sbyte gear,
        uint frameIdentifierBase,
        ulong sessionUid = 123456789)
    {
        await coordinator.OfferLiveTelemetryPacketAsync(CreatePacket(
            CreateSessionDatagram(sessionUid, frameIdentifierBase, frameIdentifierBase),
            sequenceNumber: frameIdentifierBase,
            receivedAtUtc: DateTimeOffset.UtcNow,
            receivedAtTimestamp: TimeProvider.System.GetTimestamp()));
        await coordinator.OfferLiveTelemetryPacketAsync(CreatePacket(
            CreateParticipantsDatagram(sessionUid, frameIdentifierBase, frameIdentifierBase),
            sequenceNumber: frameIdentifierBase + 1,
            receivedAtUtc: DateTimeOffset.UtcNow,
            receivedAtTimestamp: TimeProvider.System.GetTimestamp()));
        await coordinator.OfferLiveTelemetryPacketAsync(CreatePacket(
            CreateLapDataDatagram(4, 2, sessionUid, frameIdentifierBase, frameIdentifierBase),
            sequenceNumber: frameIdentifierBase + 2,
            receivedAtUtc: DateTimeOffset.UtcNow,
            receivedAtTimestamp: TimeProvider.System.GetTimestamp()));
        await coordinator.OfferLiveTelemetryPacketAsync(CreatePacket(
            CreateCarStatusDatagram(0, sessionUid, frameIdentifierBase, frameIdentifierBase),
            sequenceNumber: frameIdentifierBase + 3,
            receivedAtUtc: DateTimeOffset.UtcNow,
            receivedAtTimestamp: TimeProvider.System.GetTimestamp()));
        return await coordinator.OfferLiveTelemetryPacketAsync(CreatePacket(
            CreateCarTelemetryDatagram(rpm, throttle, gear, sessionUid, frameIdentifierBase, frameIdentifierBase),
            sequenceNumber: frameIdentifierBase + 4,
            receivedAtUtc: DateTimeOffset.UtcNow,
            receivedAtTimestamp: TimeProvider.System.GetTimestamp()));
    }

    private static UdpTelemetryPacket CreatePacket(
        byte[] payload,
        long sequenceNumber,
        DateTimeOffset receivedAtUtc,
        long receivedAtTimestamp)
    {
        return new UdpTelemetryPacket(
            sequenceNumber,
            payload,
            new IPEndPoint(IPAddress.Loopback, 20_778),
            receivedAtUtc,
            receivedAtTimestamp);
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
        WriteSingle(datagram, 7, 0f);
        WriteSingle(datagram, 11, 0f);
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

    private static byte[] CreateParticipantsDatagram(
        ulong sessionUid = 123456789,
        uint frameIdentifier = 42,
        uint overallFrameIdentifier = 84)
    {
        var definition = F125PacketDefinitions.All.Single(item => item.Kind == F125PacketKind.Participants);
        var datagram = new byte[definition.Size];
        WriteHeader(datagram, definition.Id, playerCarIndex: 0, sessionUid, frameIdentifier, overallFrameIdentifier);
        datagram[HeaderOffset] = 1;
        datagram[HeaderOffset + 1] = 0;
        datagram[HeaderOffset + 40] = 1;
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

    private static long PercentileTicks(long[] durations, double percentile)
    {
        var copy = (long[])durations.Clone();
        Array.Sort(copy);
        var index = (int)Math.Ceiling((copy.Length * percentile) - 1);
        return copy[Math.Clamp(index, 0, copy.Length - 1)];
    }

    private static TimeSpan ToTimeSpan(long stopwatchTicks)
    {
        return TimeSpan.FromSeconds((double)stopwatchTicks / Stopwatch.Frequency);
    }
}
