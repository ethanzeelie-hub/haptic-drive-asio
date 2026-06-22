using System.Globalization;
using System.Net;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Actuation.Shift;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal sealed record PaddleMappingControlInputs(
    string? SelectedDeviceId,
    string? FallbackSelectedDeviceId,
    string LeftButtonText,
    string RightButtonText,
    string DebounceText);

internal sealed record PaddleMappingControlValues(
    string LeftButtonText,
    string RightButtonText,
    string DebounceText);

internal sealed record ShiftIntentControlInputs(
    bool IsEnabled,
    ShiftIntentMode? SelectedMode);

internal sealed record MockGearPulseControlInputs(
    bool IsEnabled,
    PHprGearPulseTarget? TargetModule,
    string StrengthText,
    string FrequencyText,
    string DurationText);

internal sealed record PhprPedalEffectControlInputs(
    bool IsEnabled,
    PHprGearPulseTarget? TargetModule,
    string StrengthText,
    string FrequencyText,
    string DurationText);

internal sealed record MockPedalEffectsControlInputs(
    bool IsEnabled,
    PhprPedalEffectControlInputs RoadVibration,
    PhprPedalEffectControlInputs WheelSlip,
    PhprPedalEffectControlInputs WheelLock);

internal sealed record RealPhprGearPulseControlInputs(
    bool IsEnabled,
    string StrengthText,
    string FrequencyText,
    string DurationText);

internal sealed record NormalPhprGearPulseControlInputs(
    bool IsEnabled,
    string StrengthText,
    string FrequencyText);

internal sealed record RealRoadVibrationPedalControlInputs(
    bool IsEnabled,
    string MinimumStrengthText,
    string StrengthText,
    string MinimumFrequencyText,
    string FrequencyText,
    string DurationText);

internal sealed record RealSlipLockEffectControlInputs(
    bool IsEnabled,
    PHprGearPulseTarget? TargetModule,
    string MinimumStrengthText,
    string StrengthText,
    string MinimumFrequencyText,
    string FrequencyText,
    string TextureCadenceText,
    string DurationText);

internal sealed record RealPhprDirectControlInputs(
    bool DirectControlEnabled,
    string ReportIdText,
    string ReportLengthText,
    PHprHidReportTransport? Transport,
    string InterfaceText,
    PHprDirectOutputCandidate? SelectedCandidate);

internal sealed record ForwardingDestinationControlInputs(
    string NameText,
    string HostText,
    string PortText,
    bool Enabled);

internal sealed record Bst1ManualPulseControlInputs(
    string StrengthText,
    string OutputTrimText,
    string FrequencyText,
    string DurationText);

internal sealed record Bst1ManualPulseSettingsSnapshot(
    float StrengthPercent,
    float OutputTrimPercent,
    float FrequencyHz,
    int DurationMs);

internal sealed record Bst1PaddleGearPulseControlInputs(
    bool IsEnabled,
    string StrengthText,
    string FrequencyText,
    bool UseSharedDuration,
    string DurationText,
    int ExistingCustomDurationMs);

internal sealed record Bst1PaddleGearPulseSettingsSnapshot(
    bool IsEnabled,
    float StrengthPercent,
    float FrequencyHz,
    bool UseSharedDuration,
    int CustomDurationMs,
    string StatusMessage);

internal sealed record Bst1PulseControlValues(
    string ManualStrengthText,
    string OutputTrimText,
    string ManualFrequencyText,
    string ManualDurationText,
    bool PaddleGearPulseEnabled,
    string PaddleGearStrengthText,
    string PaddleGearFrequencyText,
    bool PaddleGearSyncDuration,
    string PaddleGearDurationText,
    bool PaddleGearDurationEnabled,
    string PaddleGearEffectiveDurationText);

internal sealed record PaddleGearBenchControlInputs(
    bool IsEnabled,
    PHprGearPulseTarget? TargetModule,
    PHprRealGearPulseSettings BrakeSourceSettings,
    PHprRealGearPulseSettings ThrottleSourceSettings,
    int SharedDurationMs);

internal sealed record PaddleGearBenchControlValues(
    bool IsEnabled,
    bool IsArmed,
    PHprGearPulseTarget TargetModule,
    PaddleGearBenchTestOutputMode OutputMode,
    string StrengthText,
    string FrequencyText,
    string DurationText,
    bool LocalGearTestModeEnabled,
    bool LocalGearTestAutoStartListener);

internal sealed record GearPulseControlValues(
    bool IsEnabled,
    string StrengthText,
    string FrequencyText,
    string DurationText);

internal sealed record RoadVibrationPedalControlValues(
    bool IsEnabled,
    string MinimumStrengthText,
    string StrengthText,
    string MinimumFrequencyText,
    string FrequencyText,
    string DurationText);

internal sealed record SlipLockEffectControlValues(
    bool IsEnabled,
    PHprGearPulseTarget TargetModule,
    string MinimumStrengthText,
    string StrengthText,
    string MinimumFrequencyText,
    string FrequencyText,
    string TextureCadenceText,
    string DurationText);

internal sealed record PedalEffectControlValues(
    bool IsEnabled,
    PHprGearPulseTarget TargetModule,
    string StrengthText,
    string FrequencyText,
    string DurationText);

internal sealed record MockGearPulseControlValues(
    bool IsEnabled,
    PHprGearPulseTarget TargetModule,
    string StrengthText,
    string FrequencyText,
    string DurationText);

