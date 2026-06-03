using HapticDrive.Asio.Core.Audio;
using NAudio.Wave;
using System.Diagnostics;

namespace HapticDrive.Asio.Audio.Devices;

public sealed class NativeAsioOutputBackend : IAsioOutputBackend
{
    private const int QueueCapacity = 3;

    private readonly object _gate = new();
    private AsioOut? _asioOut;
    private QueuedAsioWaveProvider? _waveProvider;
    private string? _driverName;
    private string? _lastError;
    private int _sampleRate;
    private int _bufferSize;
    private int _outputChannelCount;
    private bool _isOpen;
    private bool _isRunning;

    public AsioOutputBackendSnapshot GetSnapshot()
    {
        var provider = Volatile.Read(ref _waveProvider);
        var providerSnapshot = provider?.GetSnapshot() ?? QueuedAsioWaveProviderSnapshot.Empty;

        lock (_gate)
        {
            return new AsioOutputBackendSnapshot(
                _isOpen,
                _isRunning,
                _driverName,
                _sampleRate,
                _bufferSize,
                _outputChannelCount,
                providerSnapshot.SubmittedBufferCount,
                providerSnapshot.DroppedBufferCount,
                providerSnapshot.CallbackCount,
                providerSnapshot.UnderrunCount,
                providerSnapshot.QueuedBufferCount,
                providerSnapshot.LastCallbackJitter,
                providerSnapshot.MaximumCallbackJitter,
                _lastError);
        }
    }

    public ValueTask<AsioOutputBackendOpenResult> OpenAsync(
        string driverName,
        AudioOutputConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(driverName);
        ArgumentNullException.ThrowIfNull(configuration);

        lock (_gate)
        {
            if (_isOpen)
            {
                return ValueTask.FromResult(AsioOutputBackendOpenResult.Success(
                    "Native ASIO backend is already open.",
                    _sampleRate,
                    _bufferSize,
                    _outputChannelCount));
            }

            try
            {
                _asioOut = new AsioOut(driverName);
                _asioOut.PlaybackStopped += AsioOut_PlaybackStopped;
                _asioOut.ChannelOffset = 0;

                _driverName = driverName;
                _sampleRate = configuration.SampleRate;
                _bufferSize = configuration.BufferSize;
                _outputChannelCount = _asioOut.DriverOutputChannelCount;

                if (_outputChannelCount <= 0)
                {
                    DisposeAsioOut();
                    _lastError = $"ASIO driver '{driverName}' reported no output channels.";
                    return ValueTask.FromResult(AsioOutputBackendOpenResult.Failure(_lastError));
                }

                _waveProvider = new QueuedAsioWaveProvider(
                    _sampleRate,
                    _outputChannelCount,
                    _bufferSize,
                    QueueCapacity);
                _asioOut.Init(_waveProvider);
                _isOpen = true;
                _lastError = null;

                return ValueTask.FromResult(AsioOutputBackendOpenResult.Success(
                    $"Native ASIO backend opened '{driverName}'.",
                    _sampleRate,
                    _bufferSize,
                    _outputChannelCount));
            }
            catch (Exception ex)
            {
                DisposeAsioOut();
                _isOpen = false;
                _isRunning = false;
                _lastError = $"Native ASIO backend could not open '{driverName}': {ex.Message}";
                return ValueTask.FromResult(AsioOutputBackendOpenResult.Failure(_lastError, ex.Message));
            }
        }
    }

