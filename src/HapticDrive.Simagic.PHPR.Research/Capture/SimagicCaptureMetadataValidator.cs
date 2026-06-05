namespace HapticDrive.Simagic.PHPR.Research.Capture;

public sealed class SimagicCaptureMetadataValidator
{
    public SimagicCaptureValidationResult Validate(SimagicCaptureMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var messages = new List<SimagicCaptureValidationMessage>();
        ValidateRequired(metadata, messages);
        ValidateScenarioFields(metadata, messages);
        ValidatePrivateStorage(metadata, messages);
        ValidateRedaction(metadata, messages);
        return SimagicCaptureValidationResult.FromMessages(messages);
    }

    private static void ValidateRequired(
        SimagicCaptureMetadata metadata,
        List<SimagicCaptureValidationMessage> messages)
    {
        AddErrorIfMissing(messages, metadata.CaptureId, nameof(metadata.CaptureId), "Capture ID is required.");

        if (metadata.ScenarioId is null)
        {
            messages.Add(new SimagicCaptureValidationMessage(
                SimagicCaptureValidationSeverity.Error,
                nameof(metadata.ScenarioId),
                "Scenario ID is required."));
        }

        AddErrorIfMissing(messages, metadata.CaptureFileName, nameof(metadata.CaptureFileName), "Capture filename is required.");
        AddWarningIfMissing(messages, metadata.CaptureStartedAtUtc, nameof(metadata.CaptureStartedAtUtc), "Capture start time should be recorded in UTC.");
        AddWarningIfMissing(messages, metadata.CaptureDuration, nameof(metadata.CaptureDuration), "Capture duration should be recorded.");
        AddWarningIfMissing(messages, metadata.Software.CaptureTool, "Software.CaptureTool", "Capture tool should be recorded, for example USBPcap/Wireshark.");
        AddWarningIfMissing(messages, metadata.Software.CaptureToolVersion, "Software.CaptureToolVersion", "Capture tool version should be recorded.");
        AddWarningIfMissing(messages, metadata.Software.SoftwareUnderTest, "Software.SoftwareUnderTest", "Software under test should be recorded.");
        AddWarningIfMissing(messages, metadata.Action.ActionPerformed, "Action.ActionPerformed", "Exact one-action capture step should be recorded.");
        AddWarningIfMissing(messages, metadata.Action.ActualObservedBehaviour, "Action.ActualObservedBehaviour", "Observed behavior should be recorded.");

        if (metadata.Device.TargetModule == SimagicCaptureTargetModule.Unknown)
        {
            messages.Add(new SimagicCaptureValidationMessage(
                SimagicCaptureValidationSeverity.Warning,
                "Device.TargetModule",
                "Target module should be Brake, Throttle, or Both when known."));
        }
    }

    private static void ValidateScenarioFields(
        SimagicCaptureMetadata metadata,
        List<SimagicCaptureValidationMessage> messages)
    {
        if (metadata.ScenarioId is null || !SimagicCaptureScenarios.TryGet(metadata.ScenarioId.Value, out var scenario))
        {
            return;
        }

        if (scenario.RequiresStrength
            && (metadata.Action.SettingBefore.StrengthPercent is null || metadata.Action.SettingAfter.StrengthPercent is null))
        {
            messages.Add(new SimagicCaptureValidationMessage(
                SimagicCaptureValidationSeverity.Warning,
                "Action.SettingBefore/After.StrengthPercent",
                $"{scenario.Id} should record strength before and after the action."));
        }

        if (scenario.RequiresFrequency
            && (metadata.Action.SettingBefore.FrequencyHz is null || metadata.Action.SettingAfter.FrequencyHz is null))
        {
            messages.Add(new SimagicCaptureValidationMessage(
                SimagicCaptureValidationSeverity.Warning,
                "Action.SettingBefore/After.FrequencyHz",
                $"{scenario.Id} should record frequency before and after the action."));
        }

        if (scenario.RequiresDuration
            && (metadata.Action.SettingBefore.DurationMs is null || metadata.Action.SettingAfter.DurationMs is null))
        {
            messages.Add(new SimagicCaptureValidationMessage(
                SimagicCaptureValidationSeverity.Warning,
                "Action.SettingBefore/After.DurationMs",
                $"{scenario.Id} should record pulse duration before and after the action."));
        }
    }

