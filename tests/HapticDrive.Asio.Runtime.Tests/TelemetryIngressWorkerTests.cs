using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Runtime.Telemetry;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class TelemetryIngressWorkerTests
{
    [Fact]
    public async Task TelemetryIngressWorker_RestartsWithFreshChannelsAfterStopAsync()
    {
        var processedSequences = new ConcurrentQueue<long>();
        await using var worker = new TelemetryIngressWorker(
            packet =>
            {
                processedSequences.Enqueue(packet.SequenceNumber);
                return CreatePacketResult();
            },
            isRecordingEnabled: () => false,
            enqueueForRecording: _ => TelemetryRecordingOperationResult.NotRecording("Not recording."),
            waitForRecordingDrainAsync: static (_, _) => ValueTask.FromResult(TelemetryRecordingDrainResult.Complete()),
            markRecordingIncomplete: _ => { },
            isForwardingEnabled: () => false,
            forwardPacketAsync: static (_, _) => ValueTask.CompletedTask);

        await worker.StartAsync();
        worker.ProcessTelemetryPacket(CreatePacket(1, [0x01]));
        await SpinWaitAsync(() => processedSequences.Contains(1));
        await worker.StopAsync();

        await worker.StartAsync();
        worker.ProcessTelemetryPacket(CreatePacket(2, [0x02]));
        await SpinWaitAsync(() => processedSequences.Contains(2));

        var snapshot = worker.GetSnapshot();
        Assert.True(snapshot.IsRunning);
        Assert.Equal(1, snapshot.ReceivedPacketCount);
        Assert.Equal(0, snapshot.HapticDroppedPacketCount);
    }

    [Fact]
    public async Task TelemetryIngressWorker_StopDrainsRecordingPackets()
    {
        var drainStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDrain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var worker = new TelemetryIngressWorker(
            _ => CreatePacketResult(),
            isRecordingEnabled: () => true,
            enqueueForRecording: _ => TelemetryRecordingOperationResult.Success("Queued."),
            waitForRecordingDrainAsync: async (_, _) =>
            {
                drainStarted.TrySetResult();
                await allowDrain.Task.ConfigureAwait(false);
                return TelemetryRecordingDrainResult.Complete();
            },
            markRecordingIncomplete: _ => { },
            isForwardingEnabled: () => false,
            forwardPacketAsync: static (_, _) => ValueTask.CompletedTask);

        await worker.StartAsync();
        worker.EnqueueForRecording(CreatePacket(1, [0x01]));

        var stopTask = worker.StopAsync().AsTask();
        await WaitForAsync(drainStarted.Task);
        Assert.False(stopTask.IsCompleted);

        allowDrain.TrySetResult();
        await stopTask;
    }

    [Fact]
    public async Task TelemetryIngressWorker_TimeoutMarksExactRemainingCountIncomplete()
    {
        var incompleteMessages = new ConcurrentQueue<string>();
        await using var worker = new TelemetryIngressWorker(
            _ => CreatePacketResult(),
            isRecordingEnabled: () => true,
            enqueueForRecording: _ => TelemetryRecordingOperationResult.Success("Queued."),
            waitForRecordingDrainAsync: static (_, _) => ValueTask.FromResult(TelemetryRecordingDrainResult.TimedOut(7)),
            markRecordingIncomplete: message => incompleteMessages.Enqueue(message),
            isForwardingEnabled: () => false,
            forwardPacketAsync: static (_, _) => ValueTask.CompletedTask);

        await worker.StartAsync();
        await worker.StopAsync();

        var snapshot = worker.GetSnapshot();
        Assert.True(snapshot.RecordingMarkedIncomplete);
        Assert.Equal(7, snapshot.RemainingRecordingPacketCount);
        Assert.Contains(incompleteMessages, message => message.Contains("7", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TelemetryIngressWorker_RejectsEnqueueAfterAcceptingDisabled()
    {
        await using var worker = new TelemetryIngressWorker(
            _ => CreatePacketResult(),
            isRecordingEnabled: () => false,
            enqueueForRecording: _ => TelemetryRecordingOperationResult.NotRecording("Not recording."),
            waitForRecordingDrainAsync: static (_, _) => ValueTask.FromResult(TelemetryRecordingDrainResult.Complete()),
            markRecordingIncomplete: _ => { },
            isForwardingEnabled: () => true,
            forwardPacketAsync: static (_, _) => ValueTask.CompletedTask);

        await worker.StartAsync();
        await worker.StopAsync();

        Assert.False(worker.ProcessTelemetryPacket(CreatePacket(1, [0x01])));
        Assert.False(worker.EnqueueForForwarding(CreatePacket(2, [0x02])));

        var snapshot = worker.GetSnapshot();
        Assert.Contains("accepting was disabled", snapshot.LastErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TelemetryIngressWorker_DropCountsAndDepthsAreExact()
    {
        var releaseHaptic = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseForwarding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hapticStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var forwardingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var worker = new TelemetryIngressWorker(
            packet =>
            {
                hapticStarted.TrySetResult();
                releaseHaptic.Task.GetAwaiter().GetResult();
                return CreatePacketResult();
            },
            isRecordingEnabled: () => false,
            enqueueForRecording: _ => TelemetryRecordingOperationResult.NotRecording("Not recording."),
            waitForRecordingDrainAsync: static (_, _) => ValueTask.FromResult(TelemetryRecordingDrainResult.Complete()),
            markRecordingIncomplete: _ => { },
            isForwardingEnabled: () => true,
            forwardPacketAsync: async (_, _) =>
            {
                forwardingStarted.TrySetResult();
                await releaseForwarding.Task.ConfigureAwait(false);
            },
            options: new TelemetryIngressWorkerOptions(HapticChannelCapacity: 1, ForwardingChannelCapacity: 1, RecordingChannelCapacity: 8));

        await worker.StartAsync();
        worker.ProcessTelemetryPacket(CreatePacket(1, [0x01]));
        worker.EnqueueForForwarding(CreatePacket(1, [0x01]));
        await WaitForAsync(hapticStarted.Task);
        await WaitForAsync(forwardingStarted.Task);

        worker.ProcessTelemetryPacket(CreatePacket(2, [0x02]));
        worker.ProcessTelemetryPacket(CreatePacket(3, [0x03]));
        worker.ProcessTelemetryPacket(CreatePacket(4, [0x04]));
        worker.EnqueueForForwarding(CreatePacket(2, [0x02]));
        worker.EnqueueForForwarding(CreatePacket(3, [0x03]));
        worker.EnqueueForForwarding(CreatePacket(4, [0x04]));

        var snapshot = worker.GetSnapshot();
        Assert.Equal(4, snapshot.ReceivedPacketCount);
        Assert.Equal(2, snapshot.HapticDroppedPacketCount);
        Assert.Equal(2, snapshot.ForwardingDroppedPacketCount);
        Assert.Equal(1, snapshot.RemainingHapticPacketCount);
        Assert.Equal(1, snapshot.RemainingForwardingPacketCount);

        releaseHaptic.TrySetResult();
        releaseForwarding.TrySetResult();
        await worker.StopAsync();
    }

    private static HapticPipelinePacketResult CreatePacketResult()
    {
        return new HapticPipelinePacketResult(
            HapticPipelineInputSource.LiveUdp,
            TelemetryPacketParseStatus.Success,
            VehicleStateUpdated: true,
            TelemetryRecordingOperationStatus.NotRecording,
            null,
            ForwardingAttempted: false,
            "Processed.");
    }

    private static UdpTelemetryPacket CreatePacket(long sequenceNumber, byte[] payload)
    {
        return new UdpTelemetryPacket(
            sequenceNumber,
            payload,
            new IPEndPoint(IPAddress.Loopback, 20_778),
            DateTimeOffset.UtcNow,
            Stopwatch.GetTimestamp());
    }

    private static async Task SpinWaitAsync(Func<bool> predicate, int timeoutMs = 3000)
    {
        var start = Environment.TickCount64;
        while (!predicate())
        {
            if (Environment.TickCount64 - start > timeoutMs)
            {
                throw new TimeoutException("Condition was not met within the allotted time.");
            }

            await Task.Delay(10);
        }
    }

    private static async Task WaitForAsync(Task task, int timeoutMs = 3000)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
        Assert.Same(task, completed);
        await task;
    }
}
