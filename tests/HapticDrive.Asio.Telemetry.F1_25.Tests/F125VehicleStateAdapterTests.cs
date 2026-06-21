using System.Buffers.Binary;
using System.Net;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle;
using System.Text;

namespace HapticDrive.Asio.Telemetry.F1_25.Tests;

public sealed class F125VehicleStateAdapterTests
{
    [Fact]
    public void Adapter_UsesPlayerIndexAndAggregatesLastKnownPackets()
    {
        var adapter = new F125VehicleStateAdapter();
        var telemetryDatagram = CreateDatagram(F125PacketKind.CarTelemetry, playerCarIndex: 9);
        var telemetryOffset = 9 * 60;
        WriteUInt16(telemetryDatagram, telemetryOffset, 287);
        WriteSingle(telemetryDatagram, telemetryOffset + 2, 0.63f);
        WriteSingle(telemetryDatagram, telemetryOffset + 10, 0.14f);
        telemetryDatagram[HeaderOffset + telemetryOffset + 15] = 7;
        WriteUInt16(telemetryDatagram, telemetryOffset + 16, 11_234);
        telemetryDatagram[HeaderOffset + telemetryOffset + 56] = 0;
        telemetryDatagram[HeaderOffset + telemetryOffset + 57] = 1;
        telemetryDatagram[HeaderOffset + telemetryOffset + 58] = 7;
        telemetryDatagram[HeaderOffset + telemetryOffset + 59] = 99;
        telemetryDatagram[HeaderOffset + 1_322] = 8;

        var telemetryUpdate = ApplyDatagram(adapter, telemetryDatagram);

        Assert.True(telemetryUpdate.WasApplied, telemetryUpdate.Message);
        Assert.Equal((byte)9, telemetryUpdate.State.Frame.PlayerCarIndex);
        Assert.Null(telemetryUpdate.State.Motion);
        Assert.Equal(287, telemetryUpdate.State.Telemetry!.Value.SpeedKph);
        Assert.Equal(0.63f, telemetryUpdate.State.Telemetry.Value.Throttle);
        Assert.Equal(0.14f, telemetryUpdate.State.Telemetry.Value.Brake);
        Assert.Equal(7, telemetryUpdate.State.Telemetry.Value.Gear);
        Assert.Equal(11_234, telemetryUpdate.State.Telemetry.Value.EngineRpm);
        Assert.Equal(99, telemetryUpdate.State.Telemetry.Value.SurfaceTypeIds.FrontRight);

        var motionDatagram = CreateDatagram(F125PacketKind.Motion, playerCarIndex: 9);
        var motionOffset = 9 * 60;
        WriteSingle(motionDatagram, motionOffset, 101.5f);
        WriteSingle(motionDatagram, motionOffset + 44, 1.25f);

        var motionUpdate = ApplyDatagram(adapter, motionDatagram);

        Assert.True(motionUpdate.WasApplied, motionUpdate.Message);
        Assert.Equal("Motion", motionUpdate.State.Frame.Source);
        Assert.Equal(101.5f, motionUpdate.State.Motion!.Value.WorldPositionX);
        Assert.Equal(1.25f, motionUpdate.State.Motion.Value.GForceVertical);
        Assert.Equal(287, motionUpdate.State.Telemetry!.Value.SpeedKph);
        Assert.Equal("Car Telemetry", motionUpdate.State.Telemetry.Stamp.Source);
    }

    [Fact]
    public void Adapter_TreatsZeroTelemetryValuesAsPresentSamples()
    {
        var adapter = new F125VehicleStateAdapter();
        Assert.Null(adapter.Current.Telemetry);

        var datagram = CreateDatagram(F125PacketKind.CarTelemetry, playerCarIndex: 4);

        var update = ApplyDatagram(adapter, datagram);

        Assert.True(update.WasApplied, update.Message);
        Assert.NotNull(update.State.Telemetry);
        Assert.Equal(0, update.State.Telemetry!.Value.SpeedKph);
        Assert.Equal(0f, update.State.Telemetry.Value.Throttle);
        Assert.Equal(0, update.State.Telemetry.Value.EngineRpm);
        Assert.Equal(4, update.State.Telemetry.Stamp.PlayerCarIndex);
    }

