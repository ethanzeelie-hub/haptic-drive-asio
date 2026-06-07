using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Abstractions.Output;

public sealed class SafetyLimitedPhprOutputDevice : IPHprOutputDevice
{
    private readonly object _gate = new();
    private readonly MockPhprOutputDevice _inner;
    private readonly IPHprSafetyLimiter _limiter;
    private PHprSafetyContext _baseContext;

    public SafetyLimitedPhprOutputDevice(
        MockPhprOutputDevice inner,
        IPHprSafetyLimiter? limiter = null,
        PHprSafetyContext? context = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _limiter = limiter ?? new PHprSafetyLimiter(inner.SafetyLimits);
        _baseContext = context ?? PHprSafetyContext.DefaultMock;
    }

    public MockPhprOutputDevice Inner => _inner;

    public PHprSafetySnapshot SafetySnapshot => _limiter.GetSnapshot(BuildContext());

    public PHprOutputSnapshot InnerSnapshot => _inner.GetSnapshot();

    public void SetSafetyContext(PHprSafetyContext context)
    {
        lock (_gate)
        {
            _baseContext = context;
        }
    }

    public PHprOutputSnapshot GetSnapshot()
    {
        return _inner.GetSnapshot();
    }

    public async ValueTask<PHprCommandResult> SendAsync(PHprCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var decision = _limiter.Evaluate(command, BuildContext());
        if (!decision.Accepted || decision.Command is null)
        {
            var status = decision.Violation.Code == PHprSafetyViolationCode.EmergencyStopActive
                ? PHprCommandStatus.RejectedEmergencyStop
                : PHprCommandStatus.RejectedSafetyLimit;
            return PHprCommandResult.Rejected(status, decision.Message, decision.Command ?? command);
        }

        return await _inner.SendAsync(decision.Command, cancellationToken);
    }

    public async ValueTask EmergencyStopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _limiter.RecordEmergencyStop(BuildContext());
        await _inner.EmergencyStopAsync(cancellationToken);
    }

    public void ClearEmergencyStop()
    {
        _limiter.ClearEmergencyStop();
        _inner.ClearEmergencyStop();
    }

    public void ResetSafetyState()
    {
        _limiter.Reset();
        _inner.ClearEmergencyStop();
    }

    public ValueTask DisposeAsync()
    {
        return _inner.DisposeAsync();
    }

    private PHprSafetyContext BuildContext()
    {
        lock (_gate)
        {
            return PHprSafetyContext.FromOutputSnapshot(_inner.GetSnapshot(), _baseContext);
        }
    }
}
