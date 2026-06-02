using System.Buffers.Binary;
using System.Text;

namespace HapticDrive.Asio.Telemetry.F1_25.Tests;

public sealed class F125PacketParserTests
{
    [Fact]
    public void Parser_ParsesMotionPacketBody()
    {
        var datagram = CreateDatagram(F125PacketKind.Motion, playerCarIndex: 5);
        var bodyOffset = 5 * 60;
        WriteSingle(datagram, bodyOffset, 101.25f);
        WriteInt16(datagram, bodyOffset + 24, -1234);
        WriteSingle(datagram, bodyOffset + 44, -0.75f);

        var body = ParseBody<F125MotionPacketBody>(datagram);

        Assert.Equal(22, body.CarMotionData.Count);
        Assert.Equal(101.25f, body.CarMotionData[5].WorldPositionX);
        Assert.Equal(-1234, body.CarMotionData[5].WorldForwardDirX);
        Assert.Equal(-0.75f, body.CarMotionData[5].GForceVertical);
    }

    [Fact]
    public void Parser_ParsesSessionPacketBody()
    {
        var datagram = CreateDatagram(F125PacketKind.Session);
        datagram[HeaderOffset + 0] = 3;
        datagram[HeaderOffset + 1] = unchecked((byte)-4);
        datagram[HeaderOffset + 2] = 27;
        datagram[HeaderOffset + 3] = 58;
        WriteUInt16(datagram, 4, 5412);
        datagram[HeaderOffset + 14] = 1;
        datagram[HeaderOffset + 18] = 1;
        WriteSingle(datagram, 19, 0.375f);
        datagram[HeaderOffset + 23] = 3;
        datagram[HeaderOffset + 126] = 1;
        datagram[HeaderOffset + 127] = 10;
        datagram[HeaderOffset + 134] = 42;
        WriteSingle(datagram, 716, 1234.5f);
        WriteSingle(datagram, 720, 3678.5f);

        var body = ParseBody<F125SessionPacketBody>(datagram);

        Assert.Equal(3, body.Weather);
        Assert.Equal(-4, body.TrackTemperature);
        Assert.Equal(27, body.AirTemperature);
        Assert.Equal(58, body.TotalLaps);
        Assert.Equal(5412, body.TrackLength);
        Assert.Equal(1, body.GamePaused);
        Assert.Equal(21, body.MarshalZones.Count);
        Assert.Equal(0.375f, body.MarshalZones[0].ZoneStart);
        Assert.Equal(3, body.MarshalZones[0].ZoneFlag);
        Assert.Equal(64, body.WeatherForecastSamples.Count);
        Assert.Equal(10, body.WeatherForecastSamples[0].SessionType);
        Assert.Equal(42, body.WeatherForecastSamples[0].RainPercentage);
        Assert.Equal(1234.5f, body.Sector2LapDistanceStart);
        Assert.Equal(3678.5f, body.Sector3LapDistanceStart);
    }

    [Fact]
    public void Parser_ParsesLapDataPacketBody()
    {
        var datagram = CreateDatagram(F125PacketKind.LapData, playerCarIndex: 9);
        var bodyOffset = 9 * 57;
        WriteUInt32(datagram, bodyOffset, 83_456);
        WriteSingle(datagram, bodyOffset + 20, 123.75f);
        datagram[HeaderOffset + bodyOffset + 44] = 4;
        datagram[HeaderOffset + bodyOffset + 45] = 2;
        WriteSingle(datagram, bodyOffset + 52, 321.5f);
        datagram[HeaderOffset + 1_254] = 9;
        datagram[HeaderOffset + 1_255] = 12;

        var body = ParseBody<F125LapDataPacketBody>(datagram);

        Assert.Equal(22, body.LapData.Count);
        Assert.Equal(83_456U, body.LapData[9].LastLapTimeInMs);
        Assert.Equal(123.75f, body.LapData[9].LapDistance);
        Assert.Equal(4, body.LapData[9].DriverStatus);
        Assert.Equal(2, body.LapData[9].ResultStatus);
        Assert.Equal(321.5f, body.LapData[9].SpeedTrapFastestSpeed);
        Assert.Equal(9, body.TimeTrialPbCarIndex);
        Assert.Equal(12, body.TimeTrialRivalCarIndex);
    }

    [Fact]
    public void Parser_ParsesCollisionEventDetails()
    {
        var datagram = CreateDatagram(F125PacketKind.Event);
        Encoding.ASCII.GetBytes("COLL").CopyTo(datagram, HeaderOffset);
        datagram[HeaderOffset + 4] = 13;
        datagram[HeaderOffset + 5] = 2;

        var body = ParseBody<F125EventPacketBody>(datagram);

        Assert.Equal("COLL", body.EventCode);
        Assert.Equal([67, 79, 76, 76], body.EventCodeBytes);
        var details = Assert.IsType<F125CollisionEventDetails>(body.EventDetails);
        Assert.Equal(13, details.Vehicle1Index);
        Assert.Equal(2, details.Vehicle2Index);
        Assert.Equal(12, body.EventDetailsRaw.Count);
    }

