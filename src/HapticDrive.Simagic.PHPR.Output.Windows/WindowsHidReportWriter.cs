namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed class WindowsHidReportWriter : IPhprHidReportWriter
{
    private readonly object _gate = new();
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

    public void Configure(PHprHidDeviceSelector selector)
    {
        lock (_gate)
        {
            _selector = (selector ?? PHprHidDeviceSelector.None).Normalize();
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
            return PHprHidWriteResult.Failure("No P-HPR HID device path is selected.");
        }

        if (report.Payload.Length != selector.ReportLength)
        {
            return PHprHidWriteResult.Failure(
                $"P-HPR HID report length {report.Payload.Length:N0} does not match selected length {selector.ReportLength:N0}.");
        }

        try
        {
            await using var stream = new FileStream(
                selector.DevicePath!,
                FileMode.Open,
                FileAccess.Write,
                FileShare.ReadWrite,
                selector.ReportLength,
                useAsync: true);
            await stream.WriteAsync(report.Payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return PHprHidWriteResult.Success(report.Payload.Length, "P-HPR HID report written to the selected device path.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return PHprHidWriteResult.Failure("P-HPR HID report write failed.", ex.Message);
        }
    }
}
