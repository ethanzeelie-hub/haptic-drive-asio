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
        LogPath = Path.Combine(directory, "bst1-asio-pulse-flight-recorder.jsonl");
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

    public int ThreadId { get; init; } = Environment.CurrentManagedThreadId;

    public string Source { get; init; } = "manual test";

    public long? AcceptedPaddleEventSequence { get; init; }

    public string? PaddleSide { get; init; }

    public int? PaddleButtonId { get; init; }

    public long? AcceptedGearPulseId { get; init; }

    public long PulseId { get; init; }

    public string? AsioDriverName { get; init; }

    public string OutputMode { get; init; } = string.Empty;

    public int? SelectedChannel { get; init; }

    public bool AsioArmed { get; init; }

    public bool AsioRunning { get; init; }

    public bool AsioCallbackActive { get; init; }

    public bool AsioStreamStartRequested { get; init; }

    public int SampleRate { get; init; }

    public int BufferSizeFrames { get; init; }

    public int QueueCapacityBuffers { get; init; }

    public int QueueCountBeforeSubmit { get; init; }

    public int QueueCountAfterSubmit { get; init; }

    public int BuffersRequiredForPulse { get; init; }

    public int BuffersSubmitted { get; init; }

    public int BuffersAccepted { get; init; }

    public int BuffersDropped { get; init; }

    public string? FirstDropReason { get; init; }

    public long CallbackCountBeforePulse { get; init; }

    public long CallbackCountAfterPulse { get; init; }

    public long RenderedFrameCountBeforePulse { get; init; }

    public long RenderedFrameCountAfterPulse { get; init; }

    public float RequestedStrengthPercent { get; init; }

    public float OutputTrimPercent { get; init; }

    public float EffectivePreLimiterAmplitude { get; init; }

    public float RequestedFrequencyHz { get; init; }

    public int RequestedDurationMs { get; init; }

    public string DurationMode { get; init; } = "manual";

    public long GeneratedSampleCount { get; init; }

    public long SubmittedFrameCount { get; init; }

    public long RenderedCallbackCount { get; init; }

    public long DroppedFrameCount { get; init; }

    public float OutputPeak { get; init; }

    public float EffectivePostLimiterPeak { get; init; }

    public bool LimiterApplied { get; init; }

    public string? BlockedReason { get; init; }

    public DateTimeOffset? StartTimestamp { get; init; }

    public DateTimeOffset? PulseStartTimestamp { get; init; }

    public DateTimeOffset? CallbackActiveTimestamp { get; init; }

    public DateTimeOffset? FirstBufferConsumedTimestamp { get; init; }

    public DateTimeOffset? LastBufferConsumedTimestamp { get; init; }

    public DateTimeOffset? StopDueTimestamp { get; init; }

    public DateTimeOffset? StopTimestamp { get; init; }

    public bool PulseCompleted { get; init; }

    public bool StaleStopIgnored { get; init; }

    public long PulseGenerationId { get; init; }

    public string? ExceptionType { get; init; }

    public string? ExceptionMessage { get; init; }

    public string? ExceptionStackTrace { get; init; }

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
            PulseId = pulseGenerationId,
            AsioDriverName = outputStatus.DeviceName,
            OutputMode = outputStatus.Kind.ToString(),
            SelectedChannel = outputStatus.SelectedOutputChannel,
            AsioArmed = outputStatus.IsHardwareArmed,
            AsioRunning = outputStatus.Kind == AudioOutputDeviceKind.Asio
                && outputStatus.State == AudioOutputDeviceState.Started,
            AsioCallbackActive = outputStatus.IsStreaming
                || outputStatus.RenderCallbackCount > 0
                || outputStatus.BackendCallbackCount > 0,
            AsioStreamStartRequested = outputStatus.State == AudioOutputDeviceState.Started,
            SampleRate = outputStatus.SampleRate,
            BufferSizeFrames = outputStatus.BufferSize,
            QueueCapacityBuffers = outputStatus.QueueCapacityBuffers,
            QueueCountBeforeSubmit = outputStatus.QueuedBufferCount,
            QueueCountAfterSubmit = outputStatus.QueuedBufferCount,
            BuffersRequiredForPulse = outputStatus.BufferSize <= 0
                ? 0
                : (int)Math.Ceiling(request.Duration.TotalSeconds * outputStatus.SampleRate / outputStatus.BufferSize),
            CallbackCountBeforePulse = outputStatus.RenderCallbackCount + outputStatus.BackendCallbackCount,
            CallbackCountAfterPulse = outputStatus.RenderCallbackCount + outputStatus.BackendCallbackCount,
            RequestedStrengthPercent = request.StrengthPercent,
            OutputTrimPercent = request.OutputTrimPercent,
            EffectivePreLimiterAmplitude = request.EffectivePreLimiterAmplitude,
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
