namespace HapticDrive.Simagic.PHPR.Research.Capture;

public sealed record SimagicCaptureSoftwareContext
{
    public string? CaptureTool { get; init; }

    public string? CaptureToolVersion { get; init; }

    public string? SoftwareUnderTest { get; init; }

    public string? SoftwareUnderTestVersion { get; init; }

    public string? SimProVersion { get; init; }

    public string? SimHubVersion { get; init; }

    public bool? SimProRunning { get; init; }

    public bool? SimHubRunning { get; init; }

    public bool? HapticDriveRunning { get; init; }
}
