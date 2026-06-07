namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

public sealed record SimagicPcapInterfaceSummary
{
    public int InterfaceIndex { get; init; }

    public int LinkType { get; init; }
}

public sealed record SimagicPcapCaptureSummary
{
    public string SourceFileName { get; init; } = "";

    public SimagicCaptureAnalysisSourceKind SourceKind { get; init; }

    public bool Parsed { get; init; }

    public int SectionCount { get; init; }

    public int InterfaceCount { get; init; }

    public int PacketCount { get; init; }

    public long TotalCapturedBytes { get; init; }

    public IReadOnlyList<SimagicPcapInterfaceSummary> Interfaces { get; init; } = [];
}