    public ValueTask<AsioOutputBackendOperationResult> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_isOpen || _asioOut is null)
            {
                _lastError = "Native ASIO backend must be open before it can start.";
                return ValueTask.FromResult(AsioOutputBackendOperationResult.Failure(_lastError));
            }

            if (_isRunning)
            {
                return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("Native ASIO backend is already running."));
            }

            try
            {
                _waveProvider?.Reset();
                _asioOut.Play();
                _isRunning = true;
                _lastError = null;
                return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("Native ASIO backend started."));
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _lastError = $"Native ASIO backend could not start: {ex.Message}";
                return ValueTask.FromResult(AsioOutputBackendOperationResult.Failure(_lastError, ex.Message));
            }
        }
    }

    public ValueTask<AsioOutputBackendOperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_asioOut is null)
            {
                _isRunning = false;
                return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("Native ASIO backend already stopped."));
            }

            try
            {
                _asioOut.Stop();
                _waveProvider?.Reset();
                _isRunning = false;
                return ValueTask.FromResult(AsioOutputBackendOperationResult.Success("Native ASIO backend stopped."));
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _lastError = $"Native ASIO backend stopped with warning: {ex.Message}";
                return ValueTask.FromResult(AsioOutputBackendOperationResult.Failure(_lastError, ex.Message));
            }
        }
    }

    public AsioOutputBackendOperationResult Submit(
        ReadOnlyMemory<float> interleavedSamples,
        int sampleRate,
        int frameCount,
        int outputChannelCount)
    {
        var provider = Volatile.Read(ref _waveProvider);
        if (!_isRunning || provider is null)
        {
            _lastError = "Native ASIO backend is not running.";
            return AsioOutputBackendOperationResult.Failure(_lastError);
        }

        if (sampleRate != _sampleRate
            || frameCount != _bufferSize
            || outputChannelCount != _outputChannelCount)
        {
            _lastError = "Native ASIO backend rejected a buffer with mismatched sample format.";
            return AsioOutputBackendOperationResult.Failure(_lastError);
        }

        return provider.TryEnqueue(interleavedSamples.Span)
            ? AsioOutputBackendOperationResult.Success("Native ASIO backend queued buffer.")
            : AsioOutputBackendOperationResult.Failure("Native ASIO backend queue is full; buffer dropped.");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);

        lock (_gate)
        {
            DisposeAsioOut();
            _waveProvider = null;
            _isOpen = false;
            _isRunning = false;
        }
    }

    private void AsioOut_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_gate)
        {
            _isRunning = false;
            if (e.Exception is not null)
            {
                _lastError = $"Native ASIO playback stopped: {e.Exception.Message}";
            }
        }
    }

    private void DisposeAsioOut()
    {
        if (_asioOut is null)
        {
            return;
        }

        _asioOut.PlaybackStopped -= AsioOut_PlaybackStopped;
        _asioOut.Dispose();
        _asioOut = null;
    }

    private sealed class QueuedAsioWaveProvider : IWaveProvider
    {
        private readonly object _gate = new();
        private readonly float[][] _slots;
        private readonly int _samplesPerBuffer;
        private readonly TimeSpan _expectedPeriod;
        private int _readSlotIndex;
        private int _writeSlotIndex;
        private int _queuedBufferCount;
        private int _readSampleOffset;
        private long _lastCallbackTimestamp;
        private long _submittedBufferCount;
        private long _droppedBufferCount;
        private long _callbackCount;
        private long _underrunCount;
        private long _lastCallbackJitterTicks;
        private long _maximumCallbackJitterTicks;

        public QueuedAsioWaveProvider(
            int sampleRate,
            int channelCount,
            int framesPerBuffer,
            int queueCapacity)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount);
            _samplesPerBuffer = checked(framesPerBuffer * channelCount);
            _expectedPeriod = TimeSpan.FromSeconds((double)framesPerBuffer / sampleRate);
            _slots = new float[queueCapacity][];
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new float[_samplesPerBuffer];
            }
        }

        public WaveFormat WaveFormat { get; }

        public int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            var callbackTimestamp = Stopwatch.GetTimestamp();
            RecordCallback(callbackTimestamp, count);

            var bytesWritten = 0;
            while (bytesWritten < count)
            {
                var copied = CopyQueuedSamples(buffer, offset + bytesWritten, count - bytesWritten);
                if (copied > 0)
                {
                    bytesWritten += copied;
                    continue;
                }

                Array.Clear(buffer, offset + bytesWritten, count - bytesWritten);
                Interlocked.Increment(ref _underrunCount);
                return count;
            }

            return bytesWritten;
        }

        public bool TryEnqueue(ReadOnlySpan<float> samples)
        {
            if (samples.Length != _samplesPerBuffer)
            {
                Interlocked.Increment(ref _droppedBufferCount);
                return false;
            }

            lock (_gate)
            {
                if (_queuedBufferCount == _slots.Length)
                {
                    Interlocked.Increment(ref _droppedBufferCount);
                    return false;
                }

                samples.CopyTo(_slots[_writeSlotIndex]);
                _writeSlotIndex = (_writeSlotIndex + 1) % _slots.Length;
                _queuedBufferCount++;
            }

            Interlocked.Increment(ref _submittedBufferCount);
            return true;
        }

        public void Reset()
        {
            lock (_gate)
            {
                _readSlotIndex = 0;
                _writeSlotIndex = 0;
                _queuedBufferCount = 0;
                _readSampleOffset = 0;
            }
        }

        public QueuedAsioWaveProviderSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                return new QueuedAsioWaveProviderSnapshot(
                    Interlocked.Read(ref _submittedBufferCount),
                    Interlocked.Read(ref _droppedBufferCount),
                    Interlocked.Read(ref _callbackCount),
                    Interlocked.Read(ref _underrunCount),
                    _queuedBufferCount,
                    ReadOptionalTimeSpan(ref _lastCallbackJitterTicks),
                    ReadOptionalTimeSpan(ref _maximumCallbackJitterTicks));
            }
        }

        private int CopyQueuedSamples(byte[] buffer, int offset, int count)
        {
            lock (_gate)
            {
                if (_queuedBufferCount == 0)
                {
                    return 0;
                }

                var currentSlot = _slots[_readSlotIndex];
                var availableSamples = _samplesPerBuffer - _readSampleOffset;
                var requestedSamples = count / sizeof(float);
                var samplesToCopy = Math.Min(availableSamples, requestedSamples);
                var bytesToCopy = samplesToCopy * sizeof(float);
                Buffer.BlockCopy(
                    currentSlot,
                    _readSampleOffset * sizeof(float),
                    buffer,
                    offset,
                    bytesToCopy);

                _readSampleOffset += samplesToCopy;
                if (_readSampleOffset == _samplesPerBuffer)
                {
                    Array.Clear(currentSlot);
                    _readSampleOffset = 0;
                    _readSlotIndex = (_readSlotIndex + 1) % _slots.Length;
                    _queuedBufferCount--;
                }

                return bytesToCopy;
            }
        }

        private void RecordCallback(long callbackTimestamp, int requestedByteCount)
        {
            Interlocked.Increment(ref _callbackCount);
            var previousTimestamp = Interlocked.Exchange(ref _lastCallbackTimestamp, callbackTimestamp);
            if (previousTimestamp == 0)
            {
                return;
            }

            var requestedSamples = requestedByteCount / sizeof(float);
            var requestedFrames = requestedSamples / Math.Max(1, WaveFormat.Channels);
            var expectedPeriod = requestedFrames == 0
                ? _expectedPeriod
                : TimeSpan.FromSeconds((double)requestedFrames / WaveFormat.SampleRate);
            var actualPeriod = Stopwatch.GetElapsedTime(previousTimestamp, callbackTimestamp);
            var jitter = actualPeriod - expectedPeriod;
            var absoluteJitterTicks = Math.Abs(jitter.Ticks);
            Interlocked.Exchange(ref _lastCallbackJitterTicks, jitter.Ticks);
            UpdateMaximumTicks(ref _maximumCallbackJitterTicks, absoluteJitterTicks);
        }

        private static void UpdateMaximumTicks(ref long target, long candidate)
        {
            long current;
            do
            {
                current = Interlocked.Read(ref target);
                if (candidate <= current)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref target, candidate, current) != current);
        }

        private static TimeSpan? ReadOptionalTimeSpan(ref long ticks)
        {
            var value = Interlocked.Read(ref ticks);
            return value == 0 ? null : TimeSpan.FromTicks(value);
        }
    }

    private sealed record QueuedAsioWaveProviderSnapshot(
        long SubmittedBufferCount,
        long DroppedBufferCount,
        long CallbackCount,
        long UnderrunCount,
        int QueuedBufferCount,
        TimeSpan? LastCallbackJitter,
        TimeSpan? MaximumCallbackJitter)
    {
        public static QueuedAsioWaveProviderSnapshot Empty { get; } = new(
            SubmittedBufferCount: 0,
            DroppedBufferCount: 0,
            CallbackCount: 0,
            UnderrunCount: 0,
            QueuedBufferCount: 0,
            LastCallbackJitter: null,
            MaximumCallbackJitter: null);
    }
}
