using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects.Registry;

public interface IHapticEffectRuntime
{
    string Key { get; }

    void Reset();

    void ApplySettings(IReadOnlyDictionary<string, double> parameters);

    void Render(in HapticRenderFrame frame, Span<float> left, Span<float> right, int sampleRate, int frameCount);
}
