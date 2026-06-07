using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Abstractions.Output;

public sealed class MockPhprOutputDevice(PHprSafetyLimits? safetyLimits = null) : IPHprOutputDevice
{
    private readonly object _gate = new();
    private readonly PHprMockDurationScheduler _durationScheduler = new();
    private readonly List<PHprCommand> _commandHistory = [];
    private readonly List<PHprMockProtocolFrame> _frameHistory = [];
    private bool _emergencyStopActive;
    private bool _isConnected = true;
    private bool _brakeAvailable = true;
    private bool _throttleAvailable = true;
    private bool _rejectCommands;
    private string? _rejectReason;
    private long _acceptedCommandCount;
    private long _rejectedCommandCount;
    private long _emergencyStopCount;
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

    public IReadOnlyList<PHprMockProtocolFrame> FrameHistory
    {
        get
        {
            lock (_gate)
            {
                return _frameHistory.Select(frame => frame.CloneWithOffset(frame.ScheduledOffset)).ToArray();
            }
        }
    }

    public void SetConnected(bool isConnected)
    {
        lock (_gate)
        {
            _isConnected = isConnected;
        }
    }

    public void SetModuleAvailability(bool brakeAvailable, bool throttleAvailable)
    {
        lock (_gate)
        {
            _brakeAvailable = brakeAvailable;
            _throttleAvailable = throttleAvailable;
        }
    }

    public void SetRejectedCommandSimulation(bool rejectCommands, string? reason = null)
    {
        lock (_gate)
        {
            _rejectCommands = rejectCommands;
            _rejectReason = reason;
        }
    }

    public void ClearHistory()
    {
        lock (_gate)
        {
            _commandHistory.Clear();
            _frameHistory.Clear();
            _acceptedCommandCount = 0;
            _rejectedCommandCount = 0;
            _emergencyStopCount = 0;
            _lastStatus = null;
            _lastMessage = null;
        }
    }

    public void ClearEmergencyStop()
    {
        lock (_gate)
        {
            _emergencyStopActive = false;
            _lastStatus = null;
            _lastMessage = "Mock P-HPR emergency stop cleared; no hardware write was performed.";
        }
    }

    public ValueTask<PHprCommandResult> SendAsync(PHprCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var isSafeStop = IsSafeStop(command);
            if (!_isConnected && !isSafeStop)
            {
                return Reject(PHprCommandStatus.RejectedInvalidCommand, "Mock P-HPR output is disconnected; command suppressed.", command);
            }

            if (_rejectCommands)
            {
                return Reject(PHprCommandStatus.RejectedInvalidCommand, _rejectReason ?? "Mock P-HPR rejection simulation is active.", command);
            }

            if (_emergencyStopActive && !isSafeStop)
            {
                return Reject(PHprCommandStatus.RejectedEmergencyStop, "Mock P-HPR emergency stop is active; command suppressed.", command);
            }

            if (!Enum.IsDefined(command.TargetModule))
            {
                return Reject(PHprCommandStatus.RejectedInvalidCommand, "Mock P-HPR command has an invalid target module.", command);
            }

            if (!isSafeStop && !IsTargetAvailable(command.TargetModule))
            {
                return Reject(PHprCommandStatus.RejectedInvalidCommand, "Mock P-HPR target module is unavailable.", command);
            }

            var clampedCommand = command.ClampTo(SafetyLimits);
            var safeCommand = clampedCommand with
            {
                SafetyFlags = clampedCommand.SafetyFlags | PHprSafetyFlags.MockOnly
            };
            var mockCommand = PHprMockProtocolCommand.FromPHprCommand(safeCommand);
            var plannedFrames = _durationScheduler.Plan(mockCommand);
            if (!plannedFrames.Succeeded)
            {
                return Reject(PHprCommandStatus.RejectedInvalidCommand, plannedFrames.Message, safeCommand);
            }

            _commandHistory.Add(safeCommand);
            _frameHistory.AddRange(plannedFrames.Frames.Select(frame => frame.CloneWithOffset(frame.ScheduledOffset)));
            _acceptedCommandCount++;
            _lastStatus = PHprCommandStatus.Accepted;
            _lastMessage = "Mock P-HPR command accepted and mock frames recorded; no hardware write was performed.";

            return ValueTask.FromResult(PHprCommandResult.Accepted(safeCommand, _lastMessage));
        }
    }

    public ValueTask EmergencyStopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _emergencyStopActive = true;
            _emergencyStopCount++;
            _frameHistory.RemoveAll(frame => frame.ScheduledOffset > TimeSpan.Zero);
            _lastStatus = PHprCommandStatus.Accepted;
            _lastMessage = "Mock P-HPR emergency stop activated; no hardware write was performed.";
            var emergencyCommand = PHprCommand.Create(
                PHprModuleId.Both,
                0d,
                SafetyLimits.MinFrequencyHz,
                0,
                PHprCommandSource.EmergencyStop,
                safetyFlags: PHprSafetyFlags.MockOnly | PHprSafetyFlags.EmergencyStop);
            _commandHistory.Add(emergencyCommand);

            var plannedFrames = _durationScheduler.Plan(PHprMockProtocolCommand.FromPHprCommand(emergencyCommand));
            if (plannedFrames.Succeeded)
            {
                _frameHistory.AddRange(plannedFrames.Frames.Select(frame => frame.CloneWithOffset(frame.ScheduledOffset)));
            }
        }

        return ValueTask.CompletedTask;
    }

    public PHprOutputSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var lastCommand = _commandHistory.LastOrDefault();
            var lastFrame = _frameHistory.LastOrDefault()?.CloneWithOffset(_frameHistory.Last().ScheduledOffset);
            return new PHprOutputSnapshot(
                IsMock: true,
                IsConnected: _isConnected,
                IsEmergencyStopActive: _emergencyStopActive,
                AcceptedCommandCount: _acceptedCommandCount,
                RejectedCommandCount: _rejectedCommandCount,
                LastCommand: lastCommand,
                LastStatus: _lastStatus,
                LastMessage: _lastMessage,
                LastCommandUtc: lastCommand?.TimestampUtc,
                SafetyLimits: SafetyLimits,
                Mode: "MockOnly",
                BrakeAvailable: _brakeAvailable,
                ThrottleAvailable: _throttleAvailable,
                GeneratedFrameCount: _frameHistory.Count,
                LastFrame: lastFrame,
                PendingScheduledStopCount: _frameHistory.Count(frame => frame.State == PHprMockProtocolState.Stop && frame.ScheduledOffset > TimeSpan.Zero),
                EmergencyStopCount: _emergencyStopCount);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _commandHistory.Clear();
            _frameHistory.Clear();
        }

        return ValueTask.CompletedTask;
    }

    private bool IsTargetAvailable(PHprModuleId targetModule)
    {
        return targetModule switch
        {
            PHprModuleId.Brake => _brakeAvailable,
            PHprModuleId.Throttle => _throttleAvailable,
            PHprModuleId.Both => _brakeAvailable && _throttleAvailable,
            _ => false
        };
    }

    private static bool IsSafeStop(PHprCommand command)
    {
        return command.Source == PHprCommandSource.EmergencyStop
            || command.SafetyFlags.HasFlag(PHprSafetyFlags.EmergencyStop)
            || command.DurationMs <= 0
            || command.Strength01 <= 0d;
    }

    private ValueTask<PHprCommandResult> Reject(PHprCommandStatus status, string message, PHprCommand? command)
    {
        _rejectedCommandCount++;
        _lastStatus = status;
        _lastMessage = message;
        return ValueTask.FromResult(PHprCommandResult.Rejected(status, message, command));
    }
}
