using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Windows;

namespace HapticDrive.Input.Tests;

public sealed class InputDeviceDiscoveryTests
{
    [Fact]
    public void InputDeviceInfo_CapturesReadOnlyDiscoveryMetadata()
    {
        var timestamp = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var device = new InputDeviceInfo
        {
            DeviceId = "rawinput:abc123",
            DisplayName = "Simagic Alpha Evo GT Neo",
            Manufacturer = "Simagic",
            ProductName = "Alpha Evo / GT Neo input",
            VendorId = 0x0483,
            ProductId = 0xA355,
            InstanceId = "HID#VID_0483&PID_A355#<redacted>",
            DevicePath = "HID#VID_0483&PID_A355#<redacted>",
            DeviceClass = "Raw Input HID",
            Kind = InputDeviceKind.GameController,
            DiscoveryMethod = InputDiscoveryMethod.RawInput,
            ButtonCount = 12,
            AxisCount = 3,
            HidUsagePage = 0x01,
            HidUsage = 0x04,
            InputReportByteLength = 64,
            DiscoveredAtUtc = timestamp
        };

        Assert.True(device.ReadOnlyDiscoverySucceeded);
        Assert.Equal("Simagic Alpha Evo GT Neo", device.DisplayName);
        Assert.Equal((ushort)0x0483, device.VendorId);
        Assert.Equal((ushort)0xA355, device.ProductId);
        Assert.Equal(InputDiscoveryMethod.RawInput, device.DiscoveryMethod);
        Assert.Equal(timestamp, device.DiscoveredAtUtc);
    }

    [Fact]
    public async Task WindowsDiscovery_HandlesZeroDevicesSafely()
    {
        var discovery = new WindowsInputDeviceDiscovery(
            [new FakeWindowsEnumerator(InputDiscoveryMethod.RawInput, [])]);

        var snapshot = await discovery.DiscoverAsync();

        Assert.True(snapshot.HasRun);
        Assert.Empty(snapshot.Devices);
        Assert.Empty(snapshot.Errors);
        Assert.Equal(InputDiscoveryMethod.RawInput, Assert.Single(snapshot.Methods));
    }

    [Fact]
    public async Task WindowsDiscovery_RecordsEnumeratorExceptionsWithoutThrowing()
    {
        var discovery = new WindowsInputDeviceDiscovery(
            [new ThrowingWindowsEnumerator(InputDiscoveryMethod.RawInput, "synthetic failure")]);

        var snapshot = await discovery.DiscoverAsync();

        Assert.Empty(snapshot.Devices);
        Assert.Single(snapshot.Errors);
        Assert.Contains("RawInput", snapshot.Errors[0]);
        Assert.Contains("synthetic failure", snapshot.Errors[0]);
    }

    [Fact]
    public async Task WindowsDiscovery_ReturnsDeterministicMockProviderResults()
    {
        var device = CreateDevice("Simagic GT Neo Wheel", InputDeviceKind.GameController);
        var discovery = new WindowsInputDeviceDiscovery(
            [new FakeWindowsEnumerator(InputDiscoveryMethod.WindowsGameController, [device])]);

        var snapshot = await discovery.DiscoverAsync();

        var discovered = Assert.Single(snapshot.Devices);
        Assert.Equal(device.DeviceId, discovered.DeviceId);
        Assert.True(discovered.LooksLikeGtNeoOrWheelInput);
        Assert.Equal(InputDeviceCandidateKind.LikelyGtNeoWheelInputPath, discovered.CandidateKind);
        Assert.True(discovered.DiscoveredAtUtc > DateTimeOffset.MinValue);
    }

    [Theory]
    [InlineData("Simagic Alpha Evo 12Nm Wheelbase", InputDeviceKind.GameController, InputDeviceCandidateKind.LikelyGtNeoWheelInputPath, true, true, true, false)]
    [InlineData("Simagic GT Neo wheel input", InputDeviceKind.GameController, InputDeviceCandidateKind.LikelyGtNeoWheelInputPath, true, false, true, false)]
    [InlineData("Simagic P700 pedal set", InputDeviceKind.Hid, InputDeviceCandidateKind.LikelyP700Pedals, true, false, false, true)]
    [InlineData("Generic HID-compliant game controller", InputDeviceKind.GameController, InputDeviceCandidateKind.UnknownHidOrGameController, false, false, false, false)]
    public void CandidateProvider_ScoresSyntheticNames(
        string displayName,
        InputDeviceKind kind,
        InputDeviceCandidateKind expectedKind,
        bool expectedSimagic,
        bool expectedWheelbase,
        bool expectedGtNeo,
        bool expectedP700)
    {
        var provider = new WheelInputCandidateProvider();

        var scored = provider.ScoreDevice(CreateDevice(displayName, kind));

        Assert.Equal(expectedKind, scored.CandidateKind);
        Assert.Equal(expectedSimagic, scored.LooksLikeSimagic);
        Assert.Equal(expectedWheelbase, scored.LooksLikeAlphaOrWheelbase);
        Assert.Equal(expectedGtNeo, scored.LooksLikeGtNeoOrWheelInput);
        Assert.Equal(expectedP700, scored.LooksLikeP700Pedals);
        Assert.True(scored.CandidateScore > 0);
    }

