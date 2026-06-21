namespace HapticDrive.Asio.Audio.Effects.Registry;

public interface IHapticEffectRegistry
{
    IReadOnlyList<IHapticEffectDescriptor> All { get; }

    IHapticEffectDescriptor GetRequired(string key);
}
