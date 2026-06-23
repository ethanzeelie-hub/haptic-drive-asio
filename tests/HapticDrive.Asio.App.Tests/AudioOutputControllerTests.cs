using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;

namespace HapticDrive.Asio.App.Tests;

public sealed class AudioOutputControllerTests
{
    [Fact]
    public async Task ConcurrentOutputSelections_OnlyLatestGenerationApplies()
    {
        var viewModel = new OutputDeviceStatusViewModel();
        var controller = new AudioOutputController(viewModel);
        var firstStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstTask = controller.SelectOutputAsync(async (_, cancellationToken) =>
        {
            firstStarted.SetResult(null);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new AudioOutputSelectionState("asio:first", "first applied", 48_000, 256);
        }).AsTask();

        await firstStarted.Task;

        var secondTask = controller.SelectOutputAsync((_, _) =>
            Task.FromResult(new AudioOutputSelectionState("asio:second", "second applied", 96_000, 128))).AsTask();

        var firstResult = await firstTask;
        var secondResult = await secondTask;

        Assert.True(firstResult.Accepted);
        Assert.False(firstResult.Applied);
        Assert.True(secondResult.Accepted);
        Assert.True(secondResult.Applied);
        Assert.Equal("asio:second", viewModel.SelectedOutputId);
        Assert.Equal("second applied", viewModel.StatusText);
        Assert.Equal(96_000, viewModel.SampleRate);
        Assert.Equal(128, viewModel.BufferSize);
        Assert.Equal(ControllerRuntimeState.Stopped.ToString(), viewModel.RuntimeState);
    }

    [Fact]
    public async Task Shutdown_PreventsNewOperations()
    {
        var viewModel = new OutputDeviceStatusViewModel();
        var controller = new AudioOutputController(viewModel);
        var invoked = false;

        controller.BeginShutdown();
        var result = await controller.SelectOutputAsync((_, _) =>
        {
            invoked = true;
            return Task.FromResult(new AudioOutputSelectionState("asio:test", "should not run", 48_000, 256));
        });

        Assert.False(invoked);
        Assert.False(result.Accepted);
        Assert.False(result.Applied);
        Assert.Equal(ControllerRuntimeState.ShuttingDown, controller.State);
        Assert.Equal(ControllerRuntimeState.ShuttingDown.ToString(), viewModel.RuntimeState);
    }
}
