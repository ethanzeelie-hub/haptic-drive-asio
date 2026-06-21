using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Effects;

public interface IHapticEffectSource
{
    string Name { get; }

    void Reset();

    void Update(HapticEffectInput input);

    HapticEffectRenderResult Render(AudioSampleBuffer destination);
}

public sealed record HapticEffectRenderResult(
    string Name,
    bool IsEnabled,
    bool IsActive,
    float PeakLevel);