    [Fact]
    public void Parser_ParsesParticipantsPacketBody()
    {
        var datagram = CreateDatagram(F125PacketKind.Participants);
        datagram[HeaderOffset] = 5;
        var participantOffset = 1 + (4 * 57);
        datagram[HeaderOffset + participantOffset] = 0;
        datagram[HeaderOffset + participantOffset + 1] = 255;
        datagram[HeaderOffset + participantOffset + 3] = 7;
        datagram[HeaderOffset + participantOffset + 5] = 44;
        Encoding.UTF8.GetBytes("Ethan").CopyTo(datagram.AsSpan(HeaderOffset + participantOffset + 7));
        datagram[HeaderOffset + participantOffset + 39] = 1;
        WriteUInt16(datagram, participantOffset + 41, 999);
        datagram[HeaderOffset + participantOffset + 43] = 1;
        datagram[HeaderOffset + participantOffset + 44] = 1;
        datagram[HeaderOffset + participantOffset + 45] = 10;
        datagram[HeaderOffset + participantOffset + 46] = 20;
        datagram[HeaderOffset + participantOffset + 47] = 30;

        var body = ParseBody<F125ParticipantsPacketBody>(datagram);

        Assert.Equal(5, body.NumActiveCars);
        Assert.Equal(22, body.Participants.Count);
        Assert.Equal(255, body.Participants[4].DriverId);
        Assert.Equal(7, body.Participants[4].TeamId);
        Assert.Equal(44, body.Participants[4].RaceNumber);
        Assert.Equal("Ethan", body.Participants[4].Name);
        Assert.Equal(1, body.Participants[4].YourTelemetry);
        Assert.Equal(999, body.Participants[4].TechLevel);
        Assert.Equal(new F125LiveryColour(10, 20, 30), body.Participants[4].LiveryColours[0]);
    }

    [Fact]
    public void Parser_ParsesCarTelemetryPacketBodyAndRespectsPlayerIndex()
    {
        var datagram = CreateDatagram(F125PacketKind.CarTelemetry, playerCarIndex: 21);
        var bodyOffset = 21 * 60;
        WriteUInt16(datagram, bodyOffset, 312);
        WriteSingle(datagram, bodyOffset + 2, 0.81f);
        WriteSingle(datagram, bodyOffset + 10, 0.12f);
        datagram[HeaderOffset + bodyOffset + 15] = 7;
        WriteUInt16(datagram, bodyOffset + 16, 11_500);
        WriteUInt16(datagram, bodyOffset + 22, 101);
        WriteUInt16(datagram, bodyOffset + 24, 102);
        WriteUInt16(datagram, bodyOffset + 26, 103);
        WriteUInt16(datagram, bodyOffset + 28, 104);
        datagram[HeaderOffset + bodyOffset + 56] = 0;
        datagram[HeaderOffset + bodyOffset + 57] = 1;
        datagram[HeaderOffset + bodyOffset + 58] = 7;
        datagram[HeaderOffset + bodyOffset + 59] = 11;
        datagram[HeaderOffset + 1_320] = 255;
        datagram[HeaderOffset + 1_321] = 3;
        datagram[HeaderOffset + 1_322] = unchecked((byte)-1);

        var result = F125PacketParser.Parse(datagram);
        var body = Assert.IsType<F125CarTelemetryPacketBody>(result.Packet!.Body);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(21, result.Header!.PlayerCarIndex);
        Assert.Equal(0, body.CarTelemetryData[0].Speed);
        Assert.Equal(312, body.CarTelemetryData[21].Speed);
        Assert.Equal(0.81f, body.CarTelemetryData[21].Throttle);
        Assert.Equal(0.12f, body.CarTelemetryData[21].Brake);
        Assert.Equal(7, body.CarTelemetryData[21].Gear);
        Assert.Equal(11_500, body.CarTelemetryData[21].EngineRpm);
        Assert.Equal(101, body.CarTelemetryData[21].BrakesTemperature.RearLeft);
        Assert.Equal(102, body.CarTelemetryData[21].BrakesTemperature.RearRight);
        Assert.Equal(103, body.CarTelemetryData[21].BrakesTemperature.FrontLeft);
        Assert.Equal(104, body.CarTelemetryData[21].BrakesTemperature.FrontRight);
        Assert.Equal(0, body.CarTelemetryData[21].SurfaceType.RearLeft);
        Assert.Equal(1, body.CarTelemetryData[21].SurfaceType.RearRight);
        Assert.Equal(7, body.CarTelemetryData[21].SurfaceType.FrontLeft);
        Assert.Equal(11, body.CarTelemetryData[21].SurfaceType.FrontRight);
        Assert.Equal(255, body.MfdPanelIndex);
        Assert.Equal(3, body.MfdPanelIndexSecondaryPlayer);
        Assert.Equal(-1, body.SuggestedGear);
    }

