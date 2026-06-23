namespace HapticDrive.Asio.Audio.Effects.Registry;

public sealed class HapticEffectDescriptor : IHapticEffectDescriptor
{
    private readonly Func<EffectSettingsDocument, IHapticEffectRuntime> _runtimeFactory;

    public HapticEffectDescriptor(
        string key,
        string displayName,
        string description,
        HapticEffectCategory category,
        bool defaultEnabled,
        IReadOnlyList<HapticSignalRequirement> requiredSignals,
        IReadOnlyList<EffectParameterDescriptor> parameters,
        Func<EffectSettingsDocument, IHapticEffectRuntime> runtimeFactory)
    {
        Key = string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("Effect key is required.", nameof(key))
            : key;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException("Display name is required.", nameof(displayName))
            : displayName;
        Description = string.IsNullOrWhiteSpace(description)
            ? throw new ArgumentException("Description is required.", nameof(description))
            : description;
        Category = category;
        DefaultEnabled = defaultEnabled;
        RequiredSignals = requiredSignals ?? throw new ArgumentNullException(nameof(requiredSignals));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _runtimeFactory = runtimeFactory ?? throw new ArgumentNullException(nameof(runtimeFactory));
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public HapticEffectCategory Category { get; }

    public bool DefaultEnabled { get; }

    public IReadOnlyList<HapticSignalRequirement> RequiredSignals { get; }

    public IReadOnlyList<EffectParameterDescriptor> Parameters { get; }

    public EffectSettingsDocument CreateDefaultSettings()
    {
        return new EffectSettingsDocument(
            Key,
            DefaultEnabled,
            Parameters.ToDictionary(
                parameter => parameter.Key,
                parameter => parameter.DefaultValue,
                StringComparer.OrdinalIgnoreCase));
    }

    public EffectSettingsDocument Normalize(
        EffectSettingsDocument? settings,
        ICollection<string>? messages = null)
    {
        var defaults = CreateDefaultSettings();
        var enabled = settings?.Enabled ?? defaults.Enabled;
        var sourceParameters = settings?.Parameters
            ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var normalizedParameters = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in Parameters)
        {
            if (!sourceParameters.TryGetValue(parameter.Key, out var rawValue))
            {
                normalizedParameters[parameter.Key] = parameter.DefaultValue;
                messages?.Add($"{DisplayName}: missing parameter '{parameter.Key}' defaulted to {FormatValue(parameter.DefaultValue, parameter.DecimalPlaces)}.");
                continue;
            }

            if (double.IsNaN(rawValue) || double.IsInfinity(rawValue))
            {
                normalizedParameters[parameter.Key] = parameter.DefaultValue;
                messages?.Add($"{DisplayName}: invalid parameter '{parameter.Key}' defaulted to {FormatValue(parameter.DefaultValue, parameter.DecimalPlaces)}.");
                continue;
            }

            var normalized = NormalizeValue(rawValue, parameter, out var wasRepaired);
            normalizedParameters[parameter.Key] = normalized;
            if (wasRepaired)
            {
                messages?.Add($"{DisplayName}: parameter '{parameter.Key}' repaired to {FormatValue(normalized, parameter.DecimalPlaces)}.");
            }
        }

        return new EffectSettingsDocument(Key, enabled, normalizedParameters);
    }

    public IHapticEffectRuntime CreateRuntime(EffectSettingsDocument settings)
    {
        var normalized = Normalize(settings);
        return _runtimeFactory(normalized);
    }

    public IReadOnlyList<string> Validate(EffectSettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = new List<string>();
        foreach (var parameter in Parameters)
        {
            if (!settings.Parameters.TryGetValue(parameter.Key, out var value))
            {
                errors.Add($"{DisplayName}: missing parameter '{parameter.Key}'.");
                continue;
            }

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                errors.Add($"{DisplayName}: parameter '{parameter.Key}' must be finite.");
                continue;
            }

            if (parameter.Kind == EffectParameterKind.Boolean && value is not (0d or 1d))
            {
                errors.Add($"{DisplayName}: parameter '{parameter.Key}' must be 0 or 1.");
                continue;
            }

            if (parameter.Kind == EffectParameterKind.Integer && Math.Abs(value - Math.Round(value, MidpointRounding.AwayFromZero)) > 0.0001d)
            {
                errors.Add($"{DisplayName}: parameter '{parameter.Key}' must be an integer.");
                continue;
            }

            if (value < parameter.Minimum || value > parameter.Maximum)
            {
                errors.Add($"{DisplayName}: parameter '{parameter.Key}' must be between {parameter.Minimum} and {parameter.Maximum}.");
            }
        }

        return errors;
    }

    private static double NormalizeValue(
        double value,
        EffectParameterDescriptor parameter,
        out bool wasRepaired)
    {
        var normalized = Math.Clamp(value, parameter.Minimum, parameter.Maximum);

        if (parameter.Kind == EffectParameterKind.Boolean)
        {
            normalized = normalized >= 0.5d ? 1d : 0d;
        }
        else if (parameter.Kind == EffectParameterKind.Integer)
        {
            normalized = Math.Round(normalized, 0, MidpointRounding.AwayFromZero);
        }
        else if (parameter.DecimalPlaces >= 0)
        {
            normalized = Math.Round(normalized, parameter.DecimalPlaces, MidpointRounding.AwayFromZero);
        }

        wasRepaired = Math.Abs(normalized - value) > 0.0001d;
        return normalized;
    }

    private static string FormatValue(double value, int decimalPlaces)
    {
        return decimalPlaces <= 0
            ? value.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            : value.ToString($"0.{new string('#', decimalPlaces)}", System.Globalization.CultureInfo.InvariantCulture);
    }
}