    private static void ValidatePrivateStorage(
        SimagicCaptureMetadata metadata,
        List<SimagicCaptureValidationMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(metadata.RawCapturePath))
        {
            messages.Add(new SimagicCaptureValidationMessage(
                SimagicCaptureValidationSeverity.Warning,
                nameof(metadata.RawCapturePath),
                "Raw capture path should point to a private/gitignored local folder."));
            return;
        }

        if (!LooksPrivateOrGitIgnored(metadata.RawCapturePath))
        {
            messages.Add(new SimagicCaptureValidationMessage(
                SimagicCaptureValidationSeverity.Warning,
                nameof(metadata.RawCapturePath),
                "Raw capture path appears outside the recommended private/gitignored folders such as captures/private/simagic/."));
        }
    }

    private static void ValidateRedaction(
        SimagicCaptureMetadata metadata,
        List<SimagicCaptureValidationMessage> messages)
    {
        if (metadata.ContainsSerialNumbers)
        {
            messages.Add(new SimagicCaptureValidationMessage(
                SimagicCaptureValidationSeverity.Warning,
                nameof(metadata.ContainsSerialNumbers),
                "Metadata says serial numbers are present; sanitize before sharing or committing."));
        }

        if (metadata.ContainsPrivatePaths)
        {
            messages.Add(new SimagicCaptureValidationMessage(
                SimagicCaptureValidationSeverity.Warning,
                nameof(metadata.ContainsPrivatePaths),
                "Metadata says private paths are present; sanitize before sharing or committing."));
        }

        if (metadata.RedactionStatus is SimagicCaptureRedactionStatus.Unknown
            or SimagicCaptureRedactionStatus.NotReviewed
            or SimagicCaptureRedactionStatus.ContainsPrivateData)
        {
            messages.Add(new SimagicCaptureValidationMessage(
                SimagicCaptureValidationSeverity.Warning,
                nameof(metadata.RedactionStatus),
                "Redaction status should be ReviewedClean or Redacted before Stage 2I analysis input is shared."));
        }

        if (SimagicCaptureSanitizer.ContainsSensitiveData(metadata))
        {
            messages.Add(new SimagicCaptureValidationMessage(
                SimagicCaptureValidationSeverity.Warning,
                "Sanitizer",
                "Metadata contains serial-like strings or private paths that the sanitizer would redact."));
        }
    }

    private static bool LooksPrivateOrGitIgnored(string path)
    {
        var normalized = path.Replace('\\', '/').Trim().ToLowerInvariant();
        return normalized.Contains("captures/private/", StringComparison.Ordinal)
            || normalized.Contains("captures/simagic/", StringComparison.Ordinal)
            || normalized.Contains("private-captures/", StringComparison.Ordinal)
            || normalized.Contains("usb-captures/", StringComparison.Ordinal)
            || normalized.Contains("capture-metadata/private/", StringComparison.Ordinal);
    }

    private static void AddErrorIfMissing(
        List<SimagicCaptureValidationMessage> messages,
        string? value,
        string field,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            messages.Add(new SimagicCaptureValidationMessage(SimagicCaptureValidationSeverity.Error, field, message));
        }
    }

    private static void AddWarningIfMissing<T>(
        List<SimagicCaptureValidationMessage> messages,
        T? value,
        string field,
        string message)
        where T : struct
    {
        if (value is null)
        {
            messages.Add(new SimagicCaptureValidationMessage(SimagicCaptureValidationSeverity.Warning, field, message));
        }
    }

    private static void AddWarningIfMissing(
        List<SimagicCaptureValidationMessage> messages,
        string? value,
        string field,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            messages.Add(new SimagicCaptureValidationMessage(SimagicCaptureValidationSeverity.Warning, field, message));
        }
    }
}
