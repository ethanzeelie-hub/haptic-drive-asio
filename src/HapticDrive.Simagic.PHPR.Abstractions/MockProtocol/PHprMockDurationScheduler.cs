using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;

/// <summary>
/// Deterministic mock-only duration scheduler. It models Stage 2J SimHub duration evidence as
/// start frames at offset zero plus stop frames at the requested offset; it never sleeps, loops,
/// opens devices, or sends frames to hardware.
/// </summary>
public sealed class PHprMockDurationScheduler
{
    private readonly SimHubF1EcMockEncoder _encoder = new();

    public PHprMockProtocolEncodingResult Plan(PHprMockProtocolCommand command)
    {
        if (command.SourceProtocolFamily == PHprMockProtocolFamily.SimProUnknownMock)
        {
            return PHprMockProtocolEncodingResult.Failure(
                PHprMockProtocolSupportStatus.NeedsMoreCaptures,
                "SimProUnknownMock is observation-only and unsupported for detailed mock duration scheduling.");
        }

        if (command.State == PHprMockProtocolState.EmergencyStop)
        {
            return _encoder.Encode(command, TimeSpan.Zero);
        }

        if (command.State == PHprMockProtocolState.Stop)
        {
            return _encoder.Encode(command, TimeSpan.Zero);
        }

        if (command.State != PHprMockProtocolState.Start)
        {
            return PHprMockProtocolEncodingResult.Failure(
                PHprMockProtocolSupportStatus.InvalidPayload,
                "Only Start, Stop, and EmergencyStop mock states can be scheduled.");
        }

        if (command.DurationMs == 0)
        {
            var immediateStop = command with
            {
                State = PHprMockProtocolState.Stop,
                Strength01 = 0d,
                SafetyFlags = command.SafetyFlags | PHprSafetyFlags.MockOnly
            };
            return _encoder.Encode(immediateStop, TimeSpan.Zero);
        }

        var start = _encoder.Encode(command, TimeSpan.Zero);
        if (!start.Succeeded)
        {
            return start;
        }

        var stopCommand = command with
        {
            State = PHprMockProtocolState.Stop,
            Strength01 = 0d,
            DurationMs = 0,
            SafetyFlags = command.SafetyFlags | PHprSafetyFlags.MockOnly
        };
        var stop = _encoder.Encode(stopCommand, TimeSpan.FromMilliseconds(command.DurationMs));
        if (!stop.Succeeded)
        {
            return stop;
        }

        var frames = start.Frames
            .Concat(stop.Frames)
            .OrderBy(frame => frame.ScheduledOffset)
            .ThenBy(frame => frame.TargetModule)
            .ToArray();

        return PHprMockProtocolEncodingResult.Success(frames, "Mock duration planned as start plus scheduled stop; no hardware write was performed.");
    }
}

/// <summary>
/// Observation-only Stage 2K representation for SimPro Manager payloads beginning with 80 1E 89.
/// It deliberately does not infer module, strength, frequency, duration, checksum, or write semantics.
/// </summary>
public sealed record SimProUnknownMockFrame(
    int PayloadLength,
    string PayloadPreviewHex,
    PHprMockProtocolSupportStatus Status,
    bool MockOnly,
    string Message)
{
    public static SimProUnknownMockFrame Classify(IReadOnlyList<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Count < 3 || payload[0] != 0x80 || payload[1] != 0x1E || payload[2] != 0x89)
        {
            return new SimProUnknownMockFrame(
                payload.Count,
                PHprMockProtocolFrame.ConvertToHex(payload.Take(Math.Min(payload.Count, 8)).ToArray()),
                PHprMockProtocolSupportStatus.InvalidPayload,
                MockOnly: true,
                "Payload is not classified as the Stage 2K SimPro 80 1E 89 unknown mock family.");
        }

        return new SimProUnknownMockFrame(
            payload.Count,
            PHprMockProtocolFrame.ConvertToHex(payload.Take(Math.Min(payload.Count, 8)).ToArray()),
            PHprMockProtocolSupportStatus.NeedsMoreCaptures,
            MockOnly: true,
            "Payload is classified as SimProUnknownMock and remains unsupported for detailed mock encoding.");
    }
}

/// <summary>
/// Conservative SimPro mock encoder placeholder. It always refuses detailed encoding because
/// Stage 2J left the SimPro 80 1E 89 family as NeedsMoreCaptures.
/// </summary>
public sealed class SimProUnknownMockEncoder
{
    public PHprMockProtocolEncodingResult Encode(PHprMockProtocolCommand command)
    {
        _ = command;
        return PHprMockProtocolEncodingResult.Failure(
            PHprMockProtocolSupportStatus.NeedsMoreCaptures,
            "SimProUnknownMock remains NeedsMoreCaptures and unsupported for detailed mock encoding.");
    }
}
