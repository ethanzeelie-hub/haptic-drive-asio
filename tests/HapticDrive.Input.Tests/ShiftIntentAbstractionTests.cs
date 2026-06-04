using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Input.Tests;

public sealed class ShiftIntentAbstractionTests
{
    [Fact]
    public void DrivingArmedState_DefaultsToNotArmedForMenuSafety()
    {
        var state = DrivingArmedState.Default;

        Assert.False(state.IsArmed);
        Assert.True(state.MenuSafeModeEnabled);
        Assert.True(state.RequireRecentTelemetry);
        Assert.Contains("telemetry", state.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShiftIntentEvent_UsesCachedDrivingArmedStateWithoutTelemetryWait()
    {
        var timestamp = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var armed = DrivingArmedState.Armed(
            "Active driving telemetry is fresh.",
            timestamp,
            TimeSpan.FromMilliseconds(12));

        var shiftIntent = ShiftIntentEvent.CreatePaddlePress(
            PaddleSide.Right,
            armed,
            timestamp,
            sequenceNumber: 42,
            sourceDeviceId: "raw-input-alpha-evo",
            lastTelemetryGear: 5);

        Assert.True(shiftIntent.IsAcceptedByDrivingGate);
        Assert.Equal(PaddleSide.Right, shiftIntent.PaddleSide);
        Assert.Equal(timestamp, shiftIntent.TimestampUtc);
        Assert.Equal(42, shiftIntent.SequenceNumber);
        Assert.Equal("raw-input-alpha-evo", shiftIntent.SourceDeviceId);
        Assert.Equal(5, shiftIntent.LastTelemetryGear);
        Assert.Equal(ShiftIntentDirection.Upshift, shiftIntent.Direction);
        Assert.Equal(ShiftIntentSource.WheelPaddle, shiftIntent.Source);
        Assert.Equal(ShiftIntentMode.InstantPaddleOnly, shiftIntent.Mode);
        Assert.NotEqual(Guid.Empty, shiftIntent.CorrelationId);
        Assert.Equal(armed, shiftIntent.DrivingArmedAtEvent);
    }

    [Fact]
    public void ShiftIntentEvent_DefaultDirectionMapsLeftToDownshiftAndRightToUpshift()
    {
        Assert.Equal(ShiftIntentDirection.Downshift, ShiftIntentEvent.DirectionForPaddle(PaddleSide.Left));
        Assert.Equal(ShiftIntentDirection.Upshift, ShiftIntentEvent.DirectionForPaddle(PaddleSide.Right));
        Assert.Equal(ShiftIntentDirection.Unknown, ShiftIntentEvent.DirectionForPaddle(PaddleSide.Unknown));
    }

    [Fact]
    public void ShiftIntentEvent_ReportsSuppressedWhenCachedDrivingArmedStateIsFalse()
    {
        var notArmed = DrivingArmedState.NotArmed("Menu or paused state.");

        var shiftIntent = ShiftIntentEvent.CreatePaddlePress(PaddleSide.Left, notArmed);

        Assert.False(shiftIntent.IsAcceptedByDrivingGate);
        Assert.Equal("Menu or paused state.", shiftIntent.DrivingArmedAtEvent.Reason);
    }

    [Fact]
    public void InputDeviceDescriptor_DefaultsToReadOnlyDiscovery()
    {
        var descriptor = new InputDeviceDescriptor(
            "hid:vid_0483_pid_a355",
            "Candidate Wheel Input",
            "HID",
            VendorId: 0x0483,
            ProductId: 0xA355,
            UsagePage: 1,
            Usage: 4);

        Assert.True(descriptor.IsReadOnly);
        Assert.Equal("HID", descriptor.Transport);
        Assert.Equal((ushort)0x0483, descriptor.VendorId.GetValueOrDefault());
        Assert.Equal((ushort)0xA355, descriptor.ProductId.GetValueOrDefault());
    }
}
