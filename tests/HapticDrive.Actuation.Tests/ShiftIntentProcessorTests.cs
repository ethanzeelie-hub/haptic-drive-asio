using HapticDrive.Actuation.Shift;
using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Actuation.Tests;

public sealed class ShiftIntentProcessorTests
{
    [Fact]
    public void Options_DefaultToInstantPaddleOnlyAndEnabled()
    {
        var options = ShiftIntentProcessorOptions.Default;

        Assert.True(options.IsEnabled);
        Assert.Equal(ShiftIntentMode.InstantPaddleOnly, options.Mode);
    }

    [Theory]
    [InlineData(PaddleSide.Left, ShiftIntentDirection.Downshift)]
    [InlineData(PaddleSide.Right, ShiftIntentDirection.Upshift)]
    public void PaddleSides_MapToDefaultShiftDirections(
        PaddleSide side,
        ShiftIntentDirection expectedDirection)
    {
        var provider = new FakeDrivingArmedProvider(DrivingArmedState.Armed("active"));
        var processor = new ShiftIntentProcessor(provider);

        var result = processor.HandlePaddleInput(CreatePaddleEvent(side));

        Assert.True(result.WasAccepted);
        Assert.Equal(expectedDirection, result.Direction);
        Assert.Equal(expectedDirection, result.ShiftIntentEvent?.Direction);
    }

    [Fact]
    public void InstantPaddleOnly_AcceptsWhenDrivingArmedIsTrue()
    {
        var armed = DrivingArmedState.Armed("Active driving telemetry is fresh.");
        var provider = new FakeDrivingArmedProvider(armed);
        var sink = new InMemoryShiftIntentSink();
        var processor = new ShiftIntentProcessor(provider, sink: sink);
        var paddleEvent = CreatePaddleEvent(PaddleSide.Right, buttonId: 5, sequenceNumber: 42);

        var result = processor.HandlePaddleInput(paddleEvent);
        var diagnostics = processor.GetDiagnosticsSnapshot();

        Assert.True(result.WasAccepted);
        Assert.NotNull(result.ShiftIntentEvent);
        Assert.Equal(1, sink.AcceptedCount);
        Assert.Equal(1, diagnostics.AcceptedShiftIntentCount);
        Assert.Equal(0, diagnostics.SuppressedShiftIntentCount);
        Assert.Equal(armed, result.ShiftIntentEvent.DrivingArmedAtEvent);
        Assert.Equal(42, result.ShiftIntentEvent.SequenceNumber);
        Assert.Equal("windowsgamecontroller:test", result.ShiftIntentEvent.SourceDeviceId);
        Assert.Equal(5, result.ShiftIntentEvent.SourceButtonId);
    }

    [Fact]
    public void DrivingArmedFalse_SuppressesAndPreservesGateReason()
    {
        var notArmed = DrivingArmedState.NotArmed("Game is paused.");
        var provider = new FakeDrivingArmedProvider(notArmed);
        var sink = new InMemoryShiftIntentSink();
        var processor = new ShiftIntentProcessor(provider, sink: sink);

        var result = processor.HandlePaddleInput(CreatePaddleEvent(PaddleSide.Left));
        var diagnostics = processor.GetDiagnosticsSnapshot();

        Assert.False(result.WasAccepted);
        Assert.Null(result.ShiftIntentEvent);
        Assert.Equal("Game is paused.", result.SuppressionReason);
        Assert.Equal(notArmed, result.DrivingArmedStateAtEvaluation);
        Assert.Equal(0, sink.AcceptedCount);
        Assert.Equal(1, diagnostics.SuppressedShiftIntentCount);
        Assert.Equal(result, diagnostics.LastSuppressedEvent);
    }

    [Fact]
    public void PaddleEvaluation_ReadsCachedDrivingStateOnly()
    {
        var provider = new FakeDrivingArmedProvider(DrivingArmedState.Armed("active"));
        var processor = new ShiftIntentProcessor(provider);

        var result = processor.HandlePaddleInput(CreatePaddleEvent(PaddleSide.Right));

        Assert.True(result.WasAccepted);
        Assert.Equal(1, provider.CurrentReadCount);
    }

    [Fact]
    public void InstantPaddleOnly_DoesNotCreateDoubleOrConfirmationEvent()
    {
        var provider = new FakeDrivingArmedProvider(DrivingArmedState.Armed("active"));
        var sink = new InMemoryShiftIntentSink();
        var processor = new ShiftIntentProcessor(provider, sink: sink);

        processor.HandlePaddleInput(CreatePaddleEvent(PaddleSide.Right));
        var diagnostics = processor.GetDiagnosticsSnapshot();

        Assert.Equal(1, diagnostics.AcceptedShiftIntentCount);
        Assert.Single(sink.GetAcceptedEvents());
        Assert.Equal(0, diagnostics.PendingConfirmationCount);
    }

