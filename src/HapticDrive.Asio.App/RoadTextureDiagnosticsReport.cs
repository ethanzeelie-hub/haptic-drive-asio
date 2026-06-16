using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal sealed record RoadTextureDiagnosticSnapshot
{
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public long MonotonicTimestamp { get; init; } = Stopwatch.GetTimestamp();

    public string Source { get; init; } = "Unknown";

    public bool HapticsRunning { get; init; }

    public bool EmergencyMute { get; init; }

    public bool PhprEmergencyStop { get; init; }

    public string ReplaySource { get; init; } = "none";

    public double? TelemetryAgeMs { get; init; }

    public bool RoadTelemetryFresh { get; init; }

    public bool RoadDrivingArmed { get; init; }

    public ushort SpeedKph { get; init; }

    public float SpeedScale { get; init; }

    public string SurfaceTypes { get; init; } = "0/0/0/0";

    public string SurfaceClass { get; init; } = RoadTextureSurfaceClass.None.ToString();

    public string SurfaceName { get; init; } = "None";

    public float SurfaceMix { get; init; }

    public float SuspensionAccelerationContribution { get; init; }

    public float WheelVertForceContribution { get; init; }

    public float VerticalGContribution { get; init; }

    public float RoughnessMetric { get; init; }

    public float RawIntensity { get; init; }

    public float SmoothedIntensity { get; init; }

    public float OutputIntensity { get; init; }

    public float Bst1FrequencyHz { get; init; }

    public float PHprFrequencyHz { get; init; }

    public float NoiseAmount { get; init; }

    public float Bst1SpeedReferenceKph { get; init; }

    public bool GearDuckingActive { get; init; }

    public float DuckingGain { get; init; } = 1f;

    public string SuppressionReason { get; init; } = "none";

    public bool SharedRoadSignalEnabled { get; init; }

    public bool Bst1RoadEnabled { get; init; }

    public float Bst1RoadGain { get; init; }

    public float Bst1RoadPeakBeforeMixer { get; init; }

    public float Bst1RoadRmsBeforeMixer { get; init; }

    public float? Bst1RoadPeakAfterMixer { get; init; }

    public float? Bst1RoadRmsAfterMixer { get; init; }

    public float? Bst1RoadPeakAfterSafety { get; init; }

    public float? Bst1RoadRmsAfterSafety { get; init; }

    public bool RoadOnlyPostSafetyProofAvailable { get; init; }

    public string RoadOnlyProofNote { get; init; } = "unavailable";

    public string OutputPeakScope { get; init; } = "total output";

    public float TotalMixerPeak { get; init; }

    public float TotalOutputPeak { get; init; }

    public float SafetyOutputGain { get; init; }

    public float ConservativeCeiling { get; init; }

    public bool LimiterEnabled { get; init; }

    public int LimitedSamples { get; init; }

    public int ClippedSamples { get; init; }

    public bool PHprRoadEnabled { get; init; }

    public bool BrakeRoadEnabled { get; init; }

    public bool ThrottleRoadEnabled { get; init; }

    public double BrakeRoadOutputScale { get; init; }

    public double ThrottleRoadOutputScale { get; init; }

    public long RouteAttempts { get; init; }

    public double RouteAttemptsPerSecond { get; init; }

    public long RoutedCommands { get; init; }

    public double RoutedCommandsPerSecond { get; init; }

    public long IgnoredCount { get; init; }

    public string IgnoredReason { get; init; } = "none";

    public long IntervalSuppressedCount { get; init; }

    public long SafetyRejectedCount { get; init; }

    public long StaleTelemetrySuppressedCount { get; init; }

    public long GearDuckingSuppressedCount { get; init; }

    public long HigherPriorityEffectSuppressedCount { get; init; }

    public long InFlightSuppressedCount { get; init; }

    public long CommandRateSuppressedCount { get; init; }

    public double? LastCommandAgeMs { get; init; }

    public string LastCommandTarget { get; init; } = "none";

    public double? LastCommandStrength { get; init; }

    public double? LastCommandFrequencyHz { get; init; }

    public int? LastCommandDurationMs { get; init; }

    public double? LastCommandRoadIntensity { get; init; }

    public string LastCommandReason { get; init; } = "none";

    public string LastStopReason { get; init; } = "none";

    public double? LastStopAgeMs { get; init; }

    public bool LastRoadRoutedIsStaleHistorical { get; init; }

    public string PHprRoadRuntimeState { get; init; } = "Idle";

    public double UpdateCadenceMs { get; init; }

    public double HoldTimeoutMs { get; init; }

    public string ActiveRoadModules { get; init; } = "none";

    public double? LastRoadStartAgeMs { get; init; }

    public double? LastRoadUpdateAgeMs { get; init; }

    public double? LastRoadStopAgeMs { get; init; }

    public string LastRoadStopReason { get; init; } = "none";

    public long RoadStopCommandCount { get; init; }

    public long WatchdogStopCount { get; init; }

    public bool FlightRecorderActive { get; init; }

    public string FlightRecorderPath { get; init; } = "disabled";

    public string RecommendedEventType
    {
        get
        {
            if (!Bst1RoadEnabled && !PHprRoadEnabled)
            {
                return SharedRoadSignalEnabled ? "road-outputs-disabled" : "shared-road-signal-disabled";
            }

            if (!HapticsRunning)
            {
                return "haptics-stopped-road-muted";
            }

            if (!RoadTelemetryFresh)
            {
                return "telemetry-stale-road-muted";
            }

            if (GearDuckingActive)
            {
                return "gear-ducking-active";
            }

            if (PHprRoadEnabled && RoutedCommands > 0)
            {
                return "pHpr-road-command-routed";
            }

            return "road-signal-evaluated";
        }
    }

    public static RoadTextureDiagnosticSnapshot Create(
        HapticPipelineSnapshot pipeline,
        AudioRuntimeDiagnosticsSnapshot audio,
        HapticDriveProfile profile,
        PHprRoadVibrationRouterOptions phprRoadOptions,
        PHprRoadVibrationRoutingSnapshot phprRoad,
        PHprRealOutputDiagnostics realOutput,
        long higherPriorityEffectSuppressedCount,
        long inFlightSuppressedCount,
        bool flightRecorderActive,
        string flightRecorderPath)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(phprRoadOptions);
        ArgumentNullException.ThrowIfNull(phprRoad);
        ArgumentNullException.ThrowIfNull(realOutput);

        var road = pipeline.Effects.RoadTexture;
        var signal = road.Signal;
        var mixerGain = profile.Mixer.MasterGain;
        var safetyGain = audio.Pipeline?.Safety.OutputGain ?? profile.Safety.OutputGain;
        var limiterEnabled = audio.Pipeline?.Safety.LimiterEnabled ?? profile.Safety.LimiterEnabled;
        var ceiling = audio.Pipeline?.Safety.OutputGainCeiling ?? profile.Safety.OutputGainCeiling;
        var roadAfterMixerPeak = road.PeakLevel * mixerGain;
        var roadAfterMixerRms = road.RmsLevel * mixerGain;
        var approximationAvailable = audio.Pipeline is not null && audio.LimitedSampleCount == 0 && audio.ClippedSampleCount == 0;
        var lastCommand = phprRoad.LastCommand;
        var now = DateTimeOffset.UtcNow;
        var lastResult = phprRoad.LastResult;
        var lastCommandAt = phprRoad.LastCommandRoutedAtUtc ?? lastCommand?.TimestampUtc;

        return new RoadTextureDiagnosticSnapshot
        {
            CapturedAtUtc = now,
            Source = pipeline.InputSource.ToString(),
            HapticsRunning = pipeline.IsRunning,
            EmergencyMute = pipeline.EmergencyMute,
            PhprEmergencyStop = realOutput.Output.IsEmergencyStopActive,
            ReplaySource = string.IsNullOrWhiteSpace(pipeline.Replay.SourceFilePath)
                ? "none"
                : Path.GetFileName(pipeline.Replay.SourceFilePath),
            TelemetryAgeMs = pipeline.TelemetryAge?.TotalMilliseconds,
            RoadTelemetryFresh = signal.TelemetryFresh,
            RoadDrivingArmed = signal.DrivingArmed,
            SpeedKph = signal.SpeedKph,
            SpeedScale = signal.SpeedScale,
            SurfaceTypes = FormatWheels(signal.SurfaceTypeIds),
            SurfaceClass = signal.SurfaceClass.ToString(),
            SurfaceName = signal.SurfaceName,
            SurfaceMix = signal.SurfaceMix,
            SuspensionAccelerationContribution = signal.SuspensionAccelerationContribution,
            WheelVertForceContribution = signal.WheelVertForceContribution,
            VerticalGContribution = signal.VerticalGContribution,
            RoughnessMetric = signal.RoughnessMetric,
            RawIntensity = signal.RawIntensity,
            SmoothedIntensity = signal.SmoothedIntensity,
            OutputIntensity = signal.OutputIntensity,
            Bst1FrequencyHz = signal.Bst1FrequencyHz,
            PHprFrequencyHz = signal.PHprFrequencyHz,
            NoiseAmount = signal.NoiseAmount,
            Bst1SpeedReferenceKph = profile.Effects.RoadTexture.FullIntensitySpeedKph,
            GearDuckingActive = signal.GearDuckingActive,
            DuckingGain = signal.DuckingGain,
            SuppressionReason = signal.SuppressedReason ?? "none",
            SharedRoadSignalEnabled = profile.Effects.RoadTexture.IsEnabled,
            Bst1RoadEnabled = road.Bst1OutputEnabled,
            Bst1RoadGain = profile.Effects.RoadTexture.Gain,
            Bst1RoadPeakBeforeMixer = road.PeakLevel,
            Bst1RoadRmsBeforeMixer = road.RmsLevel,
            Bst1RoadPeakAfterMixer = roadAfterMixerPeak,
            Bst1RoadRmsAfterMixer = roadAfterMixerRms,
            Bst1RoadPeakAfterSafety = approximationAvailable ? roadAfterMixerPeak * safetyGain : null,
            Bst1RoadRmsAfterSafety = approximationAvailable ? roadAfterMixerRms * safetyGain : null,
            RoadOnlyPostSafetyProofAvailable = false,
            RoadOnlyProofNote = approximationAvailable
                ? "estimated from BST-1 road pre-mixer peak/RMS times master gain and safety gain; output peak remains total output"
                : "unavailable because limiter/clipping or missing pipeline data prevents a clean estimate",
            OutputPeakScope = "total output, not road-only",
            TotalMixerPeak = audio.MixerPeakLevel,
            TotalOutputPeak = audio.OutputPeakLevel,
            SafetyOutputGain = safetyGain,
            ConservativeCeiling = ceiling,
            LimiterEnabled = limiterEnabled,
            LimitedSamples = audio.LimitedSampleCount,
            ClippedSamples = audio.ClippedSampleCount,
            PHprRoadEnabled = phprRoadOptions.IsEnabled,
            BrakeRoadEnabled = phprRoadOptions.IsEnabled && phprRoadOptions.Brake.IsEnabled,
            ThrottleRoadEnabled = phprRoadOptions.IsEnabled && phprRoadOptions.Throttle.IsEnabled,
            BrakeRoadOutputScale = phprRoadOptions.Brake.Normalize().Strength01,
            ThrottleRoadOutputScale = phprRoadOptions.Throttle.Normalize().Strength01,
            RouteAttempts = phprRoad.RouteAttemptCount,
            RouteAttemptsPerSecond = CalculateRate(phprRoad.RouteAttemptCount, phprRoad.FirstRouteAttemptAtUtc, phprRoad.LastRouteAttemptAtUtc),
            RoutedCommands = phprRoad.RouteCount,
            RoutedCommandsPerSecond = CalculateRate(phprRoad.RouteCount, phprRoad.FirstRouteAttemptAtUtc, phprRoad.LastRouteAttemptAtUtc),
            IgnoredCount = phprRoad.IgnoredEvaluationCount,
            IgnoredReason = phprRoad.LastIgnoredReason ?? "none",
            IntervalSuppressedCount = phprRoad.IntervalSuppressedCount,
            SafetyRejectedCount = phprRoad.SafetyRejectedCount,
            StaleTelemetrySuppressedCount = phprRoad.StaleTelemetrySuppressedCount,
            GearDuckingSuppressedCount = phprRoad.GearDuckingSuppressedCount,
            HigherPriorityEffectSuppressedCount = higherPriorityEffectSuppressedCount,
            InFlightSuppressedCount = inFlightSuppressedCount,
            CommandRateSuppressedCount = phprRoad.CommandRateSuppressedCount,
            LastCommandAgeMs = lastCommandAt is null ? null : Math.Max(0d, (now - lastCommandAt.Value).TotalMilliseconds),
            LastCommandTarget = lastCommand?.TargetModule.ToString() ?? "none",
            LastCommandStrength = lastCommand?.Strength01,
            LastCommandFrequencyHz = lastCommand?.FrequencyHz,
            LastCommandDurationMs = lastCommand?.DurationMs,
            LastCommandRoadIntensity = lastResult?.Intensity01,
            LastCommandReason = lastResult?.Message ?? "none",
            LastStopReason = realOutput.LastStopResultMessage ?? "none",
            LastStopAgeMs = realOutput.LastStopSentAtUtc is null ? null : Math.Max(0d, (now - realOutput.LastStopSentAtUtc.Value).TotalMilliseconds),
            LastRoadRoutedIsStaleHistorical = !phprRoadOptions.IsEnabled && (lastResult?.WasRouted == true || lastCommand is not null),
            PHprRoadRuntimeState = phprRoad.RuntimeState,
            UpdateCadenceMs = phprRoad.Options.Normalize().MinimumRouteInterval.TotalMilliseconds,
            HoldTimeoutMs = phprRoad.Options.Normalize().HoldTimeout.TotalMilliseconds,
            ActiveRoadModules = phprRoad.ActiveRoadModules,
            LastRoadStartAgeMs = phprRoad.LastRoadStartAtUtc is null ? null : Math.Max(0d, (now - phprRoad.LastRoadStartAtUtc.Value).TotalMilliseconds),
            LastRoadUpdateAgeMs = phprRoad.LastRoadUpdateAtUtc is null ? null : Math.Max(0d, (now - phprRoad.LastRoadUpdateAtUtc.Value).TotalMilliseconds),
            LastRoadStopAgeMs = phprRoad.LastRoadStopAtUtc is null ? null : Math.Max(0d, (now - phprRoad.LastRoadStopAtUtc.Value).TotalMilliseconds),
            LastRoadStopReason = phprRoad.LastRoadStopReason,
            RoadStopCommandCount = phprRoad.RoadStopCommandCount,
            WatchdogStopCount = phprRoad.WatchdogStopCount,
            FlightRecorderActive = flightRecorderActive,
            FlightRecorderPath = flightRecorderPath
        };
    }

    public IReadOnlyList<string> ToDiagnosticsLines()
    {
        return
        [
            $"Road signal: sharedRoadSignalEnabled {SharedRoadSignalEnabled}; telemetryFresh {RoadTelemetryFresh}; drivingArmed {RoadDrivingArmed}; speed {SpeedKph} km/h; speedScale {SpeedScale:0.000}; speed reference {Bst1SpeedReferenceKph:0} km/h; surfaces {SurfaceTypes}; {SurfaceClass}/{SurfaceName}; surface mix/base {SurfaceMix:0.000}; suspension {SuspensionAccelerationContribution:0.000}; wheel force {WheelVertForceContribution:0.000}; vertical G {VerticalGContribution:0.000}; roughness {RoughnessMetric:0.000}; raw {RawIntensity:0.000}; smoothed {SmoothedIntensity:0.000}; output {OutputIntensity:0.000}; BST-1 {Bst1FrequencyHz:0.0} Hz; P-HPR {PHprFrequencyHz:0.0} Hz; grain/noise {NoiseAmount:P0}; gear ducking {GearDuckingActive}; ducking gain {DuckingGain:0.000}; suppression {SuppressionReason}.",
            $"BST-1 road proof: bst1RoadOutputEnabled {Bst1RoadEnabled}; gain {Bst1RoadGain:P0}; pre-mixer peak {Bst1RoadPeakBeforeMixer:0.000}/RMS {Bst1RoadRmsBeforeMixer:0.000}; after-mixer peak {FormatNullable(Bst1RoadPeakAfterMixer)}/RMS {FormatNullable(Bst1RoadRmsAfterMixer)}; post-safety estimate peak {FormatNullable(Bst1RoadPeakAfterSafety)}/RMS {FormatNullable(Bst1RoadRmsAfterSafety)}; road-only post-safety proof {RoadOnlyPostSafetyProofAvailable}; note {RoadOnlyProofNote}; total mixer peak {TotalMixerPeak:0.000}; total output peak {TotalOutputPeak:0.000}; output scope {OutputPeakScope}; safety gain {SafetyOutputGain:P0}; ceiling {ConservativeCeiling:0.00}; limiter {LimiterEnabled}; limited {LimitedSamples:N0}; clipped {ClippedSamples:N0}.",
            $"P-HPR road proof: enabled {PHprRoadEnabled}; brake {BrakeRoadEnabled} scale {BrakeRoadOutputScale:P0}; throttle {ThrottleRoadEnabled} scale {ThrottleRoadOutputScale:P0}; runtime {PHprRoadRuntimeState}; cadence {UpdateCadenceMs:0} ms; hold {HoldTimeoutMs:0} ms; active modules {ActiveRoadModules}; attempts {RouteAttempts:N0} ({RouteAttemptsPerSecond:0.00}/s); routed commands {RoutedCommands:N0} ({RoutedCommandsPerSecond:0.00}/s); ignored {IgnoredCount:N0}; ignored reason {IgnoredReason}; interval suppressed {IntervalSuppressedCount:N0}; safety rejected {SafetyRejectedCount:N0}; stale telemetry {StaleTelemetrySuppressedCount:N0}; gear ducking suppressed {GearDuckingSuppressedCount:N0}; higher priority suppressed {HigherPriorityEffectSuppressedCount:N0}; in-flight suppressed {InFlightSuppressedCount:N0}; command-rate suppressed {CommandRateSuppressedCount:N0}; last target {LastCommandTarget}; age {FormatNullable(LastCommandAgeMs)} ms; strength {FormatNullable(LastCommandStrength)}; freq {FormatNullable(LastCommandFrequencyHz)} Hz; duration {LastCommandDurationMs?.ToString("N0") ?? "none"} ms; intensity {FormatNullable(LastCommandRoadIntensity)}; reason {LastCommandReason}; road start age {FormatNullable(LastRoadStartAgeMs)} ms; road update age {FormatNullable(LastRoadUpdateAgeMs)} ms; road stop age {FormatNullable(LastRoadStopAgeMs)} ms; road stop reason {LastRoadStopReason}; stop commands {RoadStopCommandCount:N0}; watchdog stops {WatchdogStopCount:N0}; stop {LastStopReason}; stop age {FormatNullable(LastStopAgeMs)} ms; stale historical {LastRoadRoutedIsStaleHistorical}.",
            $"Road flight recorder: active {FlightRecorderActive}; path {FlightRecorderPath}; source {Source}; replay {ReplaySource}; telemetry age {(TelemetryAgeMs is null ? "none" : $"{TelemetryAgeMs:0} ms")}; recommended event {RecommendedEventType}."
        ];
    }

    private static double CalculateRate(long count, DateTimeOffset? first, DateTimeOffset? last)
    {
        if (count <= 0 || first is null || last is null)
        {
            return 0d;
        }

        var seconds = Math.Max(1d, (last.Value - first.Value).TotalSeconds);
        return count / seconds;
    }

    private static string FormatWheels<T>(VehicleWheelData<T> wheels)
    {
        return $"{wheels.FrontLeft}/{wheels.FrontRight}/{wheels.RearLeft}/{wheels.RearRight}";
    }

    private static string FormatNullable(double? value)
    {
        return value is null ? "none" : value.Value.ToString("0.000");
    }

    private static string FormatNullable(float? value)
    {
        return value is null ? "none" : value.Value.ToString("0.000");
    }
}

