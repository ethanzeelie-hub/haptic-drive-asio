using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Runtime.Pipeline;

public interface IManualAsioHardwareTestFlightRecorder
{
    string LogPath { get; }

    string? LastFallbackStatus { get; }

    void Record(ManualAsioHardwareTestFlightRecord record);
}

public sealed class FileManualAsioHardwareTestFlightRecorder : IManualAsioHardwareTestFlightRecorder
{
    private const long MaxBytes = 1024 * 1024;
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public FileManualAsioHardwareTestFlightRecorder(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        LogPath = Path.Combine(directory, "bst1-asio-gear-flight-recorder.jsonl");
    }

    public string LogPath { get; }

    public string? LastFallbackStatus { get; private set; }

    public void Record(ManualAsioHardwareTestFlightRecord record)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                RotateIfNeeded();
                var json = JsonSerializer.Serialize(record, _jsonOptions);
                using var stream = new FileStream(
                    LogPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 4096,
                    FileOptions.WriteThrough);
                using var writer = new StreamWriter(stream);
                writer.WriteLine(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
                LastFallbackStatus = null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LastFallbackStatus = $"{DateTimeOffset.UtcNow:O} bst1-recorder-failed {ex.GetType().Name}: {ex.Message}";
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(LogPath))
        {
            return;
        }

        var info = new FileInfo(LogPath);
        if (info.Length < MaxBytes)
        {
            return;
        }

        var archive = LogPath + ".1";
        if (File.Exists(archive))
        {
            File.Delete(archive);
        }

        File.Move(LogPath, archive);
    }
}

public sealed record ManualAsioHardwareTestFlightRecord
{
    public string SessionId { get; init; } = string.Empty;

    public string EventName { get; init; } = string.Empty;

    public DateTimeOffset WallClockUtc { get; init; } = DateTimeOffset.UtcNow;

    public long MonotonicTimestamp { get; init; } = Stopwatch.GetTimestamp();

    public double? ElapsedMs { get; init; }

    public string Source { get; init; } = "manual test";

    public long? AcceptedPaddleEventSequence { get; init; }

    public string? PaddleSide { get; init; }

    public int? PaddleButtonId { get; init; }

    public long? AcceptedGearPulseId { get; init; }

    public string? AsioDriverName { get; init; }

    public string OutputMode { get; init; } = string.Empty;

    public int? SelectedChannel { get; init; }

    public bool AsioArmed { get; init; }

    public bool AsioRunning { get; init; }

    public bool AsioCallbackActive { get; init; }

    public float RequestedStrengthPercent { get; init; }

    public float RequestedFrequencyHz { get; init; }

    public int RequestedDurationMs { get; init; }

    public string DurationMode { get; init; } = "manual";

    public long GeneratedSampleCount { get; init; }

    public long SubmittedFrameCount { get; init; }

    public long RenderedCallbackCount { get; init; }

    public long DroppedFrameCount { get; init; }

    public float OutputPeak { get; init; }

    public bool LimiterApplied { get; init; }

    public string? BlockedReason { get; init; }

    public DateTimeOffset? StartTimestamp { get; init; }

    public DateTimeOffset? StopDueTimestamp { get; init; }

    public DateTimeOffset? StopTimestamp { get; init; }

    public bool StaleStopIgnored { get; init; }

    public long PulseGenerationId { get; init; }

    public string? ExceptionType { get; init; }

    public string? ExceptionMessage { get; init; }

    public string? SanitizedErrorCategory { get; init; }

    public static ManualAsioHardwareTestFlightRecord From(
        string sessionId,
        string eventName,
        ManualAsioHardwareTestRequest request,
        AudioOutputStatus outputStatus,
        long pulseGenerationId)
    {
        return new ManualAsioHardwareTestFlightRecord
        {
            SessionId = sessionId,
            EventName = eventName,
            Source = request.Source,
            AcceptedPaddleEventSequence = request.AcceptedPaddleEventSequence,
            PaddleSide = request.PaddleSide,
            PaddleButtonId = request.PaddleButtonId,
            AcceptedGearPulseId = request.AcceptedGearPulseId,
            AsioDriverName = outputStatus.DeviceName,
            OutputMode = outputStatus.Kind.ToString(),
            SelectedChannel = outputStatus.SelectedOutputChannel,
            AsioArmed = outputStatus.IsHardwareArmed,
            AsioRunning = outputStatus.Kind == AudioOutputDeviceKind.Asio
                && outputStatus.State == AudioOutputDeviceState.Started,
            AsioCallbackActive = outputStatus.IsStreaming
                || outputStatus.RenderCallbackCount > 0
                || outputStatus.BackendCallbackCount > 0,
            RequestedStrengthPercent = request.StrengthPercent,
            RequestedFrequencyHz = request.FrequencyHz,
            RequestedDurationMs = request.DurationMilliseconds,
            DurationMode = request.DurationMode,
            RenderedCallbackCount = outputStatus.RenderCallbackCount,
            SubmittedFrameCount = outputStatus.SubmittedBufferCount * Math.Max(0, outputStatus.BufferSize),
            DroppedFrameCount = outputStatus.DroppedBufferCount * Math.Max(0, outputStatus.BufferSize),
            PulseGenerationId = pulseGenerationId
        };
    }
}
