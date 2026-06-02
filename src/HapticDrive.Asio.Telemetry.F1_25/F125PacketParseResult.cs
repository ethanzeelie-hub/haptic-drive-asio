namespace HapticDrive.Asio.Telemetry.F1_25;

public sealed record F125PacketParseResult(
    F125PacketParseStatus Status,
    F125PacketHeader? Header,
    F125PacketDefinition? Definition,
    F125Packet? Packet,
    byte[] RawDatagram,
    string Message)
{
    public bool Succeeded => Status == F125PacketParseStatus.Success;

    public bool WasIgnored => Status == F125PacketParseStatus.Ignored;

    public bool Failed => Status == F125PacketParseStatus.Failure;

    public static F125PacketParseResult Success(
        F125PacketHeader header,
        F125PacketDefinition definition,
        F125PacketBody body,
        byte[] rawDatagram)
    {
        return new(
            F125PacketParseStatus.Success,
            header,
            definition,
            new F125Packet(header, definition, body),
            rawDatagram,
            $"{definition.Name} packet parsed.");
    }

    public static F125PacketParseResult Ignored(
        F125PacketHeader? header,
        F125PacketDefinition? definition,
        byte[] rawDatagram,
        string message)
    {
        return new(
            F125PacketParseStatus.Ignored,
            header,
            definition,
            null,
            rawDatagram,
            message);
    }

    public static F125PacketParseResult Failure(
        F125PacketHeader? header,
        F125PacketDefinition? definition,
        byte[] rawDatagram,
        string message)
    {
        return new(
            F125PacketParseStatus.Failure,
            header,
            definition,
            null,
            rawDatagram,
            message);
    }
}
