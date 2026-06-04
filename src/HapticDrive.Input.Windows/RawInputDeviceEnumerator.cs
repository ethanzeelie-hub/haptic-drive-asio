using System.ComponentModel;
using System.Runtime.InteropServices;
using HapticDrive.Input.Abstractions.Devices;

namespace HapticDrive.Input.Windows;

public sealed class RawInputDeviceEnumerator : IWindowsInputDeviceEnumerator
{
    private const uint RidInputDeviceName = 0x20000007;
    private const uint RidInputDeviceInfo = 0x2000000B;
    private const uint RawInputDeviceTypeMouse = 0;
    private const uint RawInputDeviceTypeKeyboard = 1;
    private const uint RawInputDeviceTypeHid = 2;
    private const uint ErrorResult = uint.MaxValue;

    public InputDiscoveryMethod Method => InputDiscoveryMethod.RawInput;

    public IReadOnlyList<InputDeviceInfo> DiscoverDevices(DateTimeOffset discoveredAtUtc)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var deviceListSize = (uint)Marshal.SizeOf<RawInputDeviceList>();
        var deviceCount = 0u;
        var result = GetRawInputDeviceList(null, ref deviceCount, deviceListSize);
        if (result == ErrorResult)
        {
            throw CreateWin32Exception("Raw Input device count could not be read.");
        }

        if (deviceCount == 0)
        {
            return [];
        }

        var rawDevices = new RawInputDeviceList[deviceCount];
        result = GetRawInputDeviceList(rawDevices, ref deviceCount, deviceListSize);
        if (result == ErrorResult)
        {
            throw CreateWin32Exception("Raw Input device list could not be read.");
        }

        var devices = new List<InputDeviceInfo>((int)deviceCount);
        for (var index = 0; index < deviceCount; index++)
        {
            devices.Add(BuildDeviceInfo(rawDevices[index], index, discoveredAtUtc));
        }

