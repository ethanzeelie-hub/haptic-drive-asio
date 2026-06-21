using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects.Registry;

public interface IHapticEffectRuntime
{
    string EffectKey { get; }

    void UpdateSettings(EffectSettingsDocument settings);

    void Render(
        in HapticFrame frame,
        Span<float> left,
        Span<float> right,
        int sampleRate,
        int frameCount);
}
