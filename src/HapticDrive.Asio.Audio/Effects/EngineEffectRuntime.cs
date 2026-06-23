using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class EngineEffectRuntime : BufferedHapticEffectRuntime
{
    private readonly EngineVibrationEffect _effect;

    public EngineEffectRuntime(EffectSettingsDocument settings)
        : base("engine-rpm", "Engine vibration")
    {
        _effect = new EngineVibrationEffect();
        ApplySettings(settings.Parameters);
    }

    public EngineVibrationEffectSnapshot Snapshot => _effect.Snapshot;

    protected override void ResetCore()
    {
        _effect.Reset();
    }

    protected override void ApplySettingsCore(IReadOnlyDictionary<string, double> parameters)
    {
        _effect.UpdateOptions(HapticEffectSettingsTranslator.ToEngineVibrationEffectOptions(parameters));
    }

    protected override void RenderCore(in HapticRenderFrame frame, AudioSampleBuffer buffer)
    {
        _effect.Update(new HapticEffectInput(frame));
        _effect.Render(buffer);
    }
}