    [Fact]
    public void Adapter_MapsMotionExWheelOrder()
    {
        var adapter = new F125VehicleStateAdapter();
        var datagram = CreateDatagram(F125PacketKind.MotionEx, playerCarIndex: 2);
        WriteWheelSingles(datagram, 0, 1f, 2f, 3f, 4f);
        WriteWheelSingles(datagram, 64, 0.1f, 0.2f, 0.3f, 0.4f);
        WriteSingle(datagram, 132, 42.5f);
        WriteWheelSingles(datagram, 172, 101f, 102f, 103f, 104f);
        WriteSingle(datagram, 208, -0.35f);

        var update = ApplyDatagram(adapter, datagram);

        Assert.True(update.WasApplied, update.Message);
        Assert.Equal(1f, update.State.MotionEx!.Value.SuspensionPosition.RearLeft);
        Assert.Equal(4f, update.State.MotionEx.Value.SuspensionPosition.FrontRight);
        Assert.Equal(0.1f, update.State.MotionEx.Value.WheelSlipRatio.RearLeft);
        Assert.Equal(0.4f, update.State.MotionEx.Value.WheelSlipRatio.FrontRight);
        Assert.Equal(42.5f, update.State.MotionEx.Value.LocalVelocityX);
        Assert.Equal(101f, update.State.MotionEx.Value.WheelVertForce.RearLeft);
        Assert.Equal(104f, update.State.MotionEx.Value.WheelVertForce.FrontRight);
        Assert.Equal(-0.35f, update.State.MotionEx.Value.ChassisPitch);
    }

    [Fact]
    public void Adapter_MapsPauseGarageAndCollisionContext()
    {
        var adapter = new F125VehicleStateAdapter();
        var sessionDatagram = CreateDatagram(F125PacketKind.Session, playerCarIndex: 5);
        sessionDatagram[HeaderOffset + 6] = 10;
        sessionDatagram[HeaderOffset + 7] = unchecked((byte)-2);
        sessionDatagram[HeaderOffset + 14] = 1;
        sessionDatagram[HeaderOffset + 124] = 2;
        sessionDatagram[HeaderOffset + 125] = 1;

        var sessionUpdate = ApplyDatagram(adapter, sessionDatagram);

        Assert.True(sessionUpdate.WasApplied, sessionUpdate.Message);
        Assert.Equal(1, sessionUpdate.State.Session!.Value.GamePaused);
        Assert.Equal(10, sessionUpdate.State.Session.Value.SessionType);
        Assert.Equal(-2, sessionUpdate.State.Session.Value.TrackId);
        Assert.Equal(2, sessionUpdate.State.Session.Value.SafetyCarStatus);
        Assert.Equal(1, sessionUpdate.State.Session.Value.NetworkGame);

        var lapDatagram = CreateDatagram(F125PacketKind.LapData, playerCarIndex: 5);
        var lapOffset = 5 * 57;
        lapDatagram[HeaderOffset + lapOffset + 34] = 2;
        lapDatagram[HeaderOffset + lapOffset + 37] = 1;
        lapDatagram[HeaderOffset + lapOffset + 44] = 0;
        lapDatagram[HeaderOffset + lapOffset + 45] = 2;

        var lapUpdate = ApplyDatagram(adapter, lapDatagram);

        Assert.True(lapUpdate.WasApplied, lapUpdate.Message);
        Assert.Equal(2, lapUpdate.State.Lap!.Value.PitStatus);
        Assert.Equal(1, lapUpdate.State.Lap.Value.CurrentLapInvalid);
        Assert.Equal(0, lapUpdate.State.Lap.Value.DriverStatus);
        Assert.Equal(2, lapUpdate.State.Lap.Value.ResultStatus);

        var eventDatagram = CreateDatagram(F125PacketKind.Event, playerCarIndex: 5);
        Encoding.ASCII.GetBytes("COLL").CopyTo(eventDatagram, HeaderOffset);
        eventDatagram[HeaderOffset + 4] = 1;
        eventDatagram[HeaderOffset + 5] = 5;

        var eventUpdate = ApplyDatagram(adapter, eventDatagram);

        Assert.True(eventUpdate.WasApplied, eventUpdate.Message);
        Assert.Equal("COLL", eventUpdate.State.LastEvent!.Value.EventCode);
        Assert.Equal((byte)1, eventUpdate.State.LastEvent.Value.PrimaryVehicleIndex);
        Assert.Equal((byte)5, eventUpdate.State.LastEvent.Value.SecondaryVehicleIndex);
        Assert.True(eventUpdate.State.LastEvent.Value.InvolvesPlayer);
    }

    [Fact]
    public void Adapter_IgnoresFailedParseResultsAndInvalidPlayerIndexSafely()
    {
        var adapter = new F125VehicleStateAdapter();
        var failedParse = F125PacketParser.Parse(new byte[10]);

        var failedUpdate = adapter.Apply(failedParse);

        Assert.True(failedUpdate.WasIgnored);
        Assert.Null(failedUpdate.State.Telemetry);

        var datagram = CreateDatagram(F125PacketKind.CarTelemetry, playerCarIndex: 250);
        var parseResult = F125PacketParser.Parse(datagram);
        Assert.True(parseResult.Succeeded, parseResult.Message);

        var invalidPlayerUpdate = adapter.Apply(parseResult);

        Assert.True(invalidPlayerUpdate.WasIgnored);
        Assert.Contains("outside 22 Car Telemetry entries", invalidPlayerUpdate.Message);
        Assert.Null(invalidPlayerUpdate.State.Telemetry);
    }

