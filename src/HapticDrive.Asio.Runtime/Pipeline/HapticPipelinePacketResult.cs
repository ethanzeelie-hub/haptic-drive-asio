using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Runtime.Pipeline;

public sealed record HapticPipelinePacketResult(
    HapticPipelineInputSource Source,
    TelemetryPacketParseStatus ParseStatus,
    bool VehicleStateUpdated,
    TelemetryRecordingOperationStatus RecordingStatus,
    string? RecordingMessage,
    bool ForwardingAttempted,
    string Message);
