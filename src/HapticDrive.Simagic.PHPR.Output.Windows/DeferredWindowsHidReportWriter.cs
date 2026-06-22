using HapticDrive.Asio.Core.Safety;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed class DeferredWindowsHidReportWriter : IPhprHidReportWriter
{
    private readonly object _gate = new();
    private readonly Func<PHprHidDeviceSelector, IPhprHidReportWriter> _writerFactory;
    private readonly IOutputInterlock _outputInterlock;
    private readonly IPHprWriteAuthorization _writeAuthorization;
    private IPhprHidReportWriter? _inner;
    private PHprHidDeviceSelector _selector = PHprHidDeviceSelector.None;

    public DeferredWindowsHidReportWriter(
        Func<PHprHidDeviceSelector, IPhprHidReportWriter>? writerFactory,
        IOutputInterlock outputInterlock,
        IPHprWriteAuthorization writeAuthorization)
    {
        _writerFactory = writerFactory ?? (selector => new WindowsHidReportWriter(selector));
        _outputInterlock = outputInterlock ?? throw new ArgumentNullException(nameof(outputInterlock));
        _writeAuthorization = writeAuthorization ?? throw new ArgumentNullException(nameof(writeAuthorization));
    }

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
            if (_inner is WindowsHidReportWriter windowsWriter)
            {
                windowsWriter.Configure(_selector);
            }
            else
            {
                _inner = null;
            }
        }
    }

    public ValueTask<PHprHidWriteResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        if (!_outputInterlock.Current.AllowsOutput)
        {
            return ValueTask.FromResult(PHprHidWriteResult.Failure(
                "Real P-HPR HID writer open blocked because the global output interlock is latched.",
                status: PHprHidWriteStatus.Failed));
        }

        if (!_writeAuthorization.Current.IsAuthorized)
        {
            return ValueTask.FromResult(PHprHidWriteResult.Failure(
                "Real P-HPR HID writer open blocked because this session is not authorized for controlled writes.",
                status: PHprHidWriteStatus.Failed));
        }

        return GetOrCreate().OpenAsync(cancellationToken);
    }

    public ValueTask<PHprHidWriteResult> WriteReportAsync(PHprHidReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.State == PHprHidReportState.Start)
        {
            if (!_outputInterlock.Current.AllowsOutput)
            {
                return ValueTask.FromResult(PHprHidWriteResult.Failure(
                    "Real P-HPR HID start write blocked because the global output interlock is latched.",
                    status: PHprHidWriteStatus.Failed));
            }

            if (!_writeAuthorization.Current.IsAuthorized)
            {
                return ValueTask.FromResult(PHprHidWriteResult.Failure(
                    "Real P-HPR HID start write blocked because this session is not authorized for controlled writes.",
                    status: PHprHidWriteStatus.Failed));
            }
        }

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

    private IPhprHidReportWriter GetOrCreate()
    {
        lock (_gate)
        {
            if (_inner is not null)
            {
                return _inner;
            }

            _inner = _writerFactory(_selector);
            return _inner;
        }
    }
}
