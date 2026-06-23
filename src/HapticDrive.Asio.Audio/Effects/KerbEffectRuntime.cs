using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class KerbEffectRuntime : BufferedHapticEffectRuntime
{
    private readonly KerbEffect _effect;

    public KerbEffectRuntime(EffectSettingsDocument settings)
        : base("kerb", "Kerb")
    {
        _effect = new KerbEffect();
        ApplySettings(settings.Parameters);
    }

    public KerbEffectSnapshot Snapshot => _effect.Snapshot;

    protected override void ResetCore()
    {
        _effect.Reset();
    }

    protected override void ApplySettingsCore(IReadOnlyDictionary<string, double> parameters)
    {
        _effect.UpdateOptions(HapticEffectSettingsTranslator.ToKerbEffectOptions(parameters));
    }

    protected override void RenderCore(in HapticRenderFrame frame, AudioSampleBuffer buffer)
    {
        _effect.Update(new HapticEffectInput(frame));
        _effect.Render(buffer);
    }
}
