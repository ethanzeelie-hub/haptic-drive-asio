using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.TestBench;

public interface IAudioTestSignalGenerator
{
    AudioTestSignalDefinition Definition { get; }

    void Reset();

    void Generate(AudioSampleBuffer destination);
}
