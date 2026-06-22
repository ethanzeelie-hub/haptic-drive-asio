using System.IO;
using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Runtime.Safety;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Routing;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App.Tests;

public sealed class OutputSafetyParticipantAppTests
{
    [Fact]
    public async Task DirectPhprParticipant_RevokesAuthorizationOnTrip()
    {
        var runtime = new FakeDirectRuntime();
        var authorization = new PHprSessionWriteAuthorization();
        Assert.True(authorization.TryAuthorize(PHprControlledWriteApproval.Phrase));
        var participant = new DirectPhprOutputSafetyParticipant(runtime, authorization);

        await participant.SilenceAsync(TripSnapshot(), CancellationToken.None);

        Assert.False(authorization.Current.IsAuthorized);
        Assert.Equal(1, runtime.EmergencyStopCallCount);
    }

    [Fact]
    public async Task ApplicationSafetyController_ResetBlockedByParticipantFault()
    {
        var controller = new ApplicationSafetyController(new SafetyStateViewModel());
        var interlock = new OutputInterlock();
        var participant = new BlockingParticipant();
        await using var supervisor = new OutputInterlockSupervisor(interlock, [participant]);

        var blocked = controller.TryBuildResetBlockedMessage(supervisor, out var message);

        Assert.True(blocked);
        Assert.Contains("fault still latched", message, StringComparison.Ordinal);
        Assert.Equal(message, controller.ViewModel.Message);
    }

    [Fact]
    public void UiCannotBypassInterlockForAudioOrPhpr()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));
        var interlockSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.OutputInterlock.cs"));

        Assert.Contains("new OutputInterlockSupervisor(", source, StringComparison.Ordinal);
        Assert.Contains("_applicationSafetyController.TryBuildResetBlockedMessage(", interlockSource, StringComparison.Ordinal);
        Assert.Contains("new AudioOutputSafetyParticipant(() => _hapticPipeline)", source, StringComparison.Ordinal);
        Assert.Contains("new DirectPhprOutputSafetyParticipant(_phprDirectRuntime, _phprWriteAuthorization)", source, StringComparison.Ordinal);
    }

    private static OutputInterlockSnapshot TripSnapshot()
    {
        return new OutputInterlockSnapshot(
            IsLatched: true,
            Reason: OutputInterlockReason.UserEmergencyMute,
            Message: "test trip",
            ChangedAtUtc: DateTimeOffset.UtcNow,
            Generation: 1);
    }

    private sealed class BlockingParticipant : IOutputSafetyParticipant
    {
        public string Name => "blocked participant";

        public OutputSafetyParticipantSnapshot Current { get; } = new(
            "blocked participant",
            IsSilent: false,
            HasFault: false,
            "fault still latched");

        public ValueTask SilenceAsync(OutputInterlockSnapshot interlock, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public bool CanReset(out string blocker)
        {
            blocker = "fault still latched";
            return false;
        }

        public void OnInterlockReset(OutputInterlockSnapshot interlock)
        {
        }
    }

    private sealed class FakeDirectRuntime : IPHprDirectRuntime
    {
        public int EmergencyStopCallCount { get; private set; }

        public void Configure(PHprDirectRuntimeEnvironment environment)
        {
        }

        public PHprDirectRuntimeSnapshot GetSnapshot()
        {
            return new PHprDirectRuntimeSnapshot(
                PHprDirectRuntimeState.Idle,
                StartupCleanupAttempted: false,
                StartupCleanupSucceeded: false,
                StartupCleanupFailed: false,
                UncleanShutdownMarkerExists: false,
                DisabledAfterUncleanShutdown: false,
                DirectReady: false,
                BlockedReason: string.Empty,
                HardwareBelievedActive: false,
                PendingStopCount: 0,
                PulseId: 0,
                new PHprDirectSharedPathProof("a", "a", "b", "b", "c", "c", "d", "d"),
                new PHprDirectLatencySnapshot(),
                LastErrorCategory: null,
                LastErrorMessage: null,
                FlightRecorderPath: string.Empty,
                UncleanShutdownMarkerPath: string.Empty);
        }

        public ValueTask InitializeStartupCleanupAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<PhprDeviceCardPulseResult> SendManualPulseAsync(
            PHprModuleId moduleId,
            PHprRealGearPulseSettings settings,
            PHprSafetyContext safetyContext,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<string> RouteBenchAsync(
            PaddleGearBenchTestResult benchResult,
            PaddleGearBenchTestOptions options,
            WheelPaddleInputSnapshot paddleSnapshot,
            Func<PHprModuleId, PHprRealGearPulseSettings> deviceCardSettings,
            PHprSafetyContext safetyContext,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<PHprDirectStopAllResult> StopAllAsync(string reason, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask EmergencyStopAsync(string reason, CancellationToken cancellationToken = default)
        {
            EmergencyStopCallCount++;
            return ValueTask.CompletedTask;
        }

        public void ClearEmergencyStop()
        {
        }

        public void HandleUnhandledException(string reason, Exception? exception)
        {
        }

        public ValueTask HandlePaddleInputExceptionAsync(
            string reason,
            Exception exception,
            bool stopAllIfPulseMayHaveStarted,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }
}
