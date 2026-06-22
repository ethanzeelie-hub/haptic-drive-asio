using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Routing;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

internal enum PHprDirectRuntimeState
{
    Disabled = 0,
    Idle = 1,
    Starting = 2,
    Active = 3,
    Stopping = 4,
    EmergencyStopping = 5,
    Faulted = 6
}

internal enum PHprDirectRuntimeErrorCategory
{
    DeviceSelection = 0,
    WriterOpen = 1,
    StartWrite = 2,
    StopSchedule = 3,
    StopWrite = 4,
    WatchdogStopAll = 5,
    EmergencyStop = 6,
    StateMachineViolation = 7,
    UncleanStartup = 8,
    UnhandledException = 9,
    UserSafetyGate = 10,
    CoexistenceBlocked = 11,
    Unknown = 12
}

internal sealed record PHprDirectRuntimeEnvironment(
    PHprRealOutputOptions Options,
    PHprSoftwareConflictStatus CoexistenceStatus,
    bool RoadVibrationEnabled,
    bool SlipLockEnabled,
    bool BenchEnabled,
    PHprGearPulseTarget BenchTarget,
    string? SelectedPaddleDeviceSummary,
    long DebounceSuppressedCount)
{
    public static PHprDirectRuntimeEnvironment Disabled { get; } = new(
        PHprRealOutputOptions.Disabled,
        PHprSoftwareConflictStatus.Unknown,
        RoadVibrationEnabled: false,
        SlipLockEnabled: false,
        BenchEnabled: false,
        PHprGearPulseTarget.Both,
        SelectedPaddleDeviceSummary: null,
        DebounceSuppressedCount: 0);
}

internal sealed record PHprDirectSharedPathProof(
    string BlueButtonPulseServiceInstanceId,
    string BenchPulseServiceInstanceId,
    string BlueButtonWriterInstanceId,
    string BenchWriterInstanceId,
    string BlueButtonEncoderInstanceId,
    string BenchEncoderInstanceId,
    string BlueButtonStopMethodId,
    string BenchStopMethodId)
{
    public bool SameServiceInstance => string.Equals(BlueButtonPulseServiceInstanceId, BenchPulseServiceInstanceId, StringComparison.Ordinal);

    public bool SameWriterInstance => string.Equals(BlueButtonWriterInstanceId, BenchWriterInstanceId, StringComparison.Ordinal);

    public bool SameEncoder => string.Equals(BlueButtonEncoderInstanceId, BenchEncoderInstanceId, StringComparison.Ordinal);

    public bool SameStopMethod => string.Equals(BlueButtonStopMethodId, BenchStopMethodId, StringComparison.Ordinal);

    public bool IsProven => SameServiceInstance && SameWriterInstance && SameEncoder && SameStopMethod;
}

internal sealed record PHprDirectLatencySnapshot(
    DateTimeOffset? PaddleEventReceivedUtc = null,
    DateTimeOffset? BenchAcceptedUtc = null,
    DateTimeOffset? CommandQueuedUtc = null,
    DateTimeOffset? StartWriteAttemptedUtc = null,
    DateTimeOffset? StartWriteCompletedUtc = null,
    DateTimeOffset? StopScheduledUtc = null,
    DateTimeOffset? StopDueUtc = null,
    DateTimeOffset? StopWriteCompletedUtc = null,
    double? PaddleReceivedToBenchAcceptedMs = null,
    double? BenchAcceptedToCommandQueuedMs = null,
    double? CommandQueuedToStartWriteAttemptedMs = null,
    double? PaddleReceivedToStartWriteCompletedMs = null,
    double? InterPressIntervalMs = null);

internal sealed record PHprDirectRuntimeSnapshot(
    PHprDirectRuntimeState State,
    bool StartupCleanupAttempted,
    bool StartupCleanupSucceeded,
    bool StartupCleanupFailed,
    bool UncleanShutdownMarkerExists,
    bool DisabledAfterUncleanShutdown,
    bool DirectReady,
    string BlockedReason,
    bool HardwareBelievedActive,
    int PendingStopCount,
    long PulseId,
    PHprDirectSharedPathProof SharedPathProof,
    PHprDirectLatencySnapshot Latency,
    PHprDirectRuntimeErrorCategory? LastErrorCategory,
    string? LastErrorMessage,
    string FlightRecorderPath,
    string UncleanShutdownMarkerPath)
{
    public bool CanStartBench =>
        State == PHprDirectRuntimeState.Idle
        && DirectReady
        && !UncleanShutdownMarkerExists
        && SharedPathProof.IsProven
        && string.IsNullOrWhiteSpace(BlockedReason);
}

internal sealed record PHprDirectStopAllResult(
    bool Succeeded,
    string Message,
    PHprHidWriteResult WriteResult,
    PHprDirectRuntimeState State,
    bool MarkerExists);

internal interface IPHprDirectRuntime
{
    void Configure(PHprDirectRuntimeEnvironment environment);

    PHprDirectRuntimeSnapshot GetSnapshot();

    ValueTask InitializeStartupCleanupAsync(CancellationToken cancellationToken = default);

    ValueTask<PhprDeviceCardPulseResult> SendManualPulseAsync(
        PHprModuleId moduleId,
        PHprRealGearPulseSettings settings,
        PHprSafetyContext safetyContext,
        CancellationToken cancellationToken = default);

    ValueTask<string> RouteBenchAsync(
        PaddleGearBenchTestResult benchResult,
        PaddleGearBenchTestOptions options,
        WheelPaddleInputSnapshot paddleSnapshot,
        Func<PHprModuleId, PHprRealGearPulseSettings> deviceCardSettings,
        PHprSafetyContext safetyContext,
        CancellationToken cancellationToken = default);

    ValueTask<PHprDirectStopAllResult> StopAllAsync(
        string reason,
        CancellationToken cancellationToken = default);

    ValueTask EmergencyStopAsync(string reason, CancellationToken cancellationToken = default);

    void ClearEmergencyStop();

    void HandleUnhandledException(string reason, Exception? exception);

    ValueTask HandlePaddleInputExceptionAsync(
        string reason,
        Exception exception,
        bool stopAllIfPulseMayHaveStarted,
        CancellationToken cancellationToken = default);
}

internal interface IPHprDirectCommandDispatcher
{
    string InstanceId { get; }

    ValueTask<PhprDeviceCardPulseResult> SendPulseAsync(
        PHprModuleId moduleId,
        PHprRealGearPulseSettings settings,
        PHprSafetyContext safetyContext,
        DateTimeOffset? timestampUtc,
        CancellationToken cancellationToken = default);

    ValueTask<PHprHidWriteResult> StopAllAsync(CancellationToken cancellationToken = default);

    ValueTask EmergencyStopAsync(CancellationToken cancellationToken = default);
}

internal interface IPHprBenchFlightRecorder
{
    string LogPath { get; }

    string? LastFallbackStatus { get; }

    void Record(PhprBenchFlightRecorderRecord record);
}

internal interface IPHprBenchUncleanShutdownStore
{
    string MarkerPath { get; }

    bool Exists();

    bool TryCreate(string reason, out string? error);

    bool TryClear(out string? error);
}

internal interface IPHprDirectRuntimeClock
{
    DateTimeOffset UtcNow { get; }

    long MonotonicTimestamp { get; }

    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}

internal sealed class SystemPHprDirectRuntimeClock : IPHprDirectRuntimeClock
{
    public static SystemPHprDirectRuntimeClock Instance { get; } = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public long MonotonicTimestamp => Stopwatch.GetTimestamp();

    public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return new ValueTask(Task.Delay(delay, cancellationToken));
    }
}

