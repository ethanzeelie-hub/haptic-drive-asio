using HapticDrive.Asio.Core.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed class MockPhprOutputSafetyParticipant : IOutputSafetyParticipant
{
    private readonly PHprGearPulseRouter _gearPulseRouter;
    private readonly PHprPedalEffectsRouter? _pedalEffectsRouter;
    private OutputSafetyParticipantSnapshot _current;

    public MockPhprOutputSafetyParticipant(
        PHprGearPulseRouter gearPulseRouter,
        PHprPedalEffectsRouter? pedalEffectsRouter = null,
        string? name = null)
    {
        _gearPulseRouter = gearPulseRouter ?? throw new ArgumentNullException(nameof(gearPulseRouter));
        _pedalEffectsRouter = pedalEffectsRouter;
        Name = string.IsNullOrWhiteSpace(name) ? "Mock P-HPR output" : name.Trim();
        _current = CreateSnapshot(isSilent: true, hasFault: false, "Mock P-HPR participant ready.");
    }

    public string Name { get; }

    public OutputSafetyParticipantSnapshot Current => _current;

    public async ValueTask SilenceAsync(OutputInterlockSnapshot interlock, CancellationToken cancellationToken)
    {
        await _gearPulseRouter.EmergencyStopAsync(cancellationToken).ConfigureAwait(false);
        if (_pedalEffectsRouter is not null)
        {
            await _pedalEffectsRouter.EmergencyStopAsync(cancellationToken).ConfigureAwait(false);
        }

        var gearSnapshot = _gearPulseRouter.GetSnapshot();
        var pedalSnapshot = _pedalEffectsRouter?.GetSnapshot();
        var silent = gearSnapshot.EmergencyStopActive
            && (pedalSnapshot is null || pedalSnapshot.EmergencyStopActive);
        _current = CreateSnapshot(
            silent,
            hasFault: false,
            "Mock P-HPR output emergency-stopped by global output interlock.");
    }

    public bool CanReset(out string blocker)
    {
        if (_current.HasFault)
        {
            blocker = _current.Message;
            return false;
        }

        blocker = string.Empty;
        return true;
    }

    public void OnInterlockReset(OutputInterlockSnapshot interlock)
    {
        _gearPulseRouter.ClearEmergencyStop();
        _pedalEffectsRouter?.ClearEmergencyStop();
        _current = CreateSnapshot(isSilent: true, hasFault: false, "Mock P-HPR output interlock reset observed.");
    }

    private OutputSafetyParticipantSnapshot CreateSnapshot(bool isSilent, bool hasFault, string message)
    {
        return new OutputSafetyParticipantSnapshot(Name, isSilent, hasFault, message);
    }
}
