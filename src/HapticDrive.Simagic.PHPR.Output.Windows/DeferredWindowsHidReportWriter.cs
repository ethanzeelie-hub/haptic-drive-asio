namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed class DeferredWindowsHidReportWriter : IPhprHidReportWriter
{
    private readonly object _gate = new();
    private WindowsHidReportWriter? _inner;
    private PHprHidDeviceSelector _selector = PHprHidDeviceSelector.None;

    public PHprHidDeviceSelector Selector
    {
        get
        {
            lock (_gate)
            {
                return _inner?.Selector ?? _selector;
            }
        }
    }

    public bool IsOpen
    {
        get
        {
            lock (_gate)
            {
                return _inner?.IsOpen ?? false;
            }
        }
    }

    public void Configure(PHprHidDeviceSelector selector)
    {
        lock (_gate)
        {
            _selector = (selector ?? PHprHidDeviceSelector.None).Normalize();
            _inner?.Configure(_selector);
        }
    }

    public ValueTask<PHprHidWriteResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        return GetOrCreate().OpenAsync(cancellationToken);
    }

    public ValueTask<PHprHidWriteResult> WriteReportAsync(PHprHidReport report, CancellationToken cancellationToken = default)
    {
        return GetOrCreate().WriteReportAsync(report, cancellationToken);
    }

    public ValueTask<PHprHidWriteResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return _inner?.CloseAsync(cancellationToken)
                ?? ValueTask.FromResult(PHprHidWriteResult.Success(0, "Deferred P-HPR HID writer already closed."));
        }
    }

    private WindowsHidReportWriter GetOrCreate()
    {
        lock (_gate)
        {
            if (_inner is not null)
            {
                return _inner;
            }

            _inner = new WindowsHidReportWriter(
                allowRealDeviceAccess: true,
                _selector);
            return _inner;
        }
    }
}
