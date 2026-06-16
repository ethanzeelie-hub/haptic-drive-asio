using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.App;
using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;
using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class RoadTextureDiagnosticsReportTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DiagnosticsLinesIncludeRawSmoothedAndOutputIntensity()
    {
        var snapshot = CreateDiagnostics();

        var line = snapshot.ToDiagnosticsLines()[0];

        Assert.Contains("sharedRoadSignalEnabled", line);
        Assert.Contains("raw", line);
        Assert.Contains("smoothed", line);
        Assert.Contains("output", line);
        Assert.Contains("speed reference", line);
        Assert.Contains("grain/noise", line);
        Assert.True(snapshot.RawIntensity > 0f);
        Assert.True(snapshot.SmoothedIntensity > 0f);
        Assert.True(snapshot.OutputIntensity > 0f);
        Assert.True(snapshot.Bst1SpeedReferenceKph > 0f);
        Assert.True(snapshot.NoiseAmount > 0f);
    }

    [Fact]
    public void DiagnosticsSeparateSharedRoadSignalFromBst1RoadOutput()
    {
        var snapshot = CreateDiagnostics(profile: HapticDriveProfile.Default with
        {
            Effects = HapticDriveProfile.Default.Effects with
            {
                RoadTexture = HapticDriveProfile.Default.Effects.RoadTexture with
                {
                    IsEnabled = true,
                    Bst1OutputEnabled = false
                }
            }
        });

        var lines = snapshot.ToDiagnosticsLines();

        Assert.True(snapshot.SharedRoadSignalEnabled);
        Assert.False(snapshot.Bst1RoadEnabled);
        Assert.Contains("sharedRoadSignalEnabled True", lines[0]);
        Assert.Contains("bst1RoadOutputEnabled False", lines[1]);
    }

    [Fact]
    public void SafetyGainExplainsPostSafetyRoadEstimateWhenLimiterIsIdle()
    {
        var snapshot = CreateDiagnostics(
            profile: HapticDriveProfile.Default with
            {
                Mixer = HapticDriveProfile.Default.Mixer with { MasterGain = 1f },
                Safety = HapticDriveProfile.Default.Safety with { OutputGain = 0.5f }
            },
            roadPeak: 0.022f,
            roadRms: 0.010f);

        Assert.Equal(0.022f, snapshot.Bst1RoadPeakAfterMixer!.Value, precision: 6);
        Assert.Equal(0.011f, snapshot.Bst1RoadPeakAfterSafety!.Value, precision: 6);
        Assert.False(snapshot.RoadOnlyPostSafetyProofAvailable);
        Assert.Contains("estimated", snapshot.RoadOnlyProofNote);
    }

    [Fact]
    public void DiagnosticsDistinguishTotalOutputPeakFromRoadOnlyPeak()
    {
        var snapshot = CreateDiagnostics(roadPeak: 0.013f, totalOutputPeak: 0.25f);

        Assert.Equal("total output, not road-only", snapshot.OutputPeakScope);
        Assert.Equal(0.25f, snapshot.TotalOutputPeak, precision: 6);
        Assert.NotEqual(snapshot.TotalOutputPeak, snapshot.Bst1RoadPeakBeforeMixer);
    }

    [Fact]
    public void PhrRoadDiagnosticsExposeAttemptCommandAndSuppressionCounts()
    {
        var routedCommand = PHprCommand.Create(
            PHprModuleId.Brake,
            0.25d,
            32d,
            50,
            PHprCommandSource.RoadTexture,
            timestampUtc: BaseTime);
        var routeSnapshot = CreatePhprRoadSnapshot(
            routeAttempts: 4,
            routedCommands: 2,
            ignored: 1,
            intervalSuppressed: 3,
            safetyRejected: 1,
            staleTelemetry: 1,
            gearDucking: 2,
            commandRate: 1,
            lastCommand: routedCommand,
            lastResult: new PHprRoadVibrationRoutingResult(
                PHprRoadVibrationRoutingStatus.Routed,
                "P-HPR road vibration routed 1 command.",
                [new PHprRoadVibrationCommandResult(PHprModuleId.Brake, routedCommand, PHprCommandResult.Accepted(routedCommand, "accepted"))],
                CreateOutputSnapshot(routedCommand),
                BaseTime,
                0.42d));

        var snapshot = CreateDiagnostics(phprRoad: routeSnapshot, higherPrioritySuppressed: 5, inFlightSuppressed: 6);

        Assert.Equal(4, snapshot.RouteAttempts);
        Assert.Equal(2, snapshot.RoutedCommands);
        Assert.Equal(1, snapshot.IgnoredCount);
        Assert.Equal(3, snapshot.IntervalSuppressedCount);
        Assert.Equal(1, snapshot.SafetyRejectedCount);
        Assert.Equal(1, snapshot.StaleTelemetrySuppressedCount);
        Assert.Equal(2, snapshot.GearDuckingSuppressedCount);
        Assert.Equal(5, snapshot.HigherPriorityEffectSuppressedCount);
        Assert.Equal(6, snapshot.InFlightSuppressedCount);
        Assert.Equal(1, snapshot.CommandRateSuppressedCount);
        Assert.Equal("Brake", snapshot.LastCommandTarget);
        Assert.Equal(0.42d, snapshot.LastCommandRoadIntensity);
    }

    [Fact]
    public void DiagnosticsMarkHistoricalRoadRouteWhenRoadIsCurrentlyDisabled()
    {
        var command = PHprCommand.Create(PHprModuleId.Throttle, 0.25d, 32d, 50, PHprCommandSource.RoadTexture);
        var routeSnapshot = CreatePhprRoadSnapshot(
            lastCommand: command,
            lastResult: new PHprRoadVibrationRoutingResult(
                PHprRoadVibrationRoutingStatus.Routed,
                "previous route",
                [new PHprRoadVibrationCommandResult(PHprModuleId.Throttle, command, PHprCommandResult.Accepted(command, "accepted"))],
                CreateOutputSnapshot(command),
                BaseTime,
                0.2d),
            options: PHprRoadVibrationRouterOptions.Disabled);

        var snapshot = CreateDiagnostics(
            phprRoad: routeSnapshot,
            phprOptions: PHprRoadVibrationRouterOptions.Disabled);

        Assert.False(snapshot.PHprRoadEnabled);
        Assert.True(snapshot.LastRoadRoutedIsStaleHistorical);
    }

    [Fact]
    public void DisabledFlightRecorderIsSafeByDefault()
    {
        var recorder = DisabledRoadTextureFlightRecorder.Instance;

        recorder.Record(RoadTextureFlightRecord.From("test", CreateDiagnostics()));

        Assert.False(recorder.IsEnabled);
        Assert.Equal("disabled", recorder.LogPath);
        Assert.Null(recorder.LastFallbackStatus);
    }

    [Fact]
    public void FileFlightRecorderWritesJsonlWithoutHardware()
    {
        using var directory = new TemporaryDirectory();
        var recorder = new FileRoadTextureFlightRecorder(directory.Path);
        var diagnostics = CreateDiagnostics(flightRecorderActive: true, flightRecorderPath: recorder.LogPath);

        recorder.Record(RoadTextureFlightRecord.From("session-test", diagnostics));

        var line = File.ReadAllLines(recorder.LogPath).Single();
        Assert.Contains("\"SessionId\":\"session-test\"", line);
        Assert.Contains("\"EventName\":\"", line);
        Assert.Contains("\"RawIntensity\":", line);
        Assert.Contains("\"Bst1RoadPeakBeforeMixer\":", line);
        Assert.Contains("\"RouteAttempts\":", line);
    }

    [Fact]
    public void LocalValidationResultsIsIgnored()
    {
        var gitignore = File.ReadAllText(Path.Combine(FindRepoRoot(), ".gitignore"));

        Assert.Contains("local-validation-results/", gitignore);
    }

    private static RoadTextureDiagnosticSnapshot CreateDiagnostics(
        HapticDriveProfile? profile = null,
        float roadPeak = 0.022f,
        float roadRms = 0.010f,
        float totalOutputPeak = 0.011f,
        PHprRoadVibrationRoutingSnapshot? phprRoad = null,
        PHprRoadVibrationRouterOptions? phprOptions = null,
        long higherPrioritySuppressed = 0,
        long inFlightSuppressed = 0,
        bool flightRecorderActive = false,
        string flightRecorderPath = "disabled")
    {
        var resolvedProfile = profile ?? HapticDriveProfile.Default;
        var signal = new RoadTextureEvaluator().Evaluate(CreateVehicleState(), new RoadTextureEvaluationContext(BaseTime, true, true, false, false, null));
        var pipeline = CreatePipelineSnapshot(signal, roadPeak, roadRms, totalOutputPeak, resolvedProfile);
        var audio = AudioRuntimeDiagnosticsSnapshot.Create(
            pipeline.Output,
            pipeline.Effects,
            pipeline.Audio,
            CreateTestBenchSnapshot());
        var options = phprOptions ?? PHprRoadVibrationRouterOptions.EnabledDefault;
        return RoadTextureDiagnosticSnapshot.Create(
            pipeline,
            audio,
            resolvedProfile,
            options,
            phprRoad ?? CreatePhprRoadSnapshot(options: options),
            CreateRealOutputDiagnostics(),
            higherPrioritySuppressed,
            inFlightSuppressed,
            flightRecorderActive,
            flightRecorderPath);
    }

    private static HapticPipelineSnapshot CreatePipelineSnapshot(
        RoadTextureSignal signal,
        float roadPeak,
        float roadRms,
        float totalOutputPeak,
        HapticDriveProfile profile)
    {
        var road = new RoadTextureEffectSnapshot(
            IsEnabled: true,
            Bst1OutputEnabled: profile.Effects.RoadTexture.Bst1OutputEnabled ?? profile.Effects.RoadTexture.IsEnabled,
            IsActive: true,
            DominantSurfaceTypeId: signal.SurfaceTypeIds.RearLeft,
            DominantSurfaceName: signal.SurfaceName,
            CurrentFrequencyHz: signal.Bst1FrequencyHz,
            CurrentAmplitude: signal.OutputIntensity * profile.Effects.RoadTexture.Gain,
            SurfaceMix: signal.SurfaceMix,
            PeakLevel: roadPeak,
            Signal: signal,
            RmsLevel: roadRms);
        var effects = new HapticEffectEngineSnapshot(
            new EngineVibrationEffectSnapshot(false, false, null, 0f, 0f, 0f, 0f),
            new GearShiftEffectSnapshot(false, false, null, null, null, null, 0, 0f),
            new KerbEffectSnapshot(false, false, null, "None", 0f, 0f, 0, 0f),
            new ImpactEffectSnapshot(false, false, null, null, 0f, 0, 0f),
            road,
            new SlipEffectSnapshot(
                IsEnabled: false,
                WheelSlipEnabled: false,
                WheelLockEnabled: false,
                IsActive: false,
                CurrentSlipIntensity: 0f,
                CurrentLockIntensity: 0f,
                CurrentSlipRatio: 0f,
                CurrentSlipAngleRadians: 0f,
                CurrentMinimumWheelSpeedRatio: 1f,
                CurrentFrequencyHz: 0f,
                CurrentNoiseAmount: 0f,
                CurrentAmplitude: 0f,
                ActiveSource: "None",
                ActiveReason: "inactive",
                PeakLevel: 0f),
            ActiveEffectCount: 1,
            PeakLevel: roadPeak);
        var output = new AudioOutputStatus(
            AudioOutputDeviceKind.Null,
            AudioOutputDeviceState.Started,
            "Null Output",
            "test",
            DeviceName: null,
            SampleRate: 48_000,
            ChannelCount: 1,
            BufferSize: 256,
            RequiresPhysicalHardware: false,
            IsManualDebugOnly: false,
            IsAvailable: true);
        var audio = new AudioRenderPipelineSnapshot(
            IsRunning: true,
            IsMuted: false,
            EmergencyMute: false,
            ActiveSourceCount: 1,
            MixerPeakLevel: roadPeak * profile.Mixer.MasterGain,
            OutputPeakLevel: totalOutputPeak,
            SanitizedSampleCount: 0,
            LimitedSampleCount: 0,
            ClippedSampleCount: 0,
            Mixer: new AudioMixerSnapshot(true, false, false, 1, 1, profile.Mixer.MasterGain, roadPeak * profile.Mixer.MasterGain),
            Safety: new AudioSafetyProcessorSnapshot(false, profile.Safety.LimiterEnabled, profile.Safety.OutputGain, profile.Safety.OutputGainCeiling, roadPeak, totalOutputPeak, 0, 0, 0));

        return new HapticPipelineSnapshot(
            IsRunning: true,
            HapticPipelineInputSource.Replay,
            LastPacketAtUtc: BaseTime,
            LastVehicleStateUpdateAtUtc: BaseTime,
            PacketsObserved: 10,
            ParserSuccessCount: 10,
            ParserIgnoredCount: 0,
            ParserFailureCount: 0,
            VehicleStateUpdateCount: 10,
            RenderedBufferCount: 20,
            TelemetryAge: TimeSpan.FromMilliseconds(2),
            TelemetryMuteTimeout: TimeSpan.FromMilliseconds(500),
            TelemetryTimedOutMuted: false,
            IsMuted: false,
            EmergencyMute: false,
            LastPacketMessage: "test",
            LastVehicleStateMessage: "test",
            LastPipelineError: null,
            VehicleState: CreateVehicleState(),
            Effects: effects,
            Audio: audio,
            Output: output,
            ManualAsioHardwareTest: CreateManualSnapshot(),
            NullOutput: null,
            Forwarding: new UdpTelemetryForwarderSnapshot(false, 0, 0, 0, 0, 0, 0, null, null),
            PacketDiagnostics: [],
            Recording: new TelemetryRecordingSnapshot(false, null, 0, null, null),
            Replay: new TelemetryReplaySnapshot(true, "f1-25-test.hdrec", 10, "Replay active."));
    }

    private static PHprRoadVibrationRoutingSnapshot CreatePhprRoadSnapshot(
        long routeAttempts = 0,
        long routedCommands = 0,
        long ignored = 0,
        long intervalSuppressed = 0,
        long safetyRejected = 0,
        long staleTelemetry = 0,
        long gearDucking = 0,
        long commandRate = 0,
        PHprCommand? lastCommand = null,
        PHprRoadVibrationRoutingResult? lastResult = null,
        PHprRoadVibrationRouterOptions? options = null)
    {
        var resolvedOptions = options ?? PHprRoadVibrationRouterOptions.EnabledDefault;
        return new PHprRoadVibrationRoutingSnapshot(
            resolvedOptions,
            routeAttempts,
            EvaluationCount: routeAttempts,
            IgnoredEvaluationCount: ignored,
            RouteCount: routedCommands,
            SafetyRejectedCount: safetyRejected,
            IntervalSuppressedCount: intervalSuppressed,
            StaleTelemetrySuppressedCount: staleTelemetry,
            GearDuckingSuppressedCount: gearDucking,
            CommandRateSuppressedCount: commandRate,
            LastActive: true,
            LastIntensity01: lastResult?.Intensity01 ?? 0.1d,
            LastSignal: new RoadTextureEvaluator().Evaluate(CreateVehicleState(), new RoadTextureEvaluationContext(BaseTime, true, true, false, false, null)),
            LastCommand: lastCommand,
            LastOutputResult: lastCommand is null ? null : PHprCommandResult.Accepted(lastCommand, "accepted"),
            LastResult: lastResult,
            OutputSnapshot: CreateOutputSnapshot(lastCommand),
            FirstRouteAttemptAtUtc: routeAttempts > 0 ? BaseTime : null,
            LastRouteAttemptAtUtc: routeAttempts > 0 ? BaseTime.AddSeconds(Math.Max(1, routeAttempts)) : null,
            LastCommandRoutedAtUtc: lastCommand is null ? null : BaseTime,
            RuntimeState: lastCommand is null ? "Idle" : "Active",
            ActiveRoadModules: lastCommand?.TargetModule.ToString() ?? "none",
            LastRoadStartAtUtc: lastCommand is null ? null : BaseTime,
            LastRoadUpdateAtUtc: lastCommand is null ? null : BaseTime,
            LastRoadStopAtUtc: null,
            LastRoadStopReason: "none",
            RoadStopCommandCount: 0,
            WatchdogStopCount: 0,
            LastIgnoredReason: ignored > 0 ? "ignored for test" : null,
            LastError: null);
    }

    private static PHprOutputSnapshot CreateOutputSnapshot(PHprCommand? command)
    {
        return new PHprOutputSnapshot(
            IsMock: false,
            IsConnected: true,
            IsEmergencyStopActive: false,
            AcceptedCommandCount: command is null ? 0 : 1,
            RejectedCommandCount: 0,
            LastCommand: command,
            LastStatus: command is null ? null : PHprCommandStatus.Accepted,
            LastMessage: command is null ? null : "accepted",
            LastCommandUtc: command?.TimestampUtc,
            SafetyLimits: PHprSafetyLimits.Default,
            Mode: "test",
            BrakeAvailable: true,
            ThrottleAvailable: true,
            GeneratedFrameCount: 0);
    }

    private static PHprRealOutputDiagnostics CreateRealOutputDiagnostics()
    {
        return new PHprRealOutputDiagnostics(
            PHprRealOutputOptions.Disabled,
            new PHprDirectControlArmingState(false, false, null),
            CreateOutputSnapshot(null),
            PHprRealOutputConnectionDiagnostics.Closed,
            ReportWriteCount: 0,
            FailedReportWriteCount: 0,
            LastReportLength: 0,
            LastTarget: null,
            LastReportState: null,
            LastReportSummary: null,
            LastError: null,
            ActivePulse: false,
            LastStartSentAtUtc: null,
            LastStopSentAtUtc: BaseTime,
            LastStartReportTarget: null,
            LastStopReportTarget: null,
            LastStopResultStatus: PHprHidWriteStatus.Succeeded,
            LastStopResultMessage: "test stop",
            LastScheduledPulseDurationMs: null,
            LastScheduledStopDueAtUtc: null,
            LastEmergencyStopRequestedAtUtc: null,
            LastEmergencyStopResultStatus: null,
            LastEmergencyStopResultMessage: null,
            WatchdogStopAllCount: 0,
            LastWatchdogStopAllAtUtc: null,
            LastWatchdogStopAllMessage: null,
            BrakePulseGeneration: 0,
            ThrottlePulseGeneration: 0,
            LastPulseGeneration: 0,
            StaleStopIgnoredCount: 0,
            RetriggerCount: 0,
            BusyRejectedCount: 0,
            StaleOutputDroppedCount: 0,
            BrakeStopDueAtUtc: null,
            ThrottleStopDueAtUtc: null,
            LastStaleStopGeneration: 0,
            LastStaleStopTarget: null);
    }

    private static AudioTestBenchSnapshot CreateTestBenchSnapshot()
    {
        return new AudioTestBenchSnapshot(
            IsActive: false,
            SelectedSignal: AudioTestSignalKind.Silence,
            SelectedSignalName: "Silence",
            SampleRate: 48_000,
            ChannelCount: 1,
            BufferSize: 256,
            IsMuted: false,
            EmergencyMute: false,
            RenderedBufferCount: 0,
            RenderedFrameCount: 0,
            MixerPeakLevel: 0f,
            OutputPeakLevel: 0f,
            SanitizedSampleCount: 0,
            LimitedSampleCount: 0,
            ClippedSampleCount: 0,
            OutputKind: AudioOutputDeviceKind.Null,
            OutputDisplayName: "Null Output",
            OutputState: AudioOutputDeviceState.Stopped,
            OutputRequiresPhysicalHardware: false,
            OutputIsManualDebugOnly: false,
            StatusMessage: "test");
    }

    private static ManualAsioHardwareTestSnapshot CreateManualSnapshot()
    {
        return new ManualAsioHardwareTestSnapshot(
            IsActive: false,
            TestMode: "Manual",
            OutputMode: "Null",
            SelectedAsioDriver: "none",
            SelectedOutputChannel: null,
            AsioRunning: false,
            AsioArmed: false,
            AsioCallbackActive: false,
            HapticsRunning: false,
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
            LastError: null);
    }

    private static VehicleState CreateVehicleState()
    {
        var stamp = new VehicleStateStamp("test", 1, 1f, 1, 1, 0);
        return VehicleState.Empty with
        {
            Frame = new VehicleStateFrame(1, 1f, 1, 1, 0, "test"),
            Motion = new VehicleStateSample<VehicleMotionState>(
                new VehicleMotionState(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f),
                stamp),
            Session = new VehicleStateSample<VehicleSessionState>(
                new VehicleSessionState(0, 28, 22, 5, 5_000, 10, 1, 0, 0, 0, 0),
                stamp),
            Lap = new VehicleStateSample<VehicleLapState>(
                new VehicleLapState(0, 0, 100f, 100f, 1, 1, 0, 0, 1, 2, 0),
                stamp),
            Telemetry = new VehicleStateSample<VehicleTelemetryState>(
                new VehicleTelemetryState(
                    120,
                    0.4f,
                    0f,
                    0f,
                    0,
                    4,
                    9_500,
                    0,
                    0,
                    0,
                    90,
                    4,
                    Wheels<ushort>(300),
                    Wheels((byte)80),
                    Wheels((byte)80),
                    Wheels(22f),
                    Wheels((byte)0)),
                stamp),
            CarStatus = new VehicleStateSample<VehicleCarStatusState>(
                new VehicleCarStatusState(0, 0, 0, 55, 0, 20f, 100f, 10f, 12_000, 4_000, 8, 0, 0, 16, 16, 1, 0, 500_000f, 120_000f, 3_000_000f, 0, 0f, 0f, 0f, 0),
                stamp),
            MotionEx = new VehicleStateSample<VehicleMotionExState>(
                new VehicleMotionExState(
                    Wheels(0f),
                    Wheels(0f),
                    Wheels(0f),
                    Wheels(120 / 3.6f),
                    Wheels(0f),
                    Wheels(0f),
                    Wheels(0f),
                    Wheels(0f),
                    0.2f,
                    0f,
                    0f,
                    120 / 3.6f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    Wheels(8_000f),
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    Wheels(0f),
                    Wheels(0f)),
                stamp)
        };
    }

    private static VehicleWheelData<T> Wheels<T>(T value)
    {
        return new VehicleWheelData<T>(value, value, value, value);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".gitignore");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"road-diag-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
