namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed class PHprHidOpenCheckRunner(
    Func<PHprHidDeviceSelector, IPhprHidReportWriter>? writerFactory = null)
{
    private readonly Func<PHprHidDeviceSelector, IPhprHidReportWriter> _writerFactory =
        writerFactory ?? (selector => new WindowsHidReportWriter(allowRealDeviceAccess: true, selector));

    public async Task<PHprHidOpenCheckResult> RunAsync(
        PHprHidDeviceSelector selector,
        bool candidateHasOpenableHidPath,
        bool candidateIsRawInputOnly,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = (selector ?? PHprHidDeviceSelector.None).Normalize();

        if (candidateIsRawInputOnly)
        {
            return PHprHidOpenCheckResult.Failure(
                "Selected candidate is Raw Input metadata only and does not provide an openable HID device-interface path.",
                PHprHidWriteStatus.InvalidReport,
                "RawInputOnly");
        }

        if (!normalized.IsSelected)
        {
            return PHprHidOpenCheckResult.Failure(
                "No P-HPR HID device/interface/report is selected for open-check.",
                PHprHidWriteStatus.NotSelected,
                "NotSelected");
        }

        if (!candidateHasOpenableHidPath)
        {
            return PHprHidOpenCheckResult.Failure(
                "Selected candidate does not provide an openable HID device-interface path.",
                PHprHidWriteStatus.InvalidReport,
                "NoOpenableHidPath");
        }

        if (!PHprHidPathSafety.IsAbsoluteWindowsDevicePath(normalized.DevicePath))
        {
            return PHprHidOpenCheckResult.Failure(
                "Selected candidate path is not an absolute Windows HID device-interface path.",
                PHprHidWriteStatus.InvalidReport,
                PHprHidPathSafety.InvalidDevicePathCategory);
        }

        var writer = _writerFactory(normalized);
        var open = await writer.OpenAsync(cancellationToken);
        if (!open.Succeeded)
        {
            return PHprHidOpenCheckResult.Failure(
                "P-HPR HID open-check failed before any report was sent.",
                open.Status,
                open.ErrorMessage ?? open.Status.ToString());
        }

        var close = await writer.CloseAsync(cancellationToken);
        if (!close.Succeeded)
        {
            return new PHprHidOpenCheckResult(
                Attempted: true,
                Succeeded: false,
                Failed: true,
                OpenStatus: open.Status,
                CloseStatus: close.Status,
                "P-HPR HID open-check opened the writer but close failed; no report was sent.",
                SanitizedErrorCategory: close.ErrorMessage ?? close.Status.ToString(),
                AttemptedAtUtc: DateTimeOffset.UtcNow);
        }

        return PHprHidOpenCheckResult.Success(close.Status);
    }
}
