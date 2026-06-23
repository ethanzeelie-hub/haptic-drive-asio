using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Diagnostics;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime.Diagnostics;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Runtime.Safety;
using HapticDrive.Asio.Runtime.Telemetry;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class RuntimeHealthMonitorTests
{
    [Fact]
    public void ObservePipelineAndReplay_PublishesStructuredRuntimeHealthEvents()
    {
        var sink = new InMemoryDiagnosticSink();
        var correlationContext = new DiagnosticCorrelationContext();
        var monitor = new RuntimeHealthMonitor(sink, correlationContext, new FakeTimeProvider(new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero)));

        monitor.ObservePipeline(CreatePipelineSnapshot(telemetryStale: true, underruns: 1, droppedBuffers: 1, renderOverruns: 1), "telemetry-a", "output-a");
        monitor.ObserveTelemetryIngress(new TelemetryIngressWorkerSnapshot(true, 2, 10, 1, 1, 1, true, "drops"));
        monitor.ObserveRecording(new TelemetryRecordingSnapshot(true, "session-1.hdrec", 4, TimeSpan.FromSeconds(1), "incomplete", 8192, 0, 1, true, "Dropped packets."), "session-1.hdrec");
        monitor.ObserveReplay(new TelemetryReplaySnapshot(false, null, 12, "idle", SubscriberExceptionCount: 1, LastSubscriberErrorMessage: "subscriber failed"));

        var events = sink.Snapshot();

        Assert.Contains(events, item => item.EventId == "telemetry.stale");
        Assert.Contains(events, item => item.EventId == "audio.output-overrun");
        Assert.Contains(events, item => item.EventId == "telemetry.ingress-drop");
        Assert.Contains(events, item => item.EventId == "recording.drop");
        Assert.Contains(events, item => item.EventId == "recording.finalization-warning");
        Assert.Contains(events, item => item.EventId == "replay.subscriber-failure");
    }

    [Fact]
    public void ObserveSupervisor_PublishesParticipantFailure()
    {
        var sink = new InMemoryDiagnosticSink();
        var correlationContext = new DiagnosticCorrelationContext();
        var monitor = new RuntimeHealthMonitor(sink, correlationContext);
        var interlock = OutputInterlockSnapshot.StartupSafeDefault();
        var supervisorSnapshot = new OutputInterlockSupervisorSnapshot(
            interlock,
            [],
            ProcessedSnapshotCount: 1,
            ParticipantFailureCount: 2,
            LastFailure: "AudioOutput timed out while silencing.",
            LastProcessedAtUtc: DateTimeOffset.UtcNow);

        monitor.ObserveSupervisor(supervisorSnapshot);

        Assert.Contains(sink.Snapshot(), item => item.EventId == "safety.participant-silence-failure");
    }

    private static HapticPipelineSnapshot CreatePipelineSnapshot(bool telemetryStale, long underruns, long droppedBuffers, long renderOverruns)
    {
        var outputStatus = new AudioOutputStatus(
            AudioOutputDeviceKind.Null,
            AudioOutputDeviceState.Started,
            "Null output",
            "Streaming",
            DeviceName: null,
            SampleRate: 48_000,
            ChannelCount: 2,
            BufferSize: 128,
            RequiresPhysicalHardware: false,
            IsManualDebugOnly: false,
            IsAvailable: true,
            SubmittedBufferCount: 12,
            DroppedBufferCount: droppedBuffers,
            RenderCallbackCount: 12,
            BackendCallbackCount: 12,
            UnderrunCount: underruns,
            IsStreaming: true);

        return new HapticPipelineSnapshot(
            IsRunning: true,
            InputSource: HapticPipelineInputSource.LiveUdp,
            LastPacketAtUtc: DateTimeOffset.UtcNow,
            LastVehicleStateUpdateAtUtc: DateTimeOffset.UtcNow,
            PacketsObserved: 25,
            ParserSuccessCount: 25,
            ParserIgnoredCount: 0,
            ParserFailureCount: 0,
            VehicleStateUpdateCount: 5,
            RenderedBufferCount: 20,
            TelemetryAge: telemetryStale ? TimeSpan.FromMilliseconds(750) : TimeSpan.FromMilliseconds(20),
            TelemetryMuteTimeout: TimeSpan.FromMilliseconds(250),
            TelemetryTimedOutMuted: telemetryStale,
            IsMuted: false,
            EmergencyMute: false,
            LastPacketMessage: "Processed.",
            LastVehicleStateMessage: "Updated.",
            LastPipelineError: null,
            VehicleState: VehicleState.Empty,
            Effects: new HapticEffectEngineSnapshot(default, default, default, default, default, default, 0, 0f),
            Audio: null,
            Output: outputStatus,
            ManualAsioHardwareTest: new ManualAsioHardwareTestSnapshot(
                IsActive: false,
                TestMode: "None",
                OutputMode: "Null",
                SelectedAsioDriver: "none",
                SelectedOutputChannel: null,
                AsioRunning: false,
                AsioArmed: false,
                AsioCallbackActive: false,
                HapticsRunning: true,
                EmergencyMute: false,
                NormalMute: false,
                OutputPeakLevel: 0f,
                FramesSubmitted: 0,
                FramesRendered: 0,
                RenderCallbackCount: 0,
                SubmittedFrameCount: 0,
                DroppedFrameCount: 0,
                BackendCallbackCount: 0,
                LastPulseUsedAsio: false,
                LastManualPulseUsedAsio: false,
                LastGearPulseUsedAsio: false,
                LastPulseBlocked: false,
                LimiterApplied: false,
                PulseGenerationId: 0,
                StaleStopIgnoredCount: 0,
                BlockedReason: null,
                LastTestSignal: null,
                LastTestDuration: null,
                LastStrengthPercent: null,
                LastOutputTrimPercent: null,
                LastEffectivePreLimiterAmplitude: null,
                LastEffectivePostLimiterAmplitude: null,
                LastFrequencyHz: null,
                LastDurationMs: null,
                LastSource: null,
                LastDurationMode: null,
                ManualPulsePeak: 0f,
                FlightRecorderPath: "disabled",
                LastError: null),
            NullOutput: null,
            Forwarding: new UdpTelemetryForwarderSnapshot(false, 0, 0, 0, 0, 0, 0, null, null),
            PacketDiagnostics: [],
            Recording: new TelemetryRecordingSnapshot(false, null, 0, null, null),
            Replay: new TelemetryReplaySnapshot(false, null, 0, "idle"))
        {
            OutputInterlock = OutputInterlockSnapshot.StartupSafeDefault(),
            TelemetryFreshness = telemetryStale
                ? new(false, true, true, true, false, TimeSpan.FromMilliseconds(750), 0)
                : new(true, true, true, true, true, TimeSpan.FromMilliseconds(20), 0),
            SessionFreshness = new(true, true, true, true, true, TimeSpan.FromMilliseconds(20), 0),
            RenderOverrunCount = renderOverruns
        };
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
