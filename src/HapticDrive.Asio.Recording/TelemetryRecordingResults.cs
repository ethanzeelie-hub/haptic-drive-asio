namespace HapticDrive.Asio.Recording;

public enum TelemetryRecordingOperationStatus
{
    Success,
    Dropped,
    NotRecording,
    AlreadyRecording,
    Cancelled,
    Failure
}

public sealed record TelemetryRecordingOperationResult(
    TelemetryRecordingOperationStatus Status,
    string Message)
{
    public bool Succeeded => Status == TelemetryRecordingOperationStatus.Success;

    public static TelemetryRecordingOperationResult Success(string message)
    {
        return new(TelemetryRecordingOperationStatus.Success, message);
    }

    public static TelemetryRecordingOperationResult Dropped(string message)
    {
        return new(TelemetryRecordingOperationStatus.Dropped, message);
    }

    public static TelemetryRecordingOperationResult NotRecording(string message)
    {
        return new(TelemetryRecordingOperationStatus.NotRecording, message);
    }

    public static TelemetryRecordingOperationResult AlreadyRecording(string message)
    {
        return new(TelemetryRecordingOperationStatus.AlreadyRecording, message);
    }

    public static TelemetryRecordingOperationResult Cancelled(string message)
    {
        return new(TelemetryRecordingOperationStatus.Cancelled, message);
    }

    public static TelemetryRecordingOperationResult Failure(string message)
    {
        return new(TelemetryRecordingOperationStatus.Failure, message);
    }
}

public enum TelemetryRecordingLoadStatus
{
    Success,
    FileNotFound,
    UnsupportedVersion,
    Cancelled,
    Corrupt,
    Failure
}

public sealed record TelemetryRecordingLoadResult(
    TelemetryRecordingLoadStatus Status,
    TelemetryRecording? Recording,
    string Message)
{
    public bool Succeeded => Status == TelemetryRecordingLoadStatus.Success;

    public static TelemetryRecordingLoadResult Success(TelemetryRecording recording)
    {
        return new(TelemetryRecordingLoadStatus.Success, recording, "Recording loaded.");
    }

    public static TelemetryRecordingLoadResult FileNotFound(string message)
    {
        return new(TelemetryRecordingLoadStatus.FileNotFound, null, message);
    }

    public static TelemetryRecordingLoadResult UnsupportedVersion(string message)
    {
        return new(TelemetryRecordingLoadStatus.UnsupportedVersion, null, message);
    }

    public static TelemetryRecordingLoadResult Cancelled(string message)
    {
        return new(TelemetryRecordingLoadStatus.Cancelled, null, message);
    }

    public static TelemetryRecordingLoadResult Corrupt(string message)
    {
        return new(TelemetryRecordingLoadStatus.Corrupt, null, message);
    }

    public static TelemetryRecordingLoadResult Failure(string message)
    {
        return new(TelemetryRecordingLoadStatus.Failure, null, message);
    }
}

public sealed record TelemetryRecordingSummaryLoadResult(
    TelemetryRecordingLoadStatus Status,
    TelemetryRecordingSummary? Summary,
    string Message)
{
    public bool Succeeded => Status == TelemetryRecordingLoadStatus.Success;

    public static TelemetryRecordingSummaryLoadResult Success(TelemetryRecordingSummary summary)
    {
        return new(TelemetryRecordingLoadStatus.Success, summary, "Recording summary loaded.");
    }

    public static TelemetryRecordingSummaryLoadResult FileNotFound(string message)
    {
        return new(TelemetryRecordingLoadStatus.FileNotFound, null, message);
    }

    public static TelemetryRecordingSummaryLoadResult UnsupportedVersion(string message)
    {
        return new(TelemetryRecordingLoadStatus.UnsupportedVersion, null, message);
    }

    public static TelemetryRecordingSummaryLoadResult Cancelled(string message)
    {
        return new(TelemetryRecordingLoadStatus.Cancelled, null, message);
    }

    public static TelemetryRecordingSummaryLoadResult Corrupt(string message)
    {
        return new(TelemetryRecordingLoadStatus.Corrupt, null, message);
    }

    public static TelemetryRecordingSummaryLoadResult Failure(string message)
    {
        return new(TelemetryRecordingLoadStatus.Failure, null, message);
    }
}

public enum TelemetryReplayStatus
{
    Success,
    Cancelled,
    Failure
}

public sealed record TelemetryReplayResult(
    TelemetryReplayStatus Status,
    long PacketsReplayed,
    string Message)
{
    public bool Succeeded => Status == TelemetryReplayStatus.Success;

    public static TelemetryReplayResult Success(long packetsReplayed)
    {
        return new(TelemetryReplayStatus.Success, packetsReplayed, $"Replayed {packetsReplayed:N0} packets.");
    }

    public static TelemetryReplayResult Cancelled(long packetsReplayed)
    {
        return new(TelemetryReplayStatus.Cancelled, packetsReplayed, $"Replay cancelled after {packetsReplayed:N0} packets.");
    }

    public static TelemetryReplayResult Failure(long packetsReplayed, string message)
    {
        return new(TelemetryReplayStatus.Failure, packetsReplayed, message);
    }
}

public sealed record TelemetryRecordingSnapshot(
    bool IsRecording,
    string? FilePath,
    long PacketCount,
    TimeSpan? LastPacketRelativeTime,
    string? LastErrorMessage,
    int? QueueCapacityPackets = null,
    int QueuedPacketCount = 0,
    long DroppedPacketCount = 0,
    bool RecordingIncomplete = false,
    string? IncompleteReason = null);

public sealed record TelemetryRecordingDrainResult(
    bool Drained,
    int RemainingQueuedPacketCount,
    string Message)
{
    public static TelemetryRecordingDrainResult Complete()
    {
        return new(true, 0, "Recording queue drained.");
    }

    public static TelemetryRecordingDrainResult TimedOut(int remainingQueuedPacketCount)
    {
        return new(
            false,
            remainingQueuedPacketCount,
            $"Recording queue drain timed out with {remainingQueuedPacketCount:N0} packet(s) still queued.");
    }
}

public sealed record TelemetryReplaySnapshot(
    bool IsReplaying,
    string? SourceFilePath,
    long PacketsReplayed,
    string StatusMessage,
    TimeSpan TotalReplayDrift = default,
    TimeSpan MaxLatePacket = default,
    long SkippedSleepCount = 0,
    long SubscriberExceptionCount = 0,
    string? LastSubscriberErrorMessage = null);
