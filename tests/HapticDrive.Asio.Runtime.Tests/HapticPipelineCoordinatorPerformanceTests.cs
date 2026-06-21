using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Telemetry.F1_25;
using System.Diagnostics;
using System.Buffers.Binary;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class HapticPipelineCoordinatorPerformanceTests
{
    [Fact]
    [Trait("Category", "Performance")]
    public async Task RenderIntoBufferDoesNotBuildDiagnosticsStrings()
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
        var outputBuffer = AudioSampleBuffer.Allocate(coordinator.Format);
        var stalePacket = CreatePacket(
            CreateCarTelemetryDatagram(rpm: 9_000, throttle: 0.9f, gear: 6),
            DateTimeOffset.UtcNow - TimeSpan.FromSeconds(1),
            Stopwatch.GetTimestamp() - Stopwatch.Frequency);

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.True((await coordinator.OfferLiveTelemetryPacketAsync(stalePacket)).VehicleStateUpdated);

        coordinator.RenderIntoBufferForTesting(outputBuffer, DateTimeOffset.UtcNow);
        for (var i = 0; i < 64; i++)
        {
            coordinator.RenderIntoBufferForTesting(outputBuffer, DateTimeOffset.UtcNow);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 5_000; i++)
        {
            coordinator.RenderIntoBufferForTesting(outputBuffer, DateTimeOffset.UtcNow);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.True(allocated <= 4_096, $"Expected <= 4096 allocated bytes after warmup, observed {allocated}.");

        var snapshot = coordinator.GetSnapshot();
        Assert.True(snapshot.InterlockSilenceCount > 0 || snapshot.StaleFrameSilenceCount > 0);
    }

    private static UdpTelemetryPacket CreatePacket(
        byte[] payload,
        DateTimeOffset receivedAtUtc,
        long receivedAtTimestamp)
    {
        return new UdpTelemetryPacket(
            SequenceNumber: 1,
            Payload: payload,
            RemoteEndPoint: new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 20_778),
            ReceivedAtUtc: receivedAtUtc,
            ReceivedAtTimestamp: receivedAtTimestamp);
    }

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

    private const int HeaderOffset = F125PacketDefinitions.HeaderSize;

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
}
