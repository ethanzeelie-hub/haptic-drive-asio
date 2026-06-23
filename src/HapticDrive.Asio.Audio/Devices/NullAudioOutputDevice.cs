using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Devices;

public sealed class NullAudioOutputDevice : AudioOutputDeviceBase
{
    private const string BufferConsumedStatusMessage = "Null output consumed a buffer.";
    private long _submittedBufferCount;
    private long _submittedFrameCount;
    private long _submittedSampleCount;
    private float _lastPeakLevel;

    public NullAudioOutputDevice()
        : base(AudioOutputDeviceKind.Null, "Null Output")
    {
    }

    public override bool RequiresPhysicalHardware => false;

    public override AudioOutputStatus GetStatus()
    {
        var status = base.GetStatus();
        return status with
        {
            SubmittedBufferCount = Interlocked.Read(ref _submittedBufferCount)
        };
    }

    public override ValueTask<AudioOutputDeviceResult> OpenAsync(
        AudioOutputConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Configuration = configuration;
        DeviceName = "NullAudioOutputDevice";
        State = AudioOutputDeviceState.Open;
        StatusMessage = "Null output ready. Audio samples are discarded deterministically.";
        return SuccessAsync(StatusMessage);
    }

    public override ValueTask<AudioOutputDeviceResult> SubmitBufferAsync(
        AudioSampleBuffer buffer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(ConsumeBuffer(buffer));
    }

    protected override AudioOutputDeviceResult SubmitStreamingBuffer(AudioSampleBuffer buffer)
    {
        return ConsumeBuffer(buffer);
    }

    private AudioOutputDeviceResult ConsumeBuffer(AudioSampleBuffer buffer)
    {
        if (State != AudioOutputDeviceState.Started)
        {
            return AudioOutputDeviceResult.Failure(
                "Null output must be started before it can consume audio sample buffers.",
                GetStatus());
        }

        ValidateBufferMatchesConfiguration(buffer, Configuration);

        var peakLevel = 0f;
        foreach (var sample in buffer.Samples)
        {
            if (!float.IsFinite(sample))
            {
                continue;
            }

            peakLevel = Math.Max(peakLevel, Math.Abs(sample));
        }

        Interlocked.Increment(ref _submittedBufferCount);
        Interlocked.Add(ref _submittedFrameCount, buffer.FrameCount);
        Interlocked.Add(ref _submittedSampleCount, buffer.SampleCount);
        Volatile.Write(ref _lastPeakLevel, peakLevel);

        StatusMessage = BufferConsumedStatusMessage;
        return AudioOutputDeviceResult.Success(StatusMessage, GetStatus());
    }

    public NullAudioOutputDeviceSnapshot GetSampleSinkSnapshot()
    {
        return new NullAudioOutputDeviceSnapshot(
            Interlocked.Read(ref _submittedBufferCount),
            Interlocked.Read(ref _submittedFrameCount),
            Interlocked.Read(ref _submittedSampleCount),
            Volatile.Read(ref _lastPeakLevel));
    }
}

public sealed record NullAudioOutputDeviceSnapshot(
    long SubmittedBufferCount,
    long SubmittedFrameCount,
    long SubmittedSampleCount,
    float LastPeakLevel);
