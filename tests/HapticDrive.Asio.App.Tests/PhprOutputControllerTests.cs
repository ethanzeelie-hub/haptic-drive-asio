using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;

namespace HapticDrive.Asio.App.Tests;

public sealed class PhprOutputControllerTests
{
    [Fact]
    public void Publish_MapsWorkflowAndSafetyStatusIntoViewModel()
    {
        var viewModel = new PhprStatusViewModel();
        var controller = new PhprOutputController(viewModel);

        controller.Publish(
            workflowStatusText: "Direct mode armed for diagnostics.",
            safetyStatusText: "Authorization required before non-stop writes.");

        Assert.Equal("Direct mode armed for diagnostics.", viewModel.WorkflowStatusText);
        Assert.Equal("Authorization required before non-stop writes.", viewModel.SafetyStatusText);
    }
}
