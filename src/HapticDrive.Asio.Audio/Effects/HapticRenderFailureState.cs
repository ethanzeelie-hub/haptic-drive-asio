namespace HapticDrive.Asio.Audio.Effects;

public enum HapticRenderFailureCode
{
    None = 0,
    RuntimeException = 1
}

public readonly record struct HapticRenderFailureState(
    long FailureCount,
    HapticRenderFailureCode LastFailureCode);
