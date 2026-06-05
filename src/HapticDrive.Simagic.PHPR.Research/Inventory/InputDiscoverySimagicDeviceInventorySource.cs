using HapticDrive.Input.Abstractions.Devices;

namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public sealed class InputDiscoverySimagicDeviceInventorySource(IInputDeviceDiscovery inputDiscovery)
    : ISimagicDeviceInventorySource
{
    public SimagicDeviceInventoryMethod Method => SimagicDeviceInventoryMethod.ExistingInputDiscovery;

    public async ValueTask<SimagicDeviceInventorySourceResult> EnumerateAsync(
        DateTimeOffset discoveredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await inputDiscovery.DiscoverAsync(cancellationToken);
        var errors = snapshot.Errors
            .Select(error => new SimagicDeviceInventoryError(Method, error))
            .ToArray();
        var items = snapshot.Devices
            .Select(device => MapDevice(device, discoveredAtUtc))
            .ToArray();

        return new SimagicDeviceInventorySourceResult(items, errors);
    }

    private static SimagicDeviceInventoryItem MapDevice(InputDeviceInfo device, DateTimeOffset discoveredAtUtc)
    {
        return new SimagicDeviceInventoryItem
        {
            DeviceId = device.DeviceId,
            DisplayName = device.DisplayName,
            Manufacturer = device.Manufacturer,
            ProductName = device.ProductName,
            VendorId = device.VendorId,
            ProductId = device.ProductId,
            DeviceClass = device.DeviceClass,
            HidUsagePage = device.HidUsagePage,
            HidUsage = device.HidUsage,
            InputReportByteLength = device.InputReportByteLength,
            SafeInstanceId = SimagicDeviceInventorySanitizer.SanitizeIdentifier(device.InstanceId),
            SafeDevicePath = SimagicDeviceInventorySanitizer.SanitizeIdentifier(device.DevicePath),
            DiscoveryMethod = MapMethod(device.DiscoveryMethod),
            ReadOnlyDiscoverySucceeded = device.ReadOnlyDiscoverySucceeded,
            ErrorMessage = device.ErrorMessage,
            DiscoveredAtUtc = discoveredAtUtc,
            CandidateReason = device.CandidateReason
        };
    }

    private static SimagicDeviceInventoryMethod MapMethod(InputDiscoveryMethod method)
    {
        return method switch
        {
            InputDiscoveryMethod.RawInput => SimagicDeviceInventoryMethod.RawInputMetadata,
            InputDiscoveryMethod.WindowsGameController => SimagicDeviceInventoryMethod.WindowsGameController,
            _ => SimagicDeviceInventoryMethod.ExistingInputDiscovery
        };
    }
}
