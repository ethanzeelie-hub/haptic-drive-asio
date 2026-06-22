using System.Net;
using System.Net.Sockets;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Core.Tests;

public sealed class UdpTelemetryReceiverTests
{
    [Fact]
    public void DefaultBindAddressIsLoopback()
    {
        var options = new UdpTelemetryReceiverOptions();

        Assert.Equal(IPAddress.Loopback, options.EffectiveBindAddress);
    }

    [Fact]
    public void AllowLanTelemetryUsesAnyWhenNoBindAddressProvided()
    {
        var options = new UdpTelemetryReceiverOptions(AllowLanTelemetry: true);

        Assert.Equal(IPAddress.Any, options.EffectiveBindAddress);
    }

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

    [Fact]
    public async Task AllowedRemoteAddressRejectsUnexpectedSender()
    {
        var allowed = new HashSet<IPAddress> { IPAddress.Parse("127.0.0.2") };
        await using var receiver = new UdpTelemetryReceiver(
            new UdpTelemetryReceiverOptions(
                Port: 0,
                BindAddress: IPAddress.Loopback,
                AllowedRemoteAddresses: allowed));
        var receivedPacket = new TaskCompletionSource<UdpTelemetryPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.PacketReceived += (_, args) => receivedPacket.TrySetResult(args.Packet);

        await receiver.StartAsync();
        var boundPort = receiver.GetSnapshot().BoundPort;

        using var sender = new UdpClient();
        var payload = new byte[] { 0x01, 0x02, 0x03 };
        await sender.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, boundPort));
        await Task.Delay(100);

        Assert.False(receivedPacket.Task.IsCompleted);
        Assert.Equal(0, receiver.GetSnapshot().PacketCount);
        Assert.Equal(1, receiver.GetSnapshot().IgnoredRemotePacketCount);
    }

    [Fact]
    public async Task OversizedDatagramIsCountedAndIgnored()
    {
        await using var receiver = new UdpTelemetryReceiver(
            new UdpTelemetryReceiverOptions(
                Port: 0,
                BindAddress: IPAddress.Loopback,
                MaxDatagramBytes: 4));

        await receiver.StartAsync();
        var boundPort = receiver.GetSnapshot().BoundPort;

        using var sender = new UdpClient();
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        await sender.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, boundPort));
        await Task.Delay(100);

        var snapshot = receiver.GetSnapshot();

        Assert.Equal(0, snapshot.PacketCount);
        Assert.Equal(1, snapshot.OversizedDatagramCount);
    }

    [Fact]
    public async Task Receiver_IsolatesSubscriberExceptionsAndContinuesFanout()
    {
        await using var receiver = new UdpTelemetryReceiver(new UdpTelemetryReceiverOptions(Port: 0));
        var receivedPacket = new TaskCompletionSource<UdpTelemetryPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.PacketReceived += (_, _) => throw new InvalidOperationException("subscriber failed");
        receiver.PacketReceived += (_, args) => receivedPacket.TrySetResult(args.Packet);

        await receiver.StartAsync();
        var boundPort = receiver.GetSnapshot().BoundPort;

        using var sender = new UdpClient();
        var payload = new byte[] { 0x09, 0x08, 0x07 };
        await sender.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, boundPort));

        var packet = await WaitForAsync(receivedPacket.Task, TimeSpan.FromSeconds(3));
        var snapshot = receiver.GetSnapshot();

        Assert.Equal(payload, packet.Payload);
        Assert.Equal(1, snapshot.SubscriberExceptionCount);
        Assert.Contains("subscriber failed", snapshot.LastErrorMessage, StringComparison.Ordinal);
    }

    private static async Task<T> WaitForAsync<T>(Task<T> task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        Assert.Same(task, completed);
        return await task;
    }
}