internal sealed class FilePHprBenchUncleanShutdownStore : IPHprBenchUncleanShutdownStore
{
    public FilePHprBenchUncleanShutdownStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        MarkerPath = Path.Combine(directory, "phpr-direct-bench-unclean-shutdown.marker");
    }

    public string MarkerPath { get; }

    public bool Exists()
    {
        try
        {
            return File.Exists(MarkerPath);
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    public bool TryCreate(string reason, out string? error)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
            File.WriteAllText(
                MarkerPath,
                $"createdUtc={DateTimeOffset.UtcNow:O}{Environment.NewLine}reason={reason}{Environment.NewLine}");
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryClear(out string? error)
    {
        try
        {
            if (File.Exists(MarkerPath))
            {
                File.Delete(MarkerPath);
            }

            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = ex.Message;
            return false;
        }
    }
}

internal sealed class FilePHprBenchFlightRecorder : IPHprBenchFlightRecorder
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public FilePHprBenchFlightRecorder(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        LogPath = Path.Combine(directory, "phpr-direct-bench-flight-recorder.jsonl");
    }

    public string LogPath { get; }

    public string? LastFallbackStatus { get; private set; }

    public void Record(PhprBenchFlightRecorderRecord record)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                RotateIfNeeded();
                var json = JsonSerializer.Serialize(record with
                {
                    SelectedPHprDeviceSummary = Sanitize(record.SelectedPHprDeviceSummary),
                    PaddleListenerSelectedDevice = Sanitize(record.PaddleListenerSelectedDevice)
                }, _jsonOptions);
                using var stream = new FileStream(
                    LogPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 4096,
                    FileOptions.WriteThrough);
                using var writer = new StreamWriter(stream);
                writer.WriteLine(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
                LastFallbackStatus = null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LastFallbackStatus = $"{DateTimeOffset.UtcNow:O} recorder-failed {ex.GetType().Name}: {ex.Message}";
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(LogPath))
        {
            return;
        }

        var info = new FileInfo(LogPath);
        if (info.Length < MaxBytes)
        {
            return;
        }

        var archive = LogPath + ".1";
        if (File.Exists(archive))
        {
            File.Delete(archive);
        }

        File.Move(LogPath, archive);
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var sanitized = value;
        var hidIndex = sanitized.IndexOf(@"\\?\hid#", StringComparison.OrdinalIgnoreCase);
        if (hidIndex >= 0)
        {
            sanitized = sanitized[..hidIndex] + "[redacted-hid-path]";
        }

        sanitized = sanitized.Replace("\\\\?\\hid#", "[redacted-hid-path]", StringComparison.OrdinalIgnoreCase);
        sanitized = sanitized.Replace("serial", "serial-redacted", StringComparison.OrdinalIgnoreCase);
        return sanitized;
    }
}

internal sealed record PhprBenchFlightRecorderRecord
{
    public string SessionId { get; init; } = string.Empty;

    public long? PulseId { get; init; }

    public DateTimeOffset WallClockUtc { get; init; }

    public long MonotonicTimestamp { get; init; }

    public int ThreadId { get; init; } = Environment.CurrentManagedThreadId;

    public string EventName { get; init; } = string.Empty;

    public string? AppVersionOrCommit { get; init; }

    [JsonPropertyName("selectedPHprDeviceSummary")]
    public string? SelectedPHprDeviceSummary { get; init; }

    public string? Transport { get; init; }

    public string? ReportId { get; init; }

    public int? ReportLength { get; init; }

    public bool? DirectReady { get; init; }

    public string? WriterInstanceId { get; init; }

    public string? EncoderInstanceId { get; init; }

    public string? BlueButtonPulseServiceInstanceId { get; init; }

    public string? BenchPulseServiceInstanceId { get; init; }

    public bool? SameServiceInstance { get; init; }

    public bool? SameWriterInstance { get; init; }

    public bool? SameEncoder { get; init; }

    public bool? SameStopMethod { get; init; }

    public bool? BenchEnabled { get; init; }

    public string? BenchTarget { get; init; }

    public string? BrakeCard { get; init; }

    public string? ThrottleCard { get; init; }

    public string? PaddleListenerSelectedDevice { get; init; }

    public long? PaddleEventSequence { get; init; }

    public string? PaddleEventKind { get; init; }

    public string? MappedSide { get; init; }

    public int? MappedButton { get; init; }

    public string? AcceptedRejectedReason { get; init; }

    public string? LimiterProfile { get; init; }

    public string? PerModuleStartAllowed { get; init; }

    public string? PerModuleStartRejected { get; init; }

    public string? PerModuleStopAllowed { get; init; }

    public string? PerModuleStopRejected { get; init; }

    public long? OrdinaryStartRateRejectionCount { get; init; }

    public string? FailClosedStopAllReason { get; init; }

    public bool? PartialWriteActive { get; init; }

    public bool? StopAllFromUnsafeState { get; init; }

    public bool? StopAllFromOrdinaryLimiterRejection { get; init; }

    public string? PulseState { get; init; }

    public int? PendingStopCount { get; init; }

    public bool? StartRequested { get; init; }

    public string? StartReportFirstBytes { get; init; }

    public bool? StartWriteAttempted { get; init; }

    public bool? StartWriteSucceeded { get; init; }

    public bool? StartWriteFailed { get; init; }

    public DateTimeOffset? StopScheduledTimestamp { get; init; }

    public DateTimeOffset? StopDueTimestamp { get; init; }

    public bool? StopAttempted { get; init; }

    public string? StopReportFirstBytes { get; init; }

    public bool? StopWriteAttempted { get; init; }

    public bool? StopWriteSucceeded { get; init; }

    public bool? StopWriteFailed { get; init; }

    public bool? WatchdogArmed { get; init; }

    public bool? WatchdogFired { get; init; }

    public bool? EmergencyStopRequested { get; init; }

    public bool? EmergencyStopWriteAttempted { get; init; }

    public bool? EmergencyStopWriteSucceeded { get; init; }

    public bool? EmergencyStopWriteFailed { get; init; }

    public bool? ManualStopAllRequested { get; init; }

    public bool? ManualStopAllWriteAttempted { get; init; }

    public bool? ManualStopAllWriteSucceeded { get; init; }

    public bool? ManualStopAllWriteFailed { get; init; }

    public bool? StartupStopOnlyCleanupAttempted { get; init; }

    public bool? StartupStopOnlyCleanupSucceeded { get; init; }

    public bool? StartupStopOnlyCleanupFailed { get; init; }

    public bool? DisposeShutdownStopAttempted { get; init; }

    public bool? DisposeShutdownStopSucceeded { get; init; }

    public bool? DisposeShutdownStopFailed { get; init; }

    public string? ExceptionType { get; init; }

    public string? ExceptionMessage { get; init; }

    public string? ExceptionStackTrace { get; init; }

    public PHprDirectRuntimeErrorCategory? SanitizedErrorCategory { get; init; }

    public double? PaddleReceivedToBenchAcceptedMs { get; init; }

    public double? BenchAcceptedToCommandQueuedMs { get; init; }

    public double? CommandQueuedToStartWriteAttemptedMs { get; init; }

    public double? PaddleReceivedToStartWriteCompletedMs { get; init; }

    public double? InterPressIntervalMs { get; init; }

    public long? PulseGenerationId { get; init; }

    public long? BrakeLatestGeneration { get; init; }

    public long? ThrottleLatestGeneration { get; init; }

    public long? StaleStopIgnoredCount { get; init; }

    public long? BusyRejectedCount { get; init; }

    public long? RetriggerCount { get; init; }

    public long? StaleOutputDroppedCount { get; init; }

    public long? DebounceSuppressedCount { get; init; }

    public long? StaleRuntimeObserverIgnoredCount { get; init; }

    public DateTimeOffset? PaddleEventTimestampUtc { get; init; }

    public DateTimeOffset? AcceptedEventTimestampUtc { get; init; }

    public DateTimeOffset? FirstHidStartWriteTimestampUtc { get; init; }

    public DateTimeOffset? BrakeStopDueAtUtc { get; init; }

    public DateTimeOffset? ThrottleStopDueAtUtc { get; init; }
}

internal sealed class PHprDirectCommandDispatcher : IPHprDirectCommandDispatcher
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IPHprDirectPulseService _pulseService;
    private readonly SimagicPhprOutputDevice _output;
    private bool _stopPriorityRequested;

    public PHprDirectCommandDispatcher(
        IPHprDirectPulseService pulseService,
        SimagicPhprOutputDevice output)
    {
        _pulseService = pulseService ?? throw new ArgumentNullException(nameof(pulseService));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        InstanceId = $"dispatcher-{Guid.NewGuid():N}";
    }

    public string InstanceId { get; }

    public async ValueTask<PhprDeviceCardPulseResult> SendPulseAsync(
        PHprModuleId moduleId,
        PHprRealGearPulseSettings settings,
        PHprSafetyContext safetyContext,
        DateTimeOffset? timestampUtc,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_stopPriorityRequested)
            {
                var command = PhprDeviceCardPulseService.CreateDirectPulseCommand(moduleId, settings, timestampUtc);
                return new PhprDeviceCardPulseResult(
                    moduleId,
                    settings,
                    command,
                    PHprCommandResult.Rejected(
                        PHprCommandStatus.RejectedSafetyLimit,
                        "P-HPR direct pulse rejected because Stop All / Emergency Stop has priority.",
                        command),
                    PhprDeviceCardPulseService.RouteName);
            }

            return await _pulseService.SendDirectPulseAsync(
                moduleId,
                settings,
                safetyContext,
                timestampUtc,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<PHprHidWriteResult> StopAllAsync(CancellationToken cancellationToken = default)
    {
        _stopPriorityRequested = true;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _output.StopAllAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _stopPriorityRequested = false;
            _gate.Release();
        }
    }

    public async ValueTask EmergencyStopAsync(CancellationToken cancellationToken = default)
    {
        _stopPriorityRequested = true;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _output.EmergencyStopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _stopPriorityRequested = false;
            _gate.Release();
        }
    }
}

