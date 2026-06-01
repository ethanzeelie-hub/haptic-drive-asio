using System.Net;
using System.Net.Sockets;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Core.Tests;

public sealed class UdpTelemetryReceiverTests
{
    [Fact]
    public async Task Receiver_UsesDefaultF125ForwardedTelemetryPort()
    {
        await using var receiver = new UdpTelemetryReceiver();

        var snapshot = receiver.GetSnapshot();

        Assert.Equal(20778, snapshot.ConfiguredPort);
    }

    [Fact]
    public async Task Receiver_ReceivesRawUdpPacketAndUpdatesStats()
    {
        await using var receiver = new UdpTelemetryReceiver(new UdpTelemetryReceiverOptions(Port: 0));
        var receivedPacket = new TaskCompletionSource<UdpTelemetryPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.PacketReceived += (_, args) => receivedPacket.TrySetResult(args.Packet);

        await receiver.StartAsync();
        var startedSnapshot = receiver.GetSnapshot();
        var payload = new byte[] { 0x46, 0x31, 0x32, 0x35 };

        using var sender = new UdpClient();
        await sender.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, startedSnapshot.BoundPort));

        var packet = await WaitForAsync(receivedPacket.Task, TimeSpan.FromSeconds(3));
        var snapshot = receiver.GetSnapshot();

        Assert.Equal(payload, packet.Payload);
        Assert.Equal(1, packet.SequenceNumber);
        Assert.Equal(1, snapshot.PacketCount);
        Assert.True(snapshot.PacketRatePerSecond > 0);
        Assert.NotNull(snapshot.LastPacketAtUtc);
        Assert.False(snapshot.HasNoPacketWarning);
        Assert.Equal(startedSnapshot.BoundPort, snapshot.BoundPort);
    }

    [Fact]
    public async Task Receiver_ReportsNoPacketWarningWhenRunningWithoutPackets()
    {
        await using var receiver = new UdpTelemetryReceiver(
            new UdpTelemetryReceiverOptions(
                Port: 0,
                NoPacketWarningThreshold: TimeSpan.FromMilliseconds(10)));

        await receiver.StartAsync();
        await Task.Delay(50);

        var snapshot = receiver.GetSnapshot();

        Assert.True(snapshot.IsRunning);
        Assert.Equal(0, snapshot.PacketCount);
        Assert.True(snapshot.HasNoPacketWarning);
    }

    [Fact]
    public async Task Receiver_DoesNotWarnBeforeNoPacketThreshold()
    {
        await using var receiver = new UdpTelemetryReceiver(
            new UdpTelemetryReceiverOptions(
                Port: 0,
                NoPacketWarningThreshold: TimeSpan.FromSeconds(5)));

        await receiver.StartAsync();

        var snapshot = receiver.GetSnapshot();

        Assert.True(snapshot.IsRunning);
        Assert.False(snapshot.HasNoPacketWarning);
    }

    [Fact]
    public async Task Receiver_StopIsIdempotent()
    {
        await using var receiver = new UdpTelemetryReceiver(new UdpTelemetryReceiverOptions(Port: 0));

        await receiver.StartAsync();
        await receiver.StopAsync();
        await receiver.StopAsync();

        var snapshot = receiver.GetSnapshot();

        Assert.False(snapshot.IsRunning);
    }

    private static async Task<T> WaitForAsync<T>(Task<T> task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        Assert.Same(task, completed);
        return await task;
    }
}