    [Fact]
    public void CandidateProvider_UsesKnownSimagicVidPidForGenericWindowsControllerName()
    {
        var provider = new WheelInputCandidateProvider();
        var genericWindowsName = CreateDevice("Microsoft PC-joystick driver", InputDeviceKind.GameController) with
        {
            VendorId = 0x3670,
            ProductId = 0x0905,
            ButtonCount = 32
        };

        var scored = provider.ScoreDevice(genericWindowsName);

        Assert.True(scored.LooksLikeSimagic);
        Assert.True(scored.LooksLikeGtNeoOrWheelInput);
        Assert.Equal(InputDeviceCandidateKind.LikelyGtNeoWheelInputPath, scored.CandidateKind);
        Assert.Contains("VID_3670/PID_0905", scored.CandidateReason, StringComparison.Ordinal);
        Assert.Contains("32-button", scored.CandidateReason, StringComparison.Ordinal);
    }

    [Fact]
    public void CandidateProvider_ConsumesEmptySnapshotSafely()
    {
        var provider = new WheelInputCandidateProvider();
        var snapshot = InputDeviceDiscoverySnapshot.Create([], [], []);

        var candidates = provider.GetCandidates(snapshot);

        Assert.Empty(candidates);
        Assert.Empty(snapshot.LikelySimagicWheelBaseCandidates);
        Assert.Empty(snapshot.LikelyGtNeoWheelInputCandidates);
        Assert.Empty(snapshot.LikelyP700PedalCandidates);
        Assert.Empty(snapshot.UnknownHidOrGameControllerCandidates);
    }

    [Fact]
    public void DiscoveryInterfaces_DoNotExposeWriteOrOutputMethods()
    {
        var forbiddenTerms = new[] { "Write", "Send", "Output", "Feature", "Vibrate", "Command" };
        var methodNames = typeof(IInputDeviceDiscovery).GetMethods()
            .Concat(typeof(IWindowsInputDeviceEnumerator).GetMethods())
            .Concat(typeof(IWheelInputCandidateProvider).GetMethods())
            .Where(method => method.DeclaringType != typeof(object))
            .Select(method => method.Name)
            .ToArray();

        foreach (var methodName in methodNames)
        {
            Assert.DoesNotContain(forbiddenTerms, term => methodName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static InputDeviceInfo CreateDevice(string displayName, InputDeviceKind kind)
    {
        return new InputDeviceInfo
        {
            DeviceId = $"test:{displayName}",
            DisplayName = displayName,
            ProductName = displayName,
            DeviceClass = kind == InputDeviceKind.GameController ? "Windows game controller" : "Raw Input HID",
            Kind = kind,
            DiscoveryMethod = kind == InputDeviceKind.GameController
                ? InputDiscoveryMethod.WindowsGameController
                : InputDiscoveryMethod.RawInput,
            ButtonCount = kind == InputDeviceKind.GameController ? 12 : null,
            AxisCount = kind == InputDeviceKind.GameController ? 3 : null,
            HidUsagePage = 0x01,
            HidUsage = kind == InputDeviceKind.GameController ? (ushort)0x04 : (ushort)0x00
        };
    }

    private sealed class FakeWindowsEnumerator(
        InputDiscoveryMethod method,
        IReadOnlyList<InputDeviceInfo> devices) : IWindowsInputDeviceEnumerator
    {
        public InputDiscoveryMethod Method { get; } = method;

        public IReadOnlyList<InputDeviceInfo> DiscoverDevices(DateTimeOffset discoveredAtUtc)
        {
            return devices
                .Select(device => device with { DiscoveredAtUtc = discoveredAtUtc })
                .ToArray();
        }
    }

    private sealed class ThrowingWindowsEnumerator(
        InputDiscoveryMethod method,
        string message) : IWindowsInputDeviceEnumerator
    {
        public InputDiscoveryMethod Method { get; } = method;

        public IReadOnlyList<InputDeviceInfo> DiscoverDevices(DateTimeOffset discoveredAtUtc)
        {
            throw new InvalidOperationException(message);
        }
    }
}