internal sealed class PHprDirectRuntimeCoordinator : IPHprDirectRuntime
{
    private const int StopObserverToleranceMs = 250;
    private readonly object _gate = new();
    private readonly SimagicPhprOutputDevice _output;
    private readonly IPHprDirectPulseService _pulseService;
    private readonly IPHprDirectCommandDispatcher _dispatcher;
    private readonly IPHprBenchFlightRecorder _flightRecorder;
    private readonly IPHprBenchUncleanShutdownStore _uncleanShutdownStore;
    private readonly IPHprDirectRuntimeClock _clock;
    private readonly Func<PHprDirectSharedPathProof>? _sharedPathProofFactory;
    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private readonly string? _commitSummary;
    private PHprDirectRuntimeEnvironment _environment = PHprDirectRuntimeEnvironment.Disabled;
    private PHprDirectRuntimeState _state = PHprDirectRuntimeState.Disabled;
    private bool _startupCleanupAttempted;
    private bool _startupCleanupSucceeded;
    private bool _startupCleanupFailed;
    private bool _disabledAfterUncleanShutdown;
    private bool _emergencyStopRequested;
    private long _pulseId;
    private long _staleRuntimeObserverIgnoredCount;
    private DateTimeOffset? _lastBenchAcceptedPaddleUtc;
    private PHprDirectLatencySnapshot _latency = new();
    private PHprDirectRuntimeErrorCategory? _lastErrorCategory;
    private string? _lastErrorMessage;

    public PHprDirectRuntimeCoordinator(
        SimagicPhprOutputDevice output,
        IPHprDirectPulseService pulseService,
        IPHprDirectCommandDispatcher dispatcher,
        IPHprBenchFlightRecorder flightRecorder,
        IPHprBenchUncleanShutdownStore uncleanShutdownStore,
        IPHprDirectRuntimeClock? clock = null,
        string? commitSummary = null,
        Func<PHprDirectSharedPathProof>? sharedPathProofFactory = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _pulseService = pulseService ?? throw new ArgumentNullException(nameof(pulseService));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _flightRecorder = flightRecorder ?? throw new ArgumentNullException(nameof(flightRecorder));
        _uncleanShutdownStore = uncleanShutdownStore ?? throw new ArgumentNullException(nameof(uncleanShutdownStore));
        _clock = clock ?? SystemPHprDirectRuntimeClock.Instance;
        _commitSummary = commitSummary;
        _sharedPathProofFactory = sharedPathProofFactory;
        if (_uncleanShutdownStore.Exists())
        {
            _disabledAfterUncleanShutdown = true;
            _lastErrorCategory = PHprDirectRuntimeErrorCategory.UncleanStartup;
            _lastErrorMessage = "P-HPR bench disabled after previous unclean shutdown. Press P-HPR Stop All / Clear Device State before retesting.";
            _state = PHprDirectRuntimeState.Disabled;
            Record("unclean-startup-detected", acceptedRejectedReason: _lastErrorMessage, errorCategory: _lastErrorCategory);
        }
    }

    public void Configure(PHprDirectRuntimeEnvironment environment)
    {
        lock (_gate)
        {
            _environment = environment ?? PHprDirectRuntimeEnvironment.Disabled;
            if (_state == PHprDirectRuntimeState.Disabled && IsRuntimeReadyLocked(out _) && !_uncleanShutdownStore.Exists())
            {
                _state = PHprDirectRuntimeState.Idle;
            }
        }
    }

