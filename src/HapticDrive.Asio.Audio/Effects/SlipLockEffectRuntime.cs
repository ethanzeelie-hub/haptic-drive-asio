using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class SlipLockEffectRuntime : BufferedHapticEffectRuntime
{
    private readonly SlipEffect _effect;

    public SlipLockEffectRuntime(EffectSettingsDocument settings)
        : base("slip-lock", "Slip")
    {
        _effect = new SlipEffect();
        ApplySettings(settings.Parameters);
    }

    public SlipEffectSnapshot Snapshot => _effect.Snapshot;

    protected override void ResetCore()
    {
        _effect.Reset();
    }

    protected override void ApplySettingsCore(IReadOnlyDictionary<string, double> parameters)
    {
        _effect.UpdateOptions(HapticEffectSettingsTranslator.ToSlipEffectOptions(parameters));
    }

    protected override void RenderCore(in HapticRenderFrame frame, AudioSampleBuffer buffer)
    {
        _effect.Update(new HapticEffectInput(frame));
        _effect.Render(buffer);
    }
}
