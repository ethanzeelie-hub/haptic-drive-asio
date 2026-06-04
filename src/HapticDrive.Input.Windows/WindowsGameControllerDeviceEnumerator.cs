using System.Runtime.InteropServices;
using HapticDrive.Input.Abstractions.Devices;

namespace HapticDrive.Input.Windows;

public sealed class WindowsGameControllerDeviceEnumerator : IWindowsInputDeviceEnumerator
{
    private const uint JoyNoError = 0;

    public InputDiscoveryMethod Method => InputDiscoveryMethod.WindowsGameController;

    public IReadOnlyList<InputDeviceInfo> DiscoverDevices(DateTimeOffset discoveredAtUtc)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var deviceCount = JoyGetNumDevs();
        if (deviceCount == 0)
        {
            return [];
        }

        var devices = new List<InputDeviceInfo>();
        var capsSize = (uint)Marshal.SizeOf<JoyCaps>();
        for (var index = 0u; index < deviceCount; index++)
        {
            var caps = new JoyCaps();
            var result = JoyGetDevCaps(index, ref caps, capsSize);
            if (result != JoyNoError)
            {
                continue;
            }

            devices.Add(BuildDeviceInfo(index, caps, discoveredAtUtc));
        }

        return devices;
    }

    private static InputDeviceInfo BuildDeviceInfo(uint index, JoyCaps caps, DateTimeOffset discoveredAtUtc)
    {
        var displayName = string.IsNullOrWhiteSpace(caps.ProductName)
            ? $"Windows game controller {index}"
            : caps.ProductName.Trim();

        return new InputDeviceInfo
        {
            DeviceId = WindowsInputDevicePathSanitizer.CreateStableDeviceId(
                InputDiscoveryMethod.WindowsGameController,
                $"joy:{index}:{caps.ProductName}:{caps.RegistryKey}:{caps.OemDriver}",
                (int)index),
            DisplayName = displayName,
            ProductName = displayName,
            VendorId = caps.ManufacturerId,
            ProductId = caps.ProductId,
            InstanceId = string.IsNullOrWhiteSpace(caps.RegistryKey) ? null : caps.RegistryKey.Trim(),
            DeviceClass = "Windows game controller",
            Kind = InputDeviceKind.GameController,
            DiscoveryMethod = InputDiscoveryMethod.WindowsGameController,
            Controls = BuildControls(caps),
            ButtonCount = checked((int)caps.NumberOfButtons),
            AxisCount = checked((int)caps.NumberOfAxes),
            NativeDeviceIndex = checked((int)index),
            ReadOnlyDiscoverySucceeded = true,
            DiscoveredAtUtc = discoveredAtUtc
        };
    }

    private static IReadOnlyList<InputControlInfo> BuildControls(JoyCaps caps)
    {
        var controls = new List<InputControlInfo>();
        for (var button = 0; button < caps.NumberOfButtons; button++)
        {
            controls.Add(new InputControlInfo(
                $"button-{button + 1}",
                $"Button {button + 1}",
                InputControlKind.Button,
                checked((int)button + 1)));
        }

        var axisNames = new[] { "X", "Y", "Z", "R", "U", "V" };
        for (var axis = 0; axis < Math.Min(caps.NumberOfAxes, (uint)axisNames.Length); axis++)
        {
            controls.Add(new InputControlInfo(
                $"axis-{axisNames[axis].ToLowerInvariant()}",
                $"{axisNames[axis]} axis",
                InputControlKind.Axis,
                checked((int)axis + 1)));
        }

        return controls;
    }

    [DllImport("winmm.dll")]
    private static extern uint JoyGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern uint JoyGetDevCaps(uint joystickId, ref JoyCaps caps, uint capsSize);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JoyCaps
    {
        public ushort ManufacturerId;

        public ushort ProductId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ProductName;

        public uint XMin;

        public uint XMax;

        public uint YMin;

        public uint YMax;

        public uint ZMin;

        public uint ZMax;

        public uint NumberOfButtons;

        public uint PeriodMin;

        public uint PeriodMax;

        public uint RMin;

        public uint RMax;

        public uint UMin;

        public uint UMax;

        public uint VMin;

        public uint VMax;

        public uint Capabilities;

        public uint MaxAxes;

        public uint NumberOfAxes;

        public uint MaxButtons;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string RegistryKey;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string OemDriver;
    }
}
