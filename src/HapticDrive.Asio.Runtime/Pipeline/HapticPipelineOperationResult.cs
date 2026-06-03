using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Recording;

namespace HapticDrive.Asio.Runtime.Pipeline;

public sealed record HapticPipelineOperationResult(
    bool Succeeded,
    string Message,
    AudioOutputDeviceResult? OutputResult = null,
    TelemetryReplayResult? ReplayResult = null)
{
    public static HapticPipelineOperationResult Success(
        string message,
        AudioOutputDeviceResult? outputResult = null,
        TelemetryReplayResult? replayResult = null)
    {
        return new(true, message, outputResult, replayResult);
    }

    public static HapticPipelineOperationResult Failure(
        string message,
        AudioOutputDeviceResult? outputResult = null,
        TelemetryReplayResult? replayResult = null)
    {
        return new(false, message, outputResult, replayResult);
    }
}

