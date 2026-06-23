using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Runtime.Safety;

namespace HapticDrive.Asio.App.Tests;

public sealed class ApplicationSafetyControllerTests
{
    [Fact]
    public void Publish_MapsInterlockSnapshotIntoViewModel()
    {
        var viewModel = new SafetyStateViewModel();
        var controller = new ApplicationSafetyController(viewModel);

        controller.Publish(new OutputInterlockSnapshot(
            IsLatched: true,
            Reason: OutputInterlockReason.UserEmergencyMute,
            Message: "muted for test",
            ChangedAtUtc: DateTimeOffset.UtcNow,
            Generation: 42));

        Assert.True(viewModel.IsLatched);
        Assert.Equal(OutputInterlockReason.UserEmergencyMute.ToString(), viewModel.Reason);
        Assert.Equal("muted for test", viewModel.Message);
        Assert.Equal(42, viewModel.Generation);
        Assert.Equal("Latched: UserEmergencyMute", viewModel.StatusText);
    }
}
