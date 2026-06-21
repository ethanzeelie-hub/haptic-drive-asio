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
            new BuiltInDescriptor(
                "engine-rpm",
                "Engine RPM",
                HapticEffectCategory.Engine,
                defaultEnabled: false,
                requiredSignals:
                [
                    new HapticSignalRequirement(HapticFrameSignalNames.Telemetry, true),
                    new HapticSignalRequirement(HapticFrameSignalNames.CarStatus, false)
                ],
                parameters:
                [
                    Parameter("gain", "Gain", 0, 1, 0.5, 0.01, "%"),
                    Parameter("minimum-frequency-hz", "Minimum Frequency", 15, 80, 34, 1, "Hz"),
                    Parameter("maximum-frequency-hz", "Maximum Frequency", 20, 120, 50, 1, "Hz"),
                    Parameter("high-frequency-enabled", "High Frequency Enabled", 0, 1, 1, 1, "bool", true),
                    Parameter("high-frequency-hz", "High Frequency", 20, 120, 50, 1, "Hz", true),
                    Parameter("high-frequency-gain", "High Frequency Gain", 0, 1, 0.25, 0.01, "%", true),
                    Parameter("frequency-jitter-hz", "Frequency Jitter", 0, 10, 0, 0.1, "Hz", true),
                    Parameter("idle-throttle-gain", "Idle Throttle Gain", 0, 1, 0.35, 0.01, "%", true),
                    Parameter("pit-gain-multiplier", "Pit Gain Multiplier", 0, 1, 0.35, 0.01, "%", true)
                ]),
            new BuiltInDescriptor(
                "gear-shift",
                "Gear Shift",
                HapticEffectCategory.Shift,
                defaultEnabled: false,
                requiredSignals:
                [
                    new HapticSignalRequirement(HapticFrameSignalNames.Telemetry, true)
                ],
                parameters:
                [
                    Parameter("gain", "Gain", 0, 1, 0.5, 0.01, "%"),
                    Parameter("pulse-frequency-hz", "Pulse Frequency", 5, 120, 15, 1, "Hz"),
                    Parameter("pulse-duration-ms", "Pulse Duration", 10, 250, 80, 1, "ms"),
                    Parameter("rpm-modulation-enabled", "Modulate By RPM", 0, 1, 0, 1, "bool", true)
                ]),
            new BuiltInDescriptor(
                "kerb",
                "Kerb",
                HapticEffectCategory.Surface,
                defaultEnabled: false,
                requiredSignals:
                [
                    new HapticSignalRequirement(HapticFrameSignalNames.Telemetry, true),
                    new HapticSignalRequirement("surface-kinds", true)
                ],
                parameters:
                [
                    Parameter("gain", "Gain", 0, 1, 0.5, 0.01, "%"),
                    Parameter("base-frequency-hz", "Base Frequency", 5, 120, 20, 1, "Hz"),
                    Parameter("minimum-speed-kph", "Minimum Speed", 0, 80, 5, 1, "kph"),
                    Parameter("full-intensity-speed-kph", "Full Intensity Speed", 20, 300, 120, 1, "kph"),
                    Parameter("high-frequency-enabled", "High Frequency Enabled", 0, 1, 1, 1, "bool", true),
                    Parameter("high-frequency-hz", "High Frequency", 20, 120, 44, 1, "Hz", true),
                    Parameter("high-frequency-gain", "High Frequency Gain", 0, 1, 0.25, 0.01, "%", true),
                    Parameter("noise-amount", "Noise Amount", 0, 1, 0.08, 0.01, "%", true)
                ]),
            new BuiltInDescriptor(
                "impact",
                "Impact",
                HapticEffectCategory.Impact,
                defaultEnabled: false,
                requiredSignals:
                [
                    new HapticSignalRequirement(HapticFrameSignalNames.Motion, true),
                    new HapticSignalRequirement(HapticFrameSignalNames.Event, false)
                ],
                parameters:
                [
                    Parameter("gain", "Gain", 0, 1, 0.5, 0.01, "%"),
                    Parameter("pulse-frequency-hz", "Pulse Frequency", 5, 120, 44, 1, "Hz"),
                    Parameter("pulse-duration-ms", "Pulse Duration", 10, 300, 90, 1, "ms"),
                    Parameter("cooldown-ms", "Cooldown", 0, 1000, 120, 1, "ms"),
                    Parameter("vertical-g-threshold", "Vertical G Threshold", 0.1, 5, 0.75, 0.01, "g", true)
                ]),
            new BuiltInDescriptor(
                "road-texture",
                "Road Texture",
                HapticEffectCategory.Surface,
                defaultEnabled: true,
                requiredSignals:
                [
                    new HapticSignalRequirement(HapticFrameSignalNames.Telemetry, true),
                    new HapticSignalRequirement("surface-kinds", true)
                ],
                parameters:
                [
                    Parameter("gain", "Gain", 0, 1, 1, 0.01, "%"),
                    Parameter("shared-signal-enabled", "Shared Signal Enabled", 0, 1, 1, 1, "bool", true),
                    Parameter("bst1-output-enabled", "BST-1 Output Enabled", 0, 1, 1, 1, "bool"),
                    Parameter("minimum-speed-kph", "Minimum Speed", 0, 80, 5, 1, "kph"),
                    Parameter("full-intensity-speed-kph", "Speed Reference", 20, 360, 330, 1, "kph"),
                    Parameter("low-speed-frequency-hz", "Low Speed Frequency", 20, 70, 40, 1, "Hz"),
                    Parameter("high-speed-frequency-hz", "High Speed Frequency", 30, 90, 68, 1, "Hz"),
                    Parameter("speed-frequency-influence", "Speed Influence", 0, 1, 0.75, 0.01, "%"),
                    Parameter("grain-amount", "Grain Amount", 0, 0.6, 0.18, 0.01, "%")
                ]),
            new BuiltInDescriptor(
                "slip-lock",
                "Slip / Lock",
                HapticEffectCategory.Slip,
                defaultEnabled: false,
                requiredSignals:
                [
                    new HapticSignalRequirement(HapticFrameSignalNames.Telemetry, true),
                    new HapticSignalRequirement(HapticFrameSignalNames.MotionEx, true)
                ],
                parameters:
                [
                    Parameter("wheel-slip-enabled", "Wheel Slip Enabled", 0, 1, 0, 1, "bool"),
                    Parameter("wheel-slip-gain", "Wheel Slip Gain", 0, 1, 0.5, 0.01, "%"),
                    Parameter("wheel-slip-frequency-hz", "Wheel Slip Frequency", 5, 120, 52, 1, "Hz"),
                    Parameter("wheel-slip-noise-amount", "Wheel Slip Roughness", 0, 1, 0.18, 0.01, "%"),
                    Parameter("wheel-lock-enabled", "Wheel Lock Enabled", 0, 1, 0, 1, "bool"),
                    Parameter("wheel-lock-gain", "Wheel Lock Gain", 0, 1, 0.5, 0.01, "%"),
                    Parameter("wheel-lock-frequency-hz", "Wheel Lock Frequency", 5, 120, 68, 1, "Hz"),
                    Parameter("wheel-lock-noise-amount", "Wheel Lock Roughness", 0, 1, 0.24, 0.01, "%"),
                    Parameter("minimum-speed-kph", "Minimum Speed", 0, 120, 8, 1, "kph"),
                    Parameter("slip-ratio-threshold", "Slip Ratio Threshold", 0.01, 1, 0.08, 0.01, "ratio"),
                    Parameter("slip-angle-threshold-rad", "Slip Angle Threshold", 0.01, 1, 0.08, 0.01, "rad"),
                    Parameter("wheel-lock-wheel-speed-ratio-threshold", "Wheel Lock Speed Ratio Threshold", 0.05, 1, 0.35, 0.01, "ratio")
                ]),
            new BuiltInDescriptor(
                "diagnostic-test",
                "Diagnostic Test",
                HapticEffectCategory.Diagnostic,
                defaultEnabled: false,
                requiredSignals: [],
                parameters:
                [
                    Parameter("gain", "Gain", 0, 1, 0.1, 0.01, "%"),
                    Parameter("frequency-hz", "Frequency", 5, 120, 40, 1, "Hz"),
                    Parameter("pulse-duration-ms", "Pulse Duration", 10, 500, 100, 1, "ms")
                ])
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

    private static EffectParameterDescriptor Parameter(
        string key,
        string displayName,
        double minimum,
        double maximum,
        double defaultValue,
        double step,
        string unit,
        bool isAdvanced = false)
    {
        return new EffectParameterDescriptor(key, displayName, minimum, maximum, defaultValue, step, unit, isAdvanced);
    }

    private sealed class BuiltInDescriptor(
        string key,
        string displayName,
        HapticEffectCategory category,
        bool defaultEnabled,
        IReadOnlyList<HapticSignalRequirement> requiredSignals,
        IReadOnlyList<EffectParameterDescriptor> parameters) : IHapticEffectDescriptor
    {
        public string Key => key;

        public string DisplayName => displayName;

        public HapticEffectCategory Category => category;

        public IReadOnlyList<HapticSignalRequirement> RequiredSignals => requiredSignals;

        public IReadOnlyList<EffectParameterDescriptor> Parameters => parameters;

        public EffectSettingsDocument CreateDefaultSettings()
        {
            return new EffectSettingsDocument(
                key,
                defaultEnabled,
                parameters.ToDictionary(
                    parameter => parameter.Key,
                    parameter => parameter.DefaultValue,
                    StringComparer.OrdinalIgnoreCase));
        }

        public IHapticEffectRuntime CreateRuntime(EffectSettingsDocument settings)
        {
            return new MetadataOnlyRuntime(key, Normalize(settings));
        }

        public IReadOnlyList<string> Validate(EffectSettingsDocument settings)
        {
            var normalized = Normalize(settings);
            var errors = new List<string>();

            foreach (var parameter in parameters)
            {
                if (!normalized.Parameters.TryGetValue(parameter.Key, out var value))
                {
                    errors.Add($"{displayName}: missing parameter '{parameter.Key}'.");
                    continue;
                }

                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    errors.Add($"{displayName}: parameter '{parameter.Key}' must be finite.");
                    continue;
                }

                if (value < parameter.Minimum || value > parameter.Maximum)
                {
                    errors.Add($"{displayName}: parameter '{parameter.Key}' must be between {parameter.Minimum} and {parameter.Maximum}.");
                }
            }

            return errors;
        }

        private EffectSettingsDocument Normalize(EffectSettingsDocument settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in parameters)
            {
                var value = settings.Parameters.TryGetValue(parameter.Key, out var existing)
                    ? existing
                    : parameter.DefaultValue;
                values[parameter.Key] = value;
            }

            return new EffectSettingsDocument(key, settings.Enabled, values);
        }
    }

    private sealed class MetadataOnlyRuntime(
        string effectKey,
        EffectSettingsDocument settings) : IHapticEffectRuntime
    {
        private EffectSettingsDocument _settings = settings;

        public string EffectKey => effectKey;

        public void UpdateSettings(EffectSettingsDocument settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Render(
            in HapticFrame frame,
            Span<float> left,
            Span<float> right,
            int sampleRate,
            int frameCount)
        {
            if (!_settings.Enabled)
            {
                return;
            }

            // Stage 6 metadata runtime only. Real descriptor-backed render integration follows when the
            // engine itself is converted away from fixed effect slots.
        }
    }
}
