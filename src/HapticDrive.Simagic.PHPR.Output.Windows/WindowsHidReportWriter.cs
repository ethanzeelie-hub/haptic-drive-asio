namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed class WindowsHidReportWriter : IPhprHidReportWriter
{
    private readonly object _gate = new();
    private FileStream? _stream;
    private PHprHidDeviceSelector _selector;

    public WindowsHidReportWriter(PHprHidDeviceSelector? selector = null)
    {
        _selector = (selector ?? PHprHidDeviceSelector.None).Normalize();
    }

    public PHprHidDeviceSelector Selector
    {
        get
        {
            lock (_gate)
            {
                return _selector;
            }
        }
    }

    public bool IsOpen
    {
        get
        {
            lock (_gate)
            {
                return _stream is not null;
            }
        }
    }

    public void Configure(PHprHidDeviceSelector selector)
    {
        lock (_gate)
        {
            _stream?.Dispose();
            _stream = null;
            _selector = (selector ?? PHprHidDeviceSelector.None).Normalize();
        }
    }

    public ValueTask<PHprHidWriteResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_stream is not null)
            {
                return ValueTask.FromResult(PHprHidWriteResult.Success(_selector.ReportLength, "P-HPR HID writer already open."));
            }

            if (!_selector.IsSelected)
            {
                return ValueTask.FromResult(PHprHidWriteResult.Failure(
                    "No P-HPR HID device path is selected.",
                    status: PHprHidWriteStatus.NotSelected));
            }

            if (!PHprHidPathSafety.IsAbsoluteWindowsDevicePath(_selector.DevicePath))
            {
                return ValueTask.FromResult(PHprHidWriteResult.Failure(
                    "Selected P-HPR HID device path is not an absolute Windows device-interface path.",
                    PHprHidPathSafety.InvalidDevicePathCategory,
                    PHprHidWriteStatus.InvalidReport));
            }

            if (_selector.ReportLength != SimHubF1EcRealReportEncoder.PayloadLengthBytes)
            {
                return ValueTask.FromResult(PHprHidWriteResult.Failure(
                    $"Selected P-HPR HID report length {_selector.ReportLength:N0} does not match the SimHub F1 EC length {SimHubF1EcRealReportEncoder.PayloadLengthBytes:N0}.",
                    status: PHprHidWriteStatus.InvalidReport));
            }

            try
            {
                _stream = new FileStream(
                    _selector.DevicePath!,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    _selector.ReportLength,
                    useAsync: true);
                return ValueTask.FromResult(PHprHidWriteResult.Success(_selector.ReportLength, "P-HPR HID writer opened the selected device path."));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                var status = ex is IOException ? PHprHidWriteStatus.Disconnected : PHprHidWriteStatus.Failed;
                return ValueTask.FromResult(PHprHidWriteResult.Failure(
                    "P-HPR HID writer failed to open the selected device path.",
                    PHprHidPathSafety.SanitizeExceptionCategory(ex),
                    status));
            }
        }
    }

    public async ValueTask<PHprHidWriteResult> WriteReportAsync(
        PHprHidReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        var selector = Selector;
        if (!selector.IsSelected)
        {
            return PHprHidWriteResult.Failure("No P-HPR HID device path is selected.", status: PHprHidWriteStatus.NotSelected);
        }

        if (report.Payload.Length != selector.ReportLength)
        {
            return PHprHidWriteResult.Failure(
                $"P-HPR HID report length {report.Payload.Length:N0} does not match selected length {selector.ReportLength:N0}.",
                status: PHprHidWriteStatus.InvalidReport);
        }

        try
        {
            var stream = await GetOrOpenStreamAsync(cancellationToken);
            if (stream is null)
            {
                return PHprHidWriteResult.Failure("P-HPR HID writer is not open.", status: PHprHidWriteStatus.Disconnected);
            }

            await stream.WriteAsync(report.Payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return PHprHidWriteResult.Success(report.Payload.Length, "P-HPR HID report written to the selected device path.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            lock (_gate)
            {
                _stream?.Dispose();
                _stream = null;
            }

            var status = PHprHidPathSafety.ClassifyWriteExceptionStatus(ex);
            return PHprHidWriteResult.Failure(
                status == PHprHidWriteStatus.InvalidReport
                    ? "P-HPR HID report write failed; Windows rejected the report shape/write format."
                    : "P-HPR HID report write failed.",
                PHprHidPathSafety.SanitizeExceptionCategory(ex),
                status);
        }
    }

    public ValueTask<PHprHidWriteResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_stream is null)
            {
                return ValueTask.FromResult(PHprHidWriteResult.Success(0, "P-HPR HID writer already closed."));
            }

            try
            {
                _stream.Dispose();
                _stream = null;
                return ValueTask.FromResult(PHprHidWriteResult.Success(0, "P-HPR HID writer closed."));
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                _stream = null;
                return ValueTask.FromResult(PHprHidWriteResult.Failure(
                    "P-HPR HID writer close failed.",
                    PHprHidPathSafety.SanitizeExceptionCategory(ex)));
            }
        }
    }

    private async ValueTask<FileStream?> GetOrOpenStreamAsync(CancellationToken cancellationToken)
    {
        FileStream? stream;
        lock (_gate)
        {
            stream = _stream;
        }

        if (stream is not null)
        {
            return stream;
        }

        var openResult = await OpenAsync(cancellationToken);
        if (!openResult.Succeeded)
        {
            return null;
        }

        lock (_gate)
        {
            return _stream;
        }
    }
}