    [Fact]
    public void Parser_ParsesCarStatusPacketBody()
    {
        var datagram = CreateDatagram(F125PacketKind.CarStatus);
        var bodyOffset = 6 * 55;
        datagram[HeaderOffset + bodyOffset] = 2;
        datagram[HeaderOffset + bodyOffset + 1] = 1;
        WriteSingle(datagram, bodyOffset + 5, 12.5f);
        WriteUInt16(datagram, bodyOffset + 17, 12_750);
        datagram[HeaderOffset + bodyOffset + 28] = unchecked((byte)-1);
        WriteSingle(datagram, bodyOffset + 29, 500_000f);
        datagram[HeaderOffset + bodyOffset + 54] = 1;

        var body = ParseBody<F125CarStatusPacketBody>(datagram);

        Assert.Equal(2, body.CarStatusData[6].TractionControl);
        Assert.Equal(1, body.CarStatusData[6].AntiLockBrakes);
        Assert.Equal(12.5f, body.CarStatusData[6].FuelInTank);
        Assert.Equal(12_750, body.CarStatusData[6].MaxRpm);
        Assert.Equal(-1, body.CarStatusData[6].VehicleFiaFlags);
        Assert.Equal(500_000f, body.CarStatusData[6].EnginePowerIce);
        Assert.Equal(1, body.CarStatusData[6].NetworkPaused);
    }

    [Fact]
    public void Parser_ParsesCarDamagePacketBody()
    {
        var datagram = CreateDatagram(F125PacketKind.CarDamage);
        var bodyOffset = 2 * 46;
        WriteSingle(datagram, bodyOffset, 1.5f);
        WriteSingle(datagram, bodyOffset + 4, 2.5f);
        WriteSingle(datagram, bodyOffset + 8, 3.5f);
        WriteSingle(datagram, bodyOffset + 12, 4.5f);
        datagram[HeaderOffset + bodyOffset + 16] = 11;
        datagram[HeaderOffset + bodyOffset + 17] = 12;
        datagram[HeaderOffset + bodyOffset + 18] = 13;
        datagram[HeaderOffset + bodyOffset + 19] = 14;
        datagram[HeaderOffset + bodyOffset + 28] = 21;
        datagram[HeaderOffset + bodyOffset + 45] = 1;

        var body = ParseBody<F125CarDamagePacketBody>(datagram);

        Assert.Equal(1.5f, body.CarDamageData[2].TyresWear.RearLeft);
        Assert.Equal(2.5f, body.CarDamageData[2].TyresWear.RearRight);
        Assert.Equal(3.5f, body.CarDamageData[2].TyresWear.FrontLeft);
        Assert.Equal(4.5f, body.CarDamageData[2].TyresWear.FrontRight);
        Assert.Equal(11, body.CarDamageData[2].TyresDamage.RearLeft);
        Assert.Equal(12, body.CarDamageData[2].TyresDamage.RearRight);
        Assert.Equal(13, body.CarDamageData[2].TyresDamage.FrontLeft);
        Assert.Equal(14, body.CarDamageData[2].TyresDamage.FrontRight);
        Assert.Equal(21, body.CarDamageData[2].FrontLeftWingDamage);
        Assert.Equal(1, body.CarDamageData[2].EngineSeized);
    }

    [Fact]
    public void Parser_ParsesMotionExPacketBodyWithWheelOrder()
    {
        var datagram = CreateDatagram(F125PacketKind.MotionEx);
        WriteWheelSingles(datagram, 0, 10f, 20f, 30f, 40f);
        WriteWheelSingles(datagram, 48, 101f, 102f, 103f, 104f);
        WriteSingle(datagram, 128, 0.45f);
        WriteSingle(datagram, 132, 11.5f);
        WriteSingle(datagram, 168, 0.125f);
        WriteWheelSingles(datagram, 172, 401f, 402f, 403f, 404f);
        WriteSingle(datagram, 208, -0.35f);
        WriteWheelSingles(datagram, 212, -1f, -2f, -3f, -4f);

        var body = ParseBody<F125MotionExPacketBody>(datagram);

        Assert.Equal(10f, body.SuspensionPosition.RearLeft);
        Assert.Equal(20f, body.SuspensionPosition.RearRight);
        Assert.Equal(30f, body.SuspensionPosition.FrontLeft);
        Assert.Equal(40f, body.SuspensionPosition.FrontRight);
        Assert.Equal(101f, body.WheelSpeed.RearLeft);
        Assert.Equal(104f, body.WheelSpeed.FrontRight);
        Assert.Equal(0.45f, body.HeightOfCogAboveGround);
        Assert.Equal(11.5f, body.LocalVelocityX);
        Assert.Equal(0.125f, body.FrontWheelsAngle);
        Assert.Equal(401f, body.WheelVertForce.RearLeft);
        Assert.Equal(404f, body.WheelVertForce.FrontRight);
        Assert.Equal(-0.35f, body.ChassisPitch);
        Assert.Equal(-1f, body.WheelCamber.RearLeft);
        Assert.Equal(-4f, body.WheelCamber.FrontRight);
    }

