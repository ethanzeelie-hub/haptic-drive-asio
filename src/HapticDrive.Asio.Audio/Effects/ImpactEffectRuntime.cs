using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class ImpactEffectRuntime : BufferedHapticEffectRuntime
{
    private readonly ImpactEffect _effect;

    public ImpactEffectRuntime(EffectSettingsDocument settings)
        : base("impact", "Impact")
    {
        _effect = new ImpactEffect();
        ApplySettings(settings.Parameters);
    }

    public ImpactEffectSnapshot Snapshot => _effect.Snapshot;

    protected override void ResetCore()
    {
        _effect.Reset();
    }

    protected override void ApplySettingsCore(IReadOnlyDictionary<string, double> parameters)
    {
        _effect.UpdateOptions(HapticEffectSettingsTranslator.ToImpactEffectOptions(parameters));
    }

    protected override void RenderCore(in HapticRenderFrame frame, AudioSampleBuffer buffer)
    {
        _effect.Update(new HapticEffectInput(frame));
        _effect.Render(buffer);
    }
}
