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
    private PHprHidConnectionState _connectionState = PHprHidConnectionState.Closed;
    private bool _emergencyStopActive;
    private long _acceptedCommandCount;
    private long _rejectedCommandCount;
    private long _emergencyStopCount;
    private long _reportWriteCount;
    private long _failedReportWriteCount;
    private long _openAttemptCount;
    private long _openSuccessCount;
    private long _closeAttemptCount;
    private long _closeSuccessCount;
    private long _stopReportWriteCount;
    private long _timeoutCount;
    private long _disconnectCount;
    private long _invalidReportCount;
    private PHprCommandStatus? _lastStatus;
    private string? _lastMessage;
    private PHprCommand? _lastCommand;
    private DateTimeOffset? _lastCommandUtc;
    private int _lastReportLength;
    private PHprModuleId? _lastTarget;
    private PHprHidReportState? _lastReportState;
    private string? _lastReportSummary;
    private string? _lastError;
    private PHprHidWriteStatus? _lastOpenStatus;
    private PHprHidWriteStatus? _lastWriteStatus;
    private PHprHidWriteStatus? _lastStopStatus;
    private PHprHidWriteStatus? _lastCloseStatus;
    private DateTimeOffset? _lastOpenAtUtc;
    private DateTimeOffset? _lastWriteAtUtc;
    private DateTimeOffset? _lastStopAtUtc;
    private DateTimeOffset? _lastCloseAtUtc;
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
            var previousSelector = _options.Selector.Normalize();
            _options = (options ?? PHprRealOutputOptions.Disabled).Normalize(_limiter.Limits);
            if (!SelectorsOperationallyMatch(previousSelector, _options.Selector.Normalize())
                && _connectionState != PHprHidConnectionState.Disposed)
            {
                _connectionState = PHprHidConnectionState.Closed;
                _lastCloseStatus = PHprHidWriteStatus.Succeeded;
                _lastCloseAtUtc = DateTimeOffset.UtcNow;
                _lastMessage = "Real P-HPR selected interface changed; writer will reopen only on the next explicit command.";
            }
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
                BuildConnectionDiagnostics(),
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

    public async ValueTask<PHprHidWriteResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        PHprRealOutputOptions options;
        lock (_gate)
        {
            if (_disposed)
            {
                return PHprHidWriteResult.Failure(
                    "Real P-HPR output device is disposed.",
                    status: PHprHidWriteStatus.Failed);
            }

            options = _options;
        }

        return await OpenWriterAsync(options, requireDirectArm: true, cancellationToken);
    }

    public async ValueTask<PHprHidWriteResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        PHprRealOutputOptions options;
        lock (_gate)
        {
            options = _options;
            if (_connectionState == PHprHidConnectionState.Disposed)
            {
                return PHprHidWriteResult.Success(0, "Real P-HPR output device is already disposed.");
            }

            _connectionState = PHprHidConnectionState.Closing;
            _closeAttemptCount++;
        }

        var result = await RunWithTimeoutAsync(
            token => _writer.CloseAsync(token),
            options.WriteTimeoutMs,
            cancellationToken,
            "P-HPR HID writer close timed out.");
        lock (_gate)
        {
            _lastCloseStatus = result.Status;
            _lastCloseAtUtc = result.CompletedAtUtc;
            ApplyOperationResultLocked(result, PHprHidOperationKind.Close);
            if (result.Succeeded)
            {
                _closeSuccessCount++;
                _connectionState = PHprHidConnectionState.Closed;
            }

            _lastMessage = result.Message;
        }

        return result;
    }

    public async ValueTask<PHprCommandResult> SendAsync(
        PHprCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        PHprRealOutputOptions options;
        PHprSafetyContext context;
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

            context = BuildContext(options);
        }

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

        var isStop = IsStopCommand(safeCommand);
        var reports = isStop
            ? _encoder.EncodeStop(safeCommand.TargetModule, options.Selector.ReportId)
            : _encoder.EncodeStart(safeCommand, options.Selector.ReportId);
        var writeResult = await WriteReportsAsync(
            reports,
            options,
            isStop ? PHprHidOperationKind.Stop : PHprHidOperationKind.Write,
            cancellationToken);
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
            _lastMessage = isStop
                ? "Real P-HPR stop command accepted through gated direct control."
                : "Real P-HPR command accepted through gated direct control; stop is software-timed when duration is positive.";
            _lastError = null;
        }

        if (!isStop && safeCommand.DurationMs > 0)
        {
            ScheduleStop(safeCommand.TargetModule, safeCommand.DurationMs, options);
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

        PHprHidWriteResult? writeResult = null;
        if (options.Selector.IsSelected)
        {
            var reports = _encoder.EncodeStop(PHprModuleId.Both, options.Selector.ReportId, emergencyStop: true);
            writeResult = await WriteReportsAsync(reports, options, PHprHidOperationKind.EmergencyStop, cancellationToken);
        }

        lock (_gate)
        {
            _lastStatus = writeResult is null || writeResult.Succeeded
                ? PHprCommandStatus.Accepted
                : PHprCommandStatus.RejectedInvalidCommand;
            _lastMessage = writeResult is null
                ? "Real P-HPR emergency stop latched; no device was selected, so no stop reports were sent."
                : writeResult.Succeeded
                    ? "Real P-HPR emergency stop requested stop reports for brake and throttle."
                    : $"Real P-HPR emergency stop latched, but stop report write failed: {writeResult.Message}";
            if (writeResult is { Succeeded: false })
            {
                _lastError = writeResult.ErrorMessage ?? writeResult.Message;
            }
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

        try
        {
            if (shouldRequestStop)
            {
                await EmergencyStopAsync();
            }

            if (_writer.IsOpen || _connectionState == PHprHidConnectionState.Open)
            {
                await CloseAsync();
            }
        }
        finally
        {
            lock (_gate)
            {
                _disposed = true;
                _connectionState = PHprHidConnectionState.Disposed;
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
        var selectorSelected = _options.Selector.IsSelected;
        var connected = selectorSelected
            && _connectionState is not PHprHidConnectionState.Disconnected
                and not PHprHidConnectionState.Faulted
                and not PHprHidConnectionState.Disposed;
        return new PHprOutputSnapshot(
            IsMock: false,
            IsConnected: connected,
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
            BrakeAvailable: connected,
            ThrottleAvailable: connected,
            GeneratedFrameCount: _reportWriteCount,
            PendingScheduledStopCount: _pendingStops.Count,
            EmergencyStopCount: _emergencyStopCount);
    }

    private PHprRealOutputConnectionDiagnostics BuildConnectionDiagnostics()
    {
        return new PHprRealOutputConnectionDiagnostics(
            _connectionState,
            _writer.IsOpen,
            _openAttemptCount,
            _openSuccessCount,
            _closeAttemptCount,
            _closeSuccessCount,
            _stopReportWriteCount,
            _timeoutCount,
            _disconnectCount,
            _invalidReportCount,
            _lastOpenStatus,
            _lastWriteStatus,
            _lastStopStatus,
            _lastCloseStatus,
            _lastOpenAtUtc,
            _lastWriteAtUtc,
            _lastStopAtUtc,
            _lastCloseAtUtc);
    }

    private PHprSafetyContext BuildContext(PHprRealOutputOptions options)
    {
        var selectorSelected = options.Selector.IsSelected;
        var connected = selectorSelected
            && _connectionState is not PHprHidConnectionState.Disconnected
                and not PHprHidConnectionState.Faulted
                and not PHprHidConnectionState.Disposed;
        return _baseContext with
        {
            IsMockOutput = false,
            IsDeviceConnected = connected,
            BrakeModuleAvailable = connected,
            ThrottleModuleAvailable = connected,
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

        if (!options.DirectControlApprovalConfirmed)
        {
            return (PHprCommandStatus.RejectedInvalidCommand, $"Real P-HPR direct control requires the exact approval phrase for this session: {PHprControlledWriteApproval.Phrase}");
        }

        var selectorValidation = ValidateSelector(options, requireWriterSelectorMatch: false, writerSelector: null);
        if (selectorValidation is not null)
        {
            return (PHprCommandStatus.RejectedInvalidCommand, selectorValidation.Message);
        }

        return null;
    }

    private async ValueTask<PHprHidWriteResult> OpenWriterAsync(
        PHprRealOutputOptions options,
        bool requireDirectArm,
        CancellationToken cancellationToken)
    {
        var selectorValidation = ValidateSelector(options, requireWriterSelectorMatch: true, _writer.Selector);
        if (selectorValidation is not null)
        {
            lock (_gate)
            {
                ApplyOperationResultLocked(selectorValidation, PHprHidOperationKind.Open);
            }

            return selectorValidation;
        }

        if (requireDirectArm
            && (!options.DirectControlEnabled || !options.DirectControlArmed || !options.DirectControlApprovalConfirmed))
        {
            var gateFailure = PHprHidWriteResult.Failure(
                "Real P-HPR HID writer open requires direct control enabled, armed, and approval-confirmed for this session.",
                status: PHprHidWriteStatus.Failed);
            lock (_gate)
            {
                ApplyOperationResultLocked(gateFailure, PHprHidOperationKind.Open);
            }

            return gateFailure;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return PHprHidWriteResult.Failure(
                    "Real P-HPR output device is disposed.",
                    status: PHprHidWriteStatus.Failed);
            }

            if (_writer.IsOpen && _connectionState == PHprHidConnectionState.Open)
            {
                return PHprHidWriteResult.Success(options.Selector.ReportLength, "P-HPR HID writer already open.");
            }

            _connectionState = PHprHidConnectionState.Opening;
            _openAttemptCount++;
        }

        var result = await RunWithTimeoutAsync(
            token => _writer.OpenAsync(token),
            options.WriteTimeoutMs,
            cancellationToken,
            "P-HPR HID writer open timed out.");
        lock (_gate)
        {
            _lastOpenStatus = result.Status;
            _lastOpenAtUtc = result.CompletedAtUtc;
            ApplyOperationResultLocked(result, PHprHidOperationKind.Open);
            if (result.Succeeded)
            {
                _openSuccessCount++;
                _connectionState = PHprHidConnectionState.Open;
            }
        }

        return result;
    }

    private async ValueTask<PHprHidWriteResult> WriteReportsAsync(
        IReadOnlyList<PHprHidReport> reports,
        PHprRealOutputOptions options,
        PHprHidOperationKind operationKind,
        CancellationToken cancellationToken)
    {
        if (reports.Count == 0)
        {
            return PHprHidWriteResult.Success(0, "No P-HPR HID reports were generated.");
        }

        var openResult = await OpenWriterAsync(
            options,
            requireDirectArm: operationKind == PHprHidOperationKind.Write,
            cancellationToken);
        if (!openResult.Succeeded)
        {
            return openResult;
        }

        foreach (var report in reports)
        {
            var reportValidation = ValidateReport(report, options);
            if (reportValidation is not null)
            {
                lock (_gate)
                {
                    RecordLastReportLocked(report);
                    ApplyOperationResultLocked(reportValidation, operationKind);
                }

                return reportValidation;
            }

            var result = await RunWithTimeoutAsync(
                token => _writer.WriteReportAsync(report, token),
                options.WriteTimeoutMs,
                cancellationToken,
                "P-HPR HID report write timed out.");
            lock (_gate)
            {
                RecordLastReportLocked(report);
                ApplyOperationResultLocked(result, operationKind);
                if (result.Succeeded)
                {
                    _reportWriteCount++;
                    if (report.State is PHprHidReportState.Stop or PHprHidReportState.EmergencyStop)
                    {
                        _stopReportWriteCount++;
                    }
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

        return PHprHidWriteResult.Success(reports.Last().Length, "P-HPR HID reports written.");
    }

    private void ScheduleStop(PHprModuleId targetModule, int durationMs, PHprRealOutputOptions options)
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
                var reports = _encoder.EncodeStop(targetModule, options.Selector.ReportId);
                await WriteReportsAsync(reports, options, PHprHidOperationKind.Stop, cts.Token);
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

    private static PHprHidWriteResult? ValidateSelector(
        PHprRealOutputOptions options,
        bool requireWriterSelectorMatch,
        PHprHidDeviceSelector? writerSelector)
    {
        var selector = options.Selector.Normalize();
        if (!selector.IsSelected)
        {
            return PHprHidWriteResult.Failure(
                "No P-HPR HID device/interface/report is selected.",
                status: PHprHidWriteStatus.NotSelected);
        }

        if (selector.ReportLength != SimHubF1EcRealReportEncoder.PayloadLengthBytes)
        {
            return PHprHidWriteResult.Failure(
                $"Selected P-HPR HID report length {selector.ReportLength:N0} does not match the SimHub F1 EC length {SimHubF1EcRealReportEncoder.PayloadLengthBytes:N0}.",
                status: PHprHidWriteStatus.InvalidReport);
        }

        if (string.IsNullOrWhiteSpace(selector.InterfaceName)
            || string.Equals(selector.InterfaceName, "none", StringComparison.OrdinalIgnoreCase))
        {
            return PHprHidWriteResult.Failure(
                "Selected P-HPR HID interface name is missing.",
                status: PHprHidWriteStatus.InvalidReport);
        }

        if (requireWriterSelectorMatch
            && writerSelector is not null
            && !SelectorsOperationallyMatch(selector, writerSelector.Normalize()))
        {
            return PHprHidWriteResult.Failure(
                "Configured P-HPR HID selector does not match the writer's active selector.",
                status: PHprHidWriteStatus.InvalidReport);
        }

        return null;
    }

    private static PHprHidWriteResult? ValidateReport(PHprHidReport report, PHprRealOutputOptions options)
    {
        if (report.Payload.Length != SimHubF1EcRealReportEncoder.PayloadLengthBytes
            || report.Payload.Length != options.Selector.ReportLength)
        {
            return PHprHidWriteResult.Failure(
                $"P-HPR HID report length {report.Payload.Length:N0} does not match selected length {options.Selector.ReportLength:N0}.",
                status: PHprHidWriteStatus.InvalidReport);
        }

        if (report.ReportId != options.Selector.ReportId)
        {
            return PHprHidWriteResult.Failure(
                "P-HPR HID report ID does not match the selected report.",
                status: PHprHidWriteStatus.InvalidReport);
        }

        return null;
    }

    private void RecordLastReportLocked(PHprHidReport report)
    {
        _lastReportLength = report.Length;
        _lastTarget = report.TargetModule;
        _lastReportState = report.State;
        _lastReportSummary = $"{report.State} {report.TargetModule} {report.Length:N0} bytes";
    }

    private void ApplyOperationResultLocked(PHprHidWriteResult result, PHprHidOperationKind operationKind)
    {
        if (!result.Succeeded)
        {
            _lastError = result.ErrorMessage ?? result.Message;
        }

        switch (operationKind)
        {
            case PHprHidOperationKind.Open:
                _lastOpenStatus = result.Status;
                _lastOpenAtUtc = result.CompletedAtUtc;
                break;
            case PHprHidOperationKind.Write:
                _lastWriteStatus = result.Status;
                _lastWriteAtUtc = result.CompletedAtUtc;
                break;
            case PHprHidOperationKind.Stop:
            case PHprHidOperationKind.EmergencyStop:
                _lastStopStatus = result.Status;
                _lastStopAtUtc = result.CompletedAtUtc;
                break;
            case PHprHidOperationKind.Close:
                _lastCloseStatus = result.Status;
                _lastCloseAtUtc = result.CompletedAtUtc;
                break;
        }

        if (result.Status == PHprHidWriteStatus.TimedOut)
        {
            _timeoutCount++;
        }

        if (result.Status == PHprHidWriteStatus.Disconnected)
        {
            _disconnectCount++;
        }

        if (result.Status == PHprHidWriteStatus.InvalidReport)
        {
            _invalidReportCount++;
        }

        if (!result.Succeeded)
        {
            _connectionState = result.Status switch
            {
                PHprHidWriteStatus.Disconnected => PHprHidConnectionState.Disconnected,
                PHprHidWriteStatus.TimedOut => PHprHidConnectionState.Faulted,
                PHprHidWriteStatus.InvalidReport => PHprHidConnectionState.Faulted,
                PHprHidWriteStatus.NotSelected => PHprHidConnectionState.Closed,
                PHprHidWriteStatus.Cancelled => PHprHidConnectionState.Closed,
                _ => PHprHidConnectionState.Faulted
            };
        }
    }

    private static async ValueTask<PHprHidWriteResult> RunWithTimeoutAsync(
        Func<CancellationToken, ValueTask<PHprHidWriteResult>> operation,
        int timeoutMs,
        CancellationToken cancellationToken,
        string timeoutMessage)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Math.Clamp(timeoutMs, PHprRealOutputOptions.MinWriteTimeoutMs, PHprRealOutputOptions.MaxWriteTimeoutMs));
        try
        {
            return await operation(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PHprHidWriteResult.Failure(timeoutMessage, status: PHprHidWriteStatus.TimedOut);
        }
    }

    private static bool SelectorsOperationallyMatch(PHprHidDeviceSelector left, PHprHidDeviceSelector right)
    {
        return string.Equals(left.DevicePath, right.DevicePath, StringComparison.Ordinal)
            && left.ReportId == right.ReportId
            && left.ReportLength == right.ReportLength;
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

    private enum PHprHidOperationKind
    {
        Open = 0,
        Write = 1,
        Stop = 2,
        EmergencyStop = 3,
        Close = 4
    }
}