    [Fact]
    public void Parser_FailsKnownPacketsWithWrongLengthBeforeBodyReads()
    {
        var definition = GetDefinition(F125PacketKind.CarTelemetry);
        var datagram = new byte[definition.Size - 1];
        WriteHeader(datagram, definition.Id, playerCarIndex: 3);

        var result = F125PacketParser.Parse(datagram);

        Assert.True(result.Failed);
        Assert.Null(result.Packet);
        Assert.Contains($"expected {definition.Size} bytes", result.Message);
    }

    [Fact]
    public void Parser_FailsTruncatedDatagramsSafely()
    {
        var result = F125PacketParser.Parse(new byte[10]);

        Assert.True(result.Failed);
        Assert.Null(result.Packet);
        Assert.Contains("requires 29 bytes", result.Message);
    }

    [Fact]
    public void Parser_FailsMalformedDatagramsSafely()
    {
        var datagram = CreateDatagram(F125PacketKind.Session);
        BinaryPrimitives.WriteUInt16LittleEndian(datagram.AsSpan(0, 2), 2024);

        var result = F125PacketParser.Parse(datagram);

        Assert.True(result.Failed);
        Assert.Null(result.Packet);
        Assert.Contains("Unsupported packet format", result.Message);
    }

    [Fact]
    public void Parser_IgnoresKnownUnsupportedPacketIdsSafely()
    {
        var datagram = CreateDatagram(F125PacketKind.CarSetups);

        var result = F125PacketParser.Parse(datagram);

        Assert.True(result.WasIgnored);
        Assert.Null(result.Packet);
        Assert.NotNull(result.Definition);
        Assert.Equal(F125PacketKind.CarSetups, result.Definition.Kind);
        Assert.Contains("not parsed in Stage 07", result.Message);
    }

    [Fact]
    public void Parser_IgnoresUnknownPacketIdsSafely()
    {
        var datagram = new byte[F125PacketDefinitions.HeaderSize];
        WriteHeader(datagram, packetId: 99, playerCarIndex: 3);

        var result = F125PacketParser.Parse(datagram);

        Assert.True(result.WasIgnored);
        Assert.Null(result.Packet);
        Assert.Null(result.Definition);
        Assert.NotNull(result.Header);
        Assert.Equal(99, result.Header.PacketId);
    }

    [Fact]
    public void Parser_PreservesRawDatagramBytesOnSuccessfulParse()
    {
        var datagram = CreateDatagram(F125PacketKind.MotionEx);

        var result = F125PacketParser.Parse(datagram);
        datagram[0] = 0;

        Assert.True(result.Succeeded);
        Assert.Equal(2025, BinaryPrimitives.ReadUInt16LittleEndian(result.RawDatagram.AsSpan(0, 2)));
        Assert.NotSame(datagram, result.RawDatagram);
    }

    private const int HeaderOffset = F125PacketDefinitions.HeaderSize;

    private static TBody ParseBody<TBody>(byte[] datagram)
        where TBody : F125PacketBody
    {
        var result = F125PacketParser.Parse(datagram);

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Packet);
        return Assert.IsType<TBody>(result.Packet.Body);
    }

    private static byte[] CreateDatagram(F125PacketKind kind, byte playerCarIndex = 3)
    {
        var definition = GetDefinition(kind);
        var datagram = new byte[definition.Size];
        WriteHeader(datagram, definition.Id, playerCarIndex);
        return datagram;
    }

    private static F125PacketDefinition GetDefinition(F125PacketKind kind)
    {
        return F125PacketDefinitions.All.Single(definition => definition.Kind == kind);
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

    private static void WriteInt16(byte[] datagram, int bodyOffset, short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(datagram.AsSpan(HeaderOffset + bodyOffset, sizeof(short)), value);
    }

    private static void WriteUInt32(byte[] datagram, int bodyOffset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(HeaderOffset + bodyOffset, sizeof(uint)), value);
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
