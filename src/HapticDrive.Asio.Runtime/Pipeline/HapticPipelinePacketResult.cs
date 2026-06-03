using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.Runtime.Pipeline;

public sealed record HapticPipelinePacketResult(
    HapticPipelineInputSource Source,
    F125PacketParseStatus ParseStatus,
    bool VehicleStateUpdated,
    TelemetryRecordingOperationStatus RecordingStatus,
    bool ForwardingAttempted,
    string Message);