    [Fact]
    public void SessionUidChangeResetsOldSamples()
    {
        var adapter = new F125VehicleStateAdapter();
        var firstPacket = CreateDatagram(F125PacketKind.CarTelemetry, playerCarIndex: 0, sessionUid: 100, overallFrameIdentifier: 10);
        WriteUInt16(firstPacket, 0, 301);

        var first = ApplyDatagram(adapter, firstPacket);
        Assert.True(first.WasApplied, first.Message);
        Assert.NotNull(first.State.Telemetry);

        var secondPacket = CreateDatagram(F125PacketKind.Motion, playerCarIndex: 0, sessionUid: 200, overallFrameIdentifier: 11);
        var second = ApplyDatagram(adapter, secondPacket);

        Assert.True(second.WasApplied, second.Message);
        Assert.Equal(VehicleStateResetReason.SessionUidChanged, second.ResetReason);
        Assert.Equal(VehicleStateUpdatedSignals.Motion, second.UpdatedSignals);
        Assert.NotNull(second.State.Motion);
        Assert.Null(second.State.Telemetry);
        Assert.Equal((ulong)200, second.State.Frame.SessionUid);
    }

    [Fact]
    public void SourceIpChangeResetsOldSamples()
    {
        var adapter = new F125VehicleStateAdapter();
        var firstPacket = CreateDatagram(F125PacketKind.CarTelemetry, playerCarIndex: 0, sessionUid: 100, overallFrameIdentifier: 10);
        WriteUInt16(firstPacket, 0, 301);
        var receivedAtUtc = DateTimeOffset.UtcNow;
        var first = ApplyDatagram(adapter, firstPacket, IPAddress.Loopback, receivedAtUtc, 1);
        Assert.True(first.WasApplied, first.Message);

        var secondPacket = CreateDatagram(F125PacketKind.Motion, playerCarIndex: 0, sessionUid: 100, overallFrameIdentifier: 11);
        var second = ApplyDatagram(adapter, secondPacket, IPAddress.Parse("192.168.0.20"), receivedAtUtc.AddMilliseconds(5), 2);

        Assert.True(second.WasApplied, second.Message);
        Assert.Equal(VehicleStateResetReason.SourceChanged, second.ResetReason);
        Assert.Equal(VehicleStateUpdatedSignals.Motion, second.UpdatedSignals);
        Assert.NotNull(second.State.Motion);
        Assert.Null(second.State.Telemetry);
    }

    [Fact]
    public void PlayerCarChangeResetsOldSamples()
    {
        var adapter = new F125VehicleStateAdapter();
        var firstPacket = CreateDatagram(F125PacketKind.CarTelemetry, playerCarIndex: 3, sessionUid: 100, overallFrameIdentifier: 10);
        WriteUInt16(firstPacket, 3 * 60, 301);

        var first = ApplyDatagram(adapter, firstPacket);
        Assert.True(first.WasApplied, first.Message);

        var secondPacket = CreateDatagram(F125PacketKind.Motion, playerCarIndex: 4, sessionUid: 100, overallFrameIdentifier: 11);
        var second = ApplyDatagram(adapter, secondPacket);

        Assert.True(second.WasApplied, second.Message);
        Assert.Equal(VehicleStateResetReason.PlayerCarChanged, second.ResetReason);
        Assert.Equal(VehicleStateUpdatedSignals.Motion, second.UpdatedSignals);
        Assert.NotNull(second.State.Motion);
        Assert.Null(second.State.Telemetry);
        Assert.Equal((byte)4, second.State.Frame.PlayerCarIndex);
    }

    [Fact]
    public void OlderOverallFrameIsIgnored()
    {
        var adapter = new F125VehicleStateAdapter();
        var currentPacket = CreateDatagram(F125PacketKind.CarTelemetry, playerCarIndex: 0, sessionUid: 100, overallFrameIdentifier: 10);
        WriteUInt16(currentPacket, 0, 301);
        var current = ApplyDatagram(adapter, currentPacket);
        Assert.True(current.WasApplied, current.Message);

        var olderPacket = CreateDatagram(F125PacketKind.Session, playerCarIndex: 0, sessionUid: 100, overallFrameIdentifier: 9);
        var older = ApplyDatagram(adapter, olderPacket);

        Assert.True(older.WasIgnored);
        Assert.Equal(VehicleStateUpdatedSignals.None, older.UpdatedSignals);
        Assert.Equal(VehicleStateResetReason.None, older.ResetReason);
        Assert.Contains("older than the current VehicleState frame", older.Message, StringComparison.Ordinal);
        Assert.Equal((uint)10, older.State.Frame.OverallFrameIdentifier);
        Assert.NotNull(older.State.Telemetry);
        Assert.Null(older.State.Session);
    }

