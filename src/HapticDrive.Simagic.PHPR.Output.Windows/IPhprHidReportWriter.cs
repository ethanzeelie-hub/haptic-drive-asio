namespace HapticDrive.Simagic.PHPR.Output.Windows;

public interface IPhprHidReportWriter
{
    PHprHidDeviceSelector Selector { get; }

    bool IsOpen { get; }

    ValueTask<PHprHidWriteResult> OpenAsync(CancellationToken cancellationToken = default);

    ValueTask<PHprHidWriteResult> WriteReportAsync(PHprHidReport report, CancellationToken cancellationToken = default);

    ValueTask<PHprHidWriteResult> CloseAsync(CancellationToken cancellationToken = default);
}
