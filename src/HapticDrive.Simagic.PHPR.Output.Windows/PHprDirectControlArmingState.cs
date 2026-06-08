namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprDirectControlArmingState(
    bool DirectControlEnabled,
    bool DirectControlArmed,
    DateTimeOffset? ArmedAtUtc = null)
{
    public static PHprDirectControlArmingState Disabled { get; } = new(false, false);

    public bool CanSend => DirectControlEnabled && DirectControlArmed;
}
