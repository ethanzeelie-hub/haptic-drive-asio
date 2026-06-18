using System.Buffers.Binary;
using HapticDrive.Actuation.Driving;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Telemetry.F1_25;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class HapticPipelinePhprReplayValidationTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 8, 12, 0, 0, TimeSpan.Zero);
    private const int HeaderOffset = F125PacketDefinitions.HeaderSize;

    [Fact]
    public async Task ReplayTelemetryUpdatesDrivingArmedAndRoutesRoadEffect()
    {
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: HapticPipelineOptions.ManualRendering);
        var recording = CreateRecording(
            CreateSessionDatagram(frame: 1),
            CreateLapDatagram(frame: 2),
            CreateCarStatusDatagram(frame: 3),
            CreateCarTelemetryDatagram(frame: 4, speedKph: 120, throttle: 0.4f, brake: 0f, surfaceType: 1));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        var replayResult = await coordinator.ReplayAsync(recording, TelemetryReplayOptions.Fast);
        var renderResult = await coordinator.RenderNextBufferAsync();
        var snapshot = coordinator.GetSnapshot();
        var driving = new DrivingArmedStateService(new DrivingArmedStateServiceOptions
        {
            TelemetryFreshnessThreshold = TimeSpan.FromSeconds(5)
        });
        var drivingState = driving.UpdateFromPipelineSnapshot(snapshot, DateTimeOffset.UtcNow);

        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(output, PHprPedalEffectsRouterOptions.Default);
        var routeResult = await router.RouteAsync(snapshot, BuildMockContext(snapshot, drivingState.IsArmed), BaseTime);

        Assert.True(replayResult.Succeeded, replayResult.Message);
        Assert.True(renderResult.Succeeded, renderResult.Message);
        Assert.Equal(HapticPipelineInputSource.Replay, snapshot.InputSource);
        Assert.Equal(4, snapshot.Replay.PacketsReplayed);
        Assert.True(drivingState.IsArmed, drivingState.Reason);
        Assert.True(routeResult.WasRouted, routeResult.Message);
        var command = Assert.Single(inner.CommandHistory);
        Assert.Equal(PHprModuleId.Both, command.TargetModule);
        Assert.Equal(PHprCommandSource.RoadTexture, command.Source);
        Assert.DoesNotContain(inner.CommandHistory, command => command.Source == PHprCommandSource.PaddleShiftIntent);
    }

    [Fact]
    public async Task ReplayTelemetryRoutesSlipAndLockWithoutSyntheticGearEvents()
    {
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: HapticPipelineOptions.ManualRendering);
        var recording = CreateRecording(
            CreateSessionDatagram(frame: 1),
            CreateLapDatagram(frame: 2),
            CreateCarStatusDatagram(frame: 3),
            CreateCarTelemetryDatagram(frame: 4, speedKph: 120, throttle: 0.8f, brake: 0.8f, surfaceType: 0),
            CreateMotionExDatagram(frame: 5, wheelSpeed: 1f, wheelSlipRatio: 0.42f, wheelSlipAngle: 0.12f));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        var replayResult = await coordinator.ReplayAsync(recording, TelemetryReplayOptions.Fast);
        var snapshot = coordinator.GetSnapshot();

        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(output, PHprPedalEffectsRouterOptions.Default);
        var routeResult = await router.RouteAsync(snapshot, BuildMockContext(snapshot, drivingArmed: true), BaseTime);

        Assert.True(replayResult.Succeeded, replayResult.Message);
        Assert.Equal(HapticPipelineInputSource.Replay, snapshot.InputSource);
        Assert.Equal(5, snapshot.Replay.PacketsReplayed);
        Assert.True(routeResult.WasRouted, routeResult.Message);
        Assert.Contains(inner.CommandHistory, command => command.Source == PHprCommandSource.WheelSlip && command.TargetModule == PHprModuleId.Throttle);
        Assert.Contains(inner.CommandHistory, command => command.Source == PHprCommandSource.WheelLock && command.TargetModule == PHprModuleId.Brake);
        Assert.DoesNotContain(inner.CommandHistory, command => command.Source == PHprCommandSource.PaddleShiftIntent);
    }

    [Fact]
    public async Task ReplayPedalEffectsRespectProfileSettings()
    {
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: HapticPipelineOptions.ManualRendering);
        var recording = CreateRecording(
            CreateSessionDatagram(frame: 1),
            CreateLapDatagram(frame: 2),
            CreateCarStatusDatagram(frame: 3),
            CreateCarTelemetryDatagram(frame: 4, speedKph: 130, throttle: 0.5f, brake: 0f, surfaceType: 1));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.True((await coordinator.ReplayAsync(recording, TelemetryReplayOptions.Fast)).Succeeded);
        var snapshot = coordinator.GetSnapshot();

        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(
            output,
            PHprPedalEffectsRouterOptions.Default with
            {
                RoadVibration = PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.RoadVibration) with
                {
                    TargetModule = PHprGearPulseTarget.Brake
                },
                WheelSlip = PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.WheelSlip) with { IsEnabled = false },
                WheelLock = PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.WheelLock) with { IsEnabled = false }
            });

        var result = await router.RouteAsync(snapshot, BuildMockContext(snapshot, drivingArmed: true), BaseTime);

        Assert.True(result.WasRouted, result.Message);
        var command = Assert.Single(inner.CommandHistory);
        Assert.Equal(PHprModuleId.Brake, command.TargetModule);
        Assert.Equal(PHprCommandSource.RoadTexture, command.Source);
    }

    [Fact]
    public async Task ReplayPedalEffectsRejectStaleTelemetryAndEmergencyMute()
    {
        await using var coordinator = RuntimeTestPipelineFactory.Create(options: HapticPipelineOptions.ManualRendering);
        var recording = CreateRecording(
            CreateSessionDatagram(frame: 1),
            CreateLapDatagram(frame: 2),
            CreateCarStatusDatagram(frame: 3),
            CreateCarTelemetryDatagram(frame: 4, speedKph: 120, throttle: 0.4f, brake: 0f, surfaceType: 1));

        Assert.True((await coordinator.StartAsync()).Succeeded);
        Assert.True((await coordinator.ReplayAsync(recording, TelemetryReplayOptions.Fast)).Succeeded);
        var snapshot = coordinator.GetSnapshot();

        await using var staleInner = new MockPhprOutputDevice();
        await using var staleOutput = new SafetyLimitedPhprOutputDevice(staleInner);
        var staleRouter = new PHprPedalEffectsRouter(staleOutput, PHprPedalEffectsRouterOptions.Default);
        var staleResult = await staleRouter.RouteAsync(snapshot with { TelemetryTimedOutMuted = true }, nowUtc: BaseTime);

        await using var mutedInner = new MockPhprOutputDevice();
        await using var mutedOutput = new SafetyLimitedPhprOutputDevice(mutedInner);
        var mutedRouter = new PHprPedalEffectsRouter(mutedOutput, PHprPedalEffectsRouterOptions.Default);
        var mutedResult = await mutedRouter.RouteAsync(snapshot with { EmergencyMute = true }, nowUtc: BaseTime);

        Assert.Equal(PHprPedalEffectsRoutingStatus.RejectedBySafety, staleResult.Status);
        Assert.Equal(PHprSafetyViolationCode.TelemetryStale, staleOutput.SafetySnapshot.LastViolation?.Code);
        Assert.Empty(staleInner.CommandHistory);
        Assert.Equal(PHprPedalEffectsRoutingStatus.RejectedBySafety, mutedResult.Status);
        Assert.Equal(PHprSafetyViolationCode.EmergencyMuteActive, mutedOutput.SafetySnapshot.LastViolation?.Code);
        Assert.Empty(mutedInner.CommandHistory);
    }

    private static PHprSafetyContext BuildMockContext(HapticPipelineSnapshot snapshot, bool drivingArmed)
    {
        return PHprSafetyContext.DefaultMock with
        {
            TelemetryStale = snapshot.TelemetryTimedOutMuted,
            HapticsStopped = !snapshot.IsRunning,
            EmergencyMuteActive = snapshot.EmergencyMute,
            DrivingArmed = drivingArmed
        };
    }

    private static TelemetryRecording CreateRecording(params byte[][] datagrams)
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        return new TelemetryRecording(
            TelemetryRecordingMetadata.CreateDefault(createdAtUtc),
            datagrams
                .Select((payload, index) => new TelemetryRecordedPacket(
                    index + 1,
                    TimeSpan.FromMilliseconds(index * 10),
                    payload))
                .ToArray());
    }

    private static byte[] CreateSessionDatagram(uint frame, byte gamePaused = 0)
    {
        var datagram = CreateDatagram(F125PacketKind.Session, frame);
        datagram[HeaderOffset + 3] = 58;
        WriteUInt16(datagram, 4, 5_412);
        datagram[HeaderOffset + 14] = gamePaused;
        datagram[HeaderOffset + 127] = 1;
        return datagram;
    }

    private static byte[] CreateLapDatagram(uint frame, byte driverStatus = 4, byte resultStatus = 2)
    {
        var datagram = CreateDatagram(F125PacketKind.LapData, frame);
        datagram[HeaderOffset + 44] = driverStatus;
        datagram[HeaderOffset + 45] = resultStatus;
        return datagram;
    }

    private static byte[] CreateCarStatusDatagram(uint frame, byte networkPaused = 0)
    {
        var datagram = CreateDatagram(F125PacketKind.CarStatus, frame);
        WriteUInt16(datagram, 17, 12_750);
        datagram[HeaderOffset + 54] = networkPaused;
        return datagram;
    }

    private static byte[] CreateCarTelemetryDatagram(
        uint frame,
        ushort speedKph,
        float throttle,
        float brake,
        byte surfaceType)
    {
        var datagram = CreateDatagram(F125PacketKind.CarTelemetry, frame);
        WriteUInt16(datagram, 0, speedKph);
        WriteSingle(datagram, 2, throttle);
        WriteSingle(datagram, 10, brake);
        datagram[HeaderOffset + 15] = 4;
        WriteUInt16(datagram, 16, 9_500);
        datagram[HeaderOffset + 56] = surfaceType;
        datagram[HeaderOffset + 57] = surfaceType;
        datagram[HeaderOffset + 58] = surfaceType;
        datagram[HeaderOffset + 59] = surfaceType;
        return datagram;
    }

    private static byte[] CreateMotionExDatagram(
        uint frame,
        float wheelSpeed,
        float wheelSlipRatio,
        float wheelSlipAngle)
    {
        var datagram = CreateDatagram(F125PacketKind.MotionEx, frame);
        WriteWheelSingles(datagram, 48, wheelSpeed, wheelSpeed, wheelSpeed, wheelSpeed);
        WriteWheelSingles(datagram, 64, wheelSlipRatio, wheelSlipRatio, wheelSlipRatio, wheelSlipRatio);
        WriteWheelSingles(datagram, 80, wheelSlipAngle, wheelSlipAngle, wheelSlipAngle, wheelSlipAngle);
        return datagram;
    }

    private static byte[] CreateDatagram(F125PacketKind kind, uint frame)
    {
        var definition = F125PacketDefinitions.All.Single(item => item.Kind == kind);
        var datagram = new byte[definition.Size];
        WriteHeader(datagram, definition.Id, frame);
        return datagram;
    }

    private static void WriteHeader(byte[] datagram, byte packetId, uint frame)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(datagram.AsSpan(0, 2), F125PacketDefinitions.PacketFormat);
        datagram[2] = F125PacketDefinitions.GameYear;
        datagram[3] = 1;
        datagram[4] = 0;
        datagram[5] = F125PacketDefinitions.PacketVersion;
        datagram[6] = packetId;
        BinaryPrimitives.WriteUInt64LittleEndian(datagram.AsSpan(7, 8), 123456789);
        BinaryPrimitives.WriteInt32LittleEndian(datagram.AsSpan(15, 4), BitConverter.SingleToInt32Bits(frame / 60f));
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(19, 4), frame);
        BinaryPrimitives.WriteUInt32LittleEndian(datagram.AsSpan(23, 4), frame);
        datagram[27] = 0;
        datagram[28] = 255;
    }

    private static void WriteUInt16(byte[] datagram, int bodyOffset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(datagram.AsSpan(HeaderOffset + bodyOffset, sizeof(ushort)), value);
    }

    private static void WriteSingle(byte[] datagram, int bodyOffset, float value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(
            datagram.AsSpan(HeaderOffset + bodyOffset, sizeof(float)),
            BitConverter.SingleToInt32Bits(value));
    }

    private static void WriteWheelSingles(
        byte[] datagram,
        int bodyOffset,
        float rearLeft,
        float rearRight,
        float frontLeft,
        float frontRight)
    {
        WriteSingle(datagram, bodyOffset, rearLeft);
        WriteSingle(datagram, bodyOffset + 4, rearRight);
        WriteSingle(datagram, bodyOffset + 8, frontLeft);
        WriteSingle(datagram, bodyOffset + 12, frontRight);
    }
}
