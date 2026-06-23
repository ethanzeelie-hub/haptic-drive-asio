using HapticDrive.Asio.App.ViewModels;

namespace HapticDrive.Asio.App.Controllers;

internal sealed class RecordingReplayController
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private CancellationTokenSource? _activeOperationCts;
    private long _generation;
    private int _state = (int)ControllerRuntimeState.Stopped;

    public RecordingReplayController(RecordingReplayStatusViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        ViewModel.RuntimeState = State.ToString();
    }

    public RecordingReplayStatusViewModel ViewModel { get; }

    public ControllerRuntimeState State => (ControllerRuntimeState)Volatile.Read(ref _state);

    public void Publish(
        TelemetryUdpStatusPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        ViewModel.RecordingStatusText = presentation.RecordingsDetailText;
        ViewModel.ReplayStatusText = presentation.ReplayDetailText;
        ViewModel.WarningText = presentation.ForwardingDestinationsSummaryText;
    }

    public async ValueTask<ControllerOperationResult> RunExclusiveAsync(
        Func<long, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!CanAcceptOperations())
        {
            return new ControllerOperationResult(_generation, Accepted: false, Applied: false);
        }

        var generation = Interlocked.Increment(ref _generation);
        var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!CanAcceptOperations())
            {
                return new ControllerOperationResult(generation, Accepted: false, Applied: false);
            }

            Interlocked.Exchange(ref _activeOperationCts, operationCts);
            SetState(ControllerRuntimeState.Running);
            try
            {
                await operation(generation, operationCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (operationCts.IsCancellationRequested)
            {
                if (CanAcceptOperations())
                {
                    SetState(ControllerRuntimeState.Stopped);
                }

                return new ControllerOperationResult(generation, Accepted: true, Applied: false);
            }

            if (operationCts.IsCancellationRequested
                || !CanAcceptOperations())
            {
                if (CanAcceptOperations())
                {
                    SetState(ControllerRuntimeState.Stopped);
                }

                return new ControllerOperationResult(generation, Accepted: true, Applied: false);
            }

            SetState(ControllerRuntimeState.Stopped);
            return new ControllerOperationResult(generation, Accepted: true, Applied: true);
        }
        finally
        {
            _operationGate.Release();

            Interlocked.CompareExchange(ref _activeOperationCts, null, operationCts);
            operationCts.Dispose();
        }
    }

    public void BeginShutdown(string warningText = "Recording and replay shutdown in progress.")
    {
        SetState(ControllerRuntimeState.ShuttingDown);
        ViewModel.WarningText = warningText;
        CancelAndDispose(Interlocked.Exchange(ref _activeOperationCts, null));
    }

    public void MarkDisposed()
    {
        SetState(ControllerRuntimeState.Disposed);
        CancelAndDispose(Interlocked.Exchange(ref _activeOperationCts, null));
    }

    private bool CanAcceptOperations()
    {
        return State is not ControllerRuntimeState.ShuttingDown
            and not ControllerRuntimeState.Disposed;
    }

    private void SetState(ControllerRuntimeState state)
    {
        Volatile.Write(ref _state, (int)state);
        ViewModel.RuntimeState = state.ToString();
    }

    private static void CancelAndDispose(CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }
}
