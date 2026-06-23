using HapticDrive.Actuation.Driving;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprContinuousEffectsRuntimeInput(
    HapticFrame? HapticFrame,
    ActuationDrivingContext DrivingContext,
    bool IsPedalRoutingReady,
    PHprSafetyContext RoadSafetyContext,
    PHprSafetyContext SlipLockSafetyContext);

public sealed record PHprContinuousEffectsRuntimeSnapshot(
    bool SlipLockRuntimeStarted,
    bool SlipLockRuntimeActive,
    bool RoadRuntimeStarted,
    bool RoadRuntimeActive,
    bool RoutingSlipLock,
    bool RoutingRoadVibration,
    long RoadHigherPrioritySuppressedCount,
    long RoadInFlightSuppressedCount,
    PHprSlipLockRoutingResult? LastSlipLockRoutingResult,
    PHprRoadVibrationRoutingResult? LastRoadVibrationRoutingResult);

public sealed record PHprContinuousEffectsRuntimeStopResult(
    bool SlipLockRuntimeTimedOut,
    bool RoadRuntimeTimedOut);

public interface IPHprContinuousEffectsRuntimeClock
{
    DateTimeOffset UtcNow { get; }

    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}

public sealed class PHprContinuousEffectsRuntimeCoordinator : IAsyncDisposable
{
    private static readonly TimeSpan RuntimeCadence = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan HigherPriorityRoadYieldWindow = TimeSpan.FromMilliseconds(150);
    private readonly object _gate = new();
    private readonly PHprRoadVibrationRouter _roadVibrationRouter;
    private readonly PHprSlipLockRouter _slipLockRouter;
    private readonly Func<PHprContinuousEffectsRuntimeInput> _inputProvider;
    private readonly IPHprContinuousEffectsRuntimeClock _clock;
    private readonly CancellationTokenSource _runtimeCts = new();
    private Task? _slipLockRuntimeTask;
    private Task? _roadVibrationRuntimeTask;
    private bool _routingRoadVibration;
    private bool _routingSlipLock;
    private long _roadHigherPrioritySuppressedCount;
    private long _roadInFlightSuppressedCount;
    private PHprRoadVibrationRoutingResult? _lastRoadVibrationRoutingResult;
    private PHprSlipLockRoutingResult? _lastSlipLockRoutingResult;
    private bool _disposed;

    public PHprContinuousEffectsRuntimeCoordinator(
        PHprRoadVibrationRouter roadVibrationRouter,
        PHprSlipLockRouter slipLockRouter,
        Func<PHprContinuousEffectsRuntimeInput> inputProvider,
        IPHprContinuousEffectsRuntimeClock? clock = null)
    {
        _roadVibrationRouter = roadVibrationRouter ?? throw new ArgumentNullException(nameof(roadVibrationRouter));
        _slipLockRouter = slipLockRouter ?? throw new ArgumentNullException(nameof(slipLockRouter));
        _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
        _clock = clock ?? SystemPHprContinuousEffectsRuntimeClock.Instance;
    }

    public void StartSlipLockRuntime()
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            if (_slipLockRuntimeTask is not null || _runtimeCts.IsCancellationRequested)
            {
                return;
            }

