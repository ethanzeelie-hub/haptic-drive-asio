namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

public sealed record SimagicPayloadByteDifference
{
    public int Offset { get; init; }

    public string HexOffset { get; init; } = "";

    public string LeftValueHex { get; init; } = "";

    public string RightValueHex { get; init; } = "";
}
