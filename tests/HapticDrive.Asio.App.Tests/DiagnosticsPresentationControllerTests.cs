using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;

namespace HapticDrive.Asio.App.Tests;

public sealed class DiagnosticsPresentationControllerTests
{
    [Fact]
    public void Publish_MapsSummaryAndDetailsIntoViewModel()
    {
        var viewModel = new DiagnosticsSummaryViewModel();
        var controller = new DiagnosticsPresentationController(viewModel);

        controller.Publish(
            summaryText: "UDP 10 packet(s), parser 10 valid / 0 failed.",
            detailsText: "Pipeline healthy.");

        Assert.Equal("UDP 10 packet(s), parser 10 valid / 0 failed.", viewModel.SummaryText);
        Assert.Equal("Pipeline healthy.", viewModel.DetailsText);
    }
}
