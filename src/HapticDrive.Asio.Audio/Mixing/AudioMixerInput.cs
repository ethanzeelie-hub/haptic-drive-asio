using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Mixing;

public sealed record AudioMixerInput
{
    public AudioMixerInput(
        AudioSampleBuffer buffer,
        float gain = 1f,
        bool isMuted = false,
        string? name = null)
    {
        Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        Gain = gain;
        IsMuted = isMuted;
        Name = string.IsNullOrWhiteSpace(name) ? "Source" : name;
    }

    public AudioSampleBuffer Buffer { get; }

    public float Gain { get; }

    public bool IsMuted { get; }

    public string Name { get; }
}
