using System.Buffers.Binary;
using System.Text;

namespace HapticDrive.Asio.Telemetry.F1_25;

internal static class F125BinaryReader
{
    public static byte ReadUInt8(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, sizeof(byte));
        return data[offset++];
    }

    public static sbyte ReadInt8(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, sizeof(sbyte));
        return unchecked((sbyte)data[offset++]);
    }

    public static ushort ReadUInt16(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, sizeof(ushort));
        var value = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, sizeof(ushort)));
        offset += sizeof(ushort);
        return value;
    }

    public static short ReadInt16(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, sizeof(short));
        var value = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset, sizeof(short)));
        offset += sizeof(short);
        return value;
    }

    public static uint ReadUInt32(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, sizeof(uint));
        var value = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, sizeof(uint)));
        offset += sizeof(uint);
        return value;
    }

    public static float ReadSingle(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, sizeof(float));
        var bits = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, sizeof(float)));
        offset += sizeof(float);
        return BitConverter.Int32BitsToSingle(bits);
    }

    public static byte[] ReadBytes(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        EnsureAvailable(data, offset, count);
        var value = data.Slice(offset, count).ToArray();
        offset += count;
        return value;
    }

    public static string ReadNullTerminatedUtf8(byte[] bytes)
    {
        var terminatorIndex = Array.IndexOf(bytes, (byte)0);
        var length = terminatorIndex >= 0 ? terminatorIndex : bytes.Length;
        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    public static F125WheelData<byte> ReadByteWheelData(ReadOnlySpan<byte> data, ref int offset)
    {
        return new(
            ReadUInt8(data, ref offset),
            ReadUInt8(data, ref offset),
            ReadUInt8(data, ref offset),
            ReadUInt8(data, ref offset));
    }

    public static F125WheelData<ushort> ReadUInt16WheelData(ReadOnlySpan<byte> data, ref int offset)
    {
        return new(
            ReadUInt16(data, ref offset),
            ReadUInt16(data, ref offset),
            ReadUInt16(data, ref offset),
            ReadUInt16(data, ref offset));
    }

    public static F125WheelData<float> ReadSingleWheelData(ReadOnlySpan<byte> data, ref int offset)
    {
        return new(
            ReadSingle(data, ref offset),
            ReadSingle(data, ref offset),
            ReadSingle(data, ref offset),
            ReadSingle(data, ref offset));
    }

    public static void EnsureConsumed(ReadOnlySpan<byte> data, int offset, F125PacketKind kind)
    {
        if (offset != data.Length)
        {
            throw new FormatException($"{kind} body parser consumed {offset} bytes; body has {data.Length} bytes.");
        }
    }

    private static void EnsureAvailable(ReadOnlySpan<byte> data, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > data.Length - count)
        {
            throw new FormatException($"Body read at byte {offset} requires {count} bytes, but body has {data.Length} bytes.");
        }
    }
}
