using HapticDrive.Asio.App.ViewModels;

namespace HapticDrive.Asio.App.Controllers;

internal sealed record AudioOutputSelectionState(
    string SelectedOutputId,
    string StatusText,
    int SampleRate,
    int BufferSize);

internal sealed class AudioOutputController
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private CancellationTokenSource? _activeSelectionCts;
    private long _generation;
    private int _state = (int)ControllerRuntimeState.Stopped;

    public AudioOutputController(OutputDeviceStatusViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        ViewModel.RuntimeState = State.ToString();
    }

    public OutputDeviceStatusViewModel ViewModel { get; }

    public ControllerRuntimeState State => (ControllerRuntimeState)Volatile.Read(ref _state);

    public void Publish(
        string selectedOutputId,
        string statusText,
        int sampleRate,
        int bufferSize)
    {
        ViewModel.SelectedOutputId = selectedOutputId;
        ViewModel.StatusText = statusText;
        ViewModel.SampleRate = sampleRate;
        ViewModel.BufferSize = bufferSize;
    }

    public async ValueTask<ControllerOperationResult> SelectOutputAsync(
        Func<long, CancellationToken, Task<AudioOutputSelectionState>> applyAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applyAsync);

        if (!CanAcceptOperations())
        {
            return new ControllerOperationResult(_generation, Accepted: false, Applied: false);
        }

        var generation = Interlocked.Increment(ref _generation);
        var selectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previousCts = Interlocked.Exchange(ref _activeSelectionCts, selectionCts);
        CancelAndDispose(previousCts);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!CanAcceptOperations())
            {
                return new ControllerOperationResult(generation, Accepted: false, Applied: false);
            }

            SetState(ControllerRuntimeState.Running);

            AudioOutputSelectionState selection;
            try
            {
                selection = await applyAsync(generation, selectionCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (selectionCts.IsCancellationRequested)
            {
                if (CanAcceptOperations())
                {
                    SetState(ControllerRuntimeState.Stopped);
                }

                return new ControllerOperationResult(generation, Accepted: true, Applied: false);
            }

            if (!ShouldApply(generation, selectionCts.Token))
            {
                if (CanAcceptOperations())
                {
                    SetState(ControllerRuntimeState.Stopped);
                }

                return new ControllerOperationResult(generation, Accepted: true, Applied: false);
            }

            Publish(
                selection.SelectedOutputId,
                selection.StatusText,
                selection.SampleRate,
                selection.BufferSize);
            SetState(ControllerRuntimeState.Stopped);
            return new ControllerOperationResult(generation, Accepted: true, Applied: true);
        }
        finally
        {
            _operationGate.Release();

            if (ReferenceEquals(Volatile.Read(ref _activeSelectionCts), selectionCts))
            {
                Interlocked.CompareExchange(ref _activeSelectionCts, null, selectionCts);
            }

            selectionCts.Dispose();
        }
    }

    public void BeginShutdown(string statusText = "Output shutdown in progress.")
    {
        SetState(ControllerRuntimeState.ShuttingDown);
        ViewModel.StatusText = statusText;
        CancelAndDispose(Interlocked.Exchange(ref _activeSelectionCts, null));
    }

    public void MarkDisposed()
    {
        SetState(ControllerRuntimeState.Disposed);
        CancelAndDispose(Interlocked.Exchange(ref _activeSelectionCts, null));
    }

    private bool CanAcceptOperations()
    {
        return State is not ControllerRuntimeState.ShuttingDown
            and not ControllerRuntimeState.Disposed;
    }

    private bool ShouldApply(long generation, CancellationToken cancellationToken)
    {
        return generation == Interlocked.Read(ref _generation)
            && !cancellationToken.IsCancellationRequested
            && CanAcceptOperations();
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
