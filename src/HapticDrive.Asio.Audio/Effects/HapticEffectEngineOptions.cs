namespace HapticDrive.Asio.Audio.Effects;

public sealed record HapticEffectEngineOptions(
    EngineVibrationEffectOptions Engine,
    GearShiftEffectOptions GearShift)
{
    public static HapticEffectEngineOptions Default { get; } = new(
        EngineVibrationEffectOptions.Default,
        GearShiftEffectOptions.Default);
}
