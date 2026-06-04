namespace HapticDrive.Input.Abstractions.Devices;

public sealed class WheelInputCandidateProvider : IWheelInputCandidateProvider
{
    public InputDeviceInfo ScoreDevice(InputDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var haystack = BuildSearchText(device);
        var isHidOrGameController = device.Kind is InputDeviceKind.Hid or InputDeviceKind.GameController;
        var hasGameControllerUsage = device.HidUsagePage == 0x01 && device.HidUsage is 0x04 or 0x05 or 0x08;
        var looksLikeSimagic = ContainsAny(haystack, "simagic", "alpha evo", "gt neo", "gtneo", "p700");
        var looksLikeP700 = ContainsAny(haystack, "p700", "p 700")
            || (looksLikeSimagic && ContainsAny(haystack, "pedal", "pedals", "brake", "throttle"));
        var looksLikeGtNeo = ContainsAny(haystack, "gt neo", "gtneo")
            || (looksLikeSimagic && isHidOrGameController && !looksLikeP700);
        var looksLikeWheelBase = ContainsAny(haystack, "alpha evo", "alpha", "wheelbase", "wheel base", "steering wheel")
            || (looksLikeSimagic && ContainsAny(haystack, "base"));

        var score = 0;
        var reasons = new List<string>();

        if (looksLikeSimagic)
        {
            score += 50;
            reasons.Add("Simagic-like name or metadata");
        }

        if (looksLikeP700)
        {
            score += 45;
            reasons.Add("P700/pedal-like name");
        }

        if (looksLikeGtNeo)
        {
            score += 40;
            reasons.Add("GT Neo or wheel input path candidate");
        }

        if (looksLikeWheelBase)
        {
            score += 30;
            reasons.Add("Alpha/wheelbase-like name");
        }

        if (isHidOrGameController)
        {
            score += 10;
            reasons.Add("HID/game-controller metadata");
        }

        if (hasGameControllerUsage)
        {
            score += 10;
            reasons.Add($"HID usage page 0x{device.HidUsagePage:X4}/usage 0x{device.HidUsage:X4}");
        }

        var candidateKind = InputDeviceCandidateKind.Unknown;
        if (looksLikeP700)
        {
            candidateKind = InputDeviceCandidateKind.LikelyP700Pedals;
        }
        else if (looksLikeGtNeo)
        {
            candidateKind = InputDeviceCandidateKind.LikelyGtNeoWheelInputPath;
        }
        else if (looksLikeWheelBase)
        {
            candidateKind = InputDeviceCandidateKind.LikelySimagicWheelBase;
        }
        else if (isHidOrGameController || hasGameControllerUsage)
        {
            candidateKind = InputDeviceCandidateKind.UnknownHidOrGameController;
        }

        return device with
        {
            LooksLikeSimagic = looksLikeSimagic,
            LooksLikeAlphaOrWheelbase = looksLikeWheelBase,
            LooksLikeGtNeoOrWheelInput = looksLikeGtNeo,
            LooksLikeP700Pedals = looksLikeP700,
            CandidateKind = candidateKind,
            CandidateScore = candidateKind == InputDeviceCandidateKind.Unknown ? 0 : score,
            CandidateReason = reasons.Count == 0
                ? "No Simagic-specific signals found."
                : string.Join("; ", reasons.Distinct(StringComparer.OrdinalIgnoreCase))
        };
    }

    public IReadOnlyList<InputDeviceInfo> GetCandidates(InputDeviceDiscoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.Devices
            .Select(ScoreDevice)
            .Where(device => device.CandidateKind != InputDeviceCandidateKind.Unknown)
            .OrderByDescending(device => device.CandidateScore)
            .ThenBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildSearchText(InputDeviceInfo device)
    {
        return string.Join(
            " ",
            device.DisplayName,
            device.Manufacturer,
            device.ProductName,
            device.DeviceClass,
            device.DevicePath,
            device.InstanceId).ToLowerInvariant();
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        return needles.Any(needle => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
