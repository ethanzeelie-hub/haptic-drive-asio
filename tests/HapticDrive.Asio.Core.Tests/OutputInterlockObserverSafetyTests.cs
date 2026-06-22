using HapticDrive.Asio.Core.Safety;

namespace HapticDrive.Asio.Core.Tests;

public sealed class OutputInterlockObserverSafetyTests
{
    [Fact]
    public void TripUpdatesSnapshotEvenWhenSubscriberThrows()
    {
        var interlock = new OutputInterlock();
        Assert.True(interlock.Reset("Ready for observer safety test."));
        interlock.Changed += (_, _) => throw new InvalidOperationException("observer failed");

        interlock.Trip(OutputInterlockReason.UserEmergencyMute, "Trip despite throwing observer.");

        Assert.True(interlock.Current.IsLatched);
        Assert.Equal(OutputInterlockReason.UserEmergencyMute, interlock.Current.Reason);
        Assert.Equal("Trip despite throwing observer.", interlock.Current.Message);
    }

    [Fact]
    public void ResetUpdatesSnapshotEvenWhenSubscriberThrows()
    {
        var interlock = new OutputInterlock();
        interlock.Changed += (_, _) => throw new InvalidOperationException("observer failed");

        var reset = interlock.Reset("Reset despite throwing observer.");

        Assert.True(reset);
        Assert.False(interlock.Current.IsLatched);
        Assert.Equal("Reset despite throwing observer.", interlock.Current.Message);
    }

    [Fact]
    public void ThrowingSubscriberDoesNotPreventOtherSubscribers()
    {
        var interlock = new OutputInterlock();
        Assert.True(interlock.Reset("Ready for observer fanout test."));
        var calledAfterThrowingSubscriber = false;
        interlock.Changed += (_, _) => throw new InvalidOperationException("observer failed");
        interlock.Changed += (_, _) => calledAfterThrowingSubscriber = true;

        interlock.Trip(OutputInterlockReason.UserEmergencyMute, "Trip should reach later observers.");

        Assert.True(calledAfterThrowingSubscriber);
    }

    [Fact]
    public void ObserverFailureCountIsVisibleOrRecorded()
    {
        var interlock = new OutputInterlock();
        Assert.True(interlock.Reset("Ready for observer count test."));
        interlock.Changed += (_, _) => throw new InvalidOperationException("observer failed");
        interlock.Changed += (_, _) => throw new InvalidOperationException("observer failed again");

        interlock.Trip(OutputInterlockReason.UserEmergencyMute, "Trip records observer failures.");

        Assert.Equal(2, interlock.ObserverFailureCount);
    }
}
