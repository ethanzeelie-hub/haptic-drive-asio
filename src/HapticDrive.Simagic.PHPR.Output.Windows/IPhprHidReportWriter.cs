namespace HapticDrive.Simagic.PHPR.Output.Windows;

public interface IPhprHidReportWriter
{
    PHprHidDeviceSelector Selector { get; }

    ValueTask<PHprHidWriteResult> WriteReportAsync(PHprHidReport report, CancellationToken cancellationToken = default);
}
