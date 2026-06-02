namespace HapticDrive.Asio.Audio.Effects;

public sealed record HapticEffectEngineOptions(
    EngineVibrationEffectOptions Engine,
    GearShiftEffectOptions GearShift,
    KerbEffectOptions Kerb,
    ImpactEffectOptions Impact,
    RoadTextureEffectOptions RoadTexture,
    SlipEffectOptions Slip)
{
    public static HapticEffectEngineOptions Default { get; } = new(
        EngineVibrationEffectOptions.Default,
        GearShiftEffectOptions.Default,
        KerbEffectOptions.Default,
        ImpactEffectOptions.Default,
        RoadTextureEffectOptions.Default,
        SlipEffectOptions.Default);
}