        return devices;
    }

    private static InputDeviceInfo BuildDeviceInfo(
        RawInputDeviceList rawDevice,
        int index,
        DateTimeOffset discoveredAtUtc)
    {
        var nameResult = TryGetDeviceName(rawDevice.DeviceHandle);
        var infoResult = TryGetDeviceInfo(rawDevice.DeviceHandle);
        var rawPath = nameResult.Value;
        var sanitizedPath = WindowsInputDevicePathSanitizer.Sanitize(rawPath);
        var info = infoResult.Value;
        var kind = GetKind(info);
        var vendorId = info.Type == RawInputDeviceTypeHid ? ToUShortOrNull(info.Hid.VendorId) : null;
        var productId = info.Type == RawInputDeviceTypeHid ? ToUShortOrNull(info.Hid.ProductId) : null;
        var usagePage = info.Type == RawInputDeviceTypeHid ? (ushort?)info.Hid.UsagePage : null;
        var usage = info.Type == RawInputDeviceTypeHid ? (ushort?)info.Hid.Usage : null;
        var displayName = BuildDisplayName(kind, vendorId, productId, usagePage, usage);
        var errors = new[] { nameResult.ErrorMessage, infoResult.ErrorMessage }
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .ToArray();

        return new InputDeviceInfo
        {
            DeviceId = WindowsInputDevicePathSanitizer.CreateStableDeviceId(
                InputDiscoveryMethod.RawInput,
                rawPath ?? rawDevice.DeviceHandle.ToString(),
                index),
            DisplayName = displayName,
            ProductName = displayName,
            VendorId = vendorId,
            ProductId = productId,
            InstanceId = sanitizedPath,
            DevicePath = sanitizedPath,
            DeviceClass = GetDeviceClass(info.Type),
            Kind = kind,
            DiscoveryMethod = InputDiscoveryMethod.RawInput,
            HidUsagePage = usagePage,
            HidUsage = usage,
            ReadOnlyDiscoverySucceeded = errors.Length == 0,
            ErrorMessage = errors.Length == 0 ? null : string.Join(" ", errors),
            DiscoveredAtUtc = discoveredAtUtc
        };
    }

    private static InputDeviceKind GetKind(RawInputDeviceInfo info)
    {
        return info.Type switch
        {
            RawInputDeviceTypeMouse => InputDeviceKind.Mouse,
            RawInputDeviceTypeKeyboard => InputDeviceKind.Keyboard,
            RawInputDeviceTypeHid when info.Hid.UsagePage == 0x01 && info.Hid.Usage is 0x04 or 0x05 or 0x08 => InputDeviceKind.GameController,
            RawInputDeviceTypeHid => InputDeviceKind.Hid,
            _ => InputDeviceKind.Unknown
        };
    }

    private static string GetDeviceClass(uint type)
    {
        return type switch
        {
            RawInputDeviceTypeMouse => "Raw Input mouse",
            RawInputDeviceTypeKeyboard => "Raw Input keyboard",
            RawInputDeviceTypeHid => "Raw Input HID",
            _ => "Raw Input device"
        };
    }

    private static string BuildDisplayName(
        InputDeviceKind kind,
        ushort? vendorId,
        ushort? productId,
        ushort? usagePage,
        ushort? usage)
    {
        var prefix = kind switch
        {
            InputDeviceKind.Mouse => "Raw Input mouse",
            InputDeviceKind.Keyboard => "Raw Input keyboard",
            InputDeviceKind.GameController => "Raw Input game controller",
            InputDeviceKind.Hid => "Raw Input HID device",
            _ => "Raw Input device"
        };

        var vendorProduct = vendorId is null || productId is null
            ? "VID/PID unavailable"
            : $"VID_{vendorId:X4} PID_{productId:X4}";
        var usageText = usagePage is null || usage is null
            ? "usage unavailable"
            : $"usage 0x{usagePage:X4}/0x{usage:X4}";

        return $"{prefix} ({vendorProduct}; {usageText})";
    }

    private static DiscoveryValue<string?> TryGetDeviceName(nint deviceHandle)
    {
        var size = 0u;
        var result = GetRawInputDeviceInfo(deviceHandle, RidInputDeviceName, IntPtr.Zero, ref size);
        if (result == ErrorResult || size == 0)
        {
            return DiscoveryValue<string?>.Failure(null, CreateWin32Exception("Raw Input device path could not be read.").Message);
        }

        var buffer = Marshal.AllocHGlobal(checked((int)size * 2));
        try
        {
            result = GetRawInputDeviceInfo(deviceHandle, RidInputDeviceName, buffer, ref size);
            if (result == ErrorResult)
            {
                return DiscoveryValue<string?>.Failure(null, CreateWin32Exception("Raw Input device path could not be read.").Message);
            }

            return DiscoveryValue<string?>.Success(Marshal.PtrToStringUni(buffer));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static DiscoveryValue<RawInputDeviceInfo> TryGetDeviceInfo(nint deviceHandle)
    {
        var size = (uint)Marshal.SizeOf<RawInputDeviceInfo>();
        var info = new RawInputDeviceInfo { Size = size };
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            Marshal.StructureToPtr(info, buffer, false);
            var result = GetRawInputDeviceInfo(deviceHandle, RidInputDeviceInfo, buffer, ref size);
            if (result == ErrorResult)
            {
                return DiscoveryValue<RawInputDeviceInfo>.Failure(
                    new RawInputDeviceInfo(),
                    CreateWin32Exception("Raw Input device metadata could not be read.").Message);
            }

            return DiscoveryValue<RawInputDeviceInfo>.Success(Marshal.PtrToStructure<RawInputDeviceInfo>(buffer));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static ushort? ToUShortOrNull(uint value)
    {
        return value <= ushort.MaxValue ? (ushort)value : null;
    }

    private static Win32Exception CreateWin32Exception(string fallbackMessage)
    {
        var error = Marshal.GetLastWin32Error();
        return error == 0 ? new Win32Exception(fallbackMessage) : new Win32Exception(error, fallbackMessage);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        [Out] RawInputDeviceList[]? rawInputDeviceList,
        ref uint deviceCount,
        uint rawInputDeviceListSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        nint deviceHandle,
        uint command,
        IntPtr data,
        ref uint dataSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceList
    {
        public nint DeviceHandle;

        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceInfo
    {
        public uint Size;

        public uint Type;

        public RawInputDeviceInfoUnion Data;

        public RawInputDeviceInfoHid Hid => Data.Hid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RawInputDeviceInfoUnion
    {
        [FieldOffset(0)]
        public RawInputDeviceInfoMouse Mouse;

        [FieldOffset(0)]
        public RawInputDeviceInfoKeyboard Keyboard;

        [FieldOffset(0)]
        public RawInputDeviceInfoHid Hid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceInfoMouse
    {
        public uint Id;

        public uint NumberOfButtons;

        public uint SampleRate;

        public bool HasHorizontalWheel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceInfoKeyboard
    {
        public uint Type;

        public uint SubType;

        public uint KeyboardMode;

        public uint NumberOfFunctionKeys;

        public uint NumberOfIndicators;

        public uint NumberOfKeysTotal;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceInfoHid
    {
        public uint VendorId;

        public uint ProductId;

        public uint VersionNumber;

        public ushort UsagePage;

        public ushort Usage;
    }

    private sealed record DiscoveryValue<T>(T Value, string? ErrorMessage)
    {
        public static DiscoveryValue<T> Success(T value)
        {
            return new DiscoveryValue<T>(value, null);
        }

        public static DiscoveryValue<T> Failure(T value, string errorMessage)
        {
            return new DiscoveryValue<T>(value, errorMessage);
        }
    }
}