internal interface IRoadTextureFlightRecorder
{
    string LogPath { get; }

    bool IsEnabled { get; }

    string? LastFallbackStatus { get; }

    void Record(RoadTextureFlightRecord record);
}

internal sealed class DisabledRoadTextureFlightRecorder : IRoadTextureFlightRecorder
{
    public static DisabledRoadTextureFlightRecorder Instance { get; } = new();

    private DisabledRoadTextureFlightRecorder()
    {
    }

    public string LogPath => "disabled";

    public bool IsEnabled => false;

    public string? LastFallbackStatus => null;

    public void Record(RoadTextureFlightRecord record)
    {
    }
}

internal sealed class FileRoadTextureFlightRecorder : IRoadTextureFlightRecorder
{
    private const long MaxBytes = 1024 * 1024;
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public FileRoadTextureFlightRecorder(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        LogPath = Path.Combine(directory, "road-texture-flight-recorder.jsonl");
    }

    public string LogPath { get; }

    public bool IsEnabled => true;

    public string? LastFallbackStatus { get; private set; }

    public void Record(RoadTextureFlightRecord record)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                RotateIfNeeded();
                var json = JsonSerializer.Serialize(record, _jsonOptions);
                using var stream = new FileStream(
                    LogPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 4096);
                using var writer = new StreamWriter(stream);
                writer.WriteLine(json);
                LastFallbackStatus = null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LastFallbackStatus = $"{DateTimeOffset.UtcNow:O} road-recorder-failed {ex.GetType().Name}: {ex.Message}";
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(LogPath))
        {
            return;
        }

        var info = new FileInfo(LogPath);
        if (info.Length < MaxBytes)
        {
            return;
        }

        var archive = LogPath + ".1";
        if (File.Exists(archive))
        {
            File.Delete(archive);
        }

        File.Move(LogPath, archive);
    }
}

internal sealed record RoadTextureFlightRecord
{
    public string SessionId { get; init; } = string.Empty;

    public string EventName { get; init; } = "road-signal-evaluated";

    public DateTimeOffset WallClockUtc { get; init; } = DateTimeOffset.UtcNow;

    public long MonotonicTimestamp { get; init; } = Stopwatch.GetTimestamp();

    public int ThreadId { get; init; } = Environment.CurrentManagedThreadId;

    public RoadTextureDiagnosticSnapshot Diagnostics { get; init; } = new();

    public static RoadTextureFlightRecord From(string sessionId, RoadTextureDiagnosticSnapshot diagnostics)
    {
        return new RoadTextureFlightRecord
        {
            SessionId = sessionId,
            EventName = diagnostics.RecommendedEventType,
            WallClockUtc = diagnostics.CapturedAtUtc,
            MonotonicTimestamp = diagnostics.MonotonicTimestamp,
            Diagnostics = diagnostics
        };
    }
}
