using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class RoadTextureEffectRuntime : BufferedHapticEffectRuntime
{
    private readonly RoadTextureEffect _effect;

    public RoadTextureEffectRuntime(EffectSettingsDocument settings)
        : base("road-texture", "Road texture")
    {
        _effect = new RoadTextureEffect();
        ApplySettings(settings.Parameters);
    }

    public RoadTextureEffectSnapshot Snapshot => _effect.Snapshot;

    public void NotifyGearPulseAccepted(DateTimeOffset? timestampUtc = null)
    {
        _effect.NotifyGearPulseAccepted(timestampUtc);
    }

    protected override void ResetCore()
    {
        _effect.Reset();
    }

    protected override void ApplySettingsCore(IReadOnlyDictionary<string, double> parameters)
    {
        _effect.UpdateOptions(HapticEffectSettingsTranslator.ToRoadTextureEffectOptions(parameters));
    }

    protected override void RenderCore(in HapticRenderFrame frame, AudioSampleBuffer buffer)
    {
        _effect.Update(new HapticEffectInput(frame));
        _effect.Render(buffer);
    }
}
