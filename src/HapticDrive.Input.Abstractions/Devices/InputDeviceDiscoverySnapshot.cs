namespace HapticDrive.Input.Abstractions.Devices;

public sealed record InputDeviceDiscoverySnapshot(
    DateTimeOffset DiscoveredAtUtc,
    IReadOnlyList<InputDeviceInfo> Devices,
    IReadOnlyList<InputDiscoveryMethod> Methods,
    IReadOnlyList<string> Errors,
    bool HasRun = true)
{
    public static InputDeviceDiscoverySnapshot NotRun { get; } = new(
        DateTimeOffset.MinValue,
        [],
        [],
        [],
        HasRun: false);

    public int DeviceCount => Devices.Count;

    public bool ReadOnlyDiscoverySucceeded =>
        HasRun
        && Errors.Count == 0
        && Devices.All(device => device.ReadOnlyDiscoverySucceeded);

    public IReadOnlyList<InputDeviceInfo> LikelySimagicWheelBaseCandidates =>
        Devices
            .Where(device => device.LooksLikeAlphaOrWheelbase)
            .OrderByDescending(device => device.CandidateScore)
            .ThenBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<InputDeviceInfo> LikelyGtNeoWheelInputCandidates =>
        Devices
            .Where(device => device.LooksLikeGtNeoOrWheelInput)
            .OrderByDescending(device => device.CandidateScore)
            .ThenBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<InputDeviceInfo> LikelyP700PedalCandidates =>
        Devices
            .Where(device => device.LooksLikeP700Pedals)
            .OrderByDescending(device => device.CandidateScore)
            .ThenBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<InputDeviceInfo> UnknownHidOrGameControllerCandidates =>
        Devices
            .Where(device => device.CandidateKind == InputDeviceCandidateKind.UnknownHidOrGameController)
            .OrderByDescending(device => device.CandidateScore)
            .ThenBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static InputDeviceDiscoverySnapshot Create(
        IReadOnlyList<InputDeviceInfo> devices,
        IReadOnlyList<InputDiscoveryMethod> methods,
        IReadOnlyList<string> errors,
        DateTimeOffset? discoveredAtUtc = null)
    {
        return new InputDeviceDiscoverySnapshot(
            discoveredAtUtc ?? DateTimeOffset.UtcNow,
            devices,
            methods,
            errors);
    }
}
