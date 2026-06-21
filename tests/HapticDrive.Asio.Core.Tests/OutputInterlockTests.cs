using HapticDrive.Asio.Core.Safety;

namespace HapticDrive.Asio.Core.Tests;

public sealed class OutputInterlockTests
{
    [Fact]
    public void StartsLatched()
    {
        var interlock = new OutputInterlock();

        Assert.True(interlock.Current.IsLatched);
        Assert.Equal(OutputInterlockReason.StartupSafeDefault, interlock.Current.Reason);
        Assert.False(interlock.AllowsOutput);
    }

    [Fact]
    public void TripIsLatchedUntilExplicitReset()
    {
        var interlock = new OutputInterlock();

        Assert.True(interlock.Reset("Ready for output."));
        interlock.Trip(OutputInterlockReason.UserEmergencyMute, "User requested stop.");

        Assert.True(interlock.Current.IsLatched);
        Assert.Equal(OutputInterlockReason.UserEmergencyMute, interlock.Current.Reason);
        Assert.False(interlock.AllowsOutput);
    }

    [Fact]
    public void ResetIncrementsGeneration()
    {
        var interlock = new OutputInterlock();
        var initialGeneration = interlock.Current.Generation;

        Assert.True(interlock.Reset("Ready for output."));

        Assert.False(interlock.Current.IsLatched);
        Assert.True(interlock.Current.Generation > initialGeneration);
    }
}
