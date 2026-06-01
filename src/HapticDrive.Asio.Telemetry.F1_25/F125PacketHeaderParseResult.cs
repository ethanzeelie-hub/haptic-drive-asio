namespace HapticDrive.Asio.Telemetry.F1_25;

public sealed record F125PacketHeaderParseResult(
    F125PacketHeaderParseStatus Status,
    F125PacketHeader? Header,
    F125PacketDefinition? Definition,
    byte[] RawDatagram,
    string Message)
{
    public bool Succeeded => Status == F125PacketHeaderParseStatus.Success;

    public bool WasIgnored => Status == F125PacketHeaderParseStatus.Ignored;

    public bool Failed => Status == F125PacketHeaderParseStatus.Failure;

    public static F125PacketHeaderParseResult Success(
        F125PacketHeader header,
        F125PacketDefinition definition,
        byte[] rawDatagram)
    {
        return new(
            F125PacketHeaderParseStatus.Success,
            header,
            definition,
            rawDatagram,
            "Packet header parsed.");
    }

    public static F125PacketHeaderParseResult Ignored(
        F125PacketHeader header,
        byte[] rawDatagram,
        string message)
    {
        return new(
            F125PacketHeaderParseStatus.Ignored,
            header,
            null,
            rawDatagram,
            message);
    }

    public static F125PacketHeaderParseResult Failure(
        F125PacketHeader? header,
        byte[] rawDatagram,
        string message)
    {
        return new(
            F125PacketHeaderParseStatus.Failure,
            header,
            null,
            rawDatagram,
            message);
    }
}
