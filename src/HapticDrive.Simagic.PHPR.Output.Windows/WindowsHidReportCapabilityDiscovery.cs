using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

internal static class WindowsHidReportCapabilityDiscovery
{
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const int HidpStatusSuccess = 0x00110000;

    public static PHprHidReportCapabilities Discover(string devicePath)
    {
        if (!OperatingSystem.IsWindows()
            || !PHprHidPathSafety.IsAbsoluteWindowsDevicePath(devicePath))
        {
            return PHprHidReportCapabilities.Unavailable;
        }

        try
        {
            using var handle = CreateFile(
                devicePath,
                dwDesiredAccess: 0,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileAttributeNormal,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                return PHprHidReportCapabilities.Unavailable with
                {
                    SanitizedErrorCategory = $"CreateFile:0x{Marshal.GetLastWin32Error():X8}"
                };
            }

            if (!HidD_GetPreparsedData(handle, out var preparsedData) || preparsedData == IntPtr.Zero)
            {
                return PHprHidReportCapabilities.Unavailable with
                {
                    SanitizedErrorCategory = $"HidD_GetPreparsedData:0x{Marshal.GetLastWin32Error():X8}"
                };
            }

            try
            {
                var status = HidP_GetCaps(preparsedData, out var caps);
                if (status != HidpStatusSuccess)
                {
                    return PHprHidReportCapabilities.Unavailable with
                    {
                        SanitizedErrorCategory = $"HidP_GetCaps:0x{status:X8}"
                    };
                }

                return new PHprHidReportCapabilities
                {
                    UsagePage = caps.UsagePage,
                    Usage = caps.Usage,
                    InputReportByteLength = ToPositiveLength(caps.InputReportByteLength),
                    OutputReportByteLength = ToPositiveLength(caps.OutputReportByteLength),
                    FeatureReportByteLength = ToPositiveLength(caps.FeatureReportByteLength),
                    InputReportIds = DiscoverReportIds(
                        preparsedData,
                        HidpReportType.Input,
                        caps.NumberInputButtonCaps,
                        caps.NumberInputValueCaps),
                    OutputReportIds = DiscoverReportIds(
                        preparsedData,
                        HidpReportType.Output,
                        caps.NumberOutputButtonCaps,
                        caps.NumberOutputValueCaps),
                    FeatureReportIds = DiscoverReportIds(
                        preparsedData,
                        HidpReportType.Feature,
                        caps.NumberFeatureButtonCaps,
                        caps.NumberFeatureValueCaps)
                };
            }
            finally
            {
                _ = HidD_FreePreparsedData(preparsedData);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return PHprHidReportCapabilities.Unavailable with
            {
                SanitizedErrorCategory = PHprHidPathSafety.SanitizeExceptionCategory(ex)
            };
        }
    }

    private static int? ToPositiveLength(ushort length)
    {
        return length > 0 ? length : null;
    }

    private static IReadOnlyList<byte> DiscoverReportIds(
        IntPtr preparsedData,
        HidpReportType reportType,
        ushort buttonCapsCount,
        ushort valueCapsCount)
    {
        var reportIds = new SortedSet<byte>();
        AddButtonReportIds(preparsedData, reportType, buttonCapsCount, reportIds);
        AddValueReportIds(preparsedData, reportType, valueCapsCount, reportIds);
        return reportIds.ToArray();
    }

    private static void AddButtonReportIds(
        IntPtr preparsedData,
        HidpReportType reportType,
        ushort buttonCapsCount,
        ISet<byte> reportIds)
    {
        if (buttonCapsCount == 0)
        {
            return;
        }

        try
        {
            var count = buttonCapsCount;
            var caps = Enumerable.Range(0, count)
                .Select(_ => new HidpButtonCaps { Reserved = new uint[10] })
                .ToArray();
            if (HidP_GetButtonCaps(reportType, caps, ref count, preparsedData) != HidpStatusSuccess)
            {
                return;
            }

            foreach (var cap in caps.Take(count))
            {
                reportIds.Add(cap.ReportID);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or MarshalDirectiveException)
        {
        }
    }

    private static void AddValueReportIds(
        IntPtr preparsedData,
        HidpReportType reportType,
        ushort valueCapsCount,
        ISet<byte> reportIds)
    {
        if (valueCapsCount == 0)
        {
            return;
        }

        try
        {
            var count = valueCapsCount;
            var caps = Enumerable.Range(0, count)
                .Select(_ => new HidpValueCaps
                {
                    Reserved = new ushort[5]
                })
                .ToArray();
            if (HidP_GetValueCaps(reportType, caps, ref count, preparsedData) != HidpStatusSuccess)
            {
                return;
            }

            foreach (var cap in caps.Take(count))
            {
                reportIds.Add(cap.ReportID);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or MarshalDirectiveException)
        {
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetPreparsedData(
        SafeFileHandle hidDeviceObject,
        out IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(
        IntPtr preparsedData,
        out HidpCaps capabilities);

    [DllImport("hid.dll")]
    private static extern int HidP_GetButtonCaps(
        HidpReportType reportType,
        [In, Out] HidpButtonCaps[] buttonCaps,
        ref ushort buttonCapsLength,
        IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetValueCaps(
        HidpReportType reportType,
        [In, Out] HidpValueCaps[] valueCaps,
        ref ushort valueCapsLength,
        IntPtr preparsedData);

    private enum HidpReportType
    {
        Input = 0,
        Output = 1,
        Feature = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage;

        public ushort UsagePage;

        public ushort InputReportByteLength;

        public ushort OutputReportByteLength;

        public ushort FeatureReportByteLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;

        public ushort NumberLinkCollectionNodes;

        public ushort NumberInputButtonCaps;

        public ushort NumberInputValueCaps;

        public ushort NumberInputDataIndices;

        public ushort NumberOutputButtonCaps;

        public ushort NumberOutputValueCaps;

        public ushort NumberOutputDataIndices;

        public ushort NumberFeatureButtonCaps;

        public ushort NumberFeatureValueCaps;

        public ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpButtonCaps
    {
        public ushort UsagePage;

        public byte ReportID;

        public byte IsAlias;

        public ushort BitField;

        public ushort LinkCollection;

        public ushort LinkUsage;

        public ushort LinkUsagePage;

        public byte IsRange;

        public byte IsStringRange;

        public byte IsDesignatorRange;

        public byte IsAbsolute;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public uint[] Reserved;

        public ushort UsageMin;

        public ushort UsageMax;

        public ushort StringMin;

        public ushort StringMax;

        public ushort DesignatorMin;

        public ushort DesignatorMax;

        public ushort DataIndexMin;

        public ushort DataIndexMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpValueCaps
    {
        public ushort UsagePage;

        public byte ReportID;

        public byte IsAlias;

        public ushort BitField;

        public ushort LinkCollection;

        public ushort LinkUsage;

        public ushort LinkUsagePage;

        public byte IsRange;

        public byte IsStringRange;

        public byte IsDesignatorRange;

        public byte IsAbsolute;

        public byte HasNull;

        public byte ReservedByte;

        public ushort BitSize;

        public ushort ReportCount;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public ushort[] Reserved;

        public uint UnitsExp;

        public uint Units;

        public int LogicalMin;

        public int LogicalMax;

        public int PhysicalMin;

        public int PhysicalMax;

        public ushort UsageMin;

        public ushort UsageMax;

        public ushort StringMin;

        public ushort StringMax;

        public ushort DesignatorMin;

        public ushort DesignatorMax;

        public ushort DataIndexMin;

        public ushort DataIndexMax;
    }
}
