using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Simagic.PHPR.Abstractions.Output;

public sealed record PHprCommandResult(
    bool Succeeded,
    PHprCommandStatus Status,
    string Message,
    PHprCommand? Command)
{
    public static PHprCommandResult Accepted(PHprCommand command, string message)
    {
        return new PHprCommandResult(true, PHprCommandStatus.Accepted, message, command);
    }

    public static PHprCommandResult Rejected(PHprCommandStatus status, string message, PHprCommand? command = null)
    {
        return new PHprCommandResult(false, status, message, command);
    }
}
