namespace HapticDrive.Simagic.PHPR.Output.Windows;

public static class PHprHidPathSafety
{
    public const string InvalidDevicePathCategory = "InvalidDevicePath";

    public static bool IsAbsoluteWindowsDevicePath(string? value)
    {
        var trimmed = value?.Trim();
        return !string.IsNullOrWhiteSpace(trimmed)
            && (trimmed.StartsWith(@"\\?\", StringComparison.Ordinal)
                || trimmed.StartsWith(@"\\.\", StringComparison.Ordinal));
    }

    public static string SanitizeExceptionCategory(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return $"{ex.GetType().Name}:0x{ex.HResult:X8}";
    }
}
