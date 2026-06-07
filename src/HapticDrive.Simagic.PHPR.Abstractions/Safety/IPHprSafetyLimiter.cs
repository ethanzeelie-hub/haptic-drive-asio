using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public interface IPHprSafetyLimiter
{
    PHprSafetyLimits Limits { get; }

    PHprSafetyDecision Evaluate(PHprCommand command, PHprSafetyContext? context = null);

    PHprSafetyDecision RecordEmergencyStop(PHprSafetyContext? context = null);

    void ClearEmergencyStop();

    void Reset();

    PHprSafetySnapshot GetSnapshot(PHprSafetyContext? context = null);
}
