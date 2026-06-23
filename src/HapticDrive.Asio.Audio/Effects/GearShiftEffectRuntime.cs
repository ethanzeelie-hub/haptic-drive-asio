using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class GearShiftEffectRuntime : BufferedHapticEffectRuntime
{
    private readonly GearShiftEffect _effect;

    public GearShiftEffectRuntime(EffectSettingsDocument settings)
        : base("gear-shift", "Gear shift")
    {
        _effect = new GearShiftEffect();
        ApplySettings(settings.Parameters);
    }

    public GearShiftEffectSnapshot Snapshot => _effect.Snapshot;

    protected override void ResetCore()
    {
        _effect.Reset();
    }

    protected override void ApplySettingsCore(IReadOnlyDictionary<string, double> parameters)
    {
        _effect.UpdateOptions(HapticEffectSettingsTranslator.ToGearShiftEffectOptions(parameters));
    }

    protected override void RenderCore(in HapticRenderFrame frame, AudioSampleBuffer buffer)
    {
        _effect.Update(new HapticEffectInput(frame));
        _effect.Render(buffer);
    }
}
