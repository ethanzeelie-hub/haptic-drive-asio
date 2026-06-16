using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Actuation.Shift;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Routing;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal sealed record Bst1PaddleGearPulseRouteSettings(
    bool IsEnabled,
    float StrengthPercent,
    float OutputTrimPercent,
    float FrequencyHz,
    int DurationMs,
    string DurationMode);

internal sealed record PaddleInputRoutingHandleResult(
    bool FailedSafely,
    ShiftIntentEvaluationResult? ShiftIntentResult = null,
    PaddleGearBenchTestResult? BenchResult = null,
    PHprGearPulseRoutingResult? MockRoutingResult = null,
    PHprDirectGearPulseRoutingResult? RealRoutingResult = null,
    string? BenchRoutingMessage = null,
    string? Bst1PaddleGearPulseMessage = null);

internal sealed record PaddleInputRoutingCoordinatorDependencies(
    Func<WheelPaddleMapping> GetPaddleMapping,
    Action<DateTimeOffset> NotifyAcceptedGearPulse,
    Func<ShiftIntentEvent, CancellationToken, ValueTask<PHprGearPulseRoutingResult>> RouteAcceptedShiftToMockAsync,
    Func<ShiftIntentEvent, CancellationToken, ValueTask<PHprDirectGearPulseRoutingResult>> RouteAcceptedShiftToRealAsync,
    Func<ShiftIntentEvent, PHprGearPulseRouterOptions, CancellationToken, ValueTask<PHprGearPulseRoutingResult>> RouteBenchMockAsync,
    Func<Bst1PaddleGearPulseRouteSettings> GetBst1PaddleGearPulseRouteSettings,
    Func<ManualAsioHardwareTestRequest, CancellationToken, ValueTask<ManualAsioHardwareTestResult>> StartManualAsioHardwareTestAsync,
    Func<string, Task<bool>> ApplyPhprPedalsNormalOptionsFromControlsAsync,
    Action ConfigurePhprDirectRuntime,
    Func<WheelPaddleInputSnapshot> GetPaddleSnapshot,
    Func<PHprModuleId, PHprRealGearPulseSettings> GetDeviceCardPulseSettings,
    Func<PHprSafetyContext> BuildPaddleGearBenchDirectSafetyContext,
    Action<string, Exception?> WriteCrashLog);

internal sealed class PaddleInputRoutingCoordinator
{
    private readonly ShiftIntentProcessor _shiftIntentProcessor;
    private readonly PaddleGearBenchTestController _paddleGearBenchTestController;
    private readonly IPHprDirectRuntime _phprDirectRuntime;
    private readonly PaddleInputRoutingCoordinatorDependencies _dependencies;

    public PaddleInputRoutingCoordinator(
        ShiftIntentProcessor shiftIntentProcessor,
        PaddleGearBenchTestController paddleGearBenchTestController,
        IPHprDirectRuntime phprDirectRuntime,
        PaddleInputRoutingCoordinatorDependencies dependencies)
    {
        _shiftIntentProcessor = shiftIntentProcessor
            ?? throw new ArgumentNullException(nameof(shiftIntentProcessor));
        _paddleGearBenchTestController = paddleGearBenchTestController
            ?? throw new ArgumentNullException(nameof(paddleGearBenchTestController));
        _phprDirectRuntime = phprDirectRuntime
            ?? throw new ArgumentNullException(nameof(phprDirectRuntime));
        _dependencies = dependencies
            ?? throw new ArgumentNullException(nameof(dependencies));
    }

    public async ValueTask<PaddleInputRoutingHandleResult> HandleAsync(
        WheelPaddleInputEvent paddleEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paddleEvent);

        var stopAllIfPulseMayHaveStarted = false;
        ShiftIntentEvaluationResult? shiftIntentResult = null;
        PaddleGearBenchTestResult? benchResult = null;
        PHprGearPulseRoutingResult? mockRoutingResult = null;
        PHprDirectGearPulseRoutingResult? realRoutingResult = null;
        string? benchRoutingMessage = null;
        string? bst1PaddleGearPulseMessage = null;

