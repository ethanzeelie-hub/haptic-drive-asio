using System.Net;
using System.Net.Sockets;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Core.Tests;

public sealed class UdpTelemetryForwarderTests
{
    [Fact]
    public async Task Forwarder_WithNoDestinationsCountsInputPacketWithoutSending()
    {
        await using var forwarder = new UdpTelemetryForwarder();
        var packet = CreatePacket([0x46, 0x31, 0x32, 0x35]);

        await forwarder.ForwardAsync(packet);

        var snapshot = forwarder.GetSnapshot();

        Assert.False(snapshot.IsEnabled);
        Assert.Equal(0, snapshot.DestinationCount);
        Assert.Equal(0, snapshot.EnabledDestinationCount);
        Assert.Equal(1, snapshot.InputPacketCount);
        Assert.Equal(0, snapshot.ForwardedDatagramCount);
        Assert.Equal(0, snapshot.ForwardedByteCount);
        Assert.Equal(0, snapshot.ErrorCount);
        Assert.Null(snapshot.LastForwardedAtUtc);
    }

    [Fact]
    public async Task Forwarder_ForwardsExactRawPayloadToDestination()
    {
        using var destination = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var destinationEndPoint = (IPEndPoint)destination.Client.LocalEndPoint!;
        await using var forwarder = new UdpTelemetryForwarder(
        [
            new UdpTelemetryForwardingDestination("Local test sink", destinationEndPoint)
        ]);
        var payload = new byte[] { 0x46, 0x31, 0x32, 0x35, 0x00, 0xFF };

        await forwarder.ForwardAsync(CreatePacket(payload));

        var received = await WaitForAsync(destination.ReceiveAsync(), TimeSpan.FromSeconds(3));
        var snapshot = forwarder.GetSnapshot();

        Assert.Equal(payload, received.Buffer);
        Assert.True(snapshot.IsEnabled);
        Assert.Equal(1, snapshot.DestinationCount);
        Assert.Equal(1, snapshot.EnabledDestinationCount);
        Assert.Equal(1, snapshot.InputPacketCount);
        Assert.Equal(1, snapshot.ForwardedDatagramCount);
        Assert.Equal(payload.Length, snapshot.ForwardedByteCount);
        Assert.Equal(0, snapshot.ErrorCount);
        Assert.NotNull(snapshot.LastForwardedAtUtc);
    }

    [Fact]
    public async Task Forwarder_ForwardsMalformedF125LookingPayloadWithoutParsing()
    {
        using var destination = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var destinationEndPoint = (IPEndPoint)destination.Client.LocalEndPoint!;
        await using var forwarder = new UdpTelemetryForwarder(
        [
            new UdpTelemetryForwardingDestination("Local malformed packet sink", destinationEndPoint)
        ]);
        var malformedF125Payload = new byte[] { 0xE9, 0x07, 25, 1, 0, 1, 6, 0xAA, 0xBB };

        await forwarder.ForwardAsync(CreatePacket(malformedF125Payload));

        var received = await WaitForAsync(destination.ReceiveAsync(), TimeSpan.FromSeconds(3));
        var snapshot = forwarder.GetSnapshot();

        Assert.Equal(malformedF125Payload, received.Buffer);
        Assert.Equal(1, snapshot.InputPacketCount);
        Assert.Equal(1, snapshot.ForwardedDatagramCount);
        Assert.Equal(malformedF125Payload.Length, snapshot.ForwardedByteCount);
        Assert.Equal(0, snapshot.ErrorCount);
    }

    [Fact]
    public async Task Forwarder_ForwardsToMultipleEnabledDestinations()
    {
        using var firstDestination = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var secondDestination = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var firstEndPoint = (IPEndPoint)firstDestination.Client.LocalEndPoint!;
        var secondEndPoint = (IPEndPoint)secondDestination.Client.LocalEndPoint!;
        await using var forwarder = new UdpTelemetryForwarder(
        [
            new UdpTelemetryForwardingDestination("First local sink", firstEndPoint),
            new UdpTelemetryForwardingDestination("Second local sink", secondEndPoint)
        ]);
        var payload = new byte[] { 0x05, 0x25, 0x20, 0x78 };

        await forwarder.ForwardAsync(CreatePacket(payload));

        var firstReceived = await WaitForAsync(firstDestination.ReceiveAsync(), TimeSpan.FromSeconds(3));
        var secondReceived = await WaitForAsync(secondDestination.ReceiveAsync(), TimeSpan.FromSeconds(3));
        var snapshot = forwarder.GetSnapshot();

        Assert.Equal(payload, firstReceived.Buffer);
        Assert.Equal(payload, secondReceived.Buffer);
        Assert.Equal(1, snapshot.InputPacketCount);
        Assert.Equal(2, snapshot.ForwardedDatagramCount);
        Assert.Equal(payload.Length * 2, snapshot.ForwardedByteCount);
        Assert.Equal(0, snapshot.ErrorCount);
    }

    [Fact]
    public async Task Forwarder_SkipsDisabledDestinations()
    {
        using var enabledDestination = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var disabledDestination = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var enabledEndPoint = (IPEndPoint)enabledDestination.Client.LocalEndPoint!;
        var disabledEndPoint = (IPEndPoint)disabledDestination.Client.LocalEndPoint!;
        await using var forwarder = new UdpTelemetryForwarder(
        [
            new UdpTelemetryForwardingDestination("Enabled sink", enabledEndPoint),
            new UdpTelemetryForwardingDestination("Disabled sink", disabledEndPoint, enabled: false)
        ]);
        var payload = new byte[] { 0x10, 0x20, 0x30 };

        await forwarder.ForwardAsync(CreatePacket(payload));

        var received = await WaitForAsync(enabledDestination.ReceiveAsync(), TimeSpan.FromSeconds(3));
        var disabledReceiveTask = disabledDestination.ReceiveAsync();
        var snapshot = forwarder.GetSnapshot();

        Assert.Equal(payload, received.Buffer);
        Assert.Equal(2, snapshot.DestinationCount);
        Assert.Equal(1, snapshot.EnabledDestinationCount);
        Assert.Equal(1, snapshot.ForwardedDatagramCount);
        Assert.NotSame(disabledReceiveTask, await Task.WhenAny(disabledReceiveTask, Task.Delay(100)));
    }

    private static UdpTelemetryPacket CreatePacket(byte[] payload)
    {
        return new UdpTelemetryPacket(
            1,
            payload,
            new IPEndPoint(IPAddress.Loopback, 20_778),
            DateTimeOffset.UtcNow);
    }

    private static async Task<T> WaitForAsync<T>(Task<T> task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        Assert.Same(task, completed);
        return await task;
    }
}