internal sealed record MockPedalEffectsControlValues(
    bool IsEnabled,
    PedalEffectControlValues RoadVibration,
    PedalEffectControlValues WheelSlip,
    PedalEffectControlValues WheelLock);

internal sealed record RealPhprControlValues(
    bool DirectControlEnabled,
    bool DirectControlArmed,
    string InterfaceText,
    string ReportIdText,
    string ReportLengthText,
    PHprHidReportTransport ReportTransport,
    GearPulseControlValues BrakeGearPulse,
    GearPulseControlValues ThrottleGearPulse,
    bool RealRoadVibrationEnabled,
    RoadVibrationPedalControlValues BrakeRoadVibration,
    RoadVibrationPedalControlValues ThrottleRoadVibration,
    bool RealSlipLockEnabled,
    SlipLockEffectControlValues WheelSlip,
    SlipLockEffectControlValues WheelLock);

internal sealed record NormalPhprPedalsControlValues(
    int SharedDurationMs,
    GearPulseControlValues BrakeGearPulse,
    GearPulseControlValues ThrottleGearPulse);

internal static class ControlSettingsSnapshotBuilder
{
    private static readonly PHprSafetyLimits ControlSafetyLimits = PHprSafetyLimits.Default;

    public static ReplayTimingModeOption GetReplayTimingModeOption(ReplayTimingPreference preference)
    {
        return preference == ReplayTimingPreference.FastDebug
            ? ReplayTimingModeOption.FastDebug
            : ReplayTimingModeOption.RealTime;
    }

    public static ReplayTimingModeOption GetSelectedReplayTimingMode(ReplayTimingModeOption? selectedMode)
    {
        return selectedMode ?? ReplayTimingModeOption.RealTime;
    }

    public static ReplayTimingPreference GetReplayTimingPreference(ReplayTimingModeOption? selectedMode)
    {
        return GetSelectedReplayTimingMode(selectedMode).IsFastDebug
            ? ReplayTimingPreference.FastDebug
            : ReplayTimingPreference.RealTime;
    }

    public static ShiftIntentProcessorOptions BuildShiftIntentOptions(ShiftIntentControlInputs inputs)
    {
        return new ShiftIntentProcessorOptions
        {
            IsEnabled = inputs.IsEnabled,
            Mode = inputs.SelectedMode ?? ShiftIntentMode.InstantPaddleOnly
        }.Normalize();
    }

    public static PaddleMappingControlValues BuildPaddleMappingControlValues(WheelPaddleMapping mapping)
    {
        var normalized = mapping.Normalize();
        return new PaddleMappingControlValues(
            LeftButtonText: normalized.LeftPaddleButtonId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            RightButtonText: normalized.RightPaddleButtonId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            DebounceText: ((int)normalized.DebounceDuration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture));
    }

    public static bool TryBuildPaddleMapping(
        PaddleMappingControlInputs inputs,
        out WheelPaddleMapping mapping,
        out string message)
    {
        mapping = WheelPaddleMapping.Default;
        if (!TryParseOptionalButtonId(inputs.LeftButtonText, out var leftButtonId, out message))
        {
            return false;
        }

        if (!TryParseOptionalButtonId(inputs.RightButtonText, out var rightButtonId, out message))
        {
            return false;
        }

        if (!int.TryParse(inputs.DebounceText.Trim(), out var debounceMilliseconds)
            || debounceMilliseconds is < 0 or > 250)
        {
            message = "Paddle debounce must be between 0 and 250 ms.";
            return false;
        }

        mapping = new WheelPaddleMapping
        {
            SelectedDeviceId = inputs.SelectedDeviceId ?? inputs.FallbackSelectedDeviceId,
            SelectedMethod = InputDiscoveryMethod.WindowsGameController,
            LeftPaddleButtonId = leftButtonId,
            RightPaddleButtonId = rightButtonId,
            DebounceDuration = TimeSpan.FromMilliseconds(debounceMilliseconds)
        }.Normalize();
        message = "Paddle input mapping ready.";
        return true;
    }

    public static MockGearPulseControlValues BuildMockGearPulseControlValues(PHprGearPulseRouterOptions options)
    {
        var normalized = options.Normalize();
        return new MockGearPulseControlValues(
            IsEnabled: normalized.IsEnabled,
            TargetModule: normalized.TargetModule,
            StrengthText: PhprUiValueConverter.FormatPercent(normalized.Profile.Strength01),
            FrequencyText: PhprUiValueConverter.FormatFrequency(normalized.Profile.FrequencyHz),
            DurationText: normalized.Profile.DurationMs.ToString(CultureInfo.InvariantCulture));
    }

