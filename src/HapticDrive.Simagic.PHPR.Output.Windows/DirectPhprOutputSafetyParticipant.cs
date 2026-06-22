using HapticDrive.Asio.Core.Safety;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

internal sealed class DirectPhprOutputSafetyParticipant : IOutputSafetyParticipant
{
    private readonly IPHprDirectRuntime _runtime;
    private readonly IPHprWriteAuthorization _writeAuthorization;
    private OutputSafetyParticipantSnapshot _current;

    public DirectPhprOutputSafetyParticipant(
        IPHprDirectRuntime runtime,
        IPHprWriteAuthorization writeAuthorization,
        string? name = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _writeAuthorization = writeAuthorization ?? throw new ArgumentNullException(nameof(writeAuthorization));
        Name = string.IsNullOrWhiteSpace(name) ? "Direct P-HPR output" : name.Trim();
        _current = CreateSnapshot(isSilent: IsSilent(), hasFault: false, "Direct P-HPR participant ready.");
    }

    public string Name { get; }

    public OutputSafetyParticipantSnapshot Current => _current;

    public async ValueTask SilenceAsync(OutputInterlockSnapshot interlock, CancellationToken cancellationToken)
    {
        _writeAuthorization.Revoke($"Global output interlock latched: {interlock.Reason}.");
        await _runtime.EmergencyStopAsync(interlock.Message, cancellationToken).ConfigureAwait(false);
        _current = CreateSnapshot(IsSilent(), hasFault: false, "Direct P-HPR authorization revoked and output emergency-stopped by global output interlock.");
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
        _runtime.ClearEmergencyStop();
        _current = CreateSnapshot(IsSilent(), hasFault: false, "Direct P-HPR output interlock reset observed; reauthorization is still required.");
    }

    private bool IsSilent()
    {
        var snapshot = _runtime.GetSnapshot();
        return !snapshot.HardwareBelievedActive && snapshot.PendingStopCount == 0;
    }

    private OutputSafetyParticipantSnapshot CreateSnapshot(bool isSilent, bool hasFault, string message)
    {
        return new OutputSafetyParticipantSnapshot(Name, isSilent, hasFault, message);
    }
}
