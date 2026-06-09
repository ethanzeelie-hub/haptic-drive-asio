using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed partial class WindowsRawInputPhprDirectOutputCandidateProvider : IPHprDirectOutputCandidateProvider
{
    private const uint RidInputDeviceName = 0x20000007;
    private const uint RidInputDeviceInfo = 0x2000000B;
    private const uint RawInputDeviceTypeHid = 2;
    private const uint ErrorResult = uint.MaxValue;

    public IReadOnlyList<PHprDirectOutputCandidate> DiscoverCandidates(DateTimeOffset? discoveredAtUtc = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var deviceListSize = (uint)Marshal.SizeOf<RawInputDeviceList>();
        var deviceCount = 0u;
        var result = GetRawInputDeviceList(null, ref deviceCount, deviceListSize);
        if (result == ErrorResult || deviceCount == 0)
        {
            return [];
        }

        var rawDevices = new RawInputDeviceList[deviceCount];
        result = GetRawInputDeviceList(rawDevices, ref deviceCount, deviceListSize);
        if (result == ErrorResult)
        {
            return [];
        }

        var candidates = new List<PHprDirectOutputCandidate>();
        for (var index = 0; index < rawDevices.Length; index++)
        {
            var candidate = TryBuildCandidate(rawDevices[index], index);
            if (candidate is not null)
            {
                candidates.Add(candidate.Score());
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.VendorProductText, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.SafeDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static PHprDirectOutputCandidate? TryBuildCandidate(RawInputDeviceList rawDevice, int index)
    {
        var infoResult = TryGetDeviceInfo(rawDevice.DeviceHandle);
        if (infoResult.Value.Type != RawInputDeviceTypeHid)
        {
            return null;
        }

        var pathResult = TryGetDeviceName(rawDevice.DeviceHandle);
        if (string.IsNullOrWhiteSpace(pathResult.Value))
        {
            return null;
        }

        var info = infoResult.Value;
        var vendorId = ToUShortOrNull(info.Hid.VendorId);
        var productId = ToUShortOrNull(info.Hid.ProductId);

        return new PHprDirectOutputCandidate
        {
            CandidateId = CreateCandidateId(pathResult.Value, index),
            DevicePath = pathResult.Value,
            DisplayName = BuildDisplayName(vendorId, productId, info.Hid.UsagePage, info.Hid.Usage),
            DeviceClass = "Raw Input HID",
            VendorId = vendorId,
            ProductId = productId,
            InterfaceNumber = ParsePathToken(pathResult.Value, InterfaceRegex()),
            CollectionNumber = ParsePathToken(pathResult.Value, CollectionRegex()),
            HidUsagePage = (ushort)info.Hid.UsagePage,
            HidUsage = (ushort)info.Hid.Usage
        };
    }

    private static string BuildDisplayName(
        ushort? vendorId,
        ushort? productId,
        ushort usagePage,
        ushort usage)
    {
        var vendorProduct = vendorId is null || productId is null
            ? "VID/PID unavailable"
            : $"VID_{vendorId:X4}/PID_{productId:X4}";
        return $"Raw Input HID device ({vendorProduct}; usage 0x{usagePage:X4}/0x{usage:X4})";
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

    private static string? ParsePathToken(string value, Regex regex)
    {
        var match = regex.Match(value);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static ushort? ToUShortOrNull(uint value)
    {
        return value <= ushort.MaxValue ? (ushort)value : null;
    }

    private static string CreateCandidateId(string rawPath, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{rawPath}:{index.ToString(CultureInfo.InvariantCulture)}"));
        return $"local-hid:{Convert.ToHexString(hash)[..12].ToLowerInvariant()}";
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

    [GeneratedRegex(@"(?i)\bMI_([0-9A-F]{2})\b")]
    private static partial Regex InterfaceRegex();

    [GeneratedRegex(@"(?i)\bCOL([0-9A-F]{2})\b")]
    private static partial Regex CollectionRegex();

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
