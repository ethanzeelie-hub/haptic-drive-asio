namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public sealed class SimagicDeviceCandidateClassifier
{
    public SimagicDeviceInventoryItem ScoreItem(SimagicDeviceInventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var haystack = BuildSearchText(item);
        var looksLikeSimagic = ContainsAny(haystack, "simagic", "p700", "p 700", "p-hpr", "phpr", "gt neo", "gtneo", "alpha evo");
        var looksLikeP700 = ContainsAny(haystack, "p700", "p 700")
            || (looksLikeSimagic && ContainsAny(haystack, "pedal", "pedals", "brake", "throttle"));
        var looksLikePHpr = ContainsAny(haystack, "p-hpr", "phpr")
            || (looksLikeSimagic && ContainsAny(haystack, "haptic controller", "haptic module", "hpr"));
        var looksLikeAlpha = ContainsAny(haystack, "alpha evo", "alpha wheelbase", "wheelbase", "wheel base");
        var looksLikeGtNeo = ContainsAny(haystack, "gt neo", "gtneo")
            || (looksLikeSimagic && ContainsAny(haystack, "wheel input", "steering wheel"));
        var isHid = item.DiscoveryMethod is SimagicDeviceInventoryMethod.RawInputMetadata
                or SimagicDeviceInventoryMethod.WindowsRegistryHid
            || ContainsAny(haystack, "hid", "human interface")
            || item.HidUsagePage is not null;
        var isUsbInput = item.DiscoveryMethod == SimagicDeviceInventoryMethod.WindowsRegistryUsb
            || ContainsAny(haystack, "usb input", "usb device", "usb");

        var score = 0;
        var reasons = new List<string>();

        if (looksLikeSimagic)
        {
            score += 60;
            reasons.Add("Simagic-like name or metadata");
        }

        if (looksLikeP700)
        {
            score += 50;
            reasons.Add("P700/pedal-controller-like metadata");
        }

        if (looksLikePHpr)
        {
            score += 45;
            reasons.Add("P-HPR/haptic-controller-like metadata");
        }

        if (looksLikeAlpha)
        {
            score += 40;
            reasons.Add("Alpha Evo/wheelbase-like metadata");
        }

        if (looksLikeGtNeo)
        {
            score += 40;
            reasons.Add("GT Neo/wheel-input-like metadata");
        }

        if (item.VendorId is not null && item.ProductId is not null)
        {
            score += 10;
            reasons.Add($"VID_{item.VendorId:X4}/PID_{item.ProductId:X4} present");
        }

        if (isHid)
        {
            score += 8;
            reasons.Add("HID metadata");
        }
        else if (isUsbInput)
        {
            score += 6;
            reasons.Add("USB metadata");
        }

        var candidateKind = SimagicDeviceCandidateKind.Unknown;
        if (looksLikeP700)
        {
            candidateKind = SimagicDeviceCandidateKind.P700PedalController;
        }
        else if (looksLikePHpr)
        {
            candidateKind = SimagicDeviceCandidateKind.PHprModuleOrController;
        }
        else if (looksLikeAlpha)
        {
            candidateKind = SimagicDeviceCandidateKind.AlphaEvoWheelbase;
        }
        else if (looksLikeGtNeo)
        {
            candidateKind = SimagicDeviceCandidateKind.GtNeoWheelInput;
        }
        else if (looksLikeSimagic)
        {
            candidateKind = SimagicDeviceCandidateKind.SimagicUnknown;
        }
        else if (isHid)
        {
            candidateKind = SimagicDeviceCandidateKind.GenericHid;
        }
        else if (isUsbInput)
        {
            candidateKind = SimagicDeviceCandidateKind.GenericUsbInput;
        }

        return item with
        {
            CandidateKind = candidateKind,
            CandidateScore = candidateKind == SimagicDeviceCandidateKind.Unknown ? 0 : score,
            CandidateReason = reasons.Count == 0
                ? "No Simagic, HID, or USB input signals found."
                : string.Join("; ", reasons.Distinct(StringComparer.OrdinalIgnoreCase))
        };
    }

    private static string BuildSearchText(SimagicDeviceInventoryItem item)
    {
        return string.Join(
            " ",
            item.DisplayName,
            item.Manufacturer,
            item.ProductName,
            item.ServiceName,
            item.DriverProvider,
            item.DeviceClass,
            item.ClassGuid,
            item.SafeInstanceId,
            item.SafeDevicePath).ToLowerInvariant();
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        return needles.Any(needle => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
