using HapticDrive.Input.Abstractions.Devices;

namespace HapticDrive.Asio.App.Tests;

public sealed class PaddleInputDeviceSelectorTests
{
    [Fact]
    public void AutoSelectionPrefersThirtyTwoButtonSimagicVidPidOverZeroButtonCandidate()
    {
        var zeroButton = Device("zero", buttonCount: 0, nativeIndex: 0);
        var thirtyTwoButton = Device("thirty-two", buttonCount: 32, nativeIndex: 1);

        var selected = PaddleInputDeviceSelector.SelectPreferred([zeroButton, thirtyTwoButton]);

        Assert.NotNull(selected);
        Assert.Equal("thirty-two", selected.DeviceId);
        Assert.Equal("thirty-two", PaddleInputDeviceSelector.OrderForDisplay([zeroButton, thirtyTwoButton]).First().DeviceId);
    }

    [Fact]
    public void AutoSelectionKeepsSavedUsableDeviceWhenStillValid()
    {
        var saved = Device("saved", buttonCount: 16, nativeIndex: 0);
        var thirtyTwoButton = Device("thirty-two", buttonCount: 32, nativeIndex: 1);

        var selected = PaddleInputDeviceSelector.SelectPreferred([thirtyTwoButton, saved], saved.DeviceId);

        Assert.NotNull(selected);
        Assert.Equal("saved", selected.DeviceId);
    }

    [Fact]
    public void AutoSelectionPrefersUsableButtonCandidateOverZeroButtonCandidate()
    {
        var zeroButton = Device("zero", buttonCount: 0, nativeIndex: 0, vendorId: 0x1234, productId: 0x5678);
        var usable = Device("usable", buttonCount: 8, nativeIndex: 1, vendorId: 0x1234, productId: 0x5678);

        var selected = PaddleInputDeviceSelector.SelectPreferred([zeroButton, usable]);

        Assert.NotNull(selected);
        Assert.Equal("usable", selected.DeviceId);
    }

    [Fact]
    public void AutoSelectionDoesNotSelectZeroButtonWhenNoUsableAlternativeExists()
    {
        var zeroButton = Device("zero", buttonCount: 0, nativeIndex: 0);

        var selected = PaddleInputDeviceSelector.SelectPreferred([zeroButton]);

        Assert.Null(selected);
        Assert.False(PaddleInputDeviceSelector.HasUsableButtons(zeroButton));
    }

    private static InputDeviceInfo Device(
        string id,
        int buttonCount,
        int nativeIndex,
        ushort vendorId = PaddleInputDeviceSelector.SimagicVendorId,
        ushort productId = PaddleInputDeviceSelector.GtNeoWheelInputProductId)
    {
        return new InputDeviceInfo
        {
            DeviceId = id,
            DisplayName = "Microsoft PC-joystick driver",
            ProductName = "Microsoft PC-joystick driver",
            VendorId = vendorId,
            ProductId = productId,
            DeviceClass = "Windows game controller",
            Kind = InputDeviceKind.GameController,
            DiscoveryMethod = InputDiscoveryMethod.WindowsGameController,
            ButtonCount = buttonCount,
            AxisCount = 0,
            NativeDeviceIndex = nativeIndex
        };
    }
}
