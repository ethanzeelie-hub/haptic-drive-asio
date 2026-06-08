using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed class SimHubF1EcRealReportEncoder
{
    public const int PayloadLengthBytes = 64;
    public const byte Prefix0 = 0xF1;
    public const byte Prefix1 = 0xEC;
    public const byte BrakeModuleByte = 0x01;
    public const byte ThrottleModuleByte = 0x02;
    public const byte StartStateByte = 0x01;
    public const byte StopStateByte = 0x00;
    public const byte StopFrequencyByte = 0x0A;

    public IReadOnlyList<PHprHidReport> EncodeStart(PHprCommand command, byte? reportId = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ExpandModules(command.TargetModule)
            .Select(module => CreateReport(
                module,
                PHprHidReportState.Start,
                command.FrequencyHz,
                command.Strength01,
                reportId))
            .ToArray();
    }

    public IReadOnlyList<PHprHidReport> EncodeStop(PHprModuleId targetModule, byte? reportId = null, bool emergencyStop = false)
    {
        return ExpandModules(targetModule)
            .Select(module => CreateReport(
                module,
                emergencyStop ? PHprHidReportState.EmergencyStop : PHprHidReportState.Stop,
                StopFrequencyByte,
                0d,
                reportId))
            .ToArray();
    }

    private static IReadOnlyList<PHprModuleId> ExpandModules(PHprModuleId targetModule)
    {
        return targetModule switch
        {
            PHprModuleId.Brake => [PHprModuleId.Brake],
            PHprModuleId.Throttle => [PHprModuleId.Throttle],
            PHprModuleId.Both => [PHprModuleId.Brake, PHprModuleId.Throttle],
            _ => []
        };
    }

    private static PHprHidReport CreateReport(
        PHprModuleId targetModule,
        PHprHidReportState state,
        double frequencyHz,
        double strength01,
        byte? reportId)
    {
        var payload = new byte[PayloadLengthBytes];
        payload[0] = Prefix0;
        payload[1] = Prefix1;
        payload[2] = targetModule == PHprModuleId.Brake ? BrakeModuleByte : ThrottleModuleByte;

        if (state == PHprHidReportState.Start)
        {
            payload[3] = StartStateByte;
            payload[4] = ToByte(frequencyHz);
            payload[5] = ToByte(strength01 * 100d);
        }
        else
        {
            payload[3] = StopStateByte;
            payload[4] = StopFrequencyByte;
            payload[5] = 0x00;
        }

        return new PHprHidReport(targetModule, state, payload, reportId, DateTimeOffset.UtcNow);
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), byte.MinValue, byte.MaxValue);
    }
}
