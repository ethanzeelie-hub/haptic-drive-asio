namespace HapticDrive.Asio.Recording;

public sealed record TelemetryRecordingMetadata(
    DateTimeOffset CreatedAtUtc,
    string SourceGame,
    string SourceProfile,
    string AppVersion)
{
    public static TelemetryRecordingMetadata CreateDefault(DateTimeOffset createdAtUtc)
    {
        var appVersion = typeof(TelemetryRecordingMetadata).Assembly.GetName().Version?.ToString() ?? "unknown";
        return new(createdAtUtc, "F1 25", "Default", appVersion);
    }
}

public sealed class TelemetryRecordedPacket
{
    public TelemetryRecordedPacket(long sequenceNumber, TimeSpan relativeTime, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        SequenceNumber = sequenceNumber;
        RelativeTime = relativeTime;
        Payload = payload.ToArray();
    }

    public long SequenceNumber { get; }

    public TimeSpan RelativeTime { get; }

    public byte[] Payload { get; }
}

public sealed class TelemetryRecording
{
    public TelemetryRecording(TelemetryRecordingMetadata metadata, IEnumerable<TelemetryRecordedPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(packets);

        Metadata = metadata;
        Packets = packets.ToArray();
    }

    public TelemetryRecordingMetadata Metadata { get; }

    public IReadOnlyList<TelemetryRecordedPacket> Packets { get; }
}

public sealed record TelemetryRecordingSummary(
    string Path,
    TelemetryRecordingMetadata Metadata,
    long PacketCount,
    long FileSizeBytes,
    DateTimeOffset LastModifiedAtUtc,
    TimeSpan Duration = default,
    long PayloadBytes = 0,
    long MissingSequenceCount = 0,
    long LargestSequenceGap = 0,
    long? FirstSequenceNumber = null,
    long? LastSequenceNumber = null,
    double ApproximatePacketRateHz = 0d);