    [Fact]
    public void UpdateTelemetry_UsesCanonicalHapticFrameSnapshot()
    {
        var provider = new FakeDrivingArmedProvider(DrivingArmedState.Armed("active"));
        var processor = new ShiftIntentProcessor(provider);
        processor.UpdateTelemetry(
            CreateTelemetryFrame(gear: 5, speedKph: 198, rpm: 11_200, sessionTime: 8.5f, frameIdentifier: 42),
            lastVehicleStateUpdateAtUtc: new DateTimeOffset(2026, 6, 23, 12, 0, 0, TimeSpan.Zero),
            telemetryAge: TimeSpan.FromMilliseconds(25));

        var result = processor.HandlePaddleInput(CreatePaddleEvent(PaddleSide.Right));

        Assert.True(result.WasAccepted);
        Assert.Equal(5, result.ShiftIntentEvent?.LastTelemetryGear);
        Assert.Equal(198, result.ShiftIntentEvent?.LastKnownSpeedKph);
        Assert.Equal(11_200, result.ShiftIntentEvent?.LastKnownRpm);
        Assert.Equal(8.5f, result.ShiftIntentEvent?.LastKnownSessionTime);
        Assert.Equal(42u, result.ShiftIntentEvent?.LastKnownFrameIdentifier);
    }

    [Fact]
    public void TelemetryConfirmedOnly_DoesNotEmitImmediatePaddleIntent()
    {
        var provider = new FakeDrivingArmedProvider(DrivingArmedState.Armed("active"));
        var sink = new InMemoryShiftIntentSink();
        var processor = new ShiftIntentProcessor(
            provider,
            new ShiftIntentProcessorOptions { Mode = ShiftIntentMode.TelemetryConfirmedOnly },
            sink);

        var result = processor.HandlePaddleInput(CreatePaddleEvent(PaddleSide.Right));

        Assert.False(result.WasAccepted);
        Assert.Null(result.ShiftIntentEvent);
        Assert.Contains("TelemetryConfirmedOnly", result.SuppressionReason, StringComparison.Ordinal);
        Assert.Equal(0, sink.AcceptedCount);
    }

    [Fact]
    public void InstantWithRejectedShiftFeedback_EmitsImmediateIntentAndRecordsPendingConfirmation()
    {
        var provider = new FakeDrivingArmedProvider(DrivingArmedState.Armed("active"));
        var processor = new ShiftIntentProcessor(
            provider,
            new ShiftIntentProcessorOptions { Mode = ShiftIntentMode.InstantWithRejectedShiftFeedback });

        var result = processor.HandlePaddleInput(CreatePaddleEvent(PaddleSide.Left));
        var diagnostics = processor.GetDiagnosticsSnapshot();

        Assert.True(result.WasAccepted);
        Assert.Equal(ShiftIntentMode.InstantWithRejectedShiftFeedback, result.ShiftIntentEvent?.Mode);
        Assert.Equal(1, diagnostics.PendingConfirmationCount);
    }

    [Fact]
    public void DiagnosticsCountersAndLastEvents_UpdateForAcceptedAndSuppressedEvents()
    {
        var provider = new FakeDrivingArmedProvider(DrivingArmedState.Armed("active"));
        var processor = new ShiftIntentProcessor(provider);
        var accepted = processor.HandlePaddleInput(CreatePaddleEvent(PaddleSide.Right, sequenceNumber: 1));
        provider.State = DrivingArmedState.NotArmed("Menu safe gate.");

        var suppressed = processor.HandlePaddleInput(CreatePaddleEvent(PaddleSide.Left, sequenceNumber: 2));
        var diagnostics = processor.GetDiagnosticsSnapshot();

        Assert.True(accepted.WasAccepted);
        Assert.False(suppressed.WasAccepted);
        Assert.Equal(2, diagnostics.TotalPaddleEventsObserved);
        Assert.Equal(1, diagnostics.AcceptedShiftIntentCount);
        Assert.Equal(1, diagnostics.SuppressedShiftIntentCount);
        Assert.Equal(accepted.ShiftIntentEvent, diagnostics.LastAcceptedEvent);
        Assert.Equal(suppressed, diagnostics.LastSuppressedEvent);
        Assert.Equal(PaddleSide.Left, diagnostics.LastPaddleSide);
        Assert.Equal(ShiftIntentDirection.Downshift, diagnostics.LastDirection);
    }