    public static bool TryBuildMockGearPulseOptions(
        MockGearPulseControlInputs inputs,
        PHprGearPulseRouterOptions current,
        out PHprGearPulseRouterOptions options,
        out string message)
    {
        options = current;
        if (!PhprUiValueConverter.TryParseStrengthPercent(
                inputs.StrengthText,
                "Mock P-HPR",
                out var strength,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseFrequencyHz(
                inputs.FrequencyText,
                "Mock P-HPR",
                out var frequency,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseDurationMs(
                inputs.DurationText,
                "Mock P-HPR",
                out var duration,
                out message))
        {
            return false;
        }

        options = new PHprGearPulseRouterOptions
        {
            IsEnabled = inputs.IsEnabled,
            TargetModule = inputs.TargetModule ?? PHprGearPulseTarget.Both,
            Profile = PHprGearPulseProfile.Default with
            {
                Strength01 = strength,
                FrequencyHz = frequency,
                DurationMs = duration
            }
        }.Normalize();
        message = "Mock P-HPR gear pulse routing ready.";
        return true;
    }

    public static PaddleGearBenchControlValues BuildPaddleGearBenchControlValues(
        PaddleGearBenchTestOptions options,
        bool localGearTestModeEnabled,
        bool localGearTestAutoStartListener)
    {
        var normalized = options.Normalize();
        return new PaddleGearBenchControlValues(
            IsEnabled: normalized.IsEnabled,
            IsArmed: normalized.IsArmed,
            TargetModule: normalized.TargetModule,
            OutputMode: normalized.OutputMode,
            StrengthText: PhprUiValueConverter.FormatPercent(normalized.Profile.Strength01),
            FrequencyText: PhprUiValueConverter.FormatFrequency(normalized.Profile.FrequencyHz),
            DurationText: normalized.Profile.DurationMs.ToString(CultureInfo.InvariantCulture),
            LocalGearTestModeEnabled: localGearTestModeEnabled,
            LocalGearTestAutoStartListener: localGearTestAutoStartListener);
    }

    public static bool TryBuildPaddleGearBenchOptions(
        PaddleGearBenchControlInputs inputs,
        out PaddleGearBenchTestOptions options,
        out string message)
    {
        var target = inputs.TargetModule ?? PHprGearPulseTarget.Both;
        var sourceSettings = target == PHprGearPulseTarget.Throttle
            ? inputs.ThrottleSourceSettings
            : inputs.BrakeSourceSettings;
        var sharedDurationMs = Bst1GearPulseDurationSync.NormalizeGearDuration(inputs.SharedDurationMs);
        options = new PaddleGearBenchTestOptions
        {
            IsEnabled = inputs.IsEnabled,
            IsArmed = inputs.IsEnabled,
            OutputMode = PaddleGearBenchTestOutputMode.Direct,
            TargetModule = target,
            Profile = PHprGearPulseProfile.Default with
            {
                Strength01 = sourceSettings.Strength01,
                FrequencyHz = sourceSettings.FrequencyHz,
                DurationMs = sharedDurationMs
            }
        }.Normalize();
        message = "Paddle Gear Bench Test options ready.";
        return true;
    }

    public static MockPedalEffectsControlValues BuildMockPedalEffectsControlValues(PHprPedalEffectsRouterOptions options)
    {
        var normalized = options.Normalize();
        return new MockPedalEffectsControlValues(
            IsEnabled: normalized.IsEnabled,
            RoadVibration: BuildPedalEffectControlValues(PHprPedalEffectKind.RoadVibration, normalized.RoadVibration),
            WheelSlip: BuildPedalEffectControlValues(PHprPedalEffectKind.WheelSlip, normalized.WheelSlip),
            WheelLock: BuildPedalEffectControlValues(PHprPedalEffectKind.WheelLock, normalized.WheelLock));
    }

    public static bool TryBuildMockPedalEffectsOptions(
        MockPedalEffectsControlInputs inputs,
        PHprPedalEffectsRouterOptions current,
        out PHprPedalEffectsRouterOptions options,
        out string message)
    {
        options = current;
        if (!TryBuildPedalEffectState(
                PHprPedalEffectKind.RoadVibration,
                inputs.RoadVibration,
                out var road,
                out message)
            || !TryBuildPedalEffectState(
                PHprPedalEffectKind.WheelSlip,
                inputs.WheelSlip,
                out var slip,
                out message)
            || !TryBuildPedalEffectState(
                PHprPedalEffectKind.WheelLock,
                inputs.WheelLock,
                out var wheelLock,
                out message))
        {
            return false;
        }

        options = current with
        {
            IsEnabled = inputs.IsEnabled,
            RoadVibration = road,
            WheelSlip = slip,
            WheelLock = wheelLock
        };
        options = options.Normalize();
        message = "Mock P-HPR pedal effects routing ready.";
        return true;
    }

    public static bool TryBuildSharedPhprGearPulseDuration(
        string durationText,
        out int durationMs,
        out string message)
    {
        if (!PhprUiValueConverter.TryParseDurationMs(
                durationText,
                "P-HPR gear pulse",
                out durationMs,
                out message))
        {
            return false;
        }

        durationMs = Bst1GearPulseDurationSync.NormalizeGearDuration(durationMs);
        message = "Shared P-HPR gear pulse duration ready.";
        return true;
    }

    public static bool TryBuildNormalPhprGearPulseSettings(
        string label,
        NormalPhprGearPulseControlInputs inputs,
        int durationMs,
        out PHprRealGearPulseSettings settings,
        out string message)
    {
        settings = PHprRealGearPulseSettings.Default;
        if (!PhprUiValueConverter.TryParseStrengthPercent(
                inputs.StrengthText,
                $"{label} P-HPR",
                out var strength,
                out message)
            || !PhprUiValueConverter.TryParseFrequencyHz(
                inputs.FrequencyText,
                $"{label} P-HPR",
                out var frequency,
                out message))
        {
            return false;
        }

        settings = new PHprRealGearPulseSettings
        {
            IsEnabled = inputs.IsEnabled,
            Strength01 = strength,
            FrequencyHz = frequency,
            DurationMs = durationMs
        }.Normalize(ControlSafetyLimits);
        message = $"{label} P-HPR pulse ready.";
        return true;
    }

    public static GearPulseControlValues BuildGearPulseControlValues(PHprRealGearPulseSettings settings)
    {
        var normalized = settings.Normalize(ControlSafetyLimits);
        return new GearPulseControlValues(
            IsEnabled: normalized.IsEnabled,
            StrengthText: PhprUiValueConverter.FormatPercent(normalized.Strength01),
            FrequencyText: PhprUiValueConverter.FormatFrequency(normalized.FrequencyHz),
            DurationText: normalized.DurationMs.ToString(CultureInfo.InvariantCulture));
    }

    public static NormalPhprPedalsControlValues BuildNormalPhprPedalsControlValues(
        PHprRealOutputOptions options,
        int sharedDurationMs)
    {
        var normalized = options.Normalize(ControlSafetyLimits);
        var resolvedDuration = Bst1GearPulseDurationSync.NormalizeGearDuration(sharedDurationMs);
        return new NormalPhprPedalsControlValues(
            SharedDurationMs: resolvedDuration,
            BrakeGearPulse: BuildGearPulseControlValues(
                Bst1GearPulseDurationSync.WithSharedDuration(normalized.BrakeGearPulse, resolvedDuration)),
            ThrottleGearPulse: BuildGearPulseControlValues(
                Bst1GearPulseDurationSync.WithSharedDuration(normalized.ThrottleGearPulse, resolvedDuration)));
    }

    public static bool TryBuildRealPhprOutputOptions(
        RealPhprDirectControlInputs inputs,
        PHprRealOutputOptions current,
        PHprRealGearPulseSettings brake,
        PHprRealGearPulseSettings throttle,
        out PHprRealOutputOptions options,
        out string message)
    {
        var normalizedCurrent = current.Normalize(ControlSafetyLimits);
        options = normalizedCurrent;
        if (!TryParseOptionalReportId(inputs.ReportIdText, out var reportId, out message))
        {
            return false;
        }

        if (!int.TryParse(inputs.ReportLengthText.Trim(), out var reportLength)
            || reportLength is < 1 or > 1_024)
        {
            message = "Real P-HPR report length must be a whole number from 1 to 1024 bytes.";
            return false;
        }

        var transport = inputs.Transport ?? PHprHidReportTransport.OutputReport;
        var directEnabled = inputs.DirectControlEnabled;
        var directArmed = directEnabled;
        var selector = inputs.SelectedCandidate?.ToSelector(reportId, transport) ?? PHprHidDeviceSelector.None;
        selector = selector with
        {
            ReportId = reportId ?? selector.ReportId,
            ReportLength = reportLength,
            Transport = transport,
            InterfaceName = string.IsNullOrWhiteSpace(inputs.InterfaceText)
                ? selector.InterfaceName
                : inputs.InterfaceText.Trim()
        };
        var normalizedSelector = selector.Normalize();
        var previousSelector = normalizedCurrent.Selector.Normalize();
        var candidateSourceMethod = inputs.SelectedCandidate?.SourceMethod ?? PHprDirectOutputCandidateSourceMethod.Unknown;
        var candidateIsRawInputOnly = inputs.SelectedCandidate?.IsRawInputOnly ?? false;
        var candidateHasOpenableHidPath = inputs.SelectedCandidate?.HasOpenableHidPath ?? false;
        var candidateOutputReportCapabilityKnown = inputs.SelectedCandidate?.HasKnownOutputReportCapability ?? false;
        var candidateFeatureReportCapabilityKnown = inputs.SelectedCandidate?.HasKnownFeatureReportCapability ?? false;
        var reportShape = PHprHidReportShapeValidator.Validate(inputs.SelectedCandidate, normalizedSelector);
        var openCheckStillSameSelector = normalizedCurrent.OpenCheckAttempted
            && SelectorMatchesForOpenCheck(previousSelector, normalizedSelector)
            && normalizedCurrent.CandidateSourceMethod == candidateSourceMethod
            && normalizedCurrent.CandidateIsRawInputOnly == candidateIsRawInputOnly
            && normalizedCurrent.CandidateHasOpenableHidPath == candidateHasOpenableHidPath
            && normalizedCurrent.CandidateOutputReportCapabilityKnown == candidateOutputReportCapabilityKnown
            && normalizedCurrent.CandidateFeatureReportCapabilityKnown == candidateFeatureReportCapabilityKnown;
        options = normalizedCurrent with
        {
            DirectControlEnabled = directEnabled,
            DirectControlArmed = directArmed,
            CandidateSourceMethod = candidateSourceMethod,
            CandidateIsRawInputOnly = candidateIsRawInputOnly,
            CandidateHasOpenableHidPath = candidateHasOpenableHidPath,
            CandidateOutputReportCapabilityKnown = candidateOutputReportCapabilityKnown,
            CandidateFeatureReportCapabilityKnown = candidateFeatureReportCapabilityKnown,
            ReportShapeValidationAttempted = reportShape.Attempted,
            ReportShapeValidationSucceeded = reportShape.Succeeded,
            ReportShapeValidationFailed = reportShape.Failed,
            ReportShapeValidationMessage = reportShape.Message,
            OpenCheckAttempted = openCheckStillSameSelector && normalizedCurrent.OpenCheckAttempted,
            OpenCheckSucceeded = openCheckStillSameSelector && normalizedCurrent.OpenCheckSucceeded,
            OpenCheckFailed = openCheckStillSameSelector && normalizedCurrent.OpenCheckFailed,
            OpenCheckSanitizedErrorCategory = openCheckStillSameSelector ? normalizedCurrent.OpenCheckSanitizedErrorCategory : null,
            Selector = normalizedSelector,
            BrakeGearPulse = brake,
            ThrottleGearPulse = throttle
        };
        options = options.Normalize(ControlSafetyLimits);
        message = "Real P-HPR direct-control options ready for this session only.";
        return true;
    }

    public static byte? ParseOptionalReportIdOrNull(string text)
    {
        return TryParseOptionalReportId(text, out var reportId, out _)
            ? reportId
            : null;
    }

    public static RealPhprControlValues BuildRealPhprControlValues(
        PHprRealOutputOptions options,
        PHprRoadVibrationRouterOptions roadOptions,
        PHprSlipLockRouterOptions slipLockOptions)
    {
        var normalizedOptions = options.Normalize(ControlSafetyLimits);
        var normalizedRoad = roadOptions.Normalize(ControlSafetyLimits);
        var normalizedSlipLock = slipLockOptions.Normalize(ControlSafetyLimits);
        return new RealPhprControlValues(
            DirectControlEnabled: normalizedOptions.DirectControlEnabled,
            DirectControlArmed: normalizedOptions.DirectControlArmed,
            InterfaceText: normalizedOptions.Selector.InterfaceName,
            ReportIdText: normalizedOptions.Selector.ReportId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ReportLengthText: normalizedOptions.Selector.ReportLength.ToString(CultureInfo.InvariantCulture),
            ReportTransport: normalizedOptions.Selector.Transport,
            BrakeGearPulse: BuildGearPulseControlValues(normalizedOptions.BrakeGearPulse),
            ThrottleGearPulse: BuildGearPulseControlValues(normalizedOptions.ThrottleGearPulse),
            RealRoadVibrationEnabled: normalizedRoad.IsEnabled,
            BrakeRoadVibration: BuildRoadVibrationPedalControlValues(normalizedRoad.Brake),
            ThrottleRoadVibration: BuildRoadVibrationPedalControlValues(normalizedRoad.Throttle),
            RealSlipLockEnabled: normalizedSlipLock.IsEnabled,
            WheelSlip: BuildSlipLockEffectControlValues(PHprPedalEffectKind.WheelSlip, normalizedSlipLock.WheelSlip),
            WheelLock: BuildSlipLockEffectControlValues(PHprPedalEffectKind.WheelLock, normalizedSlipLock.WheelLock));
    }

    public static bool TryBuildRealGearPulseSettings(
        string label,
        RealPhprGearPulseControlInputs inputs,
        out PHprRealGearPulseSettings settings,
        out string message)
    {
        settings = PHprRealGearPulseSettings.Default;
        if (!PhprUiValueConverter.TryParseStrengthPercent(
                inputs.StrengthText,
                $"{label} real P-HPR",
                out var strength,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseFrequencyHz(
                inputs.FrequencyText,
                $"{label} real P-HPR",
                out var frequency,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseDurationMs(
                inputs.DurationText,
                $"{label} real P-HPR",
                out var duration,
                out message))
        {
            return false;
        }

        settings = new PHprRealGearPulseSettings
        {
            IsEnabled = inputs.IsEnabled,
            Strength01 = strength,
            FrequencyHz = frequency,
            DurationMs = duration
        }.Normalize(ControlSafetyLimits);
        message = $"{label} real P-HPR pulse settings ready.";
        return true;
    }

    public static bool TryBuildRealRoadVibrationPedalSettings(
        string label,
        RealRoadVibrationPedalControlInputs inputs,
        out PHprRoadVibrationPedalSettings settings,
        out string message)
    {
        settings = PHprRoadVibrationPedalSettings.Default;
        if (!PhprUiValueConverter.TryParseStrengthPercent(
                inputs.MinimumStrengthText,
                $"{label} minimum",
                out var minimumStrength,
                out message)
            || !PhprUiValueConverter.TryParseStrengthPercent(
                inputs.StrengthText,
                $"{label} maximum",
                out var strength,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseFrequencyHz(
                inputs.MinimumFrequencyText,
                $"{label} minimum",
                out var minimumFrequency,
                out message)
            || !PhprUiValueConverter.TryParseFrequencyHz(
                inputs.FrequencyText,
                $"{label} maximum",
                out var frequency,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseDurationMs(
                inputs.DurationText,
                label,
                out var duration,
                out message))
        {
            return false;
        }

        settings = new PHprRoadVibrationPedalSettings
        {
            IsEnabled = inputs.IsEnabled,
            MinimumStrength01 = minimumStrength,
            Strength01 = strength,
            MinimumFrequencyHz = minimumFrequency,
            FrequencyHz = frequency,
            DurationMs = duration
        }.Normalize(ControlSafetyLimits);
        message = $"{label} real P-HPR road settings ready.";
        return true;
    }

    public static RoadVibrationPedalControlValues BuildRoadVibrationPedalControlValues(PHprRoadVibrationPedalSettings settings)
    {
        var normalized = settings.Normalize(ControlSafetyLimits);
        return new RoadVibrationPedalControlValues(
            IsEnabled: normalized.IsEnabled,
            MinimumStrengthText: PhprUiValueConverter.FormatPercent(normalized.MinimumStrength01),
            StrengthText: PhprUiValueConverter.FormatPercent(normalized.Strength01),
            MinimumFrequencyText: PhprUiValueConverter.FormatFrequency(normalized.MinimumFrequencyHz),
            FrequencyText: PhprUiValueConverter.FormatFrequency(normalized.FrequencyHz),
            DurationText: normalized.DurationMs.ToString(CultureInfo.InvariantCulture));
    }

    public static bool TryBuildRealRoadVibrationOptions(
        bool isEnabled,
        PHprRoadVibrationRouterOptions current,
        PHprRoadVibrationPedalSettings brake,
        PHprRoadVibrationPedalSettings throttle,
        out PHprRoadVibrationRouterOptions options,
        out string message)
    {
        options = current.Normalize(ControlSafetyLimits) with
        {
            IsEnabled = isEnabled,
            Brake = brake,
            Throttle = throttle
        };
        options = options.Normalize(ControlSafetyLimits);
        message = "Real P-HPR road-vibration options ready.";
        return true;
    }

    public static bool TryBuildRealSlipLockEffectSettings(
        string label,
        PHprPedalEffectKind kind,
        RealSlipLockEffectControlInputs inputs,
        out PHprSlipLockEffectSettings settings,
        out string message)
    {
        settings = PHprSlipLockEffectSettings.DefaultFor(kind);
        var target = inputs.TargetModule ?? settings.TargetModule;
        if (!PhprUiValueConverter.TryParseStrengthPercent(
                inputs.MinimumStrengthText,
                $"{label} minimum",
                out var minimumStrength,
                out message)
            || !PhprUiValueConverter.TryParseStrengthPercent(
                inputs.StrengthText,
                $"{label} maximum",
                out var strength,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseFrequencyHz(
                inputs.MinimumFrequencyText,
                $"{label} minimum",
                out var minimumFrequency,
                out message)
            || !PhprUiValueConverter.TryParseFrequencyHz(
                inputs.FrequencyText,
                $"{label} maximum",
                out var frequency,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseDurationMs(
                inputs.TextureCadenceText,
                $"{label} texture cadence",
                PHprSlipLockEffectSettings.MinimumTextureCadenceMs,
                PHprSlipLockEffectSettings.MaximumTextureCadenceMs,
                out var textureCadenceMs,
                out message)
            || !PhprUiValueConverter.TryParseDurationMs(
                inputs.DurationText,
                label,
                out var duration,
                out message))
        {
            return false;
        }

        settings = new PHprSlipLockEffectSettings
        {
            IsEnabled = inputs.IsEnabled,
            TargetModule = target,
            MinimumStrength01 = minimumStrength,
            Strength01 = strength,
            MinimumFrequencyHz = minimumFrequency,
            FrequencyHz = frequency,
            TextureCadenceMs = textureCadenceMs,
            DurationMs = duration
        }.Normalize(kind, ControlSafetyLimits);
        message = $"{label} real P-HPR slip/lock settings ready.";
        return true;
    }

    public static SlipLockEffectControlValues BuildSlipLockEffectControlValues(
        PHprPedalEffectKind kind,
        PHprSlipLockEffectSettings settings)
    {
        var normalized = settings.Normalize(kind, ControlSafetyLimits);
        return new SlipLockEffectControlValues(
            IsEnabled: normalized.IsEnabled,
            TargetModule: normalized.TargetModule,
            MinimumStrengthText: PhprUiValueConverter.FormatPercent(normalized.MinimumStrength01),
            StrengthText: PhprUiValueConverter.FormatPercent(normalized.Strength01),
            MinimumFrequencyText: PhprUiValueConverter.FormatFrequency(normalized.MinimumFrequencyHz),
            FrequencyText: PhprUiValueConverter.FormatFrequency(normalized.FrequencyHz),
            TextureCadenceText: normalized.TextureCadenceMs.ToString(CultureInfo.InvariantCulture),
            DurationText: normalized.DurationMs.ToString(CultureInfo.InvariantCulture));
    }

    public static bool TryBuildRealSlipLockOptions(
        bool isEnabled,
        PHprSlipLockRouterOptions current,
        PHprSlipLockEffectSettings wheelSlip,
        PHprSlipLockEffectSettings wheelLock,
        out PHprSlipLockRouterOptions options,
        out string message)
    {
        options = current.Normalize(ControlSafetyLimits) with
        {
            IsEnabled = isEnabled,
            WheelSlip = wheelSlip,
            WheelLock = wheelLock
        };
        options = options.Normalize(ControlSafetyLimits);
        message = "Real P-HPR slip/lock options ready.";
        return true;
    }

    public static PedalEffectControlValues BuildPedalEffectControlValues(PHprPedalEffectKind kind, PHprPedalEffectState state)
    {
        var normalized = state.Normalize(kind);
        return new PedalEffectControlValues(
            IsEnabled: normalized.IsEnabled,
            TargetModule: normalized.TargetModule,
            StrengthText: PhprUiValueConverter.FormatPercent(normalized.Profile.Strength01),
            FrequencyText: PhprUiValueConverter.FormatFrequency(normalized.Profile.FrequencyHz),
            DurationText: normalized.Profile.DurationMs.ToString(CultureInfo.InvariantCulture));
    }

    public static bool TryBuildPedalEffectState(
        PHprPedalEffectKind kind,
        PhprPedalEffectControlInputs inputs,
        out PHprPedalEffectState state,
        out string message)
    {
        var defaults = PHprPedalEffectState.DefaultFor(kind);
        state = defaults;
        var label = FormatPedalEffectKind(kind);
        if (!PhprUiValueConverter.TryParseStrengthPercent(
                inputs.StrengthText,
                label,
                out var strength,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseFrequencyHz(
                inputs.FrequencyText,
                label,
                out var frequency,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseDurationMs(
                inputs.DurationText,
                label,
                out var duration,
                out message))
        {
            return false;
        }

        state = defaults with
        {
            IsEnabled = inputs.IsEnabled,
            TargetModule = inputs.TargetModule ?? defaults.TargetModule,
            Profile = defaults.Profile with
            {
                Strength01 = strength,
                FrequencyHz = frequency,
                DurationMs = duration
            }
        };
        state = state.Normalize(kind);
        message = $"{label} pedal effect ready.";
        return true;
    }

    public static bool TryBuildForwardingDestinationSetting(
        ForwardingDestinationControlInputs inputs,
        out ForwardingDestinationSetting setting,
        out string message)
    {
        setting = new ForwardingDestinationSetting();
        var host = inputs.HostText.Trim();
        var name = inputs.NameText.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            message = "Forwarding host or IP address is required.";
            return false;
        }

        if (!int.TryParse(inputs.PortText.Trim(), out var port) || port is < 1 or > 65_535)
        {
            message = "Forwarding port must be between 1 and 65535.";
            return false;
        }

        if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
        {
            message = "Forwarding host must be a valid DNS name, localhost, IPv4 address, or IPv6 address.";
            return false;
        }

        if (inputs.Enabled && IsObviousUdpLoopback(host, port))
        {
            message = $"Forwarding to {host}:{port} would loop back to the local listener port and is blocked.";
            return false;
        }

        setting = new ForwardingDestinationSetting
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"{host}:{port}" : name,
            Host = host,
            Port = port,
            Enabled = inputs.Enabled
        };
        message = "Forwarding destination ready.";
        return true;
    }

    public static Bst1PulseControlValues BuildBst1PulseControlValues(
        float manualStrengthPercent,
        float outputTrimPercent,
        float manualFrequencyHz,
        int manualDurationMs,
        bool paddleGearPulseEnabled,
        float paddleGearStrengthPercent,
        float paddleGearFrequencyHz,
        bool paddleGearSyncDuration,
        int sharedPhprGearPulseDurationMs,
        int paddleGearCustomDurationMs,
        int effectiveBst1PaddleGearDurationMs)
    {
        return new Bst1PulseControlValues(
            ManualStrengthText: manualStrengthPercent.ToString("0", CultureInfo.InvariantCulture),
            OutputTrimText: outputTrimPercent.ToString("0", CultureInfo.InvariantCulture),
            ManualFrequencyText: manualFrequencyHz.ToString("0.#", CultureInfo.InvariantCulture),
            ManualDurationText: manualDurationMs.ToString(CultureInfo.InvariantCulture),
            PaddleGearPulseEnabled: paddleGearPulseEnabled,
            PaddleGearStrengthText: paddleGearStrengthPercent.ToString("0", CultureInfo.InvariantCulture),
            PaddleGearFrequencyText: paddleGearFrequencyHz.ToString("0.#", CultureInfo.InvariantCulture),
            PaddleGearSyncDuration: paddleGearSyncDuration,
            PaddleGearDurationText: (paddleGearSyncDuration
                    ? sharedPhprGearPulseDurationMs
                    : paddleGearCustomDurationMs)
                .ToString(CultureInfo.InvariantCulture),
            PaddleGearDurationEnabled: !paddleGearSyncDuration,
            PaddleGearEffectiveDurationText:
                $"Effective duration: {effectiveBst1PaddleGearDurationMs} ms ({(paddleGearSyncDuration ? "sync" : "custom")}); P-HPR gear {sharedPhprGearPulseDurationMs} ms; custom BST-1 {paddleGearCustomDurationMs} ms.");
    }

    public static bool TryBuildBst1ManualPulseSettings(
        Bst1ManualPulseControlInputs inputs,
        out Bst1ManualPulseSettingsSnapshot settings,
        out string message)
    {
        settings = new Bst1ManualPulseSettingsSnapshot(50f, 200f, 50f, 45);
        if (!TryParsePercent(inputs.StrengthText, out var strengthPercent, out message))
        {
            return false;
        }

        if (!TryParseBst1OutputTrim(inputs.OutputTrimText, out var outputTrimPercent, out message))
        {
            return false;
        }

        if (!TryParseBst1Frequency(inputs.FrequencyText, out var frequencyHz, out message))
        {
            return false;
        }

        if (!TryParseBst1Duration(inputs.DurationText, out var durationMs, out message))
        {
            return false;
        }

        settings = new Bst1ManualPulseSettingsSnapshot(
            StrengthPercent: strengthPercent,
            OutputTrimPercent: outputTrimPercent,
            FrequencyHz: frequencyHz,
            DurationMs: durationMs);
        message = "BST-1 manual pulse settings ready.";
        return true;
    }

    public static bool TryBuildBst1PaddleGearPulseSettings(
        Bst1PaddleGearPulseControlInputs inputs,
        out Bst1PaddleGearPulseSettingsSnapshot settings,
        out string message)
    {
        settings = new Bst1PaddleGearPulseSettingsSnapshot(
            IsEnabled: false,
            StrengthPercent: 50f,
            FrequencyHz: 50f,
            UseSharedDuration: true,
            CustomDurationMs: Bst1GearPulseDurationSync.DefaultGearDurationMs,
            StatusMessage: "BST-1 paddle gear pulse is disabled.");
        if (!TryParsePercent(inputs.StrengthText, out var strengthPercent, out message))
        {
            return false;
        }

        if (!TryParseBst1Frequency(inputs.FrequencyText, out var frequencyHz, out message))
        {
            return false;
        }

        var durationText = inputs.UseSharedDuration
            ? inputs.ExistingCustomDurationMs.ToString(CultureInfo.InvariantCulture)
            : inputs.DurationText;
        if (!TryParseBst1Duration(durationText, out var durationMs, out message))
        {
            return false;
        }

        var isEnabled = inputs.IsEnabled;
        settings = new Bst1PaddleGearPulseSettingsSnapshot(
            IsEnabled: isEnabled,
            StrengthPercent: strengthPercent,
            FrequencyHz: frequencyHz,
            UseSharedDuration: inputs.UseSharedDuration,
            CustomDurationMs: durationMs,
            StatusMessage: isEnabled
                ? "BST-1 paddle gear pulse enabled for accepted bench Pressed events."
                : "BST-1 paddle gear pulse is disabled.");
        message = "BST-1 paddle gear pulse settings ready.";
        return true;
    }

    private static bool TryParseOptionalButtonId(string text, out int? buttonId, out string message)
    {
        buttonId = null;
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            message = "Button is unmapped.";
            return true;
        }

        if (!int.TryParse(trimmed, out var parsed) || parsed is < 1 or > 128)
        {
            message = "Paddle button IDs must be whole numbers from 1 to 128.";
            return false;
        }

        buttonId = parsed;
        message = "Button mapped.";
        return true;
    }

    private static bool TryParseOptionalReportId(string text, out byte? reportId, out string message)
    {
        reportId = null;
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            message = "No report ID selected.";
            return true;
        }

        var style = NumberStyles.Integer;
        var valueText = trimmed;
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            style = NumberStyles.HexNumber;
            valueText = trimmed[2..];
        }
        else if (trimmed.Any(char.IsAsciiHexDigit) && trimmed.Any(char.IsAsciiLetter))
        {
            style = NumberStyles.HexNumber;
        }

        if (!byte.TryParse(valueText, style, CultureInfo.InvariantCulture, out var parsed))
        {
            message = "Real P-HPR report ID must be blank, 0-255, 0xF1, or F1.";
            return false;
        }

        reportId = parsed;
        message = "Report ID ready.";
        return true;
    }

    private static bool SelectorMatchesForOpenCheck(
        PHprHidDeviceSelector previous,
        PHprHidDeviceSelector current)
    {
        return string.Equals(previous.DevicePath, current.DevicePath, StringComparison.Ordinal)
            && previous.ReportId == current.ReportId
            && previous.ReportLength == current.ReportLength
            && previous.Transport == current.Transport;
    }

    private static bool TryParsePercent(string text, out float value, out string message)
    {
        if (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || !float.IsFinite(value))
        {
            message = "BST-1 strength must be a number from 0 to 100%.";
            value = 50f;
            return false;
        }

        value = Math.Clamp(value, 0f, 100f);
        message = "BST-1 strength ready.";
        return true;
    }

    private static bool TryParseBst1OutputTrim(string text, out float value, out string message)
    {
        if (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || !float.IsFinite(value))
        {
            message = "BST-1 output trim must be a number from 25 to 400%.";
            value = 200f;
            return false;
        }

        value = Math.Clamp(
            value,
            ManualAsioHardwareTestRequest.MinimumOutputTrim * 100f,
            ManualAsioHardwareTestRequest.MaximumOutputTrim * 100f);
        message = "BST-1 output trim ready.";
        return true;
    }

    private static bool TryParseBst1Frequency(string text, out float value, out string message)
    {
        if (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || !float.IsFinite(value))
        {
            message = "BST-1 frequency must be a number from 10 to 80 Hz.";
            value = 50f;
            return false;
        }

        value = Math.Clamp(
            value,
            ManualAsioHardwareTestRequest.MinimumFrequencyHz,
            ManualAsioHardwareTestRequest.MaximumFrequencyHz);
        message = "BST-1 frequency ready.";
        return true;
    }

    private static bool TryParseBst1Duration(string text, out int value, out string message)
    {
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            message = "BST-1 duration must be a whole number of milliseconds.";
            value = 45;
            return false;
        }

        value = Math.Clamp(
            value,
            ManualAsioHardwareTestRequest.MinimumDurationMilliseconds,
            (int)ManualAsioHardwareTestRequest.MaximumDuration.TotalMilliseconds);
        message = "BST-1 duration ready.";
        return true;
    }

    private static string FormatPedalEffectKind(PHprPedalEffectKind kind)
    {
        return kind switch
        {
            PHprPedalEffectKind.RoadVibration => "Road vibration",
            PHprPedalEffectKind.WheelSlip => "Wheel slip",
            PHprPedalEffectKind.WheelLock => "Wheel lock",
            _ => kind.ToString()
        };
    }

    private static bool IsObviousUdpLoopback(string host, int port)
    {
        if (port != UdpTelemetryReceiverOptions.DefaultPort)
        {
            return false;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address)
            && IPAddress.IsLoopback(address);
    }
}