    public PHprDirectRuntimeSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return BuildSnapshotLocked();
        }
    }

    public async ValueTask InitializeStartupCleanupAsync(CancellationToken cancellationToken = default)
    {
        PHprRealOutputOptions options;
        bool markerExists;
        lock (_gate)
        {
            options = _environment.Options.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
            markerExists = _uncleanShutdownStore.Exists();
            _startupCleanupAttempted = true;
            if (markerExists)
            {
                _disabledAfterUncleanShutdown = true;
            }
        }

        if (!CanAttemptStopOnlyCleanup(options, out var blockedReason))
        {
            lock (_gate)
            {
                _startupCleanupSucceeded = false;
                _startupCleanupFailed = true;
                _state = PHprDirectRuntimeState.Disabled;
                _lastErrorCategory = PHprDirectRuntimeErrorCategory.DeviceSelection;
                _lastErrorMessage = blockedReason;
            }

            Record(
                "startup-stop-only-cleanup-skipped",
                acceptedRejectedReason: blockedReason,
                startupCleanupAttempted: true,
                startupCleanupFailed: true,
                errorCategory: PHprDirectRuntimeErrorCategory.DeviceSelection);
            return;
        }

        Record("startup-stop-only-cleanup-started", startupCleanupAttempted: true);
        var result = await _dispatcher.StopAllAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _startupCleanupSucceeded = result.Succeeded;
            _startupCleanupFailed = !result.Succeeded;
            if (result.Succeeded && !_disabledAfterUncleanShutdown && IsRuntimeReadyLocked(out _))
            {
                _state = PHprDirectRuntimeState.Idle;
                _lastErrorCategory = null;
                _lastErrorMessage = null;
            }
            else if (!result.Succeeded)
            {
                _state = PHprDirectRuntimeState.Faulted;
                _lastErrorCategory = PHprDirectRuntimeErrorCategory.UncleanStartup;
                _lastErrorMessage = result.Message;
            }
        }

        Record(
            "startup-stop-only-cleanup-completed",
            acceptedRejectedReason: result.Message,
            startupCleanupAttempted: true,
            startupCleanupSucceeded: result.Succeeded,
            startupCleanupFailed: !result.Succeeded,
            stopAttempted: true,
            stopWriteSucceeded: result.Succeeded,
            stopWriteFailed: !result.Succeeded,
            errorCategory: result.Succeeded ? null : PHprDirectRuntimeErrorCategory.UncleanStartup);
    }

    public async ValueTask<PhprDeviceCardPulseResult> SendManualPulseAsync(
        PHprModuleId moduleId,
        PHprRealGearPulseSettings settings,
        PHprSafetyContext safetyContext,
        CancellationToken cancellationToken = default)
    {
        var modules = new[] { moduleId };
        var result = await StartPulseSequenceAsync(
            source: "manual-blue-button",
            modules,
            module => settings,
            safetyContext,
            paddleEvent: null,
            benchOptions: null,
            cancellationToken).ConfigureAwait(false);
        return result.Results.FirstOrDefault()
            ?? RejectedPulse(moduleId, settings, "P-HPR direct pulse did not run.");
    }

    public async ValueTask<string> RouteBenchAsync(
        PaddleGearBenchTestResult benchResult,
        PaddleGearBenchTestOptions options,
        WheelPaddleInputSnapshot paddleSnapshot,
        Func<PHprModuleId, PHprRealGearPulseSettings> deviceCardSettings,
        PHprSafetyContext safetyContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(benchResult);
        ArgumentNullException.ThrowIfNull(deviceCardSettings);

        var normalized = (options ?? PaddleGearBenchTestOptions.Disabled).Normalize();
        var rejection = GetBenchRejection(benchResult, normalized, paddleSnapshot);
        if (rejection is not null)
        {
            Record(
                "bench-rejected",
                benchResult,
                normalized,
                acceptedRejectedReason: rejection,
                errorCategory: PHprDirectRuntimeErrorCategory.UserSafetyGate);
            return $"Bench Direct blocked: {rejection}.";
        }

        var modules = ExpandBenchTarget(normalized.TargetModule)
            .Where(module => deviceCardSettings(module).IsEnabled)
            .ToArray();
        if (modules.Length == 0)
        {
            var reason = $"{normalized.TargetModule} P-HPR pulse is disabled";
            Record("bench-rejected", benchResult, normalized, acceptedRejectedReason: reason, errorCategory: PHprDirectRuntimeErrorCategory.UserSafetyGate);
            return $"Bench Direct blocked: {reason}.";
        }

        var sequence = await StartPulseSequenceAsync(
            source: "direct-paddle-bench",
            modules,
            deviceCardSettings,
            safetyContext,
            benchResult.PaddleEvent,
            normalized,
            cancellationToken).ConfigureAwait(false);
        var accepted = sequence.Results.Count(result => result.Succeeded);
        return accepted > 0
            ? $"Bench Direct: sent {accepted:N0}/{sequence.Results.Count:N0} {normalized.TargetModule} pulse(s) through {PhprDeviceCardPulseService.RouteName}; pulse id {sequence.PulseId:N0}."
            : $"Bench Direct blocked: {string.Join(" ", sequence.Results.Select(result => result.CommandResult.Message))}";
    }

    public async ValueTask<PHprDirectStopAllResult> StopAllAsync(
        string reason,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _state = PHprDirectRuntimeState.EmergencyStopping;
        }

        Record("manual-stop-all-started", acceptedRejectedReason: reason, manualStopAllRequested: true, stopAttempted: true);
        PHprHidWriteResult result;
        try
        {
            result = await _dispatcher.StopAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = PHprHidWriteResult.Failure("P-HPR stop-all threw before completion.", ex.Message);
            RecordException("manual-stop-all-exception", ex, PHprDirectRuntimeErrorCategory.StopWrite);
        }

        var markerCleared = false;
        string? markerError = null;
        if (result.Succeeded)
        {
            markerCleared = _uncleanShutdownStore.TryClear(out markerError);
        }

        lock (_gate)
        {
            _disabledAfterUncleanShutdown = _uncleanShutdownStore.Exists();
            if (result.Succeeded && markerCleared && IsRuntimeReadyLocked(out _))
            {
                _state = PHprDirectRuntimeState.Idle;
                _emergencyStopRequested = false;
                _lastErrorCategory = null;
                _lastErrorMessage = null;
            }
            else
            {
                _state = PHprDirectRuntimeState.Faulted;
                _lastErrorCategory = result.Succeeded ? PHprDirectRuntimeErrorCategory.UncleanStartup : PHprDirectRuntimeErrorCategory.StopWrite;
                _lastErrorMessage = markerError ?? result.Message;
            }
        }

        Record(
            "manual-stop-all-completed",
            acceptedRejectedReason: markerError ?? result.Message,
            manualStopAllRequested: true,
            manualStopAllWriteAttempted: true,
            manualStopAllWriteSucceeded: result.Succeeded,
            manualStopAllWriteFailed: !result.Succeeded,
            stopAttempted: true,
            stopWriteSucceeded: result.Succeeded,
            stopWriteFailed: !result.Succeeded,
            errorCategory: result.Succeeded && markerCleared ? null : PHprDirectRuntimeErrorCategory.StopWrite);

        return new PHprDirectStopAllResult(
            result.Succeeded && markerCleared,
            markerError ?? result.Message,
            result,
            GetSnapshot().State,
            _uncleanShutdownStore.Exists());
    }

    public async ValueTask EmergencyStopAsync(string reason, CancellationToken cancellationToken = default)
    {
        Record("emergency-stop-started", acceptedRejectedReason: reason, emergencyStopRequested: true, stopAttempted: true);
        lock (_gate)
        {
            _emergencyStopRequested = true;
            if (_state != PHprDirectRuntimeState.Disabled)
            {
                _state = PHprDirectRuntimeState.EmergencyStopping;
            }
        }

        try
        {
            await _dispatcher.EmergencyStopAsync(cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                _state = PHprDirectRuntimeState.Faulted;
                _lastErrorCategory = PHprDirectRuntimeErrorCategory.EmergencyStop;
                _lastErrorMessage = "Emergency stop requested; press P-HPR Stop All / Clear Device State before retesting.";
            }

            Record(
                "emergency-stop-completed",
                emergencyStopRequested: true,
                emergencyStopWriteAttempted: true,
                emergencyStopWriteSucceeded: true,
                emergencyStopWriteFailed: false,
                errorCategory: PHprDirectRuntimeErrorCategory.EmergencyStop);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            lock (_gate)
            {
                _state = PHprDirectRuntimeState.Faulted;
                _lastErrorCategory = PHprDirectRuntimeErrorCategory.EmergencyStop;
                _lastErrorMessage = ex.Message;
            }

            RecordException("emergency-stop-exception", ex, PHprDirectRuntimeErrorCategory.EmergencyStop);
        }
    }

    public void ClearEmergencyStop()
    {
        _output.ClearEmergencyStop();
        lock (_gate)
        {
            _emergencyStopRequested = false;
            if (_state == PHprDirectRuntimeState.Faulted && !_uncleanShutdownStore.Exists() && IsRuntimeReadyLocked(out _))
            {
                _state = PHprDirectRuntimeState.Idle;
                _lastErrorCategory = null;
                _lastErrorMessage = null;
            }
        }

        Record("emergency-stop-cleared");
    }

    public void HandleUnhandledException(string reason, Exception? exception)
    {
        RecordException(reason, exception, PHprDirectRuntimeErrorCategory.UnhandledException);
        try
        {
            StopAllAsync($"unhandled exception recovery: {reason}").AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            RecordException("unhandled-exception-stop-all-failed", ex, PHprDirectRuntimeErrorCategory.EmergencyStop);
        }
    }

    public async ValueTask HandlePaddleInputExceptionAsync(
        string reason,
        Exception exception,
        bool stopAllIfPulseMayHaveStarted,
        CancellationToken cancellationToken = default)
    {
        RecordException(reason, exception, PHprDirectRuntimeErrorCategory.UnhandledException);
        if (!stopAllIfPulseMayHaveStarted)
        {
            return;
        }

        try
        {
            await StopAllAsync($"paddle input exception recovery: {reason}", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RecordException("paddle-input-exception-stop-all-failed", ex, PHprDirectRuntimeErrorCategory.EmergencyStop);
        }
    }

    private async ValueTask<PulseSequenceResult> StartPulseSequenceAsync(
        string source,
        IReadOnlyList<PHprModuleId> modules,
        Func<PHprModuleId, PHprRealGearPulseSettings> settingsFactory,
        PHprSafetyContext safetyContext,
        WheelPaddleInputEvent? paddleEvent,
        PaddleGearBenchTestOptions? benchOptions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var commandQueuedUtc = _clock.UtcNow;
        var proof = GetSharedPathProof();
        var settings = modules.Select(settingsFactory).ToArray();
        var maxDuration = settings.Length == 0 ? 0 : settings.Max(setting => setting.DurationMs);
        var allowRetrigger = source == "direct-paddle-bench";
        var staleDropThresholdMs = _environment.Options.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits).StalePulseDropThresholdMs;
        var paddleAgeMs = DurationMs(paddleEvent?.TimestampUtc, commandQueuedUtc);
        var staleDrop = paddleAgeMs.HasValue && paddleAgeMs.Value > staleDropThresholdMs;
        long pulseId;
        string? rejectReason;
        lock (_gate)
        {
            rejectReason = staleDrop
                ? $"paddle pulse is stale ({paddleAgeMs:0.###} ms old; drop threshold {staleDropThresholdMs:N0} ms)"
                : GetStartRejectionLocked(proof, settings, maxDuration, allowRetrigger);
            if (rejectReason is null)
            {
                if (!_uncleanShutdownStore.TryCreate(source, out var markerError))
                {
                    rejectReason = $"unclean shutdown marker could not be created: {markerError}";
                    _lastErrorCategory = PHprDirectRuntimeErrorCategory.UncleanStartup;
                    _lastErrorMessage = rejectReason;
                }
                else
                {
                    _state = PHprDirectRuntimeState.Starting;
                }
            }

            pulseId = rejectReason is null ? ++_pulseId : _pulseId;
        }

        if (rejectReason is not null)
        {
            if (staleDrop)
            {
                _output.RecordStaleOutputDropped();
            }

            Record(
                $"{source}-rejected",
                paddleEvent,
                benchOptions,
                pulseId,
                acceptedRejectedReason: rejectReason,
                startRequested: false,
                errorCategory: PHprDirectRuntimeErrorCategory.StateMachineViolation);
            return new PulseSequenceResult(pulseId, modules.Select(module => RejectedPulse(module, settingsFactory(module), rejectReason)).ToArray());
        }

        var interPressMs = DurationMs(_lastBenchAcceptedPaddleUtc, paddleEvent?.TimestampUtc);
        if (paddleEvent is not null)
        {
            _lastBenchAcceptedPaddleUtc = paddleEvent.TimestampUtc;
        }

        Record(
            allowRetrigger ? $"{source}-retrigger-accepted" : $"{source}-start-requested",
            paddleEvent,
            benchOptions,
            pulseId,
            acceptedRejectedReason: "accepted for shared pulse service",
            startRequested: true,
            startWriteAttempted: true,
            commandQueuedUtc: commandQueuedUtc);

        var results = new List<PhprDeviceCardPulseResult>(modules.Count);
        var startAttemptedUtc = _clock.UtcNow;
        try
        {
            foreach (var module in modules)
            {
                var moduleSettings = settingsFactory(module);
                var result = await _dispatcher.SendPulseAsync(
                    module,
                    moduleSettings,
                    safetyContext,
                    paddleEvent?.TimestampUtc,
                    cancellationToken).ConfigureAwait(false);
                results.Add(result);
                if (!result.Succeeded)
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordException($"{source}-start-exception", ex, PHprDirectRuntimeErrorCategory.StartWrite, pulseId);
            await FailClosedStopAllAfterStartFailureAsync(source, cancellationToken).ConfigureAwait(false);
            return new PulseSequenceResult(
                pulseId,
                modules.Select(module => RejectedPulse(module, settingsFactory(module), $"P-HPR direct start failed safely: {ex.Message}")).ToArray());
        }

        var accepted = results.Count > 0 && results.All(result => result.Succeeded);
        var startCompletedUtc = _clock.UtcNow;
        if (!accepted)
        {
            var partialWriteActive = results.Any(result => result.Succeeded);
            if (partialWriteActive)
            {
                await FailClosedStopAllAfterStartFailureAsync(
                    source,
                    cancellationToken,
                    "partial start write succeeded before later module rejection").ConfigureAwait(false);
            }
            else
            {
                _uncleanShutdownStore.TryClear(out _);
                lock (_gate)
                {
                    _state = IsRuntimeReadyLocked(out _) ? PHprDirectRuntimeState.Idle : PHprDirectRuntimeState.Disabled;
                    _lastErrorCategory = PHprDirectRuntimeErrorCategory.StartWrite;
                    _lastErrorMessage = string.Join(" ", results.Select(result => result.CommandResult.Message));
                }
            }

            Record(
                $"{source}-start-rejected",
                paddleEvent,
                benchOptions,
                pulseId,
                acceptedRejectedReason: string.Join(" ", results.Select(result => result.CommandResult.Message)),
                startWriteAttempted: true,
                startWriteSucceeded: false,
                startWriteFailed: true,
                stopAttempted: partialWriteActive,
                errorCategory: PHprDirectRuntimeErrorCategory.StartWrite);
            return new PulseSequenceResult(pulseId, results);
        }

        var stopDueUtc = startCompletedUtc.AddMilliseconds(maxDuration);
        lock (_gate)
        {
            _state = PHprDirectRuntimeState.Active;
            _latency = BuildLatencySnapshot(paddleEvent, benchOptions, commandQueuedUtc, startAttemptedUtc, startCompletedUtc, stopDueUtc, interPressMs);
            Debug.Assert(maxDuration > 0, "Active P-HPR runtime state must have a positive scheduled stop duration.");
            Debug.Assert(_uncleanShutdownStore.Exists(), "Active P-HPR runtime state must have an unclean shutdown marker.");
        }

        Record(
            $"{source}-start-succeeded",
            paddleEvent,
            benchOptions,
            pulseId,
            acceptedRejectedReason: "start write succeeded and stop is scheduled by shared output device",
            startRequested: true,
            startWriteAttempted: true,
            startWriteSucceeded: true,
            startWriteFailed: false,
            stopScheduledTimestamp: startCompletedUtc,
            stopDueTimestamp: stopDueUtc,
            commandQueuedUtc: commandQueuedUtc,
            startWriteAttemptedUtc: startAttemptedUtc,
            startWriteCompletedUtc: startCompletedUtc,
            latency: _latency);

        _ = ObserveStopCompletionAsync(pulseId, maxDuration, source, CancellationToken.None);
        return new PulseSequenceResult(pulseId, results);
    }

    private async Task ObserveStopCompletionAsync(long pulseId, int maxDurationMs, string source, CancellationToken cancellationToken)
    {
        try
        {
            await _clock.DelayAsync(TimeSpan.FromMilliseconds(maxDurationMs + StopObserverToleranceMs), cancellationToken)
                .ConfigureAwait(false);
            lock (_gate)
            {
                if (pulseId != _pulseId)
                {
                    _staleRuntimeObserverIgnoredCount++;
                    Record(
                        $"{source}-stale-observer-ignored",
                        pulseId: pulseId,
                        acceptedRejectedReason: $"ignored stale observer for pulse id {pulseId:N0}; latest pulse id {_pulseId:N0}",
                        stopAttempted: false);
                    return;
                }

                if (_state == PHprDirectRuntimeState.Active)
                {
                    _state = PHprDirectRuntimeState.Stopping;
                }
            }

            var diagnostics = _output.GetDiagnostics();
            if (!diagnostics.ActivePulse && diagnostics.Output.PendingScheduledStopCount == 0)
            {
                if (_uncleanShutdownStore.TryClear(out var markerError))
                {
                    lock (_gate)
                    {
                        if (_state is PHprDirectRuntimeState.Stopping or PHprDirectRuntimeState.Active)
                        {
                            _state = IsRuntimeReadyLocked(out _) ? PHprDirectRuntimeState.Idle : PHprDirectRuntimeState.Disabled;
                        }

                        _lastErrorCategory = null;
                        _lastErrorMessage = null;
                        _latency = _latency with { StopWriteCompletedUtc = diagnostics.LastStopSentAtUtc ?? _clock.UtcNow };
                    }

                    Record(
                        $"{source}-stop-observed",
                        pulseId: pulseId,
                        acceptedRejectedReason: "scheduled stop completed; marker cleared",
                        stopAttempted: true,
                        stopWriteSucceeded: true,
                        stopWriteFailed: false,
                        latency: _latency);
                    return;
                }

                lock (_gate)
                {
                    _state = PHprDirectRuntimeState.Faulted;
                    _lastErrorCategory = PHprDirectRuntimeErrorCategory.UncleanStartup;
                    _lastErrorMessage = markerError;
                }

                Record(
                    $"{source}-marker-clear-failed",
                    pulseId: pulseId,
                    acceptedRejectedReason: markerError,
                    stopAttempted: true,
                    stopWriteSucceeded: true,
                    stopWriteFailed: false,
                    errorCategory: PHprDirectRuntimeErrorCategory.UncleanStartup);
                return;
            }

            var stopAll = await _dispatcher.StopAllAsync(cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                if (stopAll.Succeeded && _uncleanShutdownStore.TryClear(out _))
                {
                    _state = PHprDirectRuntimeState.Idle;
                    _lastErrorCategory = null;
                    _lastErrorMessage = null;
                }
                else
                {
                    _state = PHprDirectRuntimeState.Faulted;
                    _lastErrorCategory = PHprDirectRuntimeErrorCategory.WatchdogStopAll;
                    _lastErrorMessage = stopAll.Message;
                }
            }

            Record(
                $"{source}-watchdog-stop-all",
                pulseId: pulseId,
                acceptedRejectedReason: stopAll.Message,
                stopAttempted: true,
                stopWriteSucceeded: stopAll.Succeeded,
                stopWriteFailed: !stopAll.Succeeded,
                watchdogArmed: true,
                watchdogFired: true,
                errorCategory: stopAll.Succeeded ? null : PHprDirectRuntimeErrorCategory.WatchdogStopAll);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            lock (_gate)
            {
                _state = PHprDirectRuntimeState.Faulted;
                _lastErrorCategory = PHprDirectRuntimeErrorCategory.StopSchedule;
                _lastErrorMessage = ex.Message;
            }

            RecordException($"{source}-stop-observer-exception", ex, PHprDirectRuntimeErrorCategory.StopSchedule, pulseId);
        }
    }

    private async ValueTask FailClosedStopAllAfterStartFailureAsync(
        string source,
        CancellationToken cancellationToken,
        string reason = "start failure may have left hardware active")
    {
        lock (_gate)
        {
            _state = PHprDirectRuntimeState.EmergencyStopping;
        }

        var stopAll = await _dispatcher.StopAllAsync(cancellationToken).ConfigureAwait(false);
        if (stopAll.Succeeded)
        {
            _uncleanShutdownStore.TryClear(out _);
        }

        lock (_gate)
        {
            _state = stopAll.Succeeded ? PHprDirectRuntimeState.Idle : PHprDirectRuntimeState.Faulted;
            _lastErrorCategory = stopAll.Succeeded ? null : PHprDirectRuntimeErrorCategory.StopWrite;
            _lastErrorMessage = stopAll.Succeeded ? null : stopAll.Message;
        }

        Record(
            $"{source}-fail-closed-stop-all",
            acceptedRejectedReason: $"{reason}; {stopAll.Message}",
            stopAttempted: true,
            stopWriteSucceeded: stopAll.Succeeded,
            stopWriteFailed: !stopAll.Succeeded,
            errorCategory: stopAll.Succeeded ? null : PHprDirectRuntimeErrorCategory.StopWrite);
    }

    private string? GetBenchRejection(
        PaddleGearBenchTestResult benchResult,
        PaddleGearBenchTestOptions options,
        WheelPaddleInputSnapshot paddleSnapshot)
    {
        if (!benchResult.Accepted || benchResult.ShiftIntentEvent is null)
        {
            return benchResult.SuppressionReason ?? "no accepted bench event was available";
        }

        if (benchResult.PaddleEvent.ButtonState != InputButtonState.Pressed)
        {
            return $"{benchResult.PaddleEvent.ButtonState} events do not start pulses";
        }

        if (paddleSnapshot.Status != InputListenerStatus.Listening)
        {
            return $"paddle listener is {paddleSnapshot.Status}";
        }

        if (benchResult.PaddleEvent.SourceDevice is null
            || benchResult.PaddleEvent.SourceDevice.ButtonCount is not 32)
        {
            return "mapped paddle event did not come from the 32-button VID_3670/PID_0905 listener device";
        }

        if (paddleSnapshot.LastPaddleEvent is null
            || paddleSnapshot.LastPaddleEvent.SequenceNumber != benchResult.PaddleEvent.SequenceNumber)
        {
            return "mapped paddle event is not visible in listener diagnostics";
        }

        if (!options.IsEnabled || !options.IsArmed)
        {
            return "Paddle Gear Bench is not enabled and armed";
        }

        lock (_gate)
        {
            return GetStartRejectionLocked(
                GetSharedPathProof(),
                [],
                1,
                allowRetrigger: options.OutputMode == PaddleGearBenchTestOutputMode.Direct);
        }
    }

    private string? GetStartRejectionLocked(
        PHprDirectSharedPathProof proof,
        IReadOnlyList<PHprRealGearPulseSettings> settings,
        int maxDurationMs,
        bool allowRetrigger = false)
    {
        if (allowRetrigger && _state == PHprDirectRuntimeState.Active)
        {
            if (!IsRuntimeReadyIgnoringActiveMarkerLocked(out var retriggerBlockedReason))
            {
                return retriggerBlockedReason;
            }
        }
        else if (!IsRuntimeReadyLocked(out var readyBlockedReason))
        {
            return readyBlockedReason;
        }

        if (_state != PHprDirectRuntimeState.Idle
            && !(allowRetrigger && _state == PHprDirectRuntimeState.Active))
        {
            return $"runtime state is {_state}";
        }

        if (_emergencyStopRequested)
        {
            return "Emergency Stop is active";
        }

        if (_uncleanShutdownStore.Exists()
            && !(allowRetrigger && _state == PHprDirectRuntimeState.Active))
        {
            _state = PHprDirectRuntimeState.Faulted;
            _lastErrorCategory = PHprDirectRuntimeErrorCategory.UncleanStartup;
            _lastErrorMessage = "unclean shutdown marker exists; press P-HPR Stop All / Clear Device State before retesting";
            return "unclean shutdown marker exists; press P-HPR Stop All / Clear Device State before retesting";
        }

        if (!proof.IsProven)
        {
            return "not routed through proven Devices pulse service";
        }

        var diagnostics = _output.GetDiagnostics();
        if (!allowRetrigger && (diagnostics.ActivePulse || diagnostics.Output.PendingScheduledStopCount > 0))
        {
            _output.RecordBusyRejected();
            return $"previous pulse is active or pending stop (active {diagnostics.ActivePulse}; pending stops {diagnostics.Output.PendingScheduledStopCount:N0})";
        }

        if (settings.Count > 0)
        {
            if (settings.Any(setting => !setting.IsEnabled))
            {
                return "one or more target P-HPR pedal cards are disabled";
            }

            if (maxDurationMs <= 0)
            {
                return "direct pulse duration must be positive so a scheduled stop can be proven";
            }
        }

        return null;
    }

    private bool IsRuntimeReadyIgnoringActiveMarkerLocked(out string blockedReason)
    {
        var diagnostics = _output.GetDiagnostics();
        if (!PaddleGearBenchDirectGate.TryGetReady(
                _environment.Options,
                _environment.CoexistenceStatus,
                _output.OutputInterlockSnapshot,
                diagnostics.Output,
                _environment.RoadVibrationEnabled,
                _environment.SlipLockEnabled,
                _output.WriteAuthorizationSnapshot,
                out blockedReason))
        {
            return false;
        }

        if (_disabledAfterUncleanShutdown
            && _state != PHprDirectRuntimeState.Active)
        {
            blockedReason = "P-HPR bench disabled after previous unclean shutdown. Press P-HPR Stop All / Clear Device State before retesting.";
            return false;
        }

        if (_startupCleanupAttempted && !_startupCleanupSucceeded)
        {
            blockedReason = "startup stop-only cleanup has not succeeded";
            return false;
        }

        if (!_startupCleanupAttempted)
        {
            blockedReason = "startup stop-only cleanup has not run";
            return false;
        }

        blockedReason = string.Empty;
        return true;
    }

    private bool IsRuntimeReadyLocked(out string blockedReason)
    {
        var diagnostics = _output.GetDiagnostics();
        if (!PaddleGearBenchDirectGate.TryGetReady(
                _environment.Options,
                _environment.CoexistenceStatus,
                _output.OutputInterlockSnapshot,
                diagnostics.Output,
                _environment.RoadVibrationEnabled,
                _environment.SlipLockEnabled,
                _output.WriteAuthorizationSnapshot,
                out blockedReason))
        {
            return false;
        }

        if (_disabledAfterUncleanShutdown || _uncleanShutdownStore.Exists())
        {
            blockedReason = "P-HPR bench disabled after previous unclean shutdown. Press P-HPR Stop All / Clear Device State before retesting.";
            return false;
        }

        if (_startupCleanupAttempted && !_startupCleanupSucceeded)
        {
            blockedReason = "startup stop-only cleanup has not succeeded";
            return false;
        }

        if (!_startupCleanupAttempted)
        {
            blockedReason = "startup stop-only cleanup has not run";
            return false;
        }

        blockedReason = string.Empty;
        return true;
    }

    private PHprDirectRuntimeSnapshot BuildSnapshotLocked()
    {
        var diagnostics = _output.GetDiagnostics();
        var proof = GetSharedPathProof();
        var ready = IsRuntimeReadyLocked(out var blockedReason);
        return new PHprDirectRuntimeSnapshot(
            _state,
            _startupCleanupAttempted,
            _startupCleanupSucceeded,
            _startupCleanupFailed,
            _uncleanShutdownStore.Exists(),
            _disabledAfterUncleanShutdown,
            ready,
            blockedReason,
            diagnostics.ActivePulse,
            diagnostics.Output.PendingScheduledStopCount,
            _pulseId,
            proof,
            _latency,
            _lastErrorCategory,
            _lastErrorMessage,
            _flightRecorder.LogPath,
            _uncleanShutdownStore.MarkerPath);
    }

    private PHprDirectSharedPathProof GetSharedPathProof()
    {
        if (_sharedPathProofFactory is not null)
        {
            return _sharedPathProofFactory();
        }

        return new PHprDirectSharedPathProof(
            _pulseService.InstanceId,
            _pulseService.InstanceId,
            _pulseService.WriterInstanceId,
            _pulseService.WriterInstanceId,
            _pulseService.EncoderInstanceId,
            _pulseService.EncoderInstanceId,
            _pulseService.StopMethodId,
            _pulseService.StopMethodId);
    }

    private void Record(
        string eventName,
        WheelPaddleInputEvent? paddleEvent = null,
        PaddleGearBenchTestOptions? benchOptions = null,
        long? pulseId = null,
        string? acceptedRejectedReason = null,
        bool? startRequested = null,
        bool? startWriteAttempted = null,
        bool? startWriteSucceeded = null,
        bool? startWriteFailed = null,
        DateTimeOffset? stopScheduledTimestamp = null,
        DateTimeOffset? stopDueTimestamp = null,
        bool? stopAttempted = null,
        bool? stopWriteSucceeded = null,
        bool? stopWriteFailed = null,
        bool? watchdogArmed = null,
        bool? watchdogFired = null,
        bool? emergencyStopRequested = null,
        bool? emergencyStopWriteAttempted = null,
        bool? emergencyStopWriteSucceeded = null,
        bool? emergencyStopWriteFailed = null,
        bool? manualStopAllRequested = null,
        bool? manualStopAllWriteAttempted = null,
        bool? manualStopAllWriteSucceeded = null,
        bool? manualStopAllWriteFailed = null,
        bool? startupCleanupAttempted = null,
        bool? startupCleanupSucceeded = null,
        bool? startupCleanupFailed = null,
        DateTimeOffset? commandQueuedUtc = null,
        DateTimeOffset? startWriteAttemptedUtc = null,
        DateTimeOffset? startWriteCompletedUtc = null,
        PHprDirectLatencySnapshot? latency = null,
        PHprDirectRuntimeErrorCategory? errorCategory = null)
    {
        Record(
            eventName,
            null,
            benchOptions,
            pulseId,
            acceptedRejectedReason,
            startRequested,
            startWriteAttempted,
            startWriteSucceeded,
            startWriteFailed,
            stopScheduledTimestamp,
            stopDueTimestamp,
            stopAttempted,
            stopWriteSucceeded,
            stopWriteFailed,
            watchdogArmed,
            watchdogFired,
            emergencyStopRequested,
            emergencyStopWriteAttempted,
            emergencyStopWriteSucceeded,
            emergencyStopWriteFailed,
            manualStopAllRequested,
            manualStopAllWriteAttempted,
            manualStopAllWriteSucceeded,
            manualStopAllWriteFailed,
            startupCleanupAttempted,
            startupCleanupSucceeded,
            startupCleanupFailed,
            commandQueuedUtc,
            startWriteAttemptedUtc,
            startWriteCompletedUtc,
            latency,
            errorCategory,
            paddleEvent);
    }

    private void Record(
        string eventName,
        PaddleGearBenchTestResult benchResult,
        PaddleGearBenchTestOptions benchOptions,
        string? acceptedRejectedReason,
        PHprDirectRuntimeErrorCategory? errorCategory)
    {
        Record(eventName, benchResult.PaddleEvent, benchOptions, acceptedRejectedReason: acceptedRejectedReason, errorCategory: errorCategory);
    }

    private void Record(
        string eventName,
        Exception? exception,
        PaddleGearBenchTestOptions? benchOptions,
        long? pulseId,
        string? acceptedRejectedReason,
        bool? startRequested,
        bool? startWriteAttempted,
        bool? startWriteSucceeded,
        bool? startWriteFailed,
        DateTimeOffset? stopScheduledTimestamp,
        DateTimeOffset? stopDueTimestamp,
        bool? stopAttempted,
        bool? stopWriteSucceeded,
        bool? stopWriteFailed,
        bool? watchdogArmed,
        bool? watchdogFired,
        bool? emergencyStopRequested,
        bool? emergencyStopWriteAttempted,
        bool? emergencyStopWriteSucceeded,
        bool? emergencyStopWriteFailed,
        bool? manualStopAllRequested,
        bool? manualStopAllWriteAttempted,
        bool? manualStopAllWriteSucceeded,
        bool? manualStopAllWriteFailed,
        bool? startupCleanupAttempted,
        bool? startupCleanupSucceeded,
        bool? startupCleanupFailed,
        DateTimeOffset? commandQueuedUtc,
        DateTimeOffset? startWriteAttemptedUtc,
        DateTimeOffset? startWriteCompletedUtc,
        PHprDirectLatencySnapshot? latency,
        PHprDirectRuntimeErrorCategory? errorCategory,
        WheelPaddleInputEvent? paddleEvent = null)
    {
        PHprRealOutputDiagnostics diagnostics;
        PHprDirectRuntimeState state;
        PHprDirectSharedPathProof proof;
        bool markerExists;
        lock (_gate)
        {
            diagnostics = _output.GetDiagnostics();
            state = _state;
            proof = GetSharedPathProof();
            markerExists = _uncleanShutdownStore.Exists();
        }

        var selector = _environment.Options.Selector.Normalize();
        var activeLatency = latency ?? _latency;
        _flightRecorder.Record(new PhprBenchFlightRecorderRecord
        {
            SessionId = _sessionId,
            PulseId = pulseId ?? _pulseId,
            WallClockUtc = _clock.UtcNow,
            MonotonicTimestamp = _clock.MonotonicTimestamp,
            EventName = eventName,
            AppVersionOrCommit = _commitSummary,
            SelectedPHprDeviceSummary = selector.IsSelected
                ? $"{selector.DisplayName}; {selector.InterfaceName}; private path redacted"
                : "none",
            Transport = selector.Transport.ToString(),
            ReportId = selector.ReportId is null ? "none" : $"0x{selector.ReportId.Value:X2} ({selector.ReportId.Value})",
            ReportLength = selector.ReportLength,
            DirectReady = PaddleGearBenchDirectGate.TryGetReady(
                _environment.Options,
                _environment.CoexistenceStatus,
                _output.OutputInterlockSnapshot,
                diagnostics.Output,
                _environment.RoadVibrationEnabled,
                _environment.SlipLockEnabled,
                _output.WriteAuthorizationSnapshot,
                out _),
            WriterInstanceId = proof.BenchWriterInstanceId,
            EncoderInstanceId = proof.BenchEncoderInstanceId,
            BlueButtonPulseServiceInstanceId = proof.BlueButtonPulseServiceInstanceId,
            BenchPulseServiceInstanceId = proof.BenchPulseServiceInstanceId,
            SameServiceInstance = proof.SameServiceInstance,
            SameWriterInstance = proof.SameWriterInstance,
            SameEncoder = proof.SameEncoder,
            SameStopMethod = proof.SameStopMethod,
            BenchEnabled = _environment.BenchEnabled,
            BenchTarget = _environment.BenchTarget.ToString(),
            BrakeCard = FormatCard(_environment.Options.BrakeGearPulse),
            ThrottleCard = FormatCard(_environment.Options.ThrottleGearPulse),
            PaddleListenerSelectedDevice = _environment.SelectedPaddleDeviceSummary,
            PaddleEventSequence = paddleEvent?.SequenceNumber,
            PaddleEventKind = paddleEvent?.ButtonState.ToString(),
            MappedSide = paddleEvent?.PaddleSide.ToString(),
            MappedButton = paddleEvent?.ButtonId,
            AcceptedRejectedReason = acceptedRejectedReason,
            LimiterProfile = eventName.StartsWith("direct-paddle-bench", StringComparison.Ordinal)
                ? $"DirectPaddleGearBench:{SimagicPhprOutputDevice.DirectControlSafetyLimits.MaxCommandsPerSecond:N0} starts/s direct-control profile"
                : "DirectControlDefault",
            PerModuleStartAllowed = startWriteSucceeded == true
                ? "all requested module starts accepted"
                : null,
            PerModuleStartRejected = startWriteFailed == true
                ? acceptedRejectedReason
                : null,
            PerModuleStopAllowed = stopWriteSucceeded == true
                ? "stop reports allowed"
                : null,
            PerModuleStopRejected = stopWriteFailed == true
                ? acceptedRejectedReason
                : null,
            OrdinaryStartRateRejectionCount = acceptedRejectedReason?.Contains("command rate exceeded", StringComparison.OrdinalIgnoreCase) == true
                ? 1
                : 0,
            FailClosedStopAllReason = eventName.Contains("fail-closed-stop-all", StringComparison.Ordinal)
                ? acceptedRejectedReason
                : null,
            PartialWriteActive = eventName.Contains("fail-closed-stop-all", StringComparison.Ordinal),
            StopAllFromUnsafeState = eventName.Contains("fail-closed-stop-all", StringComparison.Ordinal),
            StopAllFromOrdinaryLimiterRejection = false,
            PulseState = state.ToString(),
            PendingStopCount = diagnostics.Output.PendingScheduledStopCount,
            StartRequested = startRequested,
            StartReportFirstBytes = startRequested == true ? PHprHidReportShapeValidationResult.ExpectedF1EcStartFirstBytes : null,
            StartWriteAttempted = startWriteAttempted,
            StartWriteSucceeded = startWriteSucceeded,
            StartWriteFailed = startWriteFailed,
            StopScheduledTimestamp = stopScheduledTimestamp,
            StopDueTimestamp = stopDueTimestamp,
            StopAttempted = stopAttempted,
            StopReportFirstBytes = "F1 EC 01 00 0A 00 / F1 EC 02 00 0A 00",
            StopWriteAttempted = stopAttempted,
            StopWriteSucceeded = stopWriteSucceeded,
            StopWriteFailed = stopWriteFailed,
            WatchdogArmed = watchdogArmed,
            WatchdogFired = watchdogFired,
            EmergencyStopRequested = emergencyStopRequested,
            EmergencyStopWriteAttempted = emergencyStopWriteAttempted,
            EmergencyStopWriteSucceeded = emergencyStopWriteSucceeded,
            EmergencyStopWriteFailed = emergencyStopWriteFailed,
            ManualStopAllRequested = manualStopAllRequested,
            ManualStopAllWriteAttempted = manualStopAllWriteAttempted,
            ManualStopAllWriteSucceeded = manualStopAllWriteSucceeded,
            ManualStopAllWriteFailed = manualStopAllWriteFailed,
            StartupStopOnlyCleanupAttempted = startupCleanupAttempted,
            StartupStopOnlyCleanupSucceeded = startupCleanupSucceeded,
            StartupStopOnlyCleanupFailed = startupCleanupFailed,
            ExceptionType = exception?.GetType().FullName,
            ExceptionMessage = exception?.Message,
            ExceptionStackTrace = exception?.StackTrace,
            SanitizedErrorCategory = errorCategory,
            PaddleReceivedToBenchAcceptedMs = activeLatency.PaddleReceivedToBenchAcceptedMs,
            BenchAcceptedToCommandQueuedMs = activeLatency.BenchAcceptedToCommandQueuedMs,
            CommandQueuedToStartWriteAttemptedMs = activeLatency.CommandQueuedToStartWriteAttemptedMs,
            PaddleReceivedToStartWriteCompletedMs = activeLatency.PaddleReceivedToStartWriteCompletedMs,
            InterPressIntervalMs = activeLatency.InterPressIntervalMs,
            PulseGenerationId = diagnostics.LastPulseGeneration,
            BrakeLatestGeneration = diagnostics.BrakePulseGeneration,
            ThrottleLatestGeneration = diagnostics.ThrottlePulseGeneration,
            StaleStopIgnoredCount = diagnostics.StaleStopIgnoredCount,
            BusyRejectedCount = diagnostics.BusyRejectedCount,
            RetriggerCount = diagnostics.RetriggerCount,
            StaleOutputDroppedCount = diagnostics.StaleOutputDroppedCount,
            DebounceSuppressedCount = _environment.DebounceSuppressedCount,
            StaleRuntimeObserverIgnoredCount = _staleRuntimeObserverIgnoredCount,
            PaddleEventTimestampUtc = paddleEvent?.TimestampUtc,
            AcceptedEventTimestampUtc = activeLatency.BenchAcceptedUtc,
            FirstHidStartWriteTimestampUtc = diagnostics.LastStartSentAtUtc,
            BrakeStopDueAtUtc = diagnostics.BrakeStopDueAtUtc,
            ThrottleStopDueAtUtc = diagnostics.ThrottleStopDueAtUtc
        });

        Debug.Assert(!markerExists || state != PHprDirectRuntimeState.Idle, "Idle runtime state cannot keep an unclean shutdown marker.");
    }

    private void RecordException(
        string eventName,
        Exception? exception,
        PHprDirectRuntimeErrorCategory category,
        long? pulseId = null)
    {
        Record(
            eventName,
            exception,
            benchOptions: null,
            pulseId,
            acceptedRejectedReason: exception?.Message,
            startRequested: null,
            startWriteAttempted: null,
            startWriteSucceeded: null,
            startWriteFailed: null,
            stopScheduledTimestamp: null,
            stopDueTimestamp: null,
            stopAttempted: true,
            stopWriteSucceeded: null,
            stopWriteFailed: null,
            watchdogArmed: null,
            watchdogFired: null,
            emergencyStopRequested: true,
            emergencyStopWriteAttempted: null,
            emergencyStopWriteSucceeded: null,
            emergencyStopWriteFailed: null,
            manualStopAllRequested: null,
            manualStopAllWriteAttempted: null,
            manualStopAllWriteSucceeded: null,
            manualStopAllWriteFailed: null,
            startupCleanupAttempted: null,
            startupCleanupSucceeded: null,
            startupCleanupFailed: null,
            commandQueuedUtc: null,
            startWriteAttemptedUtc: null,
            startWriteCompletedUtc: null,
            latency: null,
            errorCategory: category);
    }

    private PHprDirectLatencySnapshot BuildLatencySnapshot(
        WheelPaddleInputEvent? paddleEvent,
        PaddleGearBenchTestOptions? benchOptions,
        DateTimeOffset commandQueuedUtc,
        DateTimeOffset startAttemptedUtc,
        DateTimeOffset startCompletedUtc,
        DateTimeOffset stopDueUtc,
        double? interPressMs)
    {
        var paddleReceivedUtc = paddleEvent?.TimestampUtc;
        DateTimeOffset? benchAcceptedUtc = benchOptions is null ? null : commandQueuedUtc;
        return new PHprDirectLatencySnapshot(
            paddleReceivedUtc,
            benchAcceptedUtc,
            commandQueuedUtc,
            startAttemptedUtc,
            startCompletedUtc,
            startCompletedUtc,
            stopDueUtc,
            null,
            DurationMs(paddleReceivedUtc, benchAcceptedUtc),
            DurationMs(benchAcceptedUtc, commandQueuedUtc),
            DurationMs(commandQueuedUtc, startAttemptedUtc),
            DurationMs(paddleReceivedUtc, startCompletedUtc),
            interPressMs);
    }

    private static double? DurationMs(DateTimeOffset? start, DateTimeOffset? end)
    {
        return start is null || end is null ? null : (end.Value - start.Value).TotalMilliseconds;
    }

    private static bool CanAttemptStopOnlyCleanup(PHprRealOutputOptions options, out string blockedReason)
    {
        var normalized = options.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        if (!normalized.Selector.IsSelected)
        {
            blockedReason = "no direct P-HPR device is selected";
            return false;
        }

        if (normalized.CandidateIsRawInputOnly || !normalized.CandidateHasOpenableHidPath)
        {
            blockedReason = "selected candidate has no openable HID path";
            return false;
        }

        if (!normalized.AllowsDirectPulseReportShape)
        {
            blockedReason = "selected candidate report shape is unavailable";
            return false;
        }

        blockedReason = string.Empty;
        return true;
    }

    private static PhprDeviceCardPulseResult RejectedPulse(
        PHprModuleId moduleId,
        PHprRealGearPulseSettings settings,
        string reason)
    {
        var normalized = (settings ?? PHprRealGearPulseSettings.Default)
            .Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        var command = PhprDeviceCardPulseService.CreateDirectPulseCommand(moduleId, normalized);
        return new PhprDeviceCardPulseResult(
            moduleId,
            normalized,
            command,
            PHprCommandResult.Rejected(PHprCommandStatus.RejectedSafetyLimit, reason, command),
            PhprDeviceCardPulseService.RouteName);
    }

    private static IReadOnlyList<PHprModuleId> ExpandBenchTarget(PHprGearPulseTarget target)
    {
        return target switch
        {
            PHprGearPulseTarget.Brake => [PHprModuleId.Brake],
            PHprGearPulseTarget.Throttle => [PHprModuleId.Throttle],
            PHprGearPulseTarget.Both => [PHprModuleId.Brake, PHprModuleId.Throttle],
            _ => []
        };
    }

    private static string FormatCard(PHprRealGearPulseSettings settings)
    {
        var normalized = (settings ?? PHprRealGearPulseSettings.Default)
            .Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        return $"enabled {normalized.IsEnabled}; strength {normalized.Strength01:P0}; frequency {normalized.FrequencyHz:0.###} Hz; duration {normalized.DurationMs} ms";
    }

    private sealed record PulseSequenceResult(
        long PulseId,
        IReadOnlyList<PhprDeviceCardPulseResult> Results);
}
