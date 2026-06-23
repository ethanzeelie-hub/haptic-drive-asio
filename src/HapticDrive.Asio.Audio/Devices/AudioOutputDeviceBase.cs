using HapticDrive.Asio.Core.Audio;
using System.Diagnostics;

namespace HapticDrive.Asio.Audio.Devices;

public abstract class AudioOutputDeviceBase : IAudioOutputDevice
{
    private const long UnsetTimeSpanTicks = long.MinValue;
    private const string RenderCallbackFailedMessage = "Render callback failed safely.";

    private AudioOutputConfiguration _configuration = AudioOutputConfiguration.Default;
    private CancellationTokenSource? _streamingCancellation;
    private Task? _streamingTask;
    private long _renderCallbackCount;
    private long _renderDroppedBufferCount;
    private long _underrunCount;
    private long _lastRenderDurationTicks = UnsetTimeSpanTicks;
    private long _maximumRenderDurationTicks = UnsetTimeSpanTicks;
    private long _lastCallbackJitterTicks = UnsetTimeSpanTicks;
    private long _maximumCallbackJitterTicks = UnsetTimeSpanTicks;
    private long _lastTelemetryAgeTicks = UnsetTimeSpanTicks;
    private int _isStreaming;

    protected AudioOutputDeviceBase(AudioOutputDeviceKind kind, string displayName)
    {
        Kind = kind;
        DisplayName = displayName;
    }

    public AudioOutputDeviceKind Kind { get; }

    public string DisplayName { get; }

    public abstract bool RequiresPhysicalHardware { get; }

    public virtual bool IsManualDebugOnly => false;

    protected AudioOutputDeviceState State { get; set; } = AudioOutputDeviceState.Created;

    protected string StatusMessage { get; set; } = "Created";

    protected string? DeviceName { get; set; }

    protected AudioOutputConfiguration Configuration
    {
        get => _configuration;
        set
        {
            ValidateConfiguration(value);
            _configuration = value;
        }
    }

    public virtual AudioOutputStatus GetStatus()
    {
        return new AudioOutputStatus(
            Kind,
            State,
            DisplayName,
            StatusMessage,
            DeviceName,
            Configuration.SampleRate,
            Configuration.ChannelCount,
            Configuration.BufferSize,
            RequiresPhysicalHardware,
            IsManualDebugOnly,
            State is AudioOutputDeviceState.Open or AudioOutputDeviceState.Started or AudioOutputDeviceState.Stopped,
            RenderCallbackCount: Interlocked.Read(ref _renderCallbackCount),
            DroppedBufferCount: Interlocked.Read(ref _renderDroppedBufferCount),
            UnderrunCount: Interlocked.Read(ref _underrunCount),
            IsStreaming: Volatile.Read(ref _isStreaming) == 1,
            LastRenderDuration: ReadOptionalTimeSpan(ref _lastRenderDurationTicks),
            MaximumRenderDuration: ReadOptionalTimeSpan(ref _maximumRenderDurationTicks),
            LastCallbackJitter: ReadOptionalTimeSpan(ref _lastCallbackJitterTicks),
            MaximumCallbackJitter: ReadOptionalTimeSpan(ref _maximumCallbackJitterTicks),
            LastTelemetryAge: ReadOptionalTimeSpan(ref _lastTelemetryAgeTicks));
    }

    public abstract ValueTask<AudioOutputDeviceResult> OpenAsync(
        AudioOutputConfiguration configuration,
        CancellationToken cancellationToken = default);