    [Fact]
    public void EqualOverallFrameCanMergeDifferentPacketTypes()
    {
        var adapter = new F125VehicleStateAdapter();
        var telemetryPacket = CreateDatagram(F125PacketKind.CarTelemetry, playerCarIndex: 0, sessionUid: 100, overallFrameIdentifier: 10);
        WriteUInt16(telemetryPacket, 0, 301);
        var telemetry = ApplyDatagram(adapter, telemetryPacket);
        Assert.True(telemetry.WasApplied, telemetry.Message);

        var sessionPacket = CreateDatagram(F125PacketKind.Session, playerCarIndex: 0, sessionUid: 100, overallFrameIdentifier: 10);
        var session = ApplyDatagram(adapter, sessionPacket);

        Assert.True(session.WasApplied, session.Message);
        Assert.Equal(VehicleStateUpdatedSignals.Session, session.UpdatedSignals);
        Assert.NotNull(session.State.Telemetry);
        Assert.NotNull(session.State.Session);
        Assert.Equal((uint)10, session.State.Frame.OverallFrameIdentifier);
    }

    [Fact]
    public void UpdatedSignalsReflectAppliedPacketType()
    {
        var adapter = new F125VehicleStateAdapter();
        var telemetryPacket = CreateDatagram(F125PacketKind.CarTelemetry, playerCarIndex: 0, sessionUid: 100, overallFrameIdentifier: 10);
        var telemetry = ApplyDatagram(adapter, telemetryPacket);
        Assert.Equal(VehicleStateUpdatedSignals.Telemetry, telemetry.UpdatedSignals);

        var eventPacket = CreateDatagram(F125PacketKind.Event, playerCarIndex: 0, sessionUid: 100, overallFrameIdentifier: 10);
        Encoding.ASCII.GetBytes("COLL").CopyTo(eventPacket, HeaderOffset);
        var @event = ApplyDatagram(adapter, eventPacket);
        Assert.Equal(VehicleStateUpdatedSignals.Event, @event.UpdatedSignals);
    }

    private const int HeaderOffset = F125PacketDefinitions.HeaderSize;

    private static F125VehicleStateUpdateResult ApplyDatagram(F125VehicleStateAdapter adapter, byte[] datagram)
    {
        return ApplyDatagram(adapter, datagram, IPAddress.Loopback, DateTimeOffset.UtcNow, TimeProvider.System.GetTimestamp());
    }

    private static F125VehicleStateUpdateResult ApplyDatagram(
        F125VehicleStateAdapter adapter,
        byte[] datagram,
        IPAddress remoteAddress,
        DateTimeOffset receivedAtUtc,
        long receivedAtTimestamp)
    {
        var parseResult = F125PacketParser.Parse(datagram);
        Assert.True(parseResult.Succeeded, parseResult.Message);
        return adapter.Apply(
            new UdpTelemetryPacket(
                1,
                datagram,
                new IPEndPoint(remoteAddress, 20_778),
                receivedAtUtc,
                receivedAtTimestamp),
            parseResult);
    }

    private static byte[] CreateDatagram(
        F125PacketKind kind,
        byte playerCarIndex,
        ulong sessionUid = 123456789,
        uint frameIdentifier = 42,
        uint overallFrameIdentifier = 84)
    {
        var definition = GetDefinition(kind);
        var datagram = new byte[definition.Size];
        WriteHeader(datagram, definition.Id, playerCarIndex, sessionUid, frameIdentifier, overallFrameIdentifier);
        return datagram;
    }

    private static F125PacketDefinition GetDefinition(F125PacketKind kind)
    {
        return F125PacketDefinitions.All.Single(definition => definition.Kind == kind);
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

    private static void WriteWheelSingles(
        byte[] datagram,
        int bodyOffset,
        float rearLeft,
        float rearRight,
        float frontLeft,
        float frontRight)
    {
        WriteSingle(datagram, bodyOffset, rearLeft);
        WriteSingle(datagram, bodyOffset + 4, rearRight);
        WriteSingle(datagram, bodyOffset + 8, frontLeft);
        WriteSingle(datagram, bodyOffset + 12, frontRight);
    }
}