    [Fact]
    public void DisabledShiftIntentLayer_SuppressesEvents()
    {
        var provider = new FakeDrivingArmedProvider(DrivingArmedState.Armed("active"));
        var processor = new ShiftIntentProcessor(
            provider,
            new ShiftIntentProcessorOptions { IsEnabled = false });

        var result = processor.HandlePaddleInput(CreatePaddleEvent(PaddleSide.Right));

        Assert.False(result.WasAccepted);
        Assert.Contains("disabled", result.SuppressionReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, processor.GetDiagnosticsSnapshot().SuppressedShiftIntentCount);
    }

    [Fact]
    public void DrivingStateProviderErrors_AreCapturedWithoutCrashing()
    {
        var provider = new FakeDrivingArmedProvider(DrivingArmedState.Armed("active"))
        {
            ThrowOnCurrent = true
        };
        var processor = new ShiftIntentProcessor(provider);

        var result = processor.HandlePaddleInput(CreatePaddleEvent(PaddleSide.Right));
        var diagnostics = processor.GetDiagnosticsSnapshot();

        Assert.False(result.WasAccepted);
        Assert.Contains("synthetic driving gate failure", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("synthetic driving gate failure", diagnostics.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicConstructorSurface_DoesNotRequireHapticOrAudioOutputInterfaces()
    {
        var forbiddenTypeNames = new[]
        {
            "IPHprOutputDevice",
            "MockPhprOutputDevice",
            "PHprCommand",
            "AsioAudioOutputDevice",
            "GearShiftEffect",
            "AudioRenderPipeline",
            "AudioMixer"
        };
        var constructorParameterNames = typeof(ShiftIntentProcessor)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        foreach (var forbiddenTypeName in forbiddenTypeNames)
        {
            Assert.DoesNotContain(forbiddenTypeName, constructorParameterNames);
        }
    }

    private static WheelPaddleInputEvent CreatePaddleEvent(
        PaddleSide side,
        int buttonId = 4,
        long sequenceNumber = 1)
    {
        return new WheelPaddleInputEvent(
            side,
            new InputDeviceSelection(
                "windowsgamecontroller:test",
                "Synthetic GT Neo wheel input",
                InputDiscoveryMethod.WindowsGameController,
                NativeDeviceIndex: 0,
                ButtonCount: 12),
            buttonId,
            new InputEventTimestamp(
                new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero).AddMilliseconds(sequenceNumber),
                10_000 + sequenceNumber),
            sequenceNumber);
    }

    private static HapticFrame CreateTelemetryFrame(
        int gear,
        int speedKph,
        int rpm,
        float sessionTime,
        uint frameIdentifier)
    {
        return new HapticFrame(
            new HapticFrameIdentity(
                new GameIntegrationId("f1-25"),
                "test",
                SessionUid: 42,
                SessionTime: sessionTime,
                FrameIdentifier: frameIdentifier,
                OverallFrameIdentifier: frameIdentifier,
                PlayerCarIndex: 0,
                CreatedAtUtc: new DateTimeOffset(2026, 6, 23, 12, 0, 0, TimeSpan.Zero),
                CreatedAtTimestamp: 42),
            new HapticTelemetrySignals(
                SpeedMetersPerSecond: speedKph / 3.6f,
                Throttle: 0.6f,
                Brake: 0f,
                Steer: 0f,
                Gear: gear,
                SuggestedGear: gear,
                EngineRpm: rpm,
                IdleRpm: 3_000,
                MaxRpm: 12_000,
                MaxGears: 8,
                TractionControlActive: false,
                AntiLockBrakesActive: false,
                SurfaceTypeIds: null,
                SurfaceKinds: null,
                TyreSlip: null,
                TyreSlipAngle: null,
                WheelSpeedMetersPerSecond: null,
                SuspensionVelocity: null,
                SuspensionAcceleration: null,
                WheelVerticalForce: null,
                BrakeTemperatureCelsius: null,
                VerticalG: null,
                Event: null),
            new HapticDrivingContext(DrivingPhase.Driving, PitState.None, false, true, true),
            new HapticFrameSignalStamps(
                Telemetry: null,
                Motion: null,
                Session: null,
                Lap: null,
                Participant: null,
                CarStatus: null,
                Damage: null,
                MotionEx: null,
                Event: null));
    }

    private sealed class FakeDrivingArmedProvider : IDrivingArmedStateProvider
    {
        public FakeDrivingArmedProvider(DrivingArmedState state)
        {
            State = state;
        }

        public event EventHandler<DrivingArmedState>? DrivingArmedChanged;

        public DrivingArmedState State { get; set; }

        public bool ThrowOnCurrent { get; init; }

        public int CurrentReadCount { get; private set; }

        public DrivingArmedState Current
        {
            get
            {
                CurrentReadCount++;
                if (ThrowOnCurrent)
                {
                    throw new InvalidOperationException("synthetic driving gate failure");
                }

                return State;
            }
        }

        public void RaiseChanged()
        {
            DrivingArmedChanged?.Invoke(this, State);
        }
    }
}