            _slipLockRuntimeTask = Task.Run(
                () => RunRealSlipLockRuntimeAsync(_runtimeCts.Token),
                _runtimeCts.Token);
        }
    }

    public void StartRoadVibrationRuntime()
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            if (_roadVibrationRuntimeTask is not null || _runtimeCts.IsCancellationRequested)
            {
                return;
            }

            _roadVibrationRuntimeTask = Task.Run(
                () => RunRealRoadVibrationRuntimeAsync(_runtimeCts.Token),
                _runtimeCts.Token);
        }
    }

    public PHprContinuousEffectsRuntimeSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new PHprContinuousEffectsRuntimeSnapshot(
                SlipLockRuntimeStarted: _slipLockRuntimeTask is not null,
                SlipLockRuntimeActive: _slipLockRuntimeTask is { IsCompleted: false },
                RoadRuntimeStarted: _roadVibrationRuntimeTask is not null,
                RoadRuntimeActive: _roadVibrationRuntimeTask is { IsCompleted: false },
                RoutingSlipLock: _routingSlipLock,
                RoutingRoadVibration: _routingRoadVibration,
                RoadHigherPrioritySuppressedCount: _roadHigherPrioritySuppressedCount,
                RoadInFlightSuppressedCount: _roadInFlightSuppressedCount,
                LastSlipLockRoutingResult: _lastSlipLockRoutingResult,
                LastRoadVibrationRoutingResult: _lastRoadVibrationRoutingResult);
        }
    }

    public async ValueTask<PHprContinuousEffectsRuntimeStopResult> StopAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        _runtimeCts.Cancel();

        Task? slipLockTask;
        Task? roadRuntimeTask;
        lock (_gate)
        {
            slipLockTask = _slipLockRuntimeTask;
            roadRuntimeTask = _roadVibrationRuntimeTask;
        }

        var slipLockTimedOut = await WaitForRuntimeToStopAsync(slipLockTask, timeout, cancellationToken).ConfigureAwait(false);
        var roadTimedOut = await WaitForRuntimeToStopAsync(roadRuntimeTask, timeout, cancellationToken).ConfigureAwait(false);
        return new PHprContinuousEffectsRuntimeStopResult(slipLockTimedOut, roadTimedOut);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            await StopAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        finally
        {
            _runtimeCts.Dispose();
        }
    }

    private async Task RunRealSlipLockRuntimeAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await _clock.DelayAsync(RuntimeCadence, cancellationToken).ConfigureAwait(false);
                var input = _inputProvider();
                await RouteRealSlipLockFromInputAsync(input).ConfigureAwait(false);
                await _slipLockRouter.StopIfHoldExpiredAsync(
                        nowUtc: _clock.UtcNow,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunRealRoadVibrationRuntimeAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await _clock.DelayAsync(RuntimeCadence, cancellationToken).ConfigureAwait(false);
                var input = _inputProvider();
                await RouteRealRoadVibrationFromInputAsync(
                        input,
                        IsHigherPriorityPedalEffectActive(_clock.UtcNow))
                    .ConfigureAwait(false);
                await _roadVibrationRouter.StopIfHoldExpiredAsync(
                        nowUtc: _clock.UtcNow,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<PHprSlipLockRoutingResult?> RouteRealSlipLockFromInputAsync(PHprContinuousEffectsRuntimeInput input)
    {
        lock (_gate)
        {
            if (_routingSlipLock)
            {
                return null;
            }

            _routingSlipLock = true;
        }

        try
        {
            if (!_slipLockRouter.GetSnapshot().Options.IsEnabled)
            {
                await _slipLockRouter.StopAsync(
                    "P-HPR slip/lock stopped because routing is disabled.",
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
                return null;
            }

            if (!input.IsPedalRoutingReady)
            {
                await _slipLockRouter.StopAsync(
                    "P-HPR slip/lock stopped because output readiness or slip/lock routing gates are not satisfied.",
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
                return null;
            }

            var result = await _slipLockRouter.RouteAsync(
                input.HapticFrame,
                input.DrivingContext,
                input.SlipLockSafetyContext).ConfigureAwait(false);
            lock (_gate)
            {
                _lastSlipLockRoutingResult = result;
            }

            return result;
        }
        finally
        {
            lock (_gate)
            {
                _routingSlipLock = false;
            }
        }
    }

    private async Task RouteRealRoadVibrationFromInputAsync(
        PHprContinuousEffectsRuntimeInput input,
        bool higherPriorityPedalEffectRouted)
    {
        lock (_gate)
        {
            if (_routingRoadVibration)
            {
                _roadInFlightSuppressedCount++;
                return;
            }

            _routingRoadVibration = true;
        }

        try
        {
            if (higherPriorityPedalEffectRouted)
            {
                lock (_gate)
                {
                    _roadHigherPrioritySuppressedCount++;
                }

                await _roadVibrationRouter.StopAsync(
                    "P-HPR road stopped because a higher-priority pedal effect routed.",
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
                return;
            }

            if (!_roadVibrationRouter.GetSnapshot().Options.IsEnabled
                || !input.IsPedalRoutingReady)
            {
                await _roadVibrationRouter.StopAsync(
                    "P-HPR road stopped because output readiness or road routing gates are not satisfied.",
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
                return;
            }

            var result = await _roadVibrationRouter.RouteAsync(
                input.HapticFrame,
                input.DrivingContext,
                input.RoadSafetyContext).ConfigureAwait(false);
            lock (_gate)
            {
                _lastRoadVibrationRoutingResult = result;
            }
        }
        finally
        {
            lock (_gate)
            {
                _routingRoadVibration = false;
            }
        }
    }

    private bool IsHigherPriorityPedalEffectActive(DateTimeOffset nowUtc)
    {
        PHprSlipLockRoutingResult? lastSlipLockRoutingResult;
        bool routingSlipLock;
        lock (_gate)
        {
            lastSlipLockRoutingResult = _lastSlipLockRoutingResult;
            routingSlipLock = _routingSlipLock;
        }

        var slipLockSnapshot = _slipLockRouter.GetSnapshot();
        return routingSlipLock
            || slipLockSnapshot.ActiveSlipLockModules != "none"
            || (lastSlipLockRoutingResult?.WasRouted == true
                && nowUtc - lastSlipLockRoutingResult.RoutedAtUtc < HigherPriorityRoadYieldWindow);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static async ValueTask<bool> WaitForRuntimeToStopAsync(
        Task? runtimeTask,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (runtimeTask is null)
        {
            return false;
        }

        try
        {
            await runtimeTask.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return true;
        }
    }

    private sealed class SystemPHprContinuousEffectsRuntimeClock : IPHprContinuousEffectsRuntimeClock
    {
        public static SystemPHprContinuousEffectsRuntimeClock Instance { get; } = new();

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            return new ValueTask(Task.Delay(delay, cancellationToken));
        }
    }
}
