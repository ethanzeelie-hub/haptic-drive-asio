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
    public async Task DoesNotCreatePerPacketTasks()
    {
        var processedPackets = 0;
        await using var worker = new TelemetryIngressWorker(
            _ =>
            {
                Interlocked.Increment(ref processedPackets);
                return CreatePacketResult();
            },
            isRecordingEnabled: () => false,
            recordPacket: _ => TelemetryRecordingOperationResult.NotRecording("Not recording."),
            markRecordingIncomplete: _ => { },
            isForwardingEnabled: () => false,
            forwardPacketAsync: static (_, _) => ValueTask.CompletedTask);

        await worker.StartAsync();
        for (var i = 0; i < 128; i++)
        {
            worker.Enqueue(CreatePacket(i + 1, [(byte)i]));
        }

        await SpinWaitAsync(() => Volatile.Read(ref processedPackets) >= 128);
        var snapshot = worker.GetSnapshot();

        Assert.Equal(3, snapshot.BackgroundWorkerCount);
        Assert.Equal(128, snapshot.ReceivedPacketCount);
        Assert.Equal(0, snapshot.HapticDroppedPacketCount);
    }

    [Fact]
    public async Task HapticChannelDropsOldestUnderLoad()
    {
        var processedSequences = new ConcurrentQueue<long>();
        await using var worker = new TelemetryIngressWorker(
            packet =>
            {
                processedSequences.Enqueue(packet.SequenceNumber);
                Thread.Sleep(2);
                return CreatePacketResult();
            },
            isRecordingEnabled: () => false,
            recordPacket: _ => TelemetryRecordingOperationResult.NotRecording("Not recording."),
            markRecordingIncomplete: _ => { },
            isForwardingEnabled: () => false,
            forwardPacketAsync: static (_, _) => ValueTask.CompletedTask,
            new TelemetryIngressWorkerOptions(HapticChannelCapacity: 8, ForwardingChannelCapacity: 8, RecordingChannelCapacity: 8));

        await worker.StartAsync();
        for (var i = 0; i < 64; i++)
        {
            worker.Enqueue(CreatePacket(i + 1, [(byte)i]));
        }

        await SpinWaitAsync(() => processedSequences.Any(sequence => sequence == 64), timeoutMs: 5000);
        await worker.StopAsync();
        var snapshot = worker.GetSnapshot();

        Assert.True(snapshot.HapticDroppedPacketCount > 0);
        Assert.Contains(64L, processedSequences);
        Assert.True(processedSequences.Count < 64);
    }

    [Fact]
    public async Task ForwarderPreservesPayloadBytes()
    {
        byte[]? forwardedPayload = null;
        var forwarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var worker = new TelemetryIngressWorker(
            _ => CreatePacketResult(),
            isRecordingEnabled: () => false,
            recordPacket: _ => TelemetryRecordingOperationResult.NotRecording("Not recording."),
            markRecordingIncomplete: _ => { },
            isForwardingEnabled: () => true,
            forwardPacketAsync: (packet, _) =>
            {
                forwardedPayload = packet.Payload.ToArray();
                forwarded.TrySetResult();
                return ValueTask.CompletedTask;
            });

        var payload = new byte[] { 0xF1, 0x25, 0xAA, 0x55 };
        await worker.StartAsync();
        worker.Enqueue(CreatePacket(1, payload));

        await WaitForAsync(forwarded.Task);

        Assert.Equal(payload, forwardedPayload);
    }

    [Fact]
    public async Task RecordingDropMarksRecordingIncomplete()
    {
        var incompleteMessages = new ConcurrentQueue<string>();
        await using var worker = new TelemetryIngressWorker(
            _ => CreatePacketResult(),
            isRecordingEnabled: () => true,
            recordPacket: _ => TelemetryRecordingOperationResult.Dropped("Recording queue full."),
            markRecordingIncomplete: message => incompleteMessages.Enqueue(message),
            isForwardingEnabled: () => false,
            forwardPacketAsync: static (_, _) => ValueTask.CompletedTask,
            new TelemetryIngressWorkerOptions(HapticChannelCapacity: 8, ForwardingChannelCapacity: 8, RecordingChannelCapacity: 1));

        await worker.StartAsync();
        worker.Enqueue(CreatePacket(1, [0x01]));
        await SpinWaitAsync(() => worker.GetSnapshot().RecordingMarkedIncomplete);
        await worker.StopAsync();

        var snapshot = worker.GetSnapshot();

        Assert.True(snapshot.RecordingMarkedIncomplete);
        Assert.NotEmpty(incompleteMessages);
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
