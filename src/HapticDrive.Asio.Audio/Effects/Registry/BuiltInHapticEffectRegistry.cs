using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects.Registry;

public sealed class BuiltInHapticEffectRegistry : IHapticEffectRegistry
{
    public static BuiltInHapticEffectRegistry Instance { get; } = new();

    private readonly IReadOnlyList<IHapticEffectDescriptor> _all;
    private readonly IReadOnlyDictionary<string, IHapticEffectDescriptor> _byKey;

    public BuiltInHapticEffectRegistry()
    {
        _all =
        [
            new HapticEffectDescriptor(
                key: "engine-rpm",
                displayName: "Engine RPM",
                description: "Continuous engine vibration driven by RPM and throttle.",
                category: HapticEffectCategory.Engine,
                defaultEnabled: false,
                requiredSignals:
                [
                    new HapticSignalRequirement(HapticSignalKind.Telemetry, true),
                    new HapticSignalRequirement(HapticSignalKind.CarStatus, false)
                ],
                parameters:
                [
                    Continuous("gain", "Gain", 0, 1, 0.5, 0.01, 2, "%"),
                    Integer("minimum-frequency-hz", "Minimum Frequency", 15, 80, 34, 1, "Hz"),
                    Integer("maximum-frequency-hz", "Maximum Frequency", 20, 120, 50, 1, "Hz"),
                    Boolean("high-frequency-enabled", "High Frequency Enabled", true, true),
                    Integer("high-frequency-hz", "High Frequency", 20, 120, 50, 1, "Hz", true),
                    Continuous("high-frequency-gain", "High Frequency Gain", 0, 1, 0.25, 0.01, 2, "%", true),
                    Continuous("frequency-jitter-hz", "Frequency Jitter", 0, 10, 0, 0.1, 1, "Hz", true),
                    Continuous("idle-throttle-gain", "Idle Throttle Gain", 0, 1, 0.35, 0.01, 2, "%", true),
                    Continuous("pit-gain-multiplier", "Pit Gain Multiplier", 0, 1, 0.35, 0.01, 2, "%", true)
                ],
                runtimeFactory: static settings => new EngineEffectRuntime(settings)),
            new HapticEffectDescriptor(
                key: "gear-shift",
                displayName: "Gear Shift",
                description: "Short pulse triggered by valid forward gear changes.",
                category: HapticEffectCategory.Shift,
                defaultEnabled: false,
                requiredSignals:
                [
                    new HapticSignalRequirement(HapticSignalKind.Telemetry, true)
                ],
                parameters:
                [
                    Continuous("gain", "Gain", 0, 1, 0.5, 0.01, 2, "%"),
                    Integer("pulse-frequency-hz", "Pulse Frequency", 5, 120, 15, 1, "Hz"),
                    Integer("pulse-duration-ms", "Pulse Duration", 10, 250, 80, 1, "ms"),
                    Boolean("rpm-modulation-enabled", "Modulate By RPM", false, true)
                ],
                runtimeFactory: static settings => new GearShiftEffectRuntime(settings)),
            new HapticEffectDescriptor(
                key: "kerb",
                displayName: "Kerb",
                description: "Continuous kerb vibration driven by surface contact and speed.",
                category: HapticEffectCategory.Surface,
                defaultEnabled: false,
                requiredSignals:
                [
                    new HapticSignalRequirement(HapticSignalKind.Telemetry, true)
                ],
                parameters:
                [
                    Continuous("gain", "Gain", 0, 1, 0.5, 0.01, 2, "%"),
                    Integer("base-frequency-hz", "Base Frequency", 5, 120, 20, 1, "Hz"),
                    Integer("minimum-speed-kph", "Minimum Speed", 0, 80, 5, 1, "kph"),
                    Integer("full-intensity-speed-kph", "Full Intensity Speed", 20, 300, 120, 1, "kph"),
                    Boolean("high-frequency-enabled", "High Frequency Enabled", true, true),
                    Integer("high-frequency-hz", "High Frequency", 20, 120, 44, 1, "Hz", true),
                    Continuous("high-frequency-gain", "High Frequency Gain", 0, 1, 0.25, 0.01, 2, "%", true),
                    Continuous("noise-amount", "Noise Amount", 0, 1, 0.08, 0.01, 2, "%", true)
                ],
                runtimeFactory: static settings => new KerbEffectRuntime(settings)),
            new HapticEffectDescriptor(
                key: "impact",
                displayName: "Impact",
                description: "Transient impact pulse driven by motion spikes or collision events.",
                category: HapticEffectCategory.Impact,
                defaultEnabled: false,
                requiredSignals:
                [
                    new HapticSignalRequirement(HapticSignalKind.Motion, true),
                    new HapticSignalRequirement(HapticSignalKind.Event, false)
                ],
                parameters:
                [
                    Continuous("gain", "Gain", 0, 1, 0.5, 0.01, 2, "%"),
                    Integer("pulse-frequency-hz", "Pulse Frequency", 5, 120, 44, 1, "Hz"),
                    Integer("pulse-duration-ms", "Pulse Duration", 10, 300, 90, 1, "ms"),
                    Integer("cooldown-ms", "Cooldown", 0, 1000, 120, 1, "ms"),
                    Continuous("vertical-g-threshold", "Vertical G Threshold", 0.1, 5, 0.75, 0.01, 2, "g", true)
                ],
                runtimeFactory: static settings => new ImpactEffectRuntime(settings)),
            new HapticEffectDescriptor(
                key: "road-texture",
                displayName: "Road Texture",
                description: "Continuous road texture signal for BST-1 output and shared routing.",
                category: HapticEffectCategory.Surface,
                defaultEnabled: true,
                requiredSignals:
                [
                    new HapticSignalRequirement(HapticSignalKind.Telemetry, true)
                ],
                parameters:
                [
                    Continuous("gain", "Gain", 0, 1, 1, 0.01, 2, "%"),
                    Boolean("shared-signal-enabled", "Shared Signal Enabled", true, true),
                    Boolean("bst1-output-enabled", "BST-1 Output Enabled", true, false),
                    Integer("minimum-speed-kph", "Minimum Speed", 0, 80, 5, 1, "kph"),
                    Integer("full-intensity-speed-kph", "Speed Reference", 20, 360, 330, 1, "kph"),
                    Integer("low-speed-frequency-hz", "Low Speed Frequency", 20, 70, 40, 1, "Hz"),
                    Integer("high-speed-frequency-hz", "High Speed Frequency", 30, 90, 68, 1, "Hz"),
                    Continuous("speed-frequency-influence", "Speed Influence", 0, 1, 0.75, 0.01, 2, "%"),
                    Continuous("grain-amount", "Grain Amount", 0, 0.6, 0.18, 0.01, 2, "%")
                ],
                runtimeFactory: static settings => new RoadTextureEffectRuntime(settings)),
            new HapticEffectDescriptor(
                key: "slip-lock",
                displayName: "Slip / Lock",
                description: "Continuous wheel slip and wheel lock vibration routed from shared motion signals.",
                category: HapticEffectCategory.Slip,
                defaultEnabled: false,
                requiredSignals:
                [
                    new HapticSignalRequirement(HapticSignalKind.Telemetry, true),
                    new HapticSignalRequirement(HapticSignalKind.MotionEx, true)
                ],
                parameters:
                [
                    Boolean("wheel-slip-enabled", "Wheel Slip Enabled", false, false),
                    Continuous("wheel-slip-gain", "Wheel Slip Gain", 0, 1, 0.5, 0.01, 2, "%"),
                    Integer("wheel-slip-frequency-hz", "Wheel Slip Frequency", 5, 120, 52, 1, "Hz"),
                    Continuous("wheel-slip-noise-amount", "Wheel Slip Roughness", 0, 1, 0.18, 0.01, 2, "%"),
                    Boolean("wheel-lock-enabled", "Wheel Lock Enabled", false, false),
                    Continuous("wheel-lock-gain", "Wheel Lock Gain", 0, 1, 0.5, 0.01, 2, "%"),
                    Integer("wheel-lock-frequency-hz", "Wheel Lock Frequency", 5, 120, 68, 1, "Hz"),
                    Continuous("wheel-lock-noise-amount", "Wheel Lock Roughness", 0, 1, 0.24, 0.01, 2, "%"),
                    Integer("minimum-speed-kph", "Minimum Speed", 0, 120, 8, 1, "kph"),
                    Continuous("slip-ratio-threshold", "Slip Ratio Threshold", 0.01, 1, 0.08, 0.01, 2, "ratio"),
                    Continuous("slip-angle-threshold-rad", "Slip Angle Threshold", 0.01, 1, 0.08, 0.01, 2, "rad"),
                    Continuous("wheel-lock-wheel-speed-ratio-threshold", "Wheel Lock Speed Ratio Threshold", 0.05, 1, 0.35, 0.01, 2, "ratio")
                ],
                runtimeFactory: static settings => new SlipLockEffectRuntime(settings))
        ];

        _byKey = _all.ToDictionary(
            descriptor => descriptor.Key,
            descriptor => descriptor,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IHapticEffectDescriptor> All => _all;

    public IHapticEffectDescriptor GetRequired(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _byKey.TryGetValue(key, out var descriptor)
            ? descriptor
            : throw new KeyNotFoundException($"Unknown haptic effect descriptor '{key}'.");
    }

    private static EffectParameterDescriptor Continuous(
        string key,
        string displayName,
        double minimum,
        double maximum,
        double defaultValue,
        double step,
        int decimalPlaces,
        string unit,
        bool isAdvanced = false)
    {
        return new EffectParameterDescriptor(
            key,
            displayName,
            EffectParameterKind.Continuous,
            minimum,
            maximum,
            defaultValue,
            step,
            decimalPlaces,
            unit,
            isAdvanced);
    }

    private static EffectParameterDescriptor Integer(
        string key,
        string displayName,
        double minimum,
        double maximum,
        double defaultValue,
        double step,
        string unit,
        bool isAdvanced = false)
    {
        return new EffectParameterDescriptor(
            key,
            displayName,
            EffectParameterKind.Integer,
            minimum,
            maximum,
            defaultValue,
            step,
            0,
            unit,
            isAdvanced);
    }

    private static EffectParameterDescriptor Boolean(
        string key,
        string displayName,
        bool defaultValue,
        bool isAdvanced)
    {
        return new EffectParameterDescriptor(
            key,
            displayName,
            EffectParameterKind.Boolean,
            0,
            1,
            defaultValue ? 1d : 0d,
            1,
            0,
            "bool",
            isAdvanced);
    }
}