    public virtual ValueTask<AudioOutputDeviceResult> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State is AudioOutputDeviceState.Open or AudioOutputDeviceState.Stopped)
        {
            State = AudioOutputDeviceState.Started;
            StatusMessage = "Started";
            return SuccessAsync("Started");
        }

        return FailureAsync("Output device must be open before it can start.");
    }

    public virtual async ValueTask<AudioOutputDeviceResult> StartStreamingAsync(
        AudioOutputRenderCallback renderCallback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(renderCallback);
        cancellationToken.ThrowIfCancellationRequested();

        if (Volatile.Read(ref _isStreaming) == 1)
        {
            return AudioOutputDeviceResult.Success("Output streaming is already active.", GetStatus());
        }

        var startResult = await StartAsync(cancellationToken).ConfigureAwait(false);
        if (!startResult.Succeeded)
        {
            return startResult;
        }

        var streamingBuffer = AudioSampleBuffer.Allocate(Configuration);
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _streamingCancellation = cancellation;
        Volatile.Write(ref _isStreaming, 1);
        _streamingTask = Task.Factory.StartNew(
            () => RunStreamingLoop(streamingBuffer, renderCallback, cancellation.Token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        StatusMessage = "Output-owned render streaming started.";
        return AudioOutputDeviceResult.Success(StatusMessage, GetStatus());
    }

    public virtual async ValueTask<AudioOutputDeviceResult> StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await StopStreamingLoopAsync().ConfigureAwait(false);

        if (State == AudioOutputDeviceState.Started)
        {
            State = AudioOutputDeviceState.Stopped;
            StatusMessage = "Stopped";
            return AudioOutputDeviceResult.Success("Stopped", GetStatus());
        }

        if (State is AudioOutputDeviceState.Open or AudioOutputDeviceState.Stopped)
        {
            State = AudioOutputDeviceState.Stopped;
            StatusMessage = "Stopped";
            return AudioOutputDeviceResult.Success("Stopped", GetStatus());
        }

        return AudioOutputDeviceResult.Failure("Output device is not running.", GetStatus());
    }

    public virtual async ValueTask DisposeAsync()
    {
        await StopStreamingLoopAsync().ConfigureAwait(false);
        State = AudioOutputDeviceState.Disposed;
        StatusMessage = "Disposed";
    }

    public virtual ValueTask<AudioOutputDeviceResult> SubmitBufferAsync(
        AudioSampleBuffer buffer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        cancellationToken.ThrowIfCancellationRequested();

        if (State != AudioOutputDeviceState.Started)
        {
            return FailureAsync("Output device must be started before it can consume audio sample buffers.");
        }

        ValidateBufferMatchesConfiguration(buffer, Configuration);
        return FailureAsync("Sample buffer streaming is not implemented for this output device.");
    }

    protected virtual AudioOutputDeviceResult SubmitStreamingBuffer(AudioSampleBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (State != AudioOutputDeviceState.Started)
        {
            return AudioOutputDeviceResult.Failure(
                "Output device must be started before it can consume audio sample buffers.",
                GetStatus());
        }

        ValidateBufferMatchesConfiguration(buffer, Configuration);
        return AudioOutputDeviceResult.Failure(
            "Sample buffer streaming is not implemented for this output device.",
            GetStatus());
    }

    protected void RecordUnderrun()
    {
        Interlocked.Increment(ref _underrunCount);
    }

    protected void RecordDroppedBuffer()
    {
        Interlocked.Increment(ref _renderDroppedBufferCount);
    }

    protected async ValueTask StopStreamingLoopAsync()
    {
        var cancellation = Interlocked.Exchange(ref _streamingCancellation, null);
        var streamingTask = Interlocked.Exchange(ref _streamingTask, null);

        if (cancellation is not null)
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
        }

        if (streamingTask is not null)
        {
            try
            {
                await streamingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellation?.Dispose();
        Volatile.Write(ref _isStreaming, 0);
    }

    protected ValueTask<AudioOutputDeviceResult> SuccessAsync(string message)
    {
        return ValueTask.FromResult(AudioOutputDeviceResult.Success(message, GetStatus()));
    }

    protected ValueTask<AudioOutputDeviceResult> FailureAsync(string message)
    {
        StatusMessage = message;
        return ValueTask.FromResult(AudioOutputDeviceResult.Failure(message, GetStatus()));
    }

    protected static void ValidateConfiguration(AudioOutputConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.SampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Sample rate must be positive.");
        }

        if (configuration.ChannelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Channel count must be positive.");
        }

        if (configuration.BufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Buffer size must be positive.");
        }
    }

    protected static void ValidateBufferMatchesConfiguration(
        AudioSampleBuffer buffer,
        AudioOutputConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ValidateConfiguration(configuration);

        if (buffer.SampleRate != configuration.SampleRate)
        {
            throw new ArgumentException(
                $"Audio buffer sample rate {buffer.SampleRate} does not match output sample rate {configuration.SampleRate}.",
                nameof(buffer));
        }

        if (buffer.ChannelCount != configuration.ChannelCount)
        {
            throw new ArgumentException(
                $"Audio buffer channel count {buffer.ChannelCount} does not match output channel count {configuration.ChannelCount}.",
                nameof(buffer));
        }

        if (buffer.FrameCount != configuration.BufferSize)
        {
            throw new ArgumentException(
                $"Audio buffer frame count {buffer.FrameCount} does not match output buffer size {configuration.BufferSize}.",
                nameof(buffer));
        }
    }

    private void RunStreamingLoop(
        AudioSampleBuffer streamingBuffer,
        AudioOutputRenderCallback renderCallback,
        CancellationToken cancellationToken)
    {
        var expectedPeriod = TimeSpan.FromSeconds((double)Configuration.BufferSize / Configuration.SampleRate);
        var expectedPeriodTicks = Math.Max(1, (long)(Stopwatch.Frequency * expectedPeriod.TotalSeconds));
        var nextCallbackTimestamp = Stopwatch.GetTimestamp();
        var previousCallbackTimestamp = 0L;
        var callbackIndex = 0L;

        while (!cancellationToken.IsCancellationRequested)
        {
            var callbackStartedTimestamp = Stopwatch.GetTimestamp();
            TimeSpan? jitter = null;
            if (previousCallbackTimestamp != 0)
            {
                var actualPeriodTicks = callbackStartedTimestamp - previousCallbackTimestamp;
                jitter = TimeSpan.FromSeconds((double)(actualPeriodTicks - expectedPeriodTicks) / Stopwatch.Frequency);
            }

            previousCallbackTimestamp = callbackStartedTimestamp;
            callbackIndex++;

            var callbackStartedAtUtc = DateTimeOffset.UtcNow;
            var context = new AudioOutputRenderContext(
                callbackIndex,
                callbackStartedAtUtc,
                expectedPeriod,
                jitter);
            var renderStartedTimestamp = Stopwatch.GetTimestamp();

            AudioOutputRenderCallbackResult renderResult;
            try
            {
                renderResult = renderCallback(streamingBuffer, context);
            }
            catch
            {
                streamingBuffer.Clear();
                renderResult = AudioOutputRenderCallbackResult.Failure(RenderCallbackFailedMessage);
            }

            var renderDuration = Stopwatch.GetElapsedTime(renderStartedTimestamp);
            RecordRenderCallback(renderDuration, jitter, renderResult.TelemetryAge);

            if (renderResult.Succeeded)
            {
                var submitResult = SubmitStreamingBuffer(streamingBuffer);
                if (!submitResult.Succeeded)
                {
                    RecordDroppedBuffer();
                    StatusMessage = submitResult.Message;
                }
            }
            else
            {
                RecordDroppedBuffer();
                StatusMessage = renderResult.Message;
            }

            nextCallbackTimestamp += expectedPeriodTicks;
            WaitUntil(nextCallbackTimestamp, cancellationToken);
        }
    }

    private void RecordRenderCallback(
        TimeSpan renderDuration,
        TimeSpan? jitter,
        TimeSpan? telemetryAge)
    {
        Interlocked.Increment(ref _renderCallbackCount);
        Interlocked.Exchange(ref _lastRenderDurationTicks, renderDuration.Ticks);
        UpdateMaximumTicks(ref _maximumRenderDurationTicks, renderDuration.Ticks);

        if (jitter is not null)
        {
            var absoluteJitterTicks = Math.Abs(jitter.Value.Ticks);
            Interlocked.Exchange(ref _lastCallbackJitterTicks, jitter.Value.Ticks);
            UpdateMaximumTicks(ref _maximumCallbackJitterTicks, absoluteJitterTicks);
        }

        Interlocked.Exchange(ref _lastTelemetryAgeTicks, telemetryAge?.Ticks ?? UnsetTimeSpanTicks);
    }

    private static void WaitUntil(long targetTimestamp, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var remainingTicks = targetTimestamp - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
            {
                return;
            }

            var remaining = TimeSpan.FromSeconds((double)remainingTicks / Stopwatch.Frequency);
            if (remaining > TimeSpan.FromMilliseconds(1))
            {
                cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(Math.Min(remaining.TotalMilliseconds, 2)));
                continue;
            }

            Thread.SpinWait(32);
        }
    }

    private static void UpdateMaximumTicks(ref long target, long candidate)
    {
        long current;
        do
        {
            current = Interlocked.Read(ref target);
            if (current != UnsetTimeSpanTicks && candidate <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref target, candidate, current) != current);
    }

    private static TimeSpan? ReadOptionalTimeSpan(ref long ticks)
    {
        var value = Interlocked.Read(ref ticks);
        return value == UnsetTimeSpanTicks ? null : TimeSpan.FromTicks(value);
    }
}
