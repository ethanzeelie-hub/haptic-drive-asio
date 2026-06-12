using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Runtime.Pipeline;

namespace HapticDrive.Asio.App.Tests;

public sealed class Stage18kBst1AsioLocalPulseTests
{
    [Fact]
    public void StartupDefaults_SelectValidatedAsioWhenMTrackDriverIsDiscoverable()
    {
        var selection = Bst1AsioStartupDefaults.Resolve([AsioAudioOutputDevice.PreferredDriverName]);

        Assert.Equal(AudioOutputDeviceKind.Asio, selection.OutputKind);
        Assert.Equal(AsioAudioOutputDevice.PreferredDriverName, selection.DriverName);
        Assert.Equal(1, selection.OutputChannel);
        Assert.True(selection.Armed);
        Assert.Contains("without starting output", selection.Message);
    }

    [Fact]
    public void StartupDefaults_FallBackToNullWhenMTrackDriverIsMissing()
    {
        var selection = Bst1AsioStartupDefaults.Resolve(["Other ASIO Driver"]);

        Assert.Equal(AudioOutputDeviceKind.Null, selection.OutputKind);
        Assert.Null(selection.DriverName);
        Assert.Null(selection.OutputChannel);
        Assert.False(selection.Armed);
    }

    [Fact]
    public void CompactStatus_ReadyWhenSelectedArmedAndStopped()
    {
        var status = Bst1AsioStatusFormatter.FormatCompact(Snapshot(
            running: false,
            callbackActive: false));

        Assert.Equal("ASIO READY - stream stopped", status);
        Assert.DoesNotContain("callback", status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("submitted", status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dropped", status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompactStatus_ActiveOnlyWhenStreamAndCallbackAreActive()
    {
        var runningOnly = Bst1AsioStatusFormatter.FormatCompact(Snapshot(
            running: true,
            callbackActive: false));
        var active = Bst1AsioStatusFormatter.FormatCompact(Snapshot(
            running: true,
            callbackActive: true));

        Assert.Contains("ASIO READY", runningOnly);
        Assert.Contains("ASIO ACTIVE", active);
    }

    [Fact]
    public void DetailedStatus_KeepsAdvancedAsioDiagnostics()
    {
        var text = Bst1AsioStatusFormatter.FormatDetailed(Snapshot(
            running: true,
            callbackActive: true,
            lastPulseUsedAsio: true,
            lastManualPulseUsedAsio: true,
            outputTrimPercent: 200f,
            effectivePreLimiterAmplitude: 1.0f,
            effectivePostLimiterAmplitude: 0.25f));

        Assert.Contains("rendered callbacks", text);
        Assert.Contains("submitted frames", text);
        Assert.Contains("dropped frames", text);
        Assert.Contains("Last manual pulse used ASIO: YES", text);
        Assert.Contains("output trim 200%", text);
    }

    [Fact]
    public void SelectChannel1_IsPureSelectionAndNeverPulseRequest()
    {
        var selection = Bst1AsioChannelSelection.Select(1, AudioOutputDeviceKind.Asio);

        Assert.Equal(1, selection.SelectedChannel);
        Assert.True(selection.ShouldRebuildPipeline);
        Assert.False(selection.ShouldStartPulse);
        Assert.Contains("selected channel 1", selection.Message);
    }

    [Fact]
    public void LastPulseCompactStatus_ReportsSuccessOrQueueFullWithoutVerboseDiagnostics()
    {
        var success = Bst1AsioStatusFormatter.FormatLastPulseCompact(Snapshot(lastPulseUsedAsio: true));
        var queueFull = Bst1AsioStatusFormatter.FormatLastPulseCompact(Snapshot(
            blocked: true,
            blockedReason: "Native ASIO backend queue is full; buffer dropped."));

        Assert.Equal("Last BST-1 pulse: succeeded", success);
        Assert.Equal("Last BST-1 pulse blocked: queue full", queueFull);
    }

    private static ManualAsioHardwareTestSnapshot Snapshot(
        bool running = false,
        bool callbackActive = false,
        bool lastPulseUsedAsio = false,
        bool lastManualPulseUsedAsio = false,
        bool blocked = false,
        string? blockedReason = null,
        float? outputTrimPercent = null,
        float? effectivePreLimiterAmplitude = null,
        float? effectivePostLimiterAmplitude = null)
    {
        return new ManualAsioHardwareTestSnapshot(
            IsActive: false,
            TestMode: "ASIO Hardware",
            OutputMode: AudioOutputDeviceKind.Asio.ToString(),
            SelectedAsioDriver: AsioAudioOutputDevice.PreferredDriverName,
            SelectedOutputChannel: 1,
            AsioRunning: running,
            AsioArmed: true,
            AsioCallbackActive: callbackActive,
            HapticsRunning: false,
            EmergencyMute: false,
            NormalMute: false,
            OutputPeakLevel: effectivePostLimiterAmplitude ?? 0f,
            FramesSubmitted: 512,
            FramesRendered: 512,
            RenderCallbackCount: 1,
            SubmittedFrameCount: 512,
            DroppedFrameCount: 0,
            BackendCallbackCount: callbackActive ? 1 : 0,
            LastPulseUsedAsio: lastPulseUsedAsio,
            LastManualPulseUsedAsio: lastManualPulseUsedAsio,
            LastGearPulseUsedAsio: false,
            LastPulseBlocked: blocked,
            LimiterApplied: false,
            PulseGenerationId: 1,
            StaleStopIgnoredCount: 0,
            BlockedReason: blockedReason,
            LastTestSignal: "50 Hz sine",
            LastTestDuration: TimeSpan.FromMilliseconds(45),
            LastStrengthPercent: 50f,
            LastOutputTrimPercent: outputTrimPercent,
            LastEffectivePreLimiterAmplitude: effectivePreLimiterAmplitude,
            LastEffectivePostLimiterAmplitude: effectivePostLimiterAmplitude,
            LastFrequencyHz: 50f,
            LastDurationMs: 45,
            LastSource: "manual test",
            LastDurationMode: "manual",
            ManualPulsePeak: effectivePostLimiterAmplitude ?? 0f,
            FlightRecorderPath: "disabled",
            LastError: null);
    }
}
