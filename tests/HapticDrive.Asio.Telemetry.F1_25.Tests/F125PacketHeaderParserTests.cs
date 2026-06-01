using System.Buffers.Binary;

namespace HapticDrive.Asio.Telemetry.F1_25.Tests;

public sealed class F125PacketHeaderParserTests
{
    [Fact]
    public void Definitions_HeaderSizeMatchesOfficialSpec()
    {
        Assert.Equal(29, F125PacketDefinitions.HeaderSize);
    }

    [Theory]
    [InlineData(0, F125PacketKind.Motion, 1349)]
    [InlineData(1, F125PacketKind.Session, 753)]
    [InlineData(2, F125PacketKind.LapData, 1285)]
    [InlineData(3, F125PacketKind.Event, 45)]
    [InlineData(4, F125PacketKind.Participants, 1284)]
    [InlineData(5, F125PacketKind.CarSetups, 1133)]
    [InlineData(6, F125PacketKind.CarTelemetry, 1352)]
    [InlineData(7, F125PacketKind.CarStatus, 1239)]
    [InlineData(8, F125PacketKind.FinalClassification, 1042)]
    [InlineData(9, F125PacketKind.LobbyInfo, 954)]
    [InlineData(10, F125PacketKind.CarDamage, 1041)]
    [InlineData(11, F125PacketKind.SessionHistory, 1460)]
    [InlineData(12, F125PacketKind.TyreSets, 231)]
    [InlineData(13, F125PacketKind.MotionEx, 273)]
    [InlineData(14, F125PacketKind.TimeTrial, 101)]
    [InlineData(15, F125PacketKind.LapPositions, 1131)]
    public void Definitions_MatchOfficialPacketIdSizeAndVersionTable(int id, F125PacketKind kind, int size)
    {
        var found = F125PacketDefinitions.TryGetById((byte)id, out var definition);

        Assert.True(found);
        Assert.NotNull(definition);
        Assert.Equal((byte)id, definition.Id);
        Assert.Equal(kind, definition.Kind);
        Assert.Equal(size, definition.Size);
        Assert.Equal(1, definition.Version);
    }

