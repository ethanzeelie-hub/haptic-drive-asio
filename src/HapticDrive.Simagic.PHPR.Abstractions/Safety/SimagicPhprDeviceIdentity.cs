namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public static class SimagicPhprDeviceIdentity
{
    public const ushort SimagicVendorId = 0x3670;

    public static IReadOnlySet<ushort> ObservedFamilyProductIds { get; } = new HashSet<ushort>
    {
        0x0500,
        0x0905,
        0xB500,
        0xB905
    };

    public static bool IsSimagicFamilyVendor(ushort? vendorId)
    {
        return vendorId == SimagicVendorId;
    }

    public static bool IsObservedSimagicFamilyProduct(ushort? productId)
    {
        return productId is not null && ObservedFamilyProductIds.Contains(productId.Value);
    }
}
