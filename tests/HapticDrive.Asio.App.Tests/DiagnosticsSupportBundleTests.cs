using System.IO;
using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Diagnostics;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Runtime.Telemetry;

namespace HapticDrive.Asio.App.Tests;

public sealed class DiagnosticRedactorTests
{
    [Fact]
    public void RedactsUserProfilePaths()
    {
        var redactor = new SupportBundleDiagnosticRedactor(DiagnosticRedactionMode.Safe);

        var result = redactor.RedactText(@"Road recorder path: C:\Users\ethan\OneDrive\Documents\secret\road-flight.jsonl");

        Assert.DoesNotContain(@"C:\Users\ethan", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactsPrivateIpAddressesInSafeMode()
    {
        var redactor = new SupportBundleDiagnosticRedactor(DiagnosticRedactionMode.Safe);

        var result = redactor.RedactText("Telemetry source 192.168.1.50 via 10.0.0.14 and localhost 127.0.0.1.");

        Assert.DoesNotContain("192.168.1.50", result, StringComparison.Ordinal);
        Assert.DoesNotContain("10.0.0.14", result, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", result, StringComparison.Ordinal);
        Assert.Contains("<private-ip>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactsSerialNumbers()
    {
        var redactor = new SupportBundleDiagnosticRedactor(DiagnosticRedactionMode.Safe);

        var result = redactor.RedactText(@"Device serial: ABC123456789 on \\?\hid#vid_3670&pid_0905#private-serial");

        Assert.DoesNotContain("ABC123456789", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-serial", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<redacted>", result, StringComparison.Ordinal);
    }
}

public sealed class DiagnosticEventTests
{
    [Fact]
    public void EventsIncludeCorrelationIds()
    {
        var generatedAtUtc = new DateTimeOffset(2026, 6, 21, 1, 2, 3, TimeSpan.Zero);
        var inputs = new StructuredDiagnosticsBuildInputs(
            generatedAtUtc,
            SelectedGameId: "f1-25",
            SelectedGameDisplayName: "F1 25",
            SelectedOutputId: "null",
            ActiveProfileName: "Default",
            SettingsError: "Recovered app settings from backup snapshot.",
            PipelineSnapshot: CreatePipelineSnapshot(
                telemetryTimedOutMuted: true,
                outputInterlock: new OutputInterlockSnapshot(
                    IsLatched: true,
                    Reason: OutputInterlockReason.TelemetryStale,
                    Message: "Telemetry stale.",
                    ChangedAtUtc: generatedAtUtc,
                    Generation: 5),
                underrunCount: 2,
                droppedBufferCount: 1,
                renderOverrunCount: 1,
                recordingDroppedPacketCount: 3,
                recordingIncomplete: true),
            ReceiverSnapshot: new UdpTelemetryReceiverSnapshot(
                IsRunning: true,
                ConfiguredPort: 20778,
                BoundPort: 20778,
                PacketCount: 120,
                IgnoredRemotePacketCount: 4,
                OversizedDatagramCount: 1,
                PacketRatePerSecond: 60,
                StartedAtUtc: generatedAtUtc.AddMinutes(-1),
                LastPacketAtUtc: generatedAtUtc,
                TimeSinceLastPacket: TimeSpan.FromMilliseconds(20),
                HasNoPacketWarning: false,
                ErrorCount: 0,
                LastErrorMessage: null),
            IngressSnapshot: new TelemetryIngressWorkerSnapshot(
                IsRunning: true,
                BackgroundWorkerCount: 3,
                ReceivedPacketCount: 120,
                HapticDroppedPacketCount: 2,
                RecordingDroppedPacketCount: 3,
                ForwardingDroppedPacketCount: 1,
                RecordingMarkedIncomplete: true,
                LastErrorMessage: "Recording ingress channel dropped one or more packets."),
            AudioDiagnostics: CreateAudioDiagnostics(underrunCount: 2, droppedBufferCount: 1),
            CorrelationIds: new SupportBundleCorrelationIds(
                AppSessionId: "app-session-1",
                TelemetrySessionId: "telemetry-session-1",
                RecordingSessionId: "recording-session-1",
                OutputDeviceSessionId: "output-session-1"));

        var result = StructuredDiagnosticsBuilder.Build(inputs);

        Assert.NotEmpty(result.Events);
        Assert.All(result.Events, item => Assert.False(string.IsNullOrWhiteSpace(item.CorrelationId)));
        Assert.Contains(result.Events, item => item.EventId == "telemetry.stale" && item.CorrelationId == "telemetry-session-1");
        Assert.Contains(result.Events, item => item.EventId == "recording.capture-integrity" && item.CorrelationId == "recording-session-1");
        Assert.Contains(result.Events, item => item.EventId == "audio.output-health" && item.CorrelationId == "output-session-1");
        Assert.Contains(result.Events, item => item.EventId == "persistence.settings-warning" && item.CorrelationId == "app-session-1");
    }

    private static HapticPipelineSnapshot CreatePipelineSnapshot(
        bool telemetryTimedOutMuted,
        OutputInterlockSnapshot outputInterlock,
        long underrunCount,
        long droppedBufferCount,
        long renderOverrunCount,
        long recordingDroppedPacketCount,
        bool recordingIncomplete)
    {
        var outputStatus = new AudioOutputStatus(
            AudioOutputDeviceKind.Null,
            AudioOutputDeviceState.Stopped,
            "Null output",
            "Stopped",
            DeviceName: null,
            SampleRate: 48_000,
            ChannelCount: 2,
            BufferSize: 128,
            RequiresPhysicalHardware: false,
            IsManualDebugOnly: false,
            IsAvailable: true,
            SubmittedBufferCount: 20,
            DroppedBufferCount: droppedBufferCount,
            RenderCallbackCount: 20,
            BackendCallbackCount: 20,
            UnderrunCount: underrunCount,
            IsStreaming: true);

        return new HapticPipelineSnapshot(
        IsRunning: true,
        InputSource: HapticPipelineInputSource.LiveUdp,
        LastPacketAtUtc: DateTimeOffset.UtcNow,
        LastVehicleStateUpdateAtUtc: DateTimeOffset.UtcNow,
        PacketsObserved: 120,
        ParserSuccessCount: 120,
        ParserIgnoredCount: 0,
        ParserFailureCount: 0,
        VehicleStateUpdateCount: 12,
        RenderedBufferCount: 100,
        TelemetryAge: telemetryTimedOutMuted ? TimeSpan.FromMilliseconds(600) : TimeSpan.FromMilliseconds(20),
        TelemetryMuteTimeout: TimeSpan.FromMilliseconds(250),
        TelemetryTimedOutMuted: telemetryTimedOutMuted,
        IsMuted: false,
        EmergencyMute: false,
        LastPacketMessage: "Processed packet successfully.",
        LastVehicleStateMessage: "Updated vehicle state.",
        LastPipelineError: null,
        VehicleState: VehicleState.Empty with
        {
            Frame = new VehicleStateFrame(
                SessionUid: 123,
                SessionTime: 1.25f,
                FrameIdentifier: 10,
                OverallFrameIdentifier: 10,
                PlayerCarIndex: 0,
                Source: "F1 25|127.0.0.1")
        },
        Effects: default,
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
        Forwarding: new UdpTelemetryForwarderSnapshot(
            IsEnabled: false,
            DestinationCount: 0,
            EnabledDestinationCount: 0,
            InputPacketCount: 0,
            ForwardedDatagramCount: 0,
            ForwardedByteCount: 0,
            ErrorCount: 0,
            LastForwardedAtUtc: null,
            LastErrorMessage: null),
        PacketDiagnostics: [],
        Recording: new TelemetryRecordingSnapshot(
            IsRecording: true,
            FilePath: "session.hdrec",
            PacketCount: 20,
            LastPacketRelativeTime: TimeSpan.FromSeconds(1),
            LastErrorMessage: null,
            QueueCapacityPackets: 8192,
            QueuedPacketCount: 0,
            DroppedPacketCount: recordingDroppedPacketCount,
            RecordingIncomplete: recordingIncomplete,
            IncompleteReason: recordingIncomplete ? "Dropped packets." : null),
        Replay: new TelemetryReplaySnapshot(
            IsReplaying: false,
            SourceFilePath: null,
            PacketsReplayed: 0,
            StatusMessage: "Idle"))
        {
            OutputInterlock = outputInterlock,
            TelemetryFreshness = new(
                IsPresent: true,
                IsSameSession: true,
                IsNotFutureFrame: true,
                IsWithinFrameLag: true,
                IsWithinAge: !telemetryTimedOutMuted,
                Age: telemetryTimedOutMuted ? TimeSpan.FromMilliseconds(600) : TimeSpan.FromMilliseconds(20),
                FrameLag: 0),
            SessionFreshness = new(
                IsPresent: true,
                IsSameSession: true,
                IsNotFutureFrame: true,
                IsWithinFrameLag: true,
                IsWithinAge: true,
                Age: TimeSpan.FromMilliseconds(20),
                FrameLag: 0),
            RenderOverrunCount = renderOverrunCount,
            StaleFrameSilenceCount = telemetryTimedOutMuted ? 4 : 0,
            InterlockSilenceCount = outputInterlock.IsLatched ? 1 : 0
        };
    }

    private static AudioRuntimeDiagnosticsSnapshot CreateAudioDiagnostics(long underrunCount, long droppedBufferCount)
    {
        var outputStatus = new AudioOutputStatus(
            AudioOutputDeviceKind.Null,
            AudioOutputDeviceState.Stopped,
            "Null output",
            "Stopped",
            DeviceName: null,
            SampleRate: 48_000,
            ChannelCount: 2,
            BufferSize: 128,
            RequiresPhysicalHardware: false,
            IsManualDebugOnly: false,
            IsAvailable: true,
            SubmittedBufferCount: 20,
            DroppedBufferCount: droppedBufferCount,
            RenderCallbackCount: 20,
            BackendCallbackCount: 20,
            UnderrunCount: underrunCount,
            IsStreaming: true);

        return AudioRuntimeDiagnosticsSnapshot.Create(
            outputStatus,
            default,
            pipeline: null,
            new AudioTestBenchSnapshot(
                IsActive: false,
                SelectedSignal: AudioTestSignalKind.Silence,
                SelectedSignalName: "Silence",
                SampleRate: 48_000,
                ChannelCount: 2,
                BufferSize: 128,
                IsMuted: false,
                EmergencyMute: false,
                RenderedBufferCount: 0,
                RenderedFrameCount: 0,
                MixerPeakLevel: 0,
                OutputPeakLevel: 0,
                SanitizedSampleCount: 0,
                LimitedSampleCount: 0,
                ClippedSampleCount: 0,
                OutputKind: AudioOutputDeviceKind.Null,
                OutputDisplayName: "Null output",
                OutputState: AudioOutputDeviceState.Stopped,
                OutputRequiresPhysicalHardware: false,
                OutputIsManualDebugOnly: false,
                StatusMessage: "Stopped"));
    }
}

public sealed class RenderPathLoggingGuardrailTests
{
    [Fact]
    public void AudioCallbackDoesNotLog()
    {
        var repositoryRoot = FindRepositoryRoot();
        var offenders = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}HapticDrive.Asio.Audio{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}HapticDrive.Asio.Runtime{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("Console.WriteLine", StringComparison.Ordinal)
                || File.ReadAllText(path).Contains("Debug.WriteLine", StringComparison.Ordinal)
                || File.ReadAllText(path).Contains("Trace.WriteLine", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HapticDrive.Asio.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
