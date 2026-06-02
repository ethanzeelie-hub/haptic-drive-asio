using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Pipeline;

public sealed class AudioRenderPipeline
{
    private readonly AudioMixer _mixer = new();
    private readonly AudioSafetyProcessor _safetyProcessor = new();
    private readonly AudioSampleBuffer _mixBuffer;

    public AudioRenderPipeline(AudioSampleFormat format)
    {
        Format = format ?? throw new ArgumentNullException(nameof(format));
        _mixBuffer = AudioSampleBuffer.Allocate(format);
    }

    public AudioSampleFormat Format { get; }

    public AudioMixerSettings MixerSettings { get; set; } = AudioMixerSettings.Default;

    public AudioSafetyProcessorOptions SafetyOptions { get; set; } = AudioSafetyProcessorOptions.Default;

    public AudioRenderPipelineSnapshot Process(
        IReadOnlyList<AudioMixerInput>? inputs,
        AudioSampleBuffer outputBuffer)
    {
        ArgumentNullException.ThrowIfNull(outputBuffer);
        AudioSampleBuffer.EnsureSameFormat(Format, outputBuffer.Format);

        var mixerSnapshot = _mixer.Mix(inputs, _mixBuffer, MixerSettings);
        var safetyOptions = SafetyOptions with
        {
            EmergencyMute = SafetyOptions.EmergencyMute || MixerSettings.EmergencyMute
        };
        var safetySnapshot = _safetyProcessor.Process(_mixBuffer, outputBuffer, safetyOptions);

        return new AudioRenderPipelineSnapshot(
            mixerSnapshot.IsRunning,
            mixerSnapshot.IsMuted,
            mixerSnapshot.EmergencyMute || safetySnapshot.EmergencyMute,
            mixerSnapshot.ActiveSourceCount,
            mixerSnapshot.PeakLevel,
            safetySnapshot.OutputPeakLevel,
            safetySnapshot.SanitizedSampleCount,
            safetySnapshot.LimitedSampleCount,
            safetySnapshot.ClippedSampleCount,
            mixerSnapshot,
            safetySnapshot);
    }

    public async ValueTask<AudioOutputDeviceResult> ProcessAndSubmitAsync(
        IReadOnlyList<AudioMixerInput>? inputs,
        AudioSampleBuffer outputBuffer,
        IAudioOutputDevice outputDevice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outputDevice);
        Process(inputs, outputBuffer);
        return await outputDevice.SubmitBufferAsync(outputBuffer, cancellationToken).ConfigureAwait(false);
    }
}
