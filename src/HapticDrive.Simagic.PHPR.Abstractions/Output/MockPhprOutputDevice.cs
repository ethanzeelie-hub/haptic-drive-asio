using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Abstractions.Output;

public sealed class MockPhprOutputDevice(PHprSafetyLimits? safetyLimits = null) : IPHprOutputDevice
{
    private readonly object _gate = new();
    private readonly List<PHprCommand> _commandHistory = [];
    private bool _emergencyStopActive;
    private long _acceptedCommandCount;
    private long _rejectedCommandCount;
    private PHprCommandStatus? _lastStatus;
    private string? _lastMessage;

    public PHprSafetyLimits SafetyLimits { get; } = safetyLimits ?? PHprSafetyLimits.Default;

    public IReadOnlyList<PHprCommand> CommandHistory
    {
        get
        {
            lock (_gate)
            {
                return _commandHistory.ToArray();
            }
        }
    }

    public ValueTask<PHprCommandResult> SendAsync(PHprCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_emergencyStopActive)
            {
                _rejectedCommandCount++;
                _lastStatus = PHprCommandStatus.RejectedEmergencyStop;
                _lastMessage = "Mock P-HPR emergency stop is active; command suppressed.";
                return ValueTask.FromResult(PHprCommandResult.Rejected(_lastStatus.Value, _lastMessage, command));
            }

            var clampedCommand = command.ClampTo(SafetyLimits);
            var safeCommand = clampedCommand with
            {
                SafetyFlags = clampedCommand.SafetyFlags | PHprSafetyFlags.MockOnly
            };

            _commandHistory.Add(safeCommand);
            _acceptedCommandCount++;
            _lastStatus = PHprCommandStatus.Accepted;
            _lastMessage = "Mock P-HPR command accepted; no hardware write was performed.";

            return ValueTask.FromResult(PHprCommandResult.Accepted(safeCommand, _lastMessage));
        }
    }

    public ValueTask EmergencyStopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _emergencyStopActive = true;
            _lastStatus = PHprCommandStatus.Accepted;
            _lastMessage = "Mock P-HPR emergency stop activated; no hardware write was performed.";
            _commandHistory.Add(PHprCommand.Create(
                PHprModuleId.Both,
                0d,
                SafetyLimits.MinFrequencyHz,
                0,
                PHprCommandSource.EmergencyStop,
                safetyFlags: PHprSafetyFlags.MockOnly | PHprSafetyFlags.EmergencyStop));
        }

        return ValueTask.CompletedTask;
    }

    public PHprOutputSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var lastCommand = _commandHistory.LastOrDefault();
            return new PHprOutputSnapshot(
                IsMock: true,
                IsConnected: true,
                IsEmergencyStopActive: _emergencyStopActive,
                AcceptedCommandCount: _acceptedCommandCount,
                RejectedCommandCount: _rejectedCommandCount,
                LastCommand: lastCommand,
                LastStatus: _lastStatus,
                LastMessage: _lastMessage,
                LastCommandUtc: lastCommand?.TimestampUtc,
                SafetyLimits: SafetyLimits);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _commandHistory.Clear();
        }

        return ValueTask.CompletedTask;
    }
}
