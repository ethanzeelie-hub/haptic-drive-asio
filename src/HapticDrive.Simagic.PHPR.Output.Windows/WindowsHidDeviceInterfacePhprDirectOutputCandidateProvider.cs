using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed partial class WindowsHidDeviceInterfacePhprDirectOutputCandidateProvider : IPHprDirectOutputCandidateProvider
{
    private const int DigcfPresent = 0x00000002;
    private const int DigcfDeviceInterface = 0x00000010;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public IReadOnlyList<PHprDirectOutputCandidate> DiscoverCandidates(DateTimeOffset? discoveredAtUtc = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        HidD_GetHidGuid(out var hidGuid);
        var deviceInfoSet = SetupDiGetClassDevs(
            ref hidGuid,
            IntPtr.Zero,
            IntPtr.Zero,
            DigcfPresent | DigcfDeviceInterface);
        if (deviceInfoSet == InvalidHandleValue)
        {
            return [];
        }

        try
        {
            var candidates = new List<PHprDirectOutputCandidate>();
            for (uint index = 0; ; index++)
            {
                var interfaceData = new SpDeviceInterfaceData
                {
                    CbSize = Marshal.SizeOf<SpDeviceInterfaceData>()
                };

                if (!SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == 259)
                    {
                        break;
                    }

                    continue;
                }

                var devicePath = TryGetDeviceInterfacePath(deviceInfoSet, interfaceData);
                if (string.IsNullOrWhiteSpace(devicePath))
                {
                    continue;
                }

                candidates.Add(BuildCandidate(devicePath, checked((int)index)).Score());
            }

            return candidates
                .OrderByDescending(candidate => candidate.Confidence)
                .ThenBy(candidate => candidate.VendorProductText, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.SafeDisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static string? TryGetDeviceInterfacePath(
        IntPtr deviceInfoSet,
        SpDeviceInterfaceData interfaceData)
    {
        _ = SetupDiGetDeviceInterfaceDetail(
            deviceInfoSet,
            ref interfaceData,
            IntPtr.Zero,
            0,
            out var requiredSize,
            IntPtr.Zero);

        if (requiredSize == 0)
        {
            return null;
        }

        var detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
        try
        {
            Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
            if (!SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet,
                    ref interfaceData,
                    detailBuffer,
                    requiredSize,
                    out _,
                    IntPtr.Zero))
            {
                return null;
            }

            return Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, 4));
        }
        finally
        {
            Marshal.FreeHGlobal(detailBuffer);
        }
    }

    private static PHprDirectOutputCandidate BuildCandidate(string devicePath, int index)
    {
        var vendorId = ParseHexToken(devicePath, "VID");
        var productId = ParseHexToken(devicePath, "PID");
        var usagePage = ParseHexToken(devicePath, "UP");
        var usage = ParseHexToken(devicePath, "U");

        return new PHprDirectOutputCandidate
        {
            CandidateId = CreateCandidateId(devicePath, index),
            DevicePath = devicePath,
            DisplayName = BuildDisplayName(vendorId, productId, usagePage, usage),
            DeviceClass = "HID device interface",
            SourceMethod = PHprDirectOutputCandidateSourceMethod.HidDeviceInterface,
            VendorId = vendorId,
            ProductId = productId,
            InterfaceNumber = ParsePathToken(devicePath, InterfaceRegex()),
            CollectionNumber = ParsePathToken(devicePath, CollectionRegex()),
            HidUsagePage = usagePage,
            HidUsage = usage
        };
    }

    private static string BuildDisplayName(
        ushort? vendorId,
        ushort? productId,
        ushort? usagePage,
        ushort? usage)
    {
        var vendorProduct = vendorId is null || productId is null
            ? "VID/PID unavailable"
            : $"VID_{vendorId:X4}/PID_{productId:X4}";
        var usageText = usagePage is null || usage is null
            ? "usage unavailable"
            : $"usage 0x{usagePage:X4}/0x{usage:X4}";
        return $"HID device interface ({vendorProduct}; {usageText})";
    }

    private static ushort? ParseHexToken(string value, string token)
    {
        var match = Regex.Match(value, $@"(?i)\b{token}_([0-9A-F]{{4}})\b");
        if (!match.Success)
        {
            return null;
        }

        return ushort.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? ParsePathToken(string value, Regex regex)
    {
        var match = regex.Match(value);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static string CreateCandidateId(string devicePath, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{devicePath}:{index.ToString(CultureInfo.InvariantCulture)}"));
        return $"hid-interface:{Convert.ToHexString(hash)[..12].ToLowerInvariant()}";
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        IntPtr enumerator,
        IntPtr hwndParent,
        int flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SpDeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [GeneratedRegex(@"(?i)\bMI_([0-9A-F]{2})\b")]
    private static partial Regex InterfaceRegex();

    [GeneratedRegex(@"(?i)\bCOL([0-9A-F]{2})\b")]
    private static partial Regex CollectionRegex();

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public int CbSize;

        public Guid InterfaceClassGuid;

        public int Flags;

        public nint Reserved;
    }
}
