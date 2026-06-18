using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Runtime.Pipeline;

public sealed record HapticPipelinePacketResult(
    HapticPipelineInputSource Source,
    TelemetryPacketParseStatus ParseStatus,
    bool VehicleStateUpdated,
    TelemetryRecordingOperationStatus RecordingStatus,
    bool ForwardingAttempted,
    string Message);
