using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;
using HapticDrive.Simagic.PHPR.Abstractions.Output;

namespace HapticDrive.Simagic.PHPR.Tests;

public sealed class PHprMockProtocolTests
{
    private readonly SimHubF1EcMockEncoder _encoder = new();

    [Theory]
    [InlineData(0.10d, "F1 EC 01 01 32 0A")]
    [InlineData(0.20d, "F1 EC 01 01 32 14")]
    [InlineData(0.40d, "F1 EC 01 01 32 28")]
    public void SimHubF1EcMock_EncodesBrakeActiveExamples(double strength01, string expectedPrefix)
    {
        var result = _encoder.Encode(Command(PHprModuleId.Brake, PHprMockProtocolState.Start, 50d, strength01, 100));

        var frame = Assert.Single(result.Frames);
        Assert.True(result.Succeeded, result.Message);
        Assert.StartsWith(expectedPrefix, frame.PayloadHex, StringComparison.Ordinal);
        Assert.Equal(64, frame.Payload.Length);
        Assert.True(frame.MockOnly);
    }

    [Fact]
    public void SimHubF1EcMock_EncodesThrottleActiveExample()
    {
        var result = _encoder.Encode(Command(PHprModuleId.Throttle, PHprMockProtocolState.Start, 50d, 0.10d, 100));

        var frame = Assert.Single(result.Frames);
        Assert.StartsWith("F1 EC 02 01 32 0A", frame.PayloadHex, StringComparison.Ordinal);
    }

    [Fact]
    public void SimHubF1EcMock_EncodesExplicitStopExamples()
    {
        var brake = Assert.Single(_encoder.Encode(Command(PHprModuleId.Brake, PHprMockProtocolState.Stop, 10d, 0d, 0)).Frames);
        var throttle = Assert.Single(_encoder.Encode(Command(PHprModuleId.Throttle, PHprMockProtocolState.Stop, 10d, 0d, 0)).Frames);

        Assert.StartsWith("F1 EC 01 00 0A 00", brake.PayloadHex, StringComparison.Ordinal);
        Assert.StartsWith("F1 EC 02 00 0A 00", throttle.PayloadHex, StringComparison.Ordinal);
    }

