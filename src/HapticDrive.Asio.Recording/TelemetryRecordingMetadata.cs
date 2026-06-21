namespace HapticDrive.Asio.Recording;

public sealed record TelemetryRecordingMetadata(
    DateTimeOffset CreatedAtUtc,
    string SourceGame,
    string SourceProfile,
    string AppVersion,
    string GameIntegrationId = "f1-25",
    string TelemetryProtocolName = "F1 25 UDP",
    string TelemetryProtocolVersion = "v3",
    string ProfileHash = "",
    string SourceEndpoint = "unknown",
    string BindAddress = "127.0.0.1",
    DateTimeOffset? EndedAtUtc = null,
    long PacketCount = 0,
    bool RecordingComplete = false,
    long DroppedPacketCount = 0)
{
    public static TelemetryRecordingMetadata CreateDefault(
        DateTimeOffset createdAtUtc,
        string sourceGame = "F1 25",
        string sourceProfile = "Default",
        string gameIntegrationId = "f1-25",
        string telemetryProtocolName = "F1 25 UDP",
        string telemetryProtocolVersion = "v3",
        string profileHash = "",
        string sourceEndpoint = "unknown",
        string bindAddress = "127.0.0.1")
    {
        var appVersion = typeof(TelemetryRecordingMetadata).Assembly.GetName().Version?.ToString() ?? "unknown";
        return new(
            createdAtUtc,
            sourceGame,
            sourceProfile,
            appVersion,
            gameIntegrationId,
            telemetryProtocolName,
            telemetryProtocolVersion,
            profileHash,
            sourceEndpoint,
            bindAddress);
    }
}

public sealed class TelemetryRecordedPacket
{
    public TelemetryRecordedPacket(long sequenceNumber, TimeSpan relativeTime, byte[] payload)
        : this(sequenceNumber, DateTimeOffset.UnixEpoch + relativeTime, relativeTime, payload)
    {
    }

    public TelemetryRecordedPacket(long sequenceNumber, DateTimeOffset receivedAtUtc, byte[] payload)
        : this(sequenceNumber, receivedAtUtc, TimeSpan.Zero, payload)
    {
    }

    public TelemetryRecordedPacket(
        long sequenceNumber,
        DateTimeOffset receivedAtUtc,
        TimeSpan relativeTime,
        byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        SequenceNumber = sequenceNumber;
        ReceivedAtUtc = receivedAtUtc;
        RelativeTime = relativeTime;
        Payload = payload.ToArray();
    }

    public long SequenceNumber { get; }

    public DateTimeOffset ReceivedAtUtc { get; }

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
