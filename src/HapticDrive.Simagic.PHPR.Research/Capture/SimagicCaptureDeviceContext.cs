namespace HapticDrive.Simagic.PHPR.Research.Capture;

public sealed record SimagicCaptureDeviceContext
{
    public string? P700FirmwareVersion { get; init; }

    public string? DeviceInventoryReference { get; init; }

    public SimagicCaptureTargetModule TargetModule { get; init; } = SimagicCaptureTargetModule.Unknown;
}
