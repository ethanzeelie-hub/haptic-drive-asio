using System.IO;
using System.Xml.Linq;

namespace HapticDrive.Asio.App.Tests;

public sealed class AppThemeResourceTests
{
    [Fact]
    public void AppMergesThemeAndStylesResourceDictionaries()
    {
        var appXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "App.xaml");
        var sources = appXaml
            .Descendants()
            .Where(element => element.Name.LocalName == "ResourceDictionary")
            .Select(element => element.Attribute("Source")?.Value)
            .Where(source => source is not null)
            .ToArray();

        Assert.Contains("Resources/Theme.xaml", sources);
        Assert.Contains("Resources/Styles.xaml", sources);
    }

    [Fact]
    public void ThemeAndStylesExposeShellResourceKeys()
    {
        var themeXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Resources", "Theme.xaml");
        var stylesXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Resources", "Styles.xaml");

        AssertResourceKeys(
            themeXaml,
            "AppBackgroundBrush",
            "AppChromeBrush",
            "AppSidebarBrush",
            "AppTopBarBrush",
            "AppSurfaceBrush",
            "AppSurfaceAltBrush",
            "AppSurfaceRaisedBrush",
            "AppBorderBrush",
            "AppBorderStrongBrush",
            "AppTextBrush",
            "AppMutedTextBrush",
            "AppAccentBrush",
            "AppAccentHoverBrush",
            "AppAccentPressedBrush",
            "AppAccentSoftBrush",
            "AppDangerBrush",
            "AppDangerHoverBrush",
            "AppDangerPressedBrush",
            "AppSuccessBrush",
            "AppWarningBrush",
            "AppInfoBrush",
            "AppInputBrush",
            "AppInputFocusBrush",
            "AppOverlayBrush",
            "AppCardCornerRadius",
            "AppControlCornerRadius",
            "AppCardPadding");

        AssertResourceKeys(
            stylesXaml,
            "CardBorderStyle",
            "MetricCardBorderStyle",
            "TopStatusBadgeStyle",
            "PrimaryButtonStyle",
            "SecondaryButtonStyle",
            "TopBarButtonStyle",
            "DangerButtonStyle",
            "FieldTextBoxStyle",
            "SidebarListBoxStyle",
            "SidebarListBoxItemStyle",
            "SectionHeadingTextBlockStyle",
            "MutedTextBlockStyle",
            "StatusBadgeTextBlockStyle",
            "PageKickerTextBlockStyle");
    }

    [Fact]
    public void EffectsPageUsesHardwareEffectSectionsAndMovedControls()
    {
        var mainWindowXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "MainWindow.xaml");
        var effectsPanel = FindElementByXName(mainWindowXaml, "EffectsPanel");
        var effectsText = GetTextValues(effectsPanel);
        var effectsNames = GetXNameValues(effectsPanel);

        Assert.Contains("Shared / Global Effect Settings", effectsText);
        Assert.Contains("BST-1 Seat Shaker", effectsText);
        Assert.Contains("Brake P-HPR", effectsText);
        Assert.Contains("Throttle P-HPR", effectsText);

        Assert.Contains("NormalPhprGearDurationTextBox", effectsNames);
        Assert.Contains("SharedRoadSignalEnabledCheckBox", effectsNames);
        Assert.Contains("Bst1RoadOutputEnabledCheckBox", effectsNames);
        Assert.DoesNotContain("RoadTextureEnabledCheckBox", effectsNames);
        Assert.Contains("Bst1PaddleGearPulseEnabledCheckBox", effectsNames);
        Assert.Contains("NormalPhprBrakeEnabledCheckBox", effectsNames);
        Assert.Contains("NormalPhprThrottleEnabledCheckBox", effectsNames);
        Assert.Contains("RealRoadBrakeStrengthTextBox", effectsNames);
        Assert.Contains("RealRoadThrottleStrengthTextBox", effectsNames);
        Assert.Contains("RealLockStrengthTextBox", effectsNames);
        Assert.Contains("RealSlipStrengthTextBox", effectsNames);

        var devicesPanelNames = GetXNameValues(FindElementByXName(mainWindowXaml, "DevicesPanel"));
        Assert.DoesNotContain("NormalPhprGearDurationTextBox", devicesPanelNames);
        Assert.DoesNotContain("Bst1PaddleGearPulseEnabledCheckBox", devicesPanelNames);
    }

    [Fact]
    public void EffectsPageRoadTextureCardsDoNotExposePulseDurationControls()
    {
        var mainWindowXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "MainWindow.xaml");
        var effectsPanel = FindElementByXName(mainWindowXaml, "EffectsPanel");
        var effectsText = GetTextValues(effectsPanel);
        var effectsNames = GetXNameValues(effectsPanel);

        Assert.True(
            effectsText.Count(text => text.Contains("No normal pulse duration", StringComparison.OrdinalIgnoreCase)) >= 2);
        Assert.DoesNotContain("RealRoadBrakeDurationTextBox", effectsNames);
        Assert.DoesNotContain("RealRoadThrottleDurationTextBox", effectsNames);
        Assert.DoesNotContain(effectsNames, name => name.Contains("RoadTextureDuration", StringComparison.Ordinal));
    }

    [Fact]
    public void DevicesPageKeepsHardwareReadinessAndManualTestControls()
    {
        var mainWindowXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "MainWindow.xaml");
        var devicesPanel = FindElementByXName(mainWindowXaml, "DevicesPanel");
        var devicesText = GetTextValues(devicesPanel);
        var devicesNames = GetXNameValues(devicesPanel);

        Assert.Contains("OutputModeComboBox", devicesNames);
        Assert.Contains("AsioDriverComboBox", devicesNames);
        Assert.Contains("AsioOutputChannelComboBox", devicesNames);
        Assert.Contains("AsioArmCheckBox", devicesNames);
        Assert.Contains("ManualBst1StrengthTextBox", devicesNames);
        Assert.Contains("ManualBst1FrequencyTextBox", devicesNames);
        Assert.Contains("ManualBst1DurationTextBox", devicesNames);

        Assert.Contains("PhprPedalsMasterEnableCheckBox", devicesNames);
        Assert.Contains("PhprPedalsModeComboBox", devicesNames);
        Assert.Contains("TestPhprBrakePulseButton", devicesNames);
        Assert.Contains("TestPhprThrottlePulseButton", devicesNames);

        Assert.Contains("PaddleInputDeviceComboBox", devicesNames);
        Assert.Contains("StartPaddleInputListenerButton", devicesNames);
        Assert.Contains("LeftPaddleButtonTextBox", devicesNames);
        Assert.Contains("RightPaddleButtonTextBox", devicesNames);
        Assert.Contains("PaddleDebounceTextBox", devicesNames);

        Assert.Contains(
            devicesText,
            text => text.Contains("Detailed Local Gear Test and Paddle Gear Bench validation controls are under Advanced Diagnostics", StringComparison.Ordinal));
    }

    [Fact]
    public void DevicesPageDoesNotContainAdvancedValidationOrLowLevelControls()
    {
        var mainWindowXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "MainWindow.xaml");
        var devicesNames = GetXNameValues(FindElementByXName(mainWindowXaml, "DevicesPanel"));

        Assert.DoesNotContain("LocalGearTestModeCheckBox", devicesNames);
        Assert.DoesNotContain("StartGearTestListenerButton", devicesNames);
        Assert.DoesNotContain("PaddleGearBenchEnabledCheckBox", devicesNames);
        Assert.DoesNotContain("PaddleGearBenchTargetComboBox", devicesNames);
        Assert.DoesNotContain("RealPhprCandidateComboBox", devicesNames);
        Assert.DoesNotContain("RealRoadBrakeMinStrengthTextBox", devicesNames);
        Assert.DoesNotContain("RealSlipTargetComboBox", devicesNames);
        Assert.DoesNotContain("MockGearPulseEnabledCheckBox", devicesNames);
        Assert.DoesNotContain("MockPedalEffectsEnabledCheckBox", devicesNames);
    }

    [Fact]
    public void AdvancedDiagnosticsContainsBenchDirectMockAndLowLevelDiagnostics()
    {
        var mainWindowXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "MainWindow.xaml");
        var advancedPanel = FindElementByXName(mainWindowXaml, "AdvancedPhprDiagnosticsPanel");
        var advancedText = GetTextValues(advancedPanel);
        var advancedNames = GetXNameValues(advancedPanel);

        Assert.Contains("Paddle Gear Bench Test", advancedText);
        Assert.Contains("LocalGearTestModeCheckBox", advancedNames);
        Assert.Contains("LocalGearTestAutoStartListenerCheckBox", advancedNames);
        Assert.Contains("StartGearTestListenerButton", advancedNames);
        Assert.Contains("PaddleGearBenchEnabledCheckBox", advancedNames);
        Assert.Contains("PaddleGearBenchTargetComboBox", advancedNames);
        Assert.Contains("PaddleGearBenchStatusText", advancedNames);

        Assert.Contains("RealPhprCandidateComboBox", advancedNames);
        Assert.Contains("RealPhprReportIdTextBox", advancedNames);
        Assert.Contains("RealRoadBrakeMinStrengthTextBox", advancedNames);
        Assert.Contains("RealRoadThrottleMinStrengthTextBox", advancedNames);
        Assert.Contains("RealSlipTargetComboBox", advancedNames);
        Assert.Contains("RealLockTargetComboBox", advancedNames);

        Assert.Contains("MockGearPulseEnabledCheckBox", advancedNames);
        Assert.Contains("MockPedalEffectsEnabledCheckBox", advancedNames);
    }

    [Fact]
    public void RoutingMixerPageContainsMixerSafetyControlsAndSummaries()
    {
        var mainWindowXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "MainWindow.xaml");
        var mixerPanel = FindElementByXName(mainWindowXaml, "MixerPanel");
        var mixerText = GetTextValues(mixerPanel);
        var mixerNames = GetXNameValues(mixerPanel);

        Assert.Contains("MasterGainSlider", mixerNames);
        Assert.Contains("MixerMuteCheckBox", mixerNames);
        Assert.Contains("SafetyOutputGainSlider", mixerNames);
        Assert.DoesNotContain("SafetyOutputCeilingSlider", mixerNames);
        Assert.DoesNotContain("LimiterEnabledCheckBox", mixerNames);
        Assert.Contains("MixerOutputPeakStatusText", mixerNames);
        Assert.Contains("MixerLimiterActivityStatusText", mixerNames);
        Assert.Contains("MixerEmergencyMuteStatusText", mixerNames);

        Assert.Contains("Routing / Mixer", mixerText);
        Assert.Contains("Mixer And Safety", mixerText);
        Assert.Contains(
            mixerText,
            text => text.Contains("Limiter stays on automatically. Internal output ceiling stays at 100%.", StringComparison.Ordinal));
        Assert.Contains("Output Routing Summary", mixerText);
        Assert.Contains("Active Effects", mixerText);
        Assert.Contains("Priority And Ducking", mixerText);
    }

    [Fact]
    public void RecordingsPageContainsReplayModeAndRenameControls()
    {
        var mainWindowXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "MainWindow.xaml");
        var recordingsPanel = FindElementByXName(mainWindowXaml, "RecordingsPanel");
        var recordingsText = GetTextValues(recordingsPanel);
        var recordingsNames = GetXNameValues(recordingsPanel);

        Assert.Contains("ReplayTimingModeComboBox", recordingsNames);
        Assert.Contains("RecordingLibraryListBox", recordingsNames);
        Assert.Contains("DeleteSelectedRecordingButton", recordingsNames);
        Assert.Contains("RenameSelectedRecordingButton", recordingsNames);
        Assert.Contains("RecordingRenameTextBox", recordingsNames);

        Assert.Contains("Recording And Replay", recordingsText);
        Assert.Contains(
            recordingsText,
            text => text.Contains("Rename to", StringComparison.Ordinal));
        Assert.Contains(
            recordingsText,
            text => text.Contains("Recording captures raw UDP payload bytes before parser validation.", StringComparison.Ordinal));
    }

    [Fact]
    public void RoutingMixerPageContainsOutputRouteAndPrioritySummaries()
    {
        var mainWindowXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "MainWindow.xaml");
        var mixerPanel = FindElementByXName(mainWindowXaml, "MixerPanel");
        var mixerText = GetTextValues(mixerPanel);
        var mixerNames = GetXNameValues(mixerPanel);

        Assert.Contains("Bst1RoutingSummaryText", mixerNames);
        Assert.Contains("Bst1EffectsSummaryText", mixerNames);
        Assert.Contains("BrakePhprRoutingSummaryText", mixerNames);
        Assert.Contains("BrakePhprEffectsSummaryText", mixerNames);
        Assert.Contains("ThrottlePhprRoutingSummaryText", mixerNames);
        Assert.Contains("ThrottlePhprEffectsSummaryText", mixerNames);
        Assert.Contains("PriorityDuckingSummaryText", mixerNames);
        Assert.Contains("ActiveEffectsSummaryText", mixerNames);

        Assert.Contains("BST-1 / ASIO", mixerText);
        Assert.Contains("Brake P-HPR", mixerText);
        Assert.Contains("Throttle P-HPR", mixerText);
        Assert.Contains(
            mixerText,
            text => text.Contains("Normal effect tuning stays on Effects", StringComparison.Ordinal));
    }

    private static XDocument LoadSourceXaml(params string[] pathSegments)
    {
        var sourcePath = FindRepositoryRoot();
        foreach (var segment in pathSegments)
        {
            sourcePath = Path.Combine(sourcePath, segment);
        }

        return XDocument.Load(sourcePath);
    }

    private static void AssertResourceKeys(XDocument xaml, params string[] expectedKeys)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var keys = xaml
            .Descendants()
            .Select(element => element.Attribute(x + "Key")?.Value)
            .Where(key => key is not null)
            .ToHashSet();

        foreach (var expectedKey in expectedKeys)
        {
            Assert.Contains(expectedKey, keys);
        }
    }

    private static XElement FindElementByXName(XDocument xaml, string name)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return xaml
            .Descendants()
            .First(element => string.Equals(element.Attribute(x + "Name")?.Value, name, StringComparison.Ordinal));
    }

    private static HashSet<string> GetXNameValues(XElement element)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return element
            .Descendants()
            .Select(descendant => descendant.Attribute(x + "Name")?.Value)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal)!;
    }

    private static IReadOnlyList<string> GetTextValues(XElement element)
    {
        return element
            .Descendants()
            .Select(descendant => descendant.Attribute("Text")?.Value)
            .Where(text => text is not null)
            .Select(text => text!)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var appXamlPath = Path.Combine(directory.FullName, "src", "HapticDrive.Asio.App", "App.xaml");
            if (File.Exists(appXamlPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
