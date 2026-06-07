using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;

/// <summary>
/// Mock-only encoder for the Stage 2J SimHub F1 EC hypothesis. It creates test payloads only;
/// the encoded bytes are not a production protocol and must never be sent to hardware.
/// </summary>
public sealed class SimHubF1EcMockEncoder
{
    public const int PayloadLengthBytes = 64;
    public const byte Prefix0 = 0xF1;
    public const byte Prefix1 = 0xEC;
    public const byte BrakeModuleByte = 0x01;
    public const byte ThrottleModuleByte = 0x02;
    public const byte StartStateByte = 0x01;
    public const byte StopStateByte = 0x00;
    public const byte StopFrequencyByte = 0x0A;

    public PHprMockProtocolEncodingResult Encode(PHprMockProtocolCommand command)
    {
        return Encode(command, TimeSpan.Zero);
    }

    public PHprMockProtocolEncodingResult Encode(PHprMockProtocolCommand command, TimeSpan scheduledOffset)
    {
        if (command.SourceProtocolFamily != PHprMockProtocolFamily.SimHubF1EcMock
            && command.SourceProtocolFamily != PHprMockProtocolFamily.GenericMock)
        {
            return PHprMockProtocolEncodingResult.Failure(
                PHprMockProtocolSupportStatus.UnsupportedForMockEncoding,
                "Only SimHubF1EcMock commands are supported by the mock F1 EC encoder.");
        }

        if (!Enum.IsDefined(command.TargetModule))
        {
            return PHprMockProtocolEncodingResult.Failure(
                PHprMockProtocolSupportStatus.InvalidPayload,
                "Invalid mock P-HPR target module.");
        }

        if (command.State is PHprMockProtocolState.Unknown)
        {
            return PHprMockProtocolEncodingResult.Failure(
                PHprMockProtocolSupportStatus.InvalidPayload,
                "Unknown mock P-HPR command state cannot be encoded.");
        }

        var modules = ExpandModules(command);
        if (modules.Count == 0)
        {
            return PHprMockProtocolEncodingResult.Failure(
                PHprMockProtocolSupportStatus.InvalidPayload,
                "No mock P-HPR modules were selected.");
        }

        var frames = modules
            .Select(module => CreateFrame(command, module, scheduledOffset))
            .ToArray();

        return PHprMockProtocolEncodingResult.Success(frames, "Mock SimHub F1 EC frames encoded; no hardware write was performed.");
    }

    private static IReadOnlyList<PHprModuleId> ExpandModules(PHprMockProtocolCommand command)
    {
        if (command.State == PHprMockProtocolState.EmergencyStop)
        {
            return [PHprModuleId.Brake, PHprModuleId.Throttle];
        }

        return command.TargetModule switch
        {
            PHprModuleId.Brake => [PHprModuleId.Brake],
            PHprModuleId.Throttle => [PHprModuleId.Throttle],
            PHprModuleId.Both => [PHprModuleId.Brake, PHprModuleId.Throttle],
            _ => []
        };
    }

    private static PHprMockProtocolFrame CreateFrame(
        PHprMockProtocolCommand command,
        PHprModuleId targetModule,
        TimeSpan scheduledOffset)
    {
        var payload = new byte[PayloadLengthBytes];
        payload[0] = Prefix0;
        payload[1] = Prefix1;
        payload[2] = targetModule == PHprModuleId.Brake ? BrakeModuleByte : ThrottleModuleByte;

        if (command.State == PHprMockProtocolState.Start)
        {
            payload[3] = StartStateByte;
            payload[4] = ToByte(command.FrequencyHz);
            payload[5] = ToByte(command.StrengthPercent);
        }
        else
        {
            payload[3] = StopStateByte;
            payload[4] = StopFrequencyByte;
            payload[5] = 0x00;
        }

        return new PHprMockProtocolFrame(
            PHprMockProtocolFamily.SimHubF1EcMock,
            targetModule,
            command.State == PHprMockProtocolState.EmergencyStop ? PHprMockProtocolState.EmergencyStop : command.State,
            payload,
            scheduledOffset,
            MockOnly: true,
            command.EvidenceConfidence,
            command.CorrelationId);
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), byte.MinValue, byte.MaxValue);
    }
}

/// <summary>
/// Mock-only decoder for supported Stage 2K SimHub F1 EC fixtures. It validates test frames
/// and does not create a production hardware decoder or live device command path.
/// </summary>
public sealed class SimHubF1EcMockDecoder
{
    public PHprMockProtocolDecodeResult Decode(IReadOnlyList<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Count != SimHubF1EcMockEncoder.PayloadLengthBytes)
        {
            return PHprMockProtocolDecodeResult.Failure(
                PHprMockProtocolSupportStatus.InvalidPayload,
                $"Mock SimHub F1 EC payloads must be {SimHubF1EcMockEncoder.PayloadLengthBytes} bytes.");
        }

        if (payload[0] != SimHubF1EcMockEncoder.Prefix0 || payload[1] != SimHubF1EcMockEncoder.Prefix1)
        {
            return PHprMockProtocolDecodeResult.Failure(
                PHprMockProtocolSupportStatus.InvalidPayload,
                "Payload does not start with the mock SimHub F1 EC prefix.");
        }

        var targetModule = payload[2] switch
        {
            SimHubF1EcMockEncoder.BrakeModuleByte => PHprModuleId.Brake,
            SimHubF1EcMockEncoder.ThrottleModuleByte => PHprModuleId.Throttle,
            _ => (PHprModuleId?)null
        };

        if (targetModule is null)
        {
            return PHprMockProtocolDecodeResult.Failure(
                PHprMockProtocolSupportStatus.InvalidPayload,
                "Mock SimHub F1 EC module byte must be 01 brake or 02 throttle.");
        }

        var state = payload[3] switch
        {
            SimHubF1EcMockEncoder.StartStateByte => PHprMockProtocolState.Start,
            SimHubF1EcMockEncoder.StopStateByte => PHprMockProtocolState.Stop,
            _ => PHprMockProtocolState.Unknown
        };

        if (state == PHprMockProtocolState.Unknown)
        {
            return PHprMockProtocolDecodeResult.Failure(
                PHprMockProtocolSupportStatus.InvalidPayload,
                "Mock SimHub F1 EC state byte must be 01 start or 00 stop.");
        }

        var command = PHprMockProtocolCommand.Create(
            targetModule.Value,
            state,
            payload[4],
            state == PHprMockProtocolState.Start ? payload[5] / 100d : 0d,
            durationMs: 0,
            PHprMockProtocolFamily.SimHubF1EcMock,
            PHprCommandSource.TestBench,
            evidenceConfidence: "Decoded Stage 2K mock fixture");

        var frame = new PHprMockProtocolFrame(
            PHprMockProtocolFamily.SimHubF1EcMock,
            targetModule.Value,
            state,
            payload.ToArray(),
            TimeSpan.Zero,
            MockOnly: true,
            command.EvidenceConfidence,
            command.CorrelationId);

        return PHprMockProtocolDecodeResult.Success(command, frame);
    }
}
