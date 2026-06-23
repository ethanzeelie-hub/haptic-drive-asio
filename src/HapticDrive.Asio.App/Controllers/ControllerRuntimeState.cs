namespace HapticDrive.Asio.App.Controllers;

internal enum ControllerRuntimeState
{
    Running,
    Stopping,
    Stopped,
    ShuttingDown,
    Disposed
}

internal readonly record struct ControllerOperationResult(
    long Generation,
    bool Accepted,
    bool Applied);
