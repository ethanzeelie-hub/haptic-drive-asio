namespace HapticDrive.Asio.Audio.Tests;

internal static class ManualHardwareTestSettings
{
    public static bool RunAsioHardwareTests => GetFlag("HAPTICDRIVE_RUN_ASIO_HARDWARE_TESTS");

    public static bool DaytonBst1Arrived => GetFlag("HAPTICDRIVE_BST1_ARRIVED");

    public static bool DaytonBst1PhysicalOutputValidated => GetFlag("HAPTICDRIVE_BST1_PHYSICAL_OUTPUT_VALIDATED");

    private static bool GetFlag(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
