using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;

public enum PHprMockProtocolFamily
{
    SimHubF1EcMock = 0,
    SimProUnknownMock = 1,
    GenericMock = 2
}

public enum PHprMockProtocolState
{
    Start = 0,
    Stop = 1,
    EmergencyStop = 2,
    Unknown = 3
}

public enum PHprMockProtocolSupportStatus
{
    SupportedMockEncoding = 0,
    UnsupportedForMockEncoding = 1,
    NeedsMoreCaptures = 2,
    InvalidPayload = 3
}

/// <summary>
/// Mock-only P-HPR command intent derived from Stage 2J hypotheses. It is not for USB writes,
/// not safe for hardware output, and must not be used as a real device-control packet.
/// </summary>
public sealed record PHprMockProtocolCommand
{
    public const double MinMockFrequencyHz = 1d;
    public const double MaxMockFrequencyHz = 250d;
    public const int MaxMockDurationMs = 60_000;

    public required PHprModuleId TargetModule { get; init; }

    public required PHprMockProtocolState State { get; init; }

    public required double FrequencyHz { get; init; }

    public required double Strength01 { get; init; }

    public required int DurationMs { get; init; }

    public required PHprMockProtocolFamily SourceProtocolFamily { get; init; }

    public required PHprCommandSource Source { get; init; }

    public required string EvidenceConfidence { get; init; }

    public required bool MockOnly { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required Guid CorrelationId { get; init; }

    public PHprSafetyFlags SafetyFlags { get; init; }

    public int StrengthPercent => (int)Math.Clamp(Math.Round(Strength01 * 100d, MidpointRounding.AwayFromZero), 0d, 100d);

    public static PHprMockProtocolCommand Create(
        PHprModuleId targetModule,
        PHprMockProtocolState state,
        double frequencyHz,
        double strength01,
        int durationMs,
        PHprMockProtocolFamily sourceProtocolFamily,
        PHprCommandSource source,
        string evidenceConfidence = "Stage 2J ReadyForMockProtocol",
        DateTimeOffset? createdAtUtc = null,
        Guid? correlationId = null,
        PHprSafetyFlags safetyFlags = PHprSafetyFlags.None)
    {
        return new PHprMockProtocolCommand
        {
            TargetModule = targetModule,
            State = state,
            FrequencyHz = ClampFinite(frequencyHz, MinMockFrequencyHz, MaxMockFrequencyHz, MinMockFrequencyHz),
            Strength01 = ClampFinite(strength01, 0d, 1d, 0d),
            DurationMs = Math.Clamp(durationMs, 0, MaxMockDurationMs),
            SourceProtocolFamily = sourceProtocolFamily,
            Source = source,
            EvidenceConfidence = evidenceConfidence,
            MockOnly = true,
            CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow,
            CorrelationId = correlationId ?? Guid.NewGuid(),
            SafetyFlags = safetyFlags | PHprSafetyFlags.MockOnly
        };
    }

    public static PHprMockProtocolCommand FromPHprCommand(
        PHprCommand command,
        PHprMockProtocolFamily sourceProtocolFamily = PHprMockProtocolFamily.SimHubF1EcMock,
        string evidenceConfidence = "Stage 2J ReadyForMockProtocol")
    {
        var state = command.Source == PHprCommandSource.EmergencyStop
            || command.SafetyFlags.HasFlag(PHprSafetyFlags.EmergencyStop)
                ? PHprMockProtocolState.EmergencyStop
                : command.DurationMs == 0 && command.Strength01 <= 0d
                    ? PHprMockProtocolState.Stop
                    : PHprMockProtocolState.Start;

        return Create(
            command.TargetModule,
            state,
            command.FrequencyHz,
            command.Strength01,
            command.DurationMs,
            sourceProtocolFamily,
            command.Source,
            evidenceConfidence,
            command.TimestampUtc,
            safetyFlags: command.SafetyFlags);
    }

    public PHprCommand ToPHprCommand()
    {
        var outputStrength = State == PHprMockProtocolState.Start ? Strength01 : 0d;
        var outputDuration = State == PHprMockProtocolState.Start ? DurationMs : 0;
        var source = State == PHprMockProtocolState.EmergencyStop ? PHprCommandSource.EmergencyStop : Source;
        var flags = SafetyFlags | PHprSafetyFlags.MockOnly;
        if (State == PHprMockProtocolState.EmergencyStop)
        {
            flags |= PHprSafetyFlags.EmergencyStop;
        }

        return PHprCommand.Create(
            TargetModule,
            outputStrength,
            FrequencyHz,
            outputDuration,
            source,
            timestampUtc: CreatedAtUtc,
            safetyFlags: flags);
    }

    private static double ClampFinite(double value, double min, double max, double fallback)
    {
        if (!double.IsFinite(value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }
}

/// <summary>
/// Mock-only protocol frame produced from Stage 2J hypotheses. The payload is for tests and
/// diagnostics only; nothing in this frame may be sent to real P-HPR hardware.
/// </summary>
public sealed record PHprMockProtocolFrame(
    PHprMockProtocolFamily Family,
    PHprModuleId TargetModule,
    PHprMockProtocolState State,
    byte[] Payload,
    TimeSpan ScheduledOffset,
    bool MockOnly,
    string EvidenceConfidence,
    Guid CorrelationId)
{
    public string PayloadHex => ConvertToHex(Payload);

    public PHprMockProtocolFrame CloneWithOffset(TimeSpan offset)
    {
        return this with
        {
            Payload = Payload.ToArray(),
            ScheduledOffset = offset
        };
    }

    public static string ConvertToHex(IReadOnlyList<byte> payload)
    {
        if (payload.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(" ", payload.Select(value => value.ToString("X2")));
    }
}

public sealed record PHprMockProtocolEncodingResult(
    bool Succeeded,
    PHprMockProtocolSupportStatus Status,
    string Message,
    IReadOnlyList<PHprMockProtocolFrame> Frames)
{
    public static PHprMockProtocolEncodingResult Success(IReadOnlyList<PHprMockProtocolFrame> frames, string message)
    {
        return new PHprMockProtocolEncodingResult(true, PHprMockProtocolSupportStatus.SupportedMockEncoding, message, frames);
    }

    public static PHprMockProtocolEncodingResult Failure(PHprMockProtocolSupportStatus status, string message)
    {
        return new PHprMockProtocolEncodingResult(false, status, message, []);
    }
}

public sealed record PHprMockProtocolDecodeResult(
    bool Succeeded,
    PHprMockProtocolSupportStatus Status,
    string Message,
    PHprMockProtocolCommand? Command,
    PHprMockProtocolFrame? Frame)
{
    public static PHprMockProtocolDecodeResult Success(PHprMockProtocolCommand command, PHprMockProtocolFrame frame)
    {
        return new PHprMockProtocolDecodeResult(true, PHprMockProtocolSupportStatus.SupportedMockEncoding, "Mock SimHub F1 EC frame decoded.", command, frame);
    }

    public static PHprMockProtocolDecodeResult Failure(PHprMockProtocolSupportStatus status, string message)
    {
        return new PHprMockProtocolDecodeResult(false, status, message, null, null);
    }
}
