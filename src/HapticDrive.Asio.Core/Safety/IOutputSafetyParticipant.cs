namespace HapticDrive.Asio.Core.Safety;

public interface IOutputSafetyParticipant
{
    string Name { get; }

    OutputSafetyParticipantSnapshot Current { get; }

    ValueTask SilenceAsync(OutputInterlockSnapshot interlock, CancellationToken cancellationToken);

    bool CanReset(out string blocker);

    void OnInterlockReset(OutputInterlockSnapshot interlock);
}