    [Fact]
    public void BothTarget_ExpandsToBrakeAndThrottleWithoutModuleZero()
    {
        var result = _encoder.Encode(Command(PHprModuleId.Both, PHprMockProtocolState.Start, 50d, 0.10d, 100));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, result.Frames.Count);
        Assert.Contains(result.Frames, frame => frame.TargetModule == PHprModuleId.Brake && frame.Payload[2] == 0x01);
        Assert.Contains(result.Frames, frame => frame.TargetModule == PHprModuleId.Throttle && frame.Payload[2] == 0x02);
        Assert.DoesNotContain(result.Frames, frame => frame.Payload[2] == 0x00);
    }

    [Fact]
    public void EmergencyStop_ProducesImmediateStopFramesForBrakeAndThrottle()
    {
        var result = _encoder.Encode(Command(PHprModuleId.Both, PHprMockProtocolState.EmergencyStop, 10d, 0d, 0));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, result.Frames.Count);
        Assert.All(result.Frames, frame =>
        {
            Assert.Equal(PHprMockProtocolState.EmergencyStop, frame.State);
            Assert.Equal(TimeSpan.Zero, frame.ScheduledOffset);
            Assert.Equal(0x00, frame.Payload[3]);
            Assert.Equal(0x0A, frame.Payload[4]);
            Assert.Equal(0x00, frame.Payload[5]);
        });
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    public void DurationScheduler_CreatesStartAtZeroAndStopAtDuration(int durationMs)
    {
        var result = new PHprMockDurationScheduler().Plan(Command(PHprModuleId.Brake, PHprMockProtocolState.Start, 50d, 0.10d, durationMs));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, result.Frames.Count);
        Assert.Equal(PHprMockProtocolState.Start, result.Frames[0].State);
        Assert.Equal(TimeSpan.Zero, result.Frames[0].ScheduledOffset);
        Assert.Equal(PHprMockProtocolState.Stop, result.Frames[1].State);
        Assert.Equal(TimeSpan.FromMilliseconds(durationMs), result.Frames[1].ScheduledOffset);
        Assert.StartsWith("F1 EC 01 01 32 0A", result.Frames[0].PayloadHex, StringComparison.Ordinal);
        Assert.StartsWith("F1 EC 01 00 0A 00", result.Frames[1].PayloadHex, StringComparison.Ordinal);
    }

    [Fact]
    public void DurationScheduler_UsesStopOnlyForZeroDuration()
    {
        var result = new PHprMockDurationScheduler().Plan(Command(PHprModuleId.Brake, PHprMockProtocolState.Start, 50d, 0.10d, 0));

        var frame = Assert.Single(result.Frames);
        Assert.Equal(PHprMockProtocolState.Stop, frame.State);
        Assert.StartsWith("F1 EC 01 00 0A 00", frame.PayloadHex, StringComparison.Ordinal);
    }

    [Fact]
    public void MockCommand_ClampsStrengthPercentAndFrequencyForMockConsistency()
    {
        var command = Command((PHprModuleId)999, PHprMockProtocolState.Start, double.PositiveInfinity, -1d, -50);
        var high = Command(PHprModuleId.Brake, PHprMockProtocolState.Start, 999d, 8d, 90_000);

        Assert.Equal(0d, command.Strength01, precision: 6);
        Assert.Equal(0, command.StrengthPercent);
        Assert.Equal(PHprMockProtocolCommand.MinMockFrequencyHz, command.FrequencyHz, precision: 6);
        Assert.Equal(0, command.DurationMs);
        Assert.Equal(1d, high.Strength01, precision: 6);
        Assert.Equal(100, high.StrengthPercent);
        Assert.Equal(PHprMockProtocolCommand.MaxMockFrequencyHz, high.FrequencyHz, precision: 6);
        Assert.Equal(PHprMockProtocolCommand.MaxMockDurationMs, high.DurationMs);
    }

    [Fact]
    public void InvalidModule_FailsSafely()
    {
        var result = _encoder.Encode(Command((PHprModuleId)999, PHprMockProtocolState.Start, 50d, 0.10d, 100));

        Assert.False(result.Succeeded);
        Assert.Equal(PHprMockProtocolSupportStatus.InvalidPayload, result.Status);
    }

    [Fact]
    public void InvalidPayload_FailsSafely()
    {
        var result = new SimHubF1EcMockDecoder().Decode([0xF1, 0xEC, 0x01]);

        Assert.False(result.Succeeded);
        Assert.Equal(PHprMockProtocolSupportStatus.InvalidPayload, result.Status);
    }

    [Fact]
    public void SimHubDecoder_RoundTripsSupportedMockFrame()
    {
        var encoded = Assert.Single(_encoder.Encode(Command(PHprModuleId.Brake, PHprMockProtocolState.Start, 50d, 0.20d, 100)).Frames);

        var decoded = new SimHubF1EcMockDecoder().Decode(encoded.Payload);

        Assert.True(decoded.Succeeded, decoded.Message);
        Assert.NotNull(decoded.Command);
        Assert.Equal(PHprModuleId.Brake, decoded.Command.TargetModule);
        Assert.Equal(PHprMockProtocolState.Start, decoded.Command.State);
        Assert.Equal(50d, decoded.Command.FrequencyHz, precision: 6);
        Assert.Equal(0.20d, decoded.Command.Strength01, precision: 6);
    }

    [Fact]
    public void SimProUnknownMock_ClassifiesFamilyButRefusesDetailedEncoding()
    {
        var simProPayload = Enumerable.Range(0, 64).Select(_ => (byte)0).ToArray();
        simProPayload[0] = 0x80;
        simProPayload[1] = 0x1E;
        simProPayload[2] = 0x89;

        var frame = SimProUnknownMockFrame.Classify(simProPayload);
        var notSimPro = SimProUnknownMockFrame.Classify(Assert.Single(_encoder.Encode(Command(PHprModuleId.Brake, PHprMockProtocolState.Start, 50d, 0.10d, 100)).Frames).Payload);
        var encoding = new SimProUnknownMockEncoder().Encode(Command(PHprModuleId.Brake, PHprMockProtocolState.Start, 50d, 0.10d, 100) with
        {
            SourceProtocolFamily = PHprMockProtocolFamily.SimProUnknownMock
        });

        Assert.Equal(PHprMockProtocolSupportStatus.NeedsMoreCaptures, frame.Status);
        Assert.Contains("SimProUnknownMock", frame.Message, StringComparison.Ordinal);
        Assert.Equal(PHprMockProtocolSupportStatus.InvalidPayload, notSimPro.Status);
        Assert.False(encoding.Succeeded);
        Assert.Equal(PHprMockProtocolSupportStatus.NeedsMoreCaptures, encoding.Status);
    }

    [Fact]
    public async Task MockPhprOutputDevice_RecordsCommandsAndGeneratedMockFrames()
    {
        await using var output = new MockPhprOutputDevice();

        var result = await output.SendAsync(PHprCommand.Create(
            PHprModuleId.Brake,
            strength01: 0.05d,
            frequencyHz: 50d,
            durationMs: 60,
            PHprCommandSource.TestBench));
        var snapshot = output.GetSnapshot();

        Assert.True(result.Succeeded, result.Message);
        Assert.Single(output.CommandHistory);
        Assert.Equal(2, output.FrameHistory.Count);
        Assert.Equal(2, snapshot.GeneratedFrameCount);
        Assert.Equal(1, snapshot.PendingScheduledStopCount);
        Assert.NotNull(snapshot.LastFrame);
        Assert.True(snapshot.BrakeAvailable);
        Assert.True(snapshot.ThrottleAvailable);
        Assert.Equal("MockOnly", snapshot.Mode);
    }

    [Fact]
    public async Task MockPhprOutputDevice_EmergencyStopClearsPendingStopsAndRecordsStopFrames()
    {
        await using var output = new MockPhprOutputDevice();
        await output.SendAsync(PHprCommand.Create(
            PHprModuleId.Brake,
            strength01: 0.05d,
            frequencyHz: 50d,
            durationMs: 60,
            PHprCommandSource.TestBench));

        await output.EmergencyStopAsync();
        var snapshot = output.GetSnapshot();

        Assert.True(snapshot.IsEmergencyStopActive);
        Assert.Equal(1, snapshot.EmergencyStopCount);
        Assert.Equal(0, snapshot.PendingScheduledStopCount);
        Assert.Equal(3, snapshot.GeneratedFrameCount);
        Assert.Contains(output.FrameHistory, frame => frame.State == PHprMockProtocolState.EmergencyStop && frame.TargetModule == PHprModuleId.Brake);
        Assert.Contains(output.FrameHistory, frame => frame.State == PHprMockProtocolState.EmergencyStop && frame.TargetModule == PHprModuleId.Throttle);
    }

    [Fact]
    public void MockProtocolInterfaces_DoNotExposeWriteCapableApiNames()
    {
        var forbiddenTerms = new[] { "Write", "HidD", "SetFeature", "DeviceHandle", "OpenDevice", "UsbReport" };
        var methodNames = typeof(PHprMockProtocolCommand).Assembly.GetTypes()
            .Where(type => type.Namespace?.Contains(".MockProtocol", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetMethods())
            .Where(method => method.DeclaringType != typeof(object))
            .Select(method => method.Name)
            .Distinct()
            .ToArray();

        foreach (var methodName in methodNames)
        {
            Assert.DoesNotContain(forbiddenTerms, term => methodName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static PHprMockProtocolCommand Command(
        PHprModuleId targetModule,
        PHprMockProtocolState state,
        double frequencyHz,
        double strength01,
        int durationMs)
    {
        return PHprMockProtocolCommand.Create(
            targetModule,
            state,
            frequencyHz,
            strength01,
            durationMs,
            PHprMockProtocolFamily.SimHubF1EcMock,
            PHprCommandSource.TestBench);
    }
}
