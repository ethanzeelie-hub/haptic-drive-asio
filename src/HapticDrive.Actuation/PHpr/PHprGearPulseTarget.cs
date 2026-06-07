using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Actuation.PHpr;

public enum PHprGearPulseTarget
{
    Brake = 0,
    Throttle = 1,
    Both = 2
}

public static class PHprGearPulseTargetExtensions
{
    public static PHprModuleId ToModuleId(this PHprGearPulseTarget target)
    {
        return target switch
        {
            PHprGearPulseTarget.Brake => PHprModuleId.Brake,
            PHprGearPulseTarget.Throttle => PHprModuleId.Throttle,
            PHprGearPulseTarget.Both => PHprModuleId.Both,
            _ => PHprModuleId.Both
        };
    }
}
