using System.Buffers.Binary;

namespace HapticDrive.Asio.Telemetry.F1_25;

public static class F125PacketHeaderParser
{
    public static F125PacketHeaderParseResult Parse(ReadOnlySpan<byte> datagram)
    {
        var rawDatagram = datagram.ToArray();

        if (datagram.Length < F125PacketDefinitions.HeaderSize)
        {
            return F125PacketHeaderParseResult.Failure(
                null,
                rawDatagram,
                $"Datagram is {datagram.Length} bytes; F1 25 packet header requires {F125PacketDefinitions.HeaderSize} bytes.");
        }

        var header = ReadHeader(datagram);

        if (header.PacketFormat != F125PacketDefinitions.PacketFormat)
        {
            return F125PacketHeaderParseResult.Failure(
                header,
                rawDatagram,
                $"Unsupported packet format {header.PacketFormat}; expected {F125PacketDefinitions.PacketFormat}.");
        }

        if (header.GameYear != F125PacketDefinitions.GameYear)
        {
            return F125PacketHeaderParseResult.Failure(
                header,
                rawDatagram,
                $"Unsupported game year {header.GameYear}; expected {F125PacketDefinitions.GameYear}.");
        }

        if (!F125PacketDefinitions.TryGetById(header.PacketId, out var definition) || definition is null)
        {
            return F125PacketHeaderParseResult.Ignored(
                header,
                rawDatagram,
                $"Unknown F1 25 packet ID {header.PacketId}.");
        }

        if (header.PacketVersion != definition.Version)
        {
            return F125PacketHeaderParseResult.Failure(
                header,
                rawDatagram,
                $"Packet ID {header.PacketId} has version {header.PacketVersion}; expected {definition.Version}.");
        }

        if (datagram.Length != definition.Size)
        {
            return F125PacketHeaderParseResult.Failure(
                header,
                rawDatagram,
                $"Packet ID {header.PacketId} is {datagram.Length} bytes; expected {definition.Size} bytes.");
        }

        return F125PacketHeaderParseResult.Success(header, definition, rawDatagram);
    }

    private static F125PacketHeader ReadHeader(ReadOnlySpan<byte> datagram)
    {
        var sessionTimeBits = BinaryPrimitives.ReadInt32LittleEndian(datagram[15..19]);

        return new F125PacketHeader(
            BinaryPrimitives.ReadUInt16LittleEndian(datagram[0..2]),
            datagram[2],
            datagram[3],
            datagram[4],
            datagram[5],
            datagram[6],
            BinaryPrimitives.ReadUInt64LittleEndian(datagram[7..15]),
            BitConverter.Int32BitsToSingle(sessionTimeBits),
            BinaryPrimitives.ReadUInt32LittleEndian(datagram[19..23]),
            BinaryPrimitives.ReadUInt32LittleEndian(datagram[23..27]),
            datagram[27],
            datagram[28]);
    }
}