        try
        {
            shiftIntentResult = _shiftIntentProcessor.HandlePaddleInput(paddleEvent);
            benchResult = _paddleGearBenchTestController.HandlePaddleInput(
                paddleEvent,
                _dependencies.GetPaddleMapping());

            if (shiftIntentResult.WasAccepted && shiftIntentResult.ShiftIntentEvent is not null)
            {
                var acceptedAtUtc = shiftIntentResult.ShiftIntentEvent.AcceptedAtUtc
                    ?? shiftIntentResult.ShiftIntentEvent.TimestampUtc;
                _dependencies.NotifyAcceptedGearPulse(acceptedAtUtc);
                mockRoutingResult = await _dependencies
                    .RouteAcceptedShiftToMockAsync(shiftIntentResult.ShiftIntentEvent, cancellationToken)
                    .ConfigureAwait(false);
                realRoutingResult = await _dependencies
                    .RouteAcceptedShiftToRealAsync(shiftIntentResult.ShiftIntentEvent, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (benchResult.Accepted && benchResult.ShiftIntentEvent is not null)
            {
                stopAllIfPulseMayHaveStarted =
                    benchResult.Options.Normalize().OutputMode == PaddleGearBenchTestOutputMode.Direct;
                var benchRoutingResult = await RoutePaddleGearBenchAsync(benchResult, cancellationToken)
                    .ConfigureAwait(false);
                benchRoutingMessage = benchRoutingResult.Message;
                bst1PaddleGearPulseMessage = benchRoutingResult.Bst1PaddleGearPulseMessage;
            }

            return new PaddleInputRoutingHandleResult(
                FailedSafely: false,
                ShiftIntentResult: shiftIntentResult,
                BenchResult: benchResult,
                MockRoutingResult: mockRoutingResult,
                RealRoutingResult: realRoutingResult,
                BenchRoutingMessage: benchRoutingMessage,
                Bst1PaddleGearPulseMessage: bst1PaddleGearPulseMessage);
        }
        catch (Exception ex)
        {
            await RecoverFromPaddleInputExceptionAsync(
                    "paddle-input-event-exception",
                    ex,
                    stopAllIfPulseMayHaveStarted,
                    cancellationToken)
                .ConfigureAwait(false);
            return new PaddleInputRoutingHandleResult(
                FailedSafely: true,
                ShiftIntentResult: shiftIntentResult,
                BenchResult: benchResult,
                MockRoutingResult: mockRoutingResult,
                RealRoutingResult: realRoutingResult,
                BenchRoutingMessage: benchRoutingMessage,
                Bst1PaddleGearPulseMessage: bst1PaddleGearPulseMessage);
        }
    }

    public async ValueTask HandleUiUpdateExceptionAsync(
        string reason,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);

        await RecoverFromPaddleInputExceptionAsync(
                reason,
                exception,
                stopAllIfPulseMayHaveStarted: false,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PaddleGearBenchRoutingResult> RoutePaddleGearBenchAsync(
        PaddleGearBenchTestResult benchResult,
        CancellationToken cancellationToken)
    {
        var options = benchResult.Options.Normalize();
        if (benchResult.ShiftIntentEvent is null)
        {
            _paddleGearBenchTestController.RecordOutputStatus("No bench shift event was available.");
            return new PaddleGearBenchRoutingResult(
                "Bench routing skipped: no bench shift event was available.",
                null);
        }

        if (options.OutputMode == PaddleGearBenchTestOutputMode.Mock)
        {
            var bst1Task = RouteBst1PaddleGearBenchAsync(benchResult, cancellationToken);
            var phprTask = RoutePaddleGearBenchMockAsync(benchResult, options, cancellationToken);
            await Task.WhenAll(phprTask, bst1Task).ConfigureAwait(false);
            var message = $"{phprTask.Result} {bst1Task.Result}".Trim();
            _paddleGearBenchTestController.RecordOutputStatus(message);
            return new PaddleGearBenchRoutingResult(message, bst1Task.Result);
        }

        var directTask = RoutePaddleGearBenchDirectAsync(benchResult, options, cancellationToken);
        var bstTask = RouteBst1PaddleGearBenchAsync(benchResult, cancellationToken);
        await Task.WhenAll(directTask, bstTask).ConfigureAwait(false);
        var directMessage = $"{directTask.Result} {bstTask.Result}".Trim();
        _paddleGearBenchTestController.RecordOutputStatus(directMessage);
        return new PaddleGearBenchRoutingResult(directMessage, bstTask.Result);
    }

    private async Task<string> RoutePaddleGearBenchMockAsync(
        PaddleGearBenchTestResult benchResult,
        PaddleGearBenchTestOptions options,
        CancellationToken cancellationToken)
    {
        var routeOptions = new PHprGearPulseRouterOptions
        {
            IsEnabled = true,
            TargetModule = options.TargetModule,
            Profile = options.Profile
        }.Normalize();
        var result = await _dependencies.RouteBenchMockAsync(
                benchResult.ShiftIntentEvent!,
                routeOptions,
                cancellationToken)
            .ConfigureAwait(false);
        return $"Bench Mock: {result.Message}";
    }

    private async Task<string> RouteBst1PaddleGearBenchAsync(
        PaddleGearBenchTestResult benchResult,
        CancellationToken cancellationToken)
    {
        var settings = _dependencies.GetBst1PaddleGearPulseRouteSettings();
        if (!settings.IsEnabled)
        {
            return "BST-1 paddle pulse skipped: disabled.";
        }

        if (benchResult is not { Accepted: true, ShiftIntentEvent: not null })
        {
            return "BST-1 paddle pulse skipped: bench event was not accepted.";
        }

        var request = new ManualAsioHardwareTestRequest(
            settings.FrequencyHz,
            TimeSpan.FromMilliseconds(settings.DurationMs),
            settings.StrengthPercent / 100f,
            settings.OutputTrimPercent / 100f,
            Source: "paddle gear bench",
            DurationMode: settings.DurationMode,
            AcceptedPaddleEventSequence: benchResult.PaddleEvent.SequenceNumber,
            PaddleSide: benchResult.PaddleEvent.PaddleSide.ToString(),
            PaddleButtonId: benchResult.PaddleEvent.ButtonId,
            AcceptedGearPulseId: benchResult.ShiftIntentEvent.SequenceNumber);
        var result = await _dependencies.StartManualAsioHardwareTestAsync(request, cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded
            ? $"Paddle gear BST-1 pulse accepted on ASIO channel {result.Snapshot.SelectedOutputChannel}; {settings.DurationMode} duration {settings.DurationMs:N0} ms."
            : $"Paddle gear BST-1 pulse blocked: {result.Snapshot.BlockedReason ?? result.Message}";
    }

    private async Task<string> RoutePaddleGearBenchDirectAsync(
        PaddleGearBenchTestResult benchResult,
        PaddleGearBenchTestOptions options,
        CancellationToken cancellationToken)
    {
        if (!await _dependencies.ApplyPhprPedalsNormalOptionsFromControlsAsync(
                "P-HPR pedal settings applied for Paddle Gear Bench Test."))
        {
            return "Bench Direct blocked: Devices-tab pedal card settings are invalid.";
        }

        _dependencies.ConfigurePhprDirectRuntime();
        return await _phprDirectRuntime.RouteBenchAsync(
                benchResult,
                options,
                _dependencies.GetPaddleSnapshot(),
                _dependencies.GetDeviceCardPulseSettings,
                _dependencies.BuildPaddleGearBenchDirectSafetyContext(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RecoverFromPaddleInputExceptionAsync(
        string reason,
        Exception exception,
        bool stopAllIfPulseMayHaveStarted,
        CancellationToken cancellationToken)
    {
        try
        {
            await _phprDirectRuntime.HandlePaddleInputExceptionAsync(
                    reason,
                    exception,
                    stopAllIfPulseMayHaveStarted,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            _dependencies.WriteCrashLog($"{reason}-recovery-failed", exception);
        }
    }
    private sealed record PaddleGearBenchRoutingResult(
        string Message,
        string? Bst1PaddleGearPulseMessage);
}
