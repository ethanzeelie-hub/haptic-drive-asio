using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed class SimagicPhprOutputDevice : IPHprOutputDevice
{
    public static PHprSafetyLimits DirectControlSafetyLimits { get; } = PHprSafetyLimits.Default with
    {
        AllowRealDeviceWrites = true
    };

    private readonly object _gate = new();
    private readonly IPhprHidReportWriter _writer;
    private readonly SimHubF1EcRealReportEncoder _encoder = new();
    private readonly IPHprSafetyLimiter _limiter;
    private readonly List<CancellationTokenSource> _pendingStops = [];
    private PHprRealOutputOptions _options;
    private PHprSafetyContext _baseContext;
    private bool _emergencyStopActive;
    private long _acceptedCommandCount;
    private long _rejectedCommandCount;
    private long _emergencyStopCount;
    private long _reportWriteCount;
    private long _failedReportWriteCount;
    private PHprCommandStatus? _lastStatus;
    private string? _lastMessage;
    private PHprCommand? _lastCommand;
    private DateTimeOffset? _lastCommandUtc;
    private int _lastReportLength;
    private PHprModuleId? _lastTarget;
    private PHprHidReportState? _lastReportState;
    private string? _lastReportSummary;
    private string? _lastError;
    private bool _disposed;

    public SimagicPhprOutputDevice(
        IPhprHidReportWriter writer,
        PHprRealOutputOptions? options = null,
        IPHprSafetyLimiter? limiter = null,
        PHprSafetyContext? context = null)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _options = (options ?? PHprRealOutputOptions.Disabled).Normalize();
        _limiter = limiter ?? new PHprSafetyLimiter(DirectControlSafetyLimits);
        _baseContext = context ?? PHprSafetyContext.DefaultMock with
        {
            IsMockOutput = false,
            RequiresRealDeviceWrites = true
        };
    }

    public void Configure(PHprRealOutputOptions options)
    {
        lock (_gate)
        {
            _options = (options ?? PHprRealOutputOptions.Disabled).Normalize(_limiter.Limits);
        }
    }

    public void SetSafetyContext(PHprSafetyContext context)
    {
        lock (_gate)
        {
            _baseContext = context with
            {
                IsMockOutput = false,
                RequiresRealDeviceWrites = true
            };
        }
    }

    public void ClearEmergencyStop()
    {
        lock (_gate)
        {
            _emergencyStopActive = false;
            _limiter.ClearEmergencyStop();
            _lastMessage = "Real P-HPR emergency stop latch cleared; direct control still requires explicit enable and arm.";
        }
    }

    public PHprRealOutputDiagnostics GetDiagnostics()
    {
        lock (_gate)
        {
            return new PHprRealOutputDiagnostics(
                _options,
                new PHprDirectControlArmingState(
                    _options.DirectControlEnabled,
                    _options.DirectControlArmed,
                    _options.DirectControlArmed ? DateTimeOffset.UtcNow : null),
                BuildSnapshot(),
                _reportWriteCount,
                _failedReportWriteCount,
                _lastReportLength,
                _lastTarget,
                _lastReportState,
                _lastReportSummary,
                _lastError);
        }
    }

    public PHprOutputSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return BuildSnapshot();
        }
    }

    public async ValueTask<PHprCommandResult> SendAsync(
        PHprCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        PHprRealOutputOptions options;
        lock (_gate)
        {
            if (_disposed)
            {
                return RejectLocked(PHprCommandStatus.RejectedInvalidCommand, "Real P-HPR output device is disposed.", command);
            }

            options = _options;
            var gateRejection = GetGateRejection(options, command);
            if (gateRejection is not null)
            {
                return RejectLocked(gateRejection.Value.Status, gateRejection.Value.Message, command);
            }
        }

        var context = BuildContext(options);
        if (!IsStopCommand(command) && context.SoftwareConflictStatus != PHprSoftwareConflictStatus.Clear)
        {
            return Reject(
                PHprCommandStatus.RejectedSafetyLimit,
                $"Real P-HPR direct control requires clear SimPro/SimHub coexistence; current status is {context.SoftwareConflictStatus}.",
                command);
        }

        var decision = _limiter.Evaluate(command, context);
        if (!decision.Accepted || decision.Command is null)
        {
            return Reject(PHprCommandStatus.RejectedSafetyLimit, decision.Message, decision.Command ?? command);
        }

        var safeCommand = decision.Command with
        {
            SafetyFlags = decision.Command.SafetyFlags & ~PHprSafetyFlags.MockOnly
        };

        var reports = IsStopCommand(safeCommand)
            ? _encoder.EncodeStop(safeCommand.TargetModule, options.Selector.ReportId)
            : _encoder.EncodeStart(safeCommand, options.Selector.ReportId);
        var writeResult = await WriteReportsAsync(reports, cancellationToken);
        if (!writeResult.Succeeded)
        {
            return Reject(PHprCommandStatus.RejectedInvalidCommand, writeResult.Message, safeCommand, writeResult.ErrorMessage);
        }

        lock (_gate)
        {
            _acceptedCommandCount++;
            _lastCommand = safeCommand;
            _lastCommandUtc = safeCommand.TimestampUtc;
            _lastStatus = PHprCommandStatus.Accepted;
            _lastMessage = "Real P-HPR command accepted through gated direct control; stop is software-timed when duration is positive.";
            _lastError = null;
        }

        if (!IsStopCommand(safeCommand) && safeCommand.DurationMs > 0)
        {
            ScheduleStop(safeCommand.TargetModule, safeCommand.DurationMs, options.Selector.ReportId);
        }

        return PHprCommandResult.Accepted(safeCommand, "Real P-HPR command accepted through gated direct control.");
    }

    public async ValueTask EmergencyStopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<CancellationTokenSource> pendingStops;
        PHprRealOutputOptions options;
        lock (_gate)
        {
            _emergencyStopActive = true;
            _emergencyStopCount++;
            _limiter.RecordEmergencyStop(BuildContext(_options));
            pendingStops = _pendingStops.ToList();
            _pendingStops.Clear();
            options = _options;
        }

        foreach (var pendingStop in pendingStops)
        {
            pendingStop.Cancel();
            pendingStop.Dispose();
        }

        if (options.Selector.IsSelected)
        {
            var reports = _encoder.EncodeStop(PHprModuleId.Both, options.Selector.ReportId, emergencyStop: true);
            await WriteReportsAsync(reports, cancellationToken);
        }

        lock (_gate)
        {
            _lastStatus = PHprCommandStatus.Accepted;
            _lastMessage = "Real P-HPR emergency stop requested stop reports for brake and throttle when a device was selected.";
        }
    }

    public async ValueTask DisposeAsync()
    {
        bool shouldRequestStop;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            shouldRequestStop = _options.Selector.IsSelected
                && (_options.DirectControlArmed || _pendingStops.Count > 0);
        }

        if (!shouldRequestStop)
        {
            lock (_gate)
            {
                _disposed = true;
            }

            return;
        }

        try
        {
            await EmergencyStopAsync();
        }
        finally
        {
            lock (_gate)
            {
                _disposed = true;
                foreach (var pendingStop in _pendingStops)
                {
                    pendingStop.Cancel();
                    pendingStop.Dispose();
                }

                _pendingStops.Clear();
            }
        }
    }

    private PHprOutputSnapshot BuildSnapshot()
    {
        return new PHprOutputSnapshot(
            IsMock: false,
            IsConnected: _options.Selector.IsSelected,
            IsEmergencyStopActive: _emergencyStopActive,
            AcceptedCommandCount: _acceptedCommandCount,
            RejectedCommandCount: _rejectedCommandCount,
            LastCommand: _lastCommand,
            LastStatus: _lastStatus,
            LastMessage: _lastMessage,
            LastCommandUtc: _lastCommandUtc,
            SafetyLimits: _limiter.Limits,
            Mode: _options.DirectControlEnabled
                ? _options.DirectControlArmed ? "RealDirectArmed" : "RealDirectEnabledUnarmed"
                : "RealDirectDisabled",
            BrakeAvailable: _options.Selector.IsSelected,
            ThrottleAvailable: _options.Selector.IsSelected,
            GeneratedFrameCount: _reportWriteCount,
            PendingScheduledStopCount: _pendingStops.Count,
            EmergencyStopCount: _emergencyStopCount);
    }

    private PHprSafetyContext BuildContext(PHprRealOutputOptions options)
    {
        return _baseContext with
        {
            IsMockOutput = false,
            IsDeviceConnected = options.Selector.IsSelected,
            BrakeModuleAvailable = options.Selector.IsSelected,
            ThrottleModuleAvailable = options.Selector.IsSelected,
            EmergencyStopActive = _emergencyStopActive,
            RequiresRealDeviceWrites = true
        };
    }

    private static (PHprCommandStatus Status, string Message)? GetGateRejection(
        PHprRealOutputOptions options,
        PHprCommand command)
    {
        if (IsStopCommand(command))
        {
            return null;
        }

        if (!options.DirectControlEnabled)
        {
            return (PHprCommandStatus.RejectedInvalidCommand, "Real P-HPR direct control is disabled.");
        }

        if (!options.DirectControlArmed)
        {
            return (PHprCommandStatus.RejectedInvalidCommand, "Real P-HPR direct control is not armed.");
        }

        if (!options.Selector.IsSelected)
        {
            return (PHprCommandStatus.RejectedInvalidCommand, "No P-HPR HID device/interface/report is selected.");
        }

        return null;
    }

    private async ValueTask<PHprHidWriteResult> WriteReportsAsync(
        IReadOnlyList<PHprHidReport> reports,
        CancellationToken cancellationToken)
    {
        foreach (var report in reports)
        {
            var result = await _writer.WriteReportAsync(report, cancellationToken);
            lock (_gate)
            {
                _lastReportLength = report.Length;
                _lastTarget = report.TargetModule;
                _lastReportState = report.State;
                _lastReportSummary = $"{report.State} {report.TargetModule} {report.Length:N0} bytes";
                if (result.Succeeded)
                {
                    _reportWriteCount++;
                }
                else
                {
                    _failedReportWriteCount++;
                    _lastError = result.ErrorMessage ?? result.Message;
                }
            }

            if (!result.Succeeded)
            {
                return result;
            }
        }

        return PHprHidWriteResult.Success(reports.LastOrDefault()?.Length ?? 0, "P-HPR HID reports written.");
    }

    private void ScheduleStop(PHprModuleId targetModule, int durationMs, byte? reportId)
    {
        var cts = new CancellationTokenSource();
        lock (_gate)
        {
            _pendingStops.Add(cts);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(durationMs, cts.Token);
                var reports = _encoder.EncodeStop(targetModule, reportId);
                await WriteReportsAsync(reports, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                lock (_gate)
                {
                    _pendingStops.Remove(cts);
                }

                cts.Dispose();
            }
        });
    }

    private static bool IsStopCommand(PHprCommand command)
    {
        return command.Source == PHprCommandSource.EmergencyStop
            || command.SafetyFlags.HasFlag(PHprSafetyFlags.EmergencyStop)
            || command.DurationMs <= 0
            || command.Strength01 <= 0d;
    }

    private PHprCommandResult Reject(PHprCommandStatus status, string message, PHprCommand command, string? error = null)
    {
        lock (_gate)
        {
            return RejectLocked(status, message, command, error);
        }
    }

    private PHprCommandResult RejectLocked(PHprCommandStatus status, string message, PHprCommand command, string? error = null)
    {
        _rejectedCommandCount++;
        _lastCommand = command;
        _lastCommandUtc = command.TimestampUtc;
        _lastStatus = status;
        _lastMessage = message;
        _lastError = error;
        return PHprCommandResult.Rejected(status, message, command);
    }
}
