using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Input.Abstractions.Paddles;

public sealed class PollingWheelPaddleInputSource : IWheelPaddleInputSource
{
    private readonly IInputButtonStateReader _reader;
    private readonly WheelPaddleInputProcessor _processor;
    private readonly WheelPaddleInputSourceOptions _options;
    private CancellationTokenSource? _listenerCancellation;
    private Task? _listenerTask;
    private InputDeviceSelection? _selectedDevice;
    private bool _disposed;

    public PollingWheelPaddleInputSource(
        IInputButtonStateReader reader,
        WheelPaddleInputSourceOptions? options = null,
        IInputEventClock? clock = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _processor = new WheelPaddleInputProcessor(clock);
        _options = (options ?? WheelPaddleInputSourceOptions.Default).Normalize();
        _processor.RawButtonChanged += (_, e) => RawButtonChanged?.Invoke(this, e);
        _processor.PaddleInputReceived += (_, e) => PaddleInputReceived?.Invoke(this, e);
    }

    public event EventHandler<ShiftIntentEvent>? ShiftIntentReceived
    {
        add { }
        remove { }
    }

    public event EventHandler<WheelPaddleRawButtonEvent>? RawButtonChanged;

    public event EventHandler<WheelPaddleInputEvent>? PaddleInputReceived;

    public string? SelectedDeviceId => _selectedDevice?.DeviceId;

    public async ValueTask StartAsync(
        InputDeviceSelection selection,
        WheelPaddleMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(selection);

        await StopAsync(cancellationToken).ConfigureAwait(false);

        var normalizedMapping = mapping.Normalize() with
        {
            SelectedDeviceId = selection.DeviceId,
            SelectedMethod = _reader.Method
        };
        _selectedDevice = selection;
        _processor.RefreshSelection(selection, InputListenerStatus.Starting);
        _processor.RefreshMapping(normalizedMapping);

        try
        {
            await _reader.StartAsync(selection, cancellationToken).ConfigureAwait(false);
            _processor.RefreshStatus(InputListenerStatus.Listening);
            var listenerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var listenerToken = listenerCancellation.Token;
            _listenerCancellation = listenerCancellation;
            _listenerTask = Task.Run(() => PollAsync(listenerToken), CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _processor.RefreshStatus(InputListenerStatus.Error, ex.Message);
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        var cancellation = _listenerCancellation;
        var listenerTask = _listenerTask;
        _listenerCancellation = null;
        _listenerTask = null;

        if (cancellation is not null)
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            if (listenerTask is not null)
            {
                try
                {
                    await listenerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            cancellation?.Dispose();
        }

        try
        {
            await _reader.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _processor.RefreshStatus(InputListenerStatus.Error, ex.Message);
            return;
        }

        _processor.RefreshStatus(InputListenerStatus.Stopped);
    }

    public void RefreshMapping(WheelPaddleMapping mapping)
    {
        var selectedDeviceId = _selectedDevice?.DeviceId ?? mapping.SelectedDeviceId;
        _processor.RefreshMapping(mapping.Normalize() with
        {
            SelectedDeviceId = selectedDeviceId,
            SelectedMethod = _reader.Method
        });
    }

    public WheelPaddleInputSnapshot GetPaddleSnapshot()
    {
        return _processor.GetSnapshot();
    }

    public ShiftIntentSourceSnapshot GetSnapshot()
    {
        var snapshot = GetPaddleSnapshot();
        return new ShiftIntentSourceSnapshot(
            snapshot.Status is InputListenerStatus.Listening,
            snapshot.SelectedDevice?.DeviceId,
            snapshot.PaddlePressCount,
            snapshot.LastPaddleSide,
            snapshot.LastPaddleEventUtc,
            snapshot.LastErrorMessage);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                _processor.RefreshStatus(snapshot.Status, snapshot.ErrorMessage);

                if (snapshot.Status == InputListenerStatus.Listening)
                {
                    foreach (var button in snapshot.Buttons.OrderBy(pair => pair.Key))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _processor.ProcessButtonState(button.Key, button.Value, _selectedDevice);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _processor.RefreshStatus(InputListenerStatus.Error, ex.Message);
            }

            await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}
