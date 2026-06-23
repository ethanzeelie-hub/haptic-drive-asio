using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public readonly record struct HapticEffectInput(
    HapticRenderFrame RenderFrame)
{
    public HapticFrame Frame => RenderFrame.Frame;

    public HapticFrameFreshnessSnapshot Freshness => RenderFrame.Freshness;
}
