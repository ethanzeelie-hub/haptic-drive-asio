using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;

namespace HapticDrive.Asio.App.Tests;

public sealed class RecordingReplayControllerTests
{
    [Fact]
    public async Task RecordingOperations_RunExclusively()
    {
        var viewModel = new RecordingReplayStatusViewModel();
        var controller = new RecordingReplayController(viewModel);
        var firstStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionOrder = new List<int>();

        var firstTask = controller.RunExclusiveAsync(async (_, _) =>
        {
            executionOrder.Add(1);
            firstStarted.SetResult(null);
            await releaseFirst.Task;
        }).AsTask();

        await firstStarted.Task;

        var secondTask = controller.RunExclusiveAsync((_, _) =>
        {
            executionOrder.Add(2);
            return Task.CompletedTask;
        }).AsTask();

        await Task.Delay(50);
        Assert.Equal([1], executionOrder);

        releaseFirst.SetResult(null);

        var firstResult = await firstTask;
        var secondResult = await secondTask;

        Assert.True(firstResult.Accepted);
        Assert.True(firstResult.Applied);
        Assert.True(secondResult.Accepted);
        Assert.True(secondResult.Applied);
        Assert.Equal([1, 2], executionOrder);
        Assert.Equal(ControllerRuntimeState.Stopped.ToString(), viewModel.RuntimeState);
    }

    [Fact]
    public async Task Shutdown_PreventsNewOperations()
    {
        var viewModel = new RecordingReplayStatusViewModel();
        var controller = new RecordingReplayController(viewModel);
        var invoked = false;

        controller.BeginShutdown();
        var result = await controller.RunExclusiveAsync((_, _) =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        Assert.False(invoked);
        Assert.False(result.Accepted);
        Assert.False(result.Applied);
        Assert.Equal(ControllerRuntimeState.ShuttingDown.ToString(), viewModel.RuntimeState);
    }
}
