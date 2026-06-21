using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.Runtime.Tests;

internal static class RuntimeTestPipelineFactory
{
    public static HapticPipelineCoordinator Create(
        AudioOutputConfiguration? configuration = null,
        IAudioOutputDevice? outputDevice = null,
        IUdpTelemetryForwarder? telemetryForwarder = null,
        TelemetryRecordingService? recordingService = null,
        ITelemetryReplayService? replayService = null,
        HapticDriveProfile? profile = null,
        HapticPipelineOptions? options = null,
        IEnumerable<UdpTelemetryForwardingDestination>? forwardingDestinations = null,
        IGameTelemetryAdapter? telemetryGameAdapter = null,
        IOutputInterlock? outputInterlock = null)
    {
        if (outputInterlock is null)
        {
            outputInterlock = new OutputInterlock();
            outputInterlock.Reset("Runtime tests default to an armed interlock unless a test opts into the startup latch.");
        }

        return new HapticPipelineCoordinator(
            telemetryGameAdapter ?? new F125GameTelemetryAdapter(),
            configuration,
            outputDevice,
            telemetryForwarder,
            recordingService,
            replayService,
            profile,
            options,
            forwardingDestinations,
            outputInterlock);
    }
}
