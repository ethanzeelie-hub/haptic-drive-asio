using HapticDrive.Asio.Core.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed class ContinuousPhprOutputSafetyParticipant : IOutputSafetyParticipant
{
    private readonly PHprContinuousEffectsRuntimeCoordinator _runtime;
    private readonly PHprRoadVibrationRouter _roadVibrationRouter;
    private readonly PHprSlipLockRouter _slipLockRouter;
    private OutputSafetyParticipantSnapshot _current;

    public ContinuousPhprOutputSafetyParticipant(
        PHprContinuousEffectsRuntimeCoordinator runtime,
        PHprRoadVibrationRouter roadVibrationRouter,
        PHprSlipLockRouter slipLockRouter,
        string? name = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _roadVibrationRouter = roadVibrationRouter ?? throw new ArgumentNullException(nameof(roadVibrationRouter));
        _slipLockRouter = slipLockRouter ?? throw new ArgumentNullException(nameof(slipLockRouter));
        Name = string.IsNullOrWhiteSpace(name) ? "Continuous P-HPR output" : name.Trim();
        _current = CreateSnapshot(isSilent: IsSilent(), hasFault: false, "Continuous P-HPR participant ready.");
    }

    public string Name { get; }

    public OutputSafetyParticipantSnapshot Current => _current;

    public async ValueTask SilenceAsync(OutputInterlockSnapshot interlock, CancellationToken cancellationToken)
    {
        await _roadVibrationRouter.StopAsync(
            $"Global output interlock latched: {interlock.Reason}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await _slipLockRouter.StopAsync(
            $"Global output interlock latched: {interlock.Reason}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _current = CreateSnapshot(
            IsSilent(),
            hasFault: false,
            "Continuous P-HPR output entered silent state for global output interlock.");
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
        _current = CreateSnapshot(IsSilent(), hasFault: false, "Continuous P-HPR output interlock reset observed.");
    }

    private bool IsSilent()
    {
        var runtimeSnapshot = _runtime.GetSnapshot();
        var roadSnapshot = _roadVibrationRouter.GetSnapshot();
        var slipLockSnapshot = _slipLockRouter.GetSnapshot();
        _ = runtimeSnapshot;
        return string.Equals(roadSnapshot.ActiveRoadModules, "none", StringComparison.OrdinalIgnoreCase)
            && string.Equals(slipLockSnapshot.ActiveSlipLockModules, "none", StringComparison.OrdinalIgnoreCase);
    }

    private OutputSafetyParticipantSnapshot CreateSnapshot(bool isSilent, bool hasFault, string message)
    {
        return new OutputSafetyParticipantSnapshot(Name, isSilent, hasFault, message);
    }
}