    [Fact]
    public void Parser_ReadsHeaderFieldsAtDocumentedOffsets()
    {
        var datagram = CreateDatagram(F125PacketKind.CarTelemetry);
        BinaryPrimitives.WriteUInt16LittleEndian(datagram.AsSpan(0, 2), 2025);
        datagram[2] = 25;
        datagram[3] = 1;
        datagram[4] = 2;
        datagram[5] = 1;
        datagram[6] = 6;
        BinaryPrimitives.WriteUInt64LittleEndian(datagram.AsSpan(7, 8), 0x0102030405060708);
        BinaryPrimitives.WriteInt32LittleEndian(datagram.AsSpan(15, 4), BitConverter.SingleToInt32Bits(123.5f));
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(19, 4), 0x11223344);
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(23, 4), 0x55667788);
        datagram[27] = 14;
        datagram[28] = 255;

        var result = F125PacketHeaderParser.Parse(datagram);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Header);
        Assert.NotNull(result.Definition);
        Assert.Equal(2025, result.Header.PacketFormat);
        Assert.Equal(25, result.Header.GameYear);
        Assert.Equal(1, result.Header.GameMajorVersion);
        Assert.Equal(2, result.Header.GameMinorVersion);
        Assert.Equal(1, result.Header.PacketVersion);
        Assert.Equal(6, result.Header.PacketId);
        Assert.Equal(0x0102030405060708UL, result.Header.SessionUid);
        Assert.Equal(123.5f, result.Header.SessionTime);
        Assert.Equal(0x11223344U, result.Header.FrameIdentifier);
        Assert.Equal(0x55667788U, result.Header.OverallFrameIdentifier);
        Assert.Equal(14, result.Header.PlayerCarIndex);
        Assert.Equal(255, result.Header.SecondaryPlayerCarIndex);
        Assert.Equal(F125PacketKind.CarTelemetry, result.Definition.Kind);
    }

    [Fact]
    public void Parser_PreservesRawDatagramBytesOnSuccessfulParse()
    {
        var datagram = CreateDatagram(F125PacketKind.MotionEx);

        var result = F125PacketHeaderParser.Parse(datagram);
        datagram[0] = 0;

        Assert.True(result.Succeeded);
        Assert.Equal(2025, BinaryPrimitives.ReadUInt16LittleEndian(result.RawDatagram.AsSpan(0, 2)));
        Assert.NotSame(datagram, result.RawDatagram);
    }

    [Fact]
    public void Parser_FailsWhenDatagramIsShorterThanHeader()
    {
        var datagram = new byte[F125PacketDefinitions.HeaderSize - 1];

        var result = F125PacketHeaderParser.Parse(datagram);

        Assert.True(result.Failed);
        Assert.Null(result.Header);
        Assert.Contains("requires 29 bytes", result.Message);
    }

    [Fact]
    public void Parser_FailsWhenPacketFormatIsNotF125()
    {
        var datagram = CreateDatagram(F125PacketKind.Session);
        BinaryPrimitives.WriteUInt16LittleEndian(datagram.AsSpan(0, 2), 2024);

        var result = F125PacketHeaderParser.Parse(datagram);

        Assert.True(result.Failed);
        Assert.NotNull(result.Header);
        Assert.Contains("Unsupported packet format", result.Message);
    }

    [Fact]
    public void Parser_FailsWhenGameYearIsNotF125()
    {
        var datagram = CreateDatagram(F125PacketKind.Session);
        datagram[2] = 24;

        var result = F125PacketHeaderParser.Parse(datagram);

        Assert.True(result.Failed);
        Assert.NotNull(result.Header);
        Assert.Contains("Unsupported game year", result.Message);
    }

    [Fact]
    public void Parser_IgnoresUnknownPacketIdsSafely()
    {
        var datagram = new byte[F125PacketDefinitions.HeaderSize];
        WriteHeader(datagram, packetId: 99);

        var result = F125PacketHeaderParser.Parse(datagram);

        Assert.True(result.WasIgnored);
        Assert.NotNull(result.Header);
        Assert.Null(result.Definition);
        Assert.Equal(99, result.Header.PacketId);
    }

    [Fact]
    public void Parser_FailsWhenKnownPacketVersionIsWrong()
    {
        var datagram = CreateDatagram(F125PacketKind.Motion);
        datagram[5] = 2;

        var result = F125PacketHeaderParser.Parse(datagram);

        Assert.True(result.Failed);
        Assert.Contains("version 2", result.Message);
    }

    [Fact]
    public void Parser_FailsWhenKnownPacketLengthIsWrong()
    {
        var definition = GetDefinition(F125PacketKind.CarStatus);
        var datagram = new byte[definition.Size - 1];
        WriteHeader(datagram, definition.Id);

        var result = F125PacketHeaderParser.Parse(datagram);

        Assert.True(result.Failed);
        Assert.Contains($"expected {definition.Size} bytes", result.Message);
    }

    [Fact]
    public void Parser_DoesNotAssumePlayerCarIndexZero()
    {
        var datagram = CreateDatagram(F125PacketKind.LapData);
        datagram[27] = 21;

        var result = F125PacketHeaderParser.Parse(datagram);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Header);
        Assert.Equal(21, result.Header.PlayerCarIndex);
    }

    private static byte[] CreateDatagram(F125PacketKind kind)
    {
        var definition = GetDefinition(kind);
        var datagram = new byte[definition.Size];
        WriteHeader(datagram, definition.Id);
        return datagram;
    }

    private static F125PacketDefinition GetDefinition(F125PacketKind kind)
    {
        return F125PacketDefinitions.All.Single(definition => definition.Kind == kind);
    }

    private static void WriteHeader(byte[] datagram, byte packetId)
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
        datagram[27] = 3;
        datagram[28] = 255;
    }
}
