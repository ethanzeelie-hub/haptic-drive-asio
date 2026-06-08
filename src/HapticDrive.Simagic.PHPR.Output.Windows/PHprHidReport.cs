using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public enum PHprHidReportState
{
    Start = 0,
    Stop = 1,
    EmergencyStop = 2
}

public sealed record PHprHidReport(
    PHprModuleId TargetModule,
    PHprHidReportState State,
    byte[] Payload,
    byte? ReportId,
    DateTimeOffset CreatedAtUtc)
{
    public int Length => Payload.Length;

    public string PayloadHex => string.Join(" ", Payload.Select(value => value.ToString("X2")));
}
