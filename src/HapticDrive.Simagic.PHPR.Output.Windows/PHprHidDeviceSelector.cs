namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprHidDeviceSelector(
    string? DevicePath,
    string DisplayName,
    string InterfaceName,
    byte? ReportId,
    int ReportLength,
    PHprHidReportTransport Transport = PHprHidReportTransport.OutputReport)
{
    public static PHprHidDeviceSelector None { get; } = new(
        null,
        "No P-HPR HID device selected",
        "none",
        null,
        SimHubF1EcRealReportEncoder.PayloadLengthBytes,
        PHprHidReportTransport.OutputReport);

    public bool IsSelected => !string.IsNullOrWhiteSpace(DevicePath) && ReportLength > 0;

    public PHprHidDeviceSelector Normalize()
    {
        var reportLength = ReportLength <= 0
            ? SimHubF1EcRealReportEncoder.PayloadLengthBytes
            : ReportLength;

        return this with
        {
            DevicePath = string.IsNullOrWhiteSpace(DevicePath) ? null : DevicePath.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? "P-HPR HID device" : DisplayName.Trim(),
            InterfaceName = string.IsNullOrWhiteSpace(InterfaceName) ? "manual selection" : InterfaceName.Trim(),
            ReportLength = reportLength,
            Transport = Transport
        };
    }
}
