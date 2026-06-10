using HapticDrive.Input.Abstractions.Devices;

namespace HapticDrive.Asio.App;

internal static class PaddleInputDeviceSelector
{
    public const ushort SimagicVendorId = 0x3670;
    public const ushort GtNeoWheelInputProductId = 0x0905;
    public const int PreferredGtNeoButtonCount = 32;

    public static IReadOnlyList<InputDeviceInfo> OrderForDisplay(
        IEnumerable<InputDeviceInfo> devices,
        string? savedDeviceId = null)
    {
        ArgumentNullException.ThrowIfNull(devices);

        return devices
            .Where(IsSupportedWindowsGameController)
            .OrderByDescending(device => IsSavedUsableDevice(device, savedDeviceId))
            .ThenByDescending(IsPreferredSimagicGtNeoThirtyTwoButtonController)
            .ThenByDescending(IsKnownSimagicGtNeoWindowsController)
            .ThenByDescending(HasUsableButtons)
            .ThenByDescending(GetUsableButtonCount)
            .ThenByDescending(device => device.LooksLikeGtNeoOrWheelInput)
            .ThenByDescending(device => device.CandidateScore)
            .ThenBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(device => device.NativeDeviceIndex ?? int.MaxValue)
            .ToArray();
    }

    public static InputDeviceInfo? SelectPreferred(
        IEnumerable<InputDeviceInfo> devices,
        string? savedDeviceId = null)
    {
        var ordered = OrderForDisplay(devices, savedDeviceId);
        var saved = ordered.FirstOrDefault(device => IsSavedUsableDevice(device, savedDeviceId));
        if (saved is not null)
        {
            return saved;
        }

        return ordered.FirstOrDefault(HasUsableButtons);
    }

    public static bool IsSupportedWindowsGameController(InputDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return device.DiscoveryMethod == InputDiscoveryMethod.WindowsGameController
            && device.Kind == InputDeviceKind.GameController
            && device.NativeDeviceIndex is not null;
    }

    public static bool IsKnownSimagicGtNeoWindowsController(InputDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return IsSupportedWindowsGameController(device)
            && device.VendorId == SimagicVendorId
            && device.ProductId == GtNeoWheelInputProductId;
    }

    public static bool IsPreferredSimagicGtNeoThirtyTwoButtonController(InputDeviceInfo device)
    {
        return IsKnownSimagicGtNeoWindowsController(device)
            && GetUsableButtonCount(device) >= PreferredGtNeoButtonCount;
    }

    public static bool HasUsableButtons(InputDeviceInfo device)
    {
        return GetUsableButtonCount(device) > 0;
    }

    public static int GetUsableButtonCount(InputDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var buttonCount = device.ButtonCount.GetValueOrDefault();
        var controlButtonCount = device.Controls.Count(control =>
            control.Kind is InputControlKind.Button or InputControlKind.PaddleCandidate);

        return Math.Max(buttonCount, controlButtonCount);
    }

    private static bool IsSavedUsableDevice(InputDeviceInfo device, string? savedDeviceId)
    {
        return HasUsableButtons(device)
            && !string.IsNullOrWhiteSpace(savedDeviceId)
            && string.Equals(device.DeviceId, savedDeviceId, StringComparison.OrdinalIgnoreCase);
    }
}
