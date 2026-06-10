namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprHidReportCapabilities
{
    public static PHprHidReportCapabilities Unavailable { get; } = new();

    public ushort? UsagePage { get; init; }

    public ushort? Usage { get; init; }

    public int? InputReportByteLength { get; init; }

    public int? OutputReportByteLength { get; init; }

    public int? FeatureReportByteLength { get; init; }

    public IReadOnlyList<byte> InputReportIds { get; init; } = [];

    public IReadOnlyList<byte> OutputReportIds { get; init; } = [];

    public IReadOnlyList<byte> FeatureReportIds { get; init; } = [];

    public string? SanitizedErrorCategory { get; init; }

    public bool HasAnyReportLength =>
        InputReportByteLength is > 0
        || OutputReportByteLength is > 0
        || FeatureReportByteLength is > 0;

    public bool HasOutputReportCapability => OutputReportByteLength is > 0;

    public bool HasFeatureReportCapability => FeatureReportByteLength is > 0;
}
