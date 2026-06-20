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
    public void MainWindowSourceContainsTestingValidationNavigationAndViewHost()
    {
        var mainWindowXaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml"));
        var mainWindowCode = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("TestingValidationViewControl", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("\"Testing / Validation\"", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("TestingValidationViewControl.Visibility = isTestingPage", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("TestingValidationViewControl.Apply(BuildTestingValidationStatusPresentation());", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("TestingValidationStatusPresenter.Build(new TestingValidationStatusSnapshot(", mainWindowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceContainsAdvancedDiagnosticsNavigationAndViewHost()
    {
        var mainWindowXaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml"));
        var mainWindowCode = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("AdvancedDiagnosticsViewControl", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("\"Advanced / Diagnostics\"", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("AdvancedDiagnosticsViewControl.Visibility = isAdvancedPage", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("AdvancedDiagnosticsViewControl.Apply(presentation);", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("DiagnosticsStatusPresenter.Build(snapshot)", mainWindowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellCopyDoesNotExposeLegacyStage18ChromeText()
    {
        var xamlSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml"));
        var codeSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.DoesNotContain("Stage 18", xamlSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Stage 18", codeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardContainsReadyChecklistCard()
    {
        var dashboardXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "DashboardView.xaml");
        var names = GetXNameValues(dashboardXaml.Root!);
        var text = GetTextValues(dashboardXaml.Root!);

        Assert.Contains("DashboardSummaryPanel", names);
        Assert.Contains("DashboardWorkflowStatusText", names);
        Assert.Contains("DashboardNextStepText", names);
        Assert.Contains("DashboardChecklistItemsControl", names);
        Assert.Contains("Ready Checklist", text);
        Assert.Contains("Next Step", text);
    }

    [Fact]
    public void MainWindowSourceContainsDashboardNavigationAndViewHost()
    {
        var mainWindowXaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml"));
        var mainWindowCode = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("DashboardViewControl", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("\"Dashboard\"", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("TopBarContextText.Text = $\"{page.NavigationLabel} / safe control\";", mainWindowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceContainsDevicesNavigationAndViewHost()
    {
        var mainWindowXaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml"));
        var mainWindowCode = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("DevicesViewControl", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("DevicesViewControl.Visibility = page.NavigationLabel == \"Devices\"", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("DevicesViewControl.Apply(presentation);", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("DevicesStatusPresenter.Build(new DevicesStatusSnapshot(", mainWindowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceContainsEffectsNavigationAndViewHost()
    {
        var mainWindowXaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml"));
        var mainWindowCode = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("EffectsViewControl", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("EffectsViewControl.Visibility = page.NavigationLabel == \"Effects\"", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("EffectsViewControl.Apply(presentation);", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("EffectsStatusPresenter.Build(new EffectsStatusSnapshot(", mainWindowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceContainsRoutingMixerNavigationAndViewHost()
    {
        var mainWindowXaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml"));
        var mainWindowCode = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("RoutingMixerViewControl", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("RoutingMixerViewControl.Visibility = page.NavigationLabel == \"Routing / Mixer\"", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("RoutingMixerViewControl.Apply(presentation);", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("RoutingMixerStatusPresenter.Build(new RoutingMixerStatusSnapshot(", mainWindowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceContainsTelemetryUdpNavigationAndViewHost()
    {
        var mainWindowXaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml"));
        var mainWindowCode = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("TelemetryUdpViewControl", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("TelemetryUdpViewControl.Visibility = isTelemetryPage", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("TelemetryUdpViewControl.Apply(BuildTelemetryUdpStatusPresentation());", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("TelemetryUdpStatusPresenter.Build(new TelemetryUdpStatusSnapshot(", mainWindowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceContainsProfilesNavigationAndViewHost()
    {
        var mainWindowXaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml"));
        var mainWindowCode = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("ProfilesViewControl", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("ProfilesViewControl.Visibility = isProfilesPage", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("ProfilesViewControl.Apply(BuildProfilesStatusPresentation(message, validationMessages));", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("ProfilesStatusPresenter.Build(new ProfilesStatusSnapshot(", mainWindowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardViewDoesNotExposeRawHidOrReportCopy()
    {
        var dashboardXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "DashboardView.xaml");
        var dashboardText = GetTextValues(dashboardXaml.Root!);

        Assert.DoesNotContain(dashboardText, text => text.Contains("Report ID", StringComparison.Ordinal));
        Assert.DoesNotContain(dashboardText, text => text.Contains("FeatureReport", StringComparison.Ordinal));
        Assert.DoesNotContain(dashboardText, text => text.Contains("HID", StringComparison.Ordinal));
        Assert.DoesNotContain(dashboardText, text => text.Contains("candidate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EffectsPageUsesHardwareEffectSectionsAndMovedControls()
    {
        var effectsXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "EffectsView.xaml");
        var effectsPanel = FindElementByXName(effectsXaml, "EffectsPanel");
        var effectsText = GetTextValues(effectsPanel);
        var effectsNames = GetXNameValues(effectsPanel);

        Assert.Contains("Shared / Global Effect Settings", effectsText);
        Assert.Contains("BST-1 Seat Shaker", effectsText);
        Assert.Contains("Brake P-HPR", effectsText);
        Assert.Contains("Throttle P-HPR", effectsText);

        Assert.Contains("NormalPhprGearDurationTextBox", effectsNames);
        Assert.Contains("SharedRoadSignalEnabledCheckBox", effectsNames);
        Assert.Contains("Bst1RoadOutputEnabledCheckBox", effectsNames);
        Assert.Contains("RoadTextureLowSpeedFrequencySlider", effectsNames);
        Assert.Contains("RoadTextureHighSpeedFrequencySlider", effectsNames);
        Assert.Contains("RoadTextureSpeedReferenceSlider", effectsNames);
        Assert.Contains("RoadTextureSpeedFrequencyInfluenceSlider", effectsNames);
        Assert.Contains("RoadTextureGrainAmountSlider", effectsNames);
        Assert.Contains("SlipWheelSlipEnabledCheckBox", effectsNames);
        Assert.Contains("SlipWheelSlipGainSlider", effectsNames);
        Assert.Contains("SlipWheelSlipFrequencySlider", effectsNames);
        Assert.Contains("SlipWheelSlipNoiseSlider", effectsNames);
        Assert.Contains("SlipThresholdSlider", effectsNames);
        Assert.Contains("SlipWheelLockEnabledCheckBox", effectsNames);
        Assert.Contains("SlipWheelLockGainSlider", effectsNames);
        Assert.Contains("SlipWheelLockFrequencySlider", effectsNames);
        Assert.Contains("SlipWheelLockNoiseSlider", effectsNames);
        Assert.Contains("SlipWheelLockSensitivitySlider", effectsNames);
        Assert.DoesNotContain("RoadTextureEnabledCheckBox", effectsNames);
        Assert.Contains("Bst1PaddleGearPulseEnabledCheckBox", effectsNames);
        Assert.Contains("NormalPhprBrakeEnabledCheckBox", effectsNames);
        Assert.Contains("NormalPhprThrottleEnabledCheckBox", effectsNames);
        Assert.Contains("RealRoadBrakeStrengthTextBox", effectsNames);
        Assert.Contains("RealRoadThrottleStrengthTextBox", effectsNames);
        Assert.Contains("RealLockStrengthTextBox", effectsNames);
        Assert.Contains("RealLockCadenceTextBox", effectsNames);
        Assert.Contains("RealSlipStrengthTextBox", effectsNames);
        Assert.Contains("RealSlipCadenceTextBox", effectsNames);
        Assert.DoesNotContain("RealSlipLockEnabledCheckBox", effectsNames);

        var devicesXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "DevicesView.xaml");
        var devicesPanelNames = GetXNameValues(FindElementByXName(devicesXaml, "DevicesPanel"));
        Assert.DoesNotContain("NormalPhprGearDurationTextBox", devicesPanelNames);
        Assert.DoesNotContain("Bst1PaddleGearPulseEnabledCheckBox", devicesPanelNames);
    }

    [Fact]
    public void EffectsPageRoadTextureCardsDoNotExposePulseDurationControls()
    {
        var effectsXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "EffectsView.xaml");
        var effectsPanel = FindElementByXName(effectsXaml, "EffectsPanel");
        var effectsText = GetTextValues(effectsPanel);
        var effectsNames = GetXNameValues(effectsPanel);

        Assert.True(
            effectsText.Count(text => text.Contains("No normal pulse duration", StringComparison.OrdinalIgnoreCase)) >= 2);
        Assert.DoesNotContain("RealRoadBrakeDurationTextBox", effectsNames);
        Assert.DoesNotContain("RealRoadThrottleDurationTextBox", effectsNames);
        Assert.DoesNotContain(effectsNames, name => name.Contains("RoadTextureDuration", StringComparison.Ordinal));
    }

    [Fact]
    public void DevicesPageKeepsHardwareReadinessAndSetupControls()
    {
        var devicesXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "DevicesView.xaml");
        var devicesPanel = FindElementByXName(devicesXaml, "DevicesPanel");
        var devicesText = GetTextValues(devicesPanel);
        var devicesNames = GetXNameValues(devicesPanel);

        Assert.Contains("OutputModeComboBox", devicesNames);
        Assert.Contains("AsioDriverComboBox", devicesNames);
        Assert.Contains("AsioOutputChannelComboBox", devicesNames);
        Assert.Contains("AsioArmCheckBox", devicesNames);

        Assert.Contains("PhprPedalsMasterEnableCheckBox", devicesNames);
        Assert.Contains("PhprPedalsModeComboBox", devicesNames);

        Assert.Contains("PaddleInputDeviceComboBox", devicesNames);
        Assert.Contains("StartPaddleInputListenerButton", devicesNames);
        Assert.Contains("LeftPaddleButtonTextBox", devicesNames);
        Assert.Contains("RightPaddleButtonTextBox", devicesNames);
        Assert.Contains("PaddleDebounceTextBox", devicesNames);
        Assert.Contains(
            devicesPanel
                .Descendants()
                .Where(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "PaddleDebounceTextBox")
                .Select(element => element.Attribute("LostFocus")?.Value),
            value => string.Equals(value, "PaddleMappingControl_LostFocus", StringComparison.Ordinal));

        Assert.Contains(
            devicesText,
            text => text.Contains("Detailed paddle bench and validation tools now live on Testing / Validation", StringComparison.Ordinal));
        Assert.Contains(
            devicesText,
            text => text.Contains("Choose Disabled, Mock, or Direct here.", StringComparison.Ordinal));
        Assert.DoesNotContain("HID", devicesText);
        Assert.DoesNotContain("Report ID", devicesText);
    }

    [Fact]
    public void DevicesPageDoesNotContainAdvancedValidationOrLowLevelControls()
    {
        var devicesXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "DevicesView.xaml");
        var devicesNames = GetXNameValues(FindElementByXName(devicesXaml, "DevicesPanel"));

        Assert.DoesNotContain("LocalGearTestModeCheckBox", devicesNames);
        Assert.DoesNotContain("StartGearTestListenerButton", devicesNames);
        Assert.DoesNotContain("PaddleGearBenchEnabledCheckBox", devicesNames);
        Assert.DoesNotContain("PaddleGearBenchTargetComboBox", devicesNames);
        Assert.DoesNotContain("RealPhprCandidateComboBox", devicesNames);
        Assert.DoesNotContain("RealRoadBrakeMinStrengthTextBox", devicesNames);
        Assert.DoesNotContain("RealSlipTargetComboBox", devicesNames);
        Assert.DoesNotContain("MockGearPulseEnabledCheckBox", devicesNames);
        Assert.DoesNotContain("MockPedalEffectsEnabledCheckBox", devicesNames);
        Assert.DoesNotContain("ManualBst1StrengthTextBox", devicesNames);
        Assert.DoesNotContain("ManualBst1FrequencyTextBox", devicesNames);
        Assert.DoesNotContain("ManualBst1DurationTextBox", devicesNames);
        Assert.DoesNotContain("TestPhprBrakePulseButton", devicesNames);
        Assert.DoesNotContain("TestPhprThrottlePulseButton", devicesNames);
    }

    [Fact]
    public void DevicesViewDoesNotExposeRawHidOrReportCopy()
    {
        var devicesXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "DevicesView.xaml");
        var devicesText = GetTextValues(FindElementByXName(devicesXaml, "DevicesPanel"));

        Assert.DoesNotContain(devicesText, text => text.Contains("Report ID", StringComparison.Ordinal));
        Assert.DoesNotContain(devicesText, text => text.Contains("FeatureReport", StringComparison.Ordinal));
        Assert.DoesNotContain(devicesText, text => text.Contains("HID", StringComparison.Ordinal));
        Assert.DoesNotContain(devicesText, text => text.Contains("candidate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TestingPageContainsMovedManualAndValidationControls()
    {
        var testingXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "TestingValidationView.xaml");
        var testingPanel = FindElementByXName(testingXaml, "TestingPanel");
        var testingText = GetTextValues(testingPanel);
        var testingNames = GetXNameValues(testingPanel);
        var testBenchPanel = FindElementByXName(testingXaml, "TestBenchPanel");

        Assert.Contains("ManualBst1StrengthTextBox", testingNames);
        Assert.Contains("ManualBst1FrequencyTextBox", testingNames);
        Assert.Contains("ManualBst1DurationTextBox", testingNames);
        Assert.Contains("TestPhprBrakePulseButton", testingNames);
        Assert.Contains("TestPhprThrottlePulseButton", testingNames);
        Assert.Contains("LocalGearTestModeCheckBox", testingNames);
        Assert.Contains("StartGearTestListenerButton", testingNames);
        Assert.Contains("PaddleGearBenchEnabledCheckBox", testingNames);
        Assert.Contains("PaddleGearBenchTargetComboBox", testingNames);
        Assert.Contains("PhprValidationUserPresentCheckBox", testingNames);
        Assert.Contains("PhprValidationStatusText", testingNames);
        Assert.Contains("Manual Bass Shaker Test", testingText);
        Assert.Contains("Manual P-HPR Pedal Test", testingText);
        Assert.Contains("Paddle Gear Bench", testingText);
        Assert.Contains("Controlled Validation Harness", testingText);
        Assert.Contains("Synthetic Test Bench", GetTextValues(testBenchPanel));
        Assert.Contains(
            testingText,
            text => text.Contains("manual pulse checks, paddle bench tools, and local validation exports live here", StringComparison.OrdinalIgnoreCase));
        Assert.True(testingText.Count(text => text.Contains("Use this when", StringComparison.Ordinal)) >= 4);
        Assert.Contains(
            GetTextValues(testBenchPanel),
            text => text.Contains("Use this when you want a repeatable software-only signal path check", StringComparison.Ordinal));
    }

    [Fact]
    public void AdvancedDiagnosticsContainsDirectMockAndLowLevelDiagnostics()
    {
        var advancedXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "AdvancedDiagnosticsView.xaml");
        var advancedPanel = FindElementByXName(advancedXaml, "AdvancedPhprDiagnosticsPanel");
        var advancedText = GetTextValues(advancedPanel);
        var advancedNames = GetXNameValues(advancedPanel);
        var diagnosticsNames = GetXNameValues(FindElementByXName(advancedXaml, "DiagnosticsPanel"));
        var diagnosticsText = GetTextValues(FindElementByXName(advancedXaml, "DiagnosticsPanel"));

        Assert.Contains("RealPhprCandidateComboBox", advancedNames);
        Assert.Contains("RealPhprReportIdTextBox", advancedNames);
        Assert.Contains("RealRoadBrakeMinStrengthTextBox", advancedNames);
        Assert.Contains("RealRoadThrottleMinStrengthTextBox", advancedNames);
        Assert.Contains("RealSlipTargetComboBox", advancedNames);
        Assert.Contains("RealLockTargetComboBox", advancedNames);

        Assert.Contains("MockGearPulseEnabledCheckBox", advancedNames);
        Assert.Contains("MockPedalEffectsEnabledCheckBox", advancedNames);
        Assert.Contains("RoadTextureFlightRecorderCheckBox", diagnosticsNames);
        Assert.Contains("DiagnosticsItemsControl", diagnosticsNames);
        Assert.Contains("Advanced Diagnostics", advancedText);
        Assert.Contains("P-HPR Real Direct Control", advancedText);
        Assert.Contains("Runtime Diagnostics", diagnosticsText);
        Assert.Contains("Copy Report", File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "Views",
            "AdvancedDiagnosticsView.xaml")), StringComparison.Ordinal);
        Assert.DoesNotContain("Paddle Gear Bench Test", advancedText);
        Assert.DoesNotContain("LocalGearTestModeCheckBox", advancedNames);
        Assert.DoesNotContain("PaddleGearBenchEnabledCheckBox", advancedNames);
        Assert.DoesNotContain("PhprValidationUserPresentCheckBox", advancedNames);
    }

    [Fact]
    public void RoutingMixerPageContainsMixerSafetyControlsAndSummaries()
    {
        var routingMixerXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "RoutingMixerView.xaml");
        var mixerPanel = FindElementByXName(routingMixerXaml, "MixerPanel");
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
            text => text.Contains("Limiter protection stays on automatically to protect the output path.", StringComparison.Ordinal));
        Assert.Contains("Output Routing Summary", mixerText);
        Assert.Contains("Active Effects", mixerText);
        Assert.Contains("Priority And Ducking", mixerText);
    }

    [Fact]
    public void RecordingsPageContainsReplayModeAndRenameControls()
    {
        var telemetryXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "TelemetryUdpView.xaml");
        var recordingsPanel = FindElementByXName(telemetryXaml, "RecordingsPanel");
        var recordingsText = GetTextValues(recordingsPanel);
        var recordingsNames = GetXNameValues(recordingsPanel);

        Assert.Contains("ReplayTimingModeComboBox", recordingsNames);
        Assert.Contains("RecordingLibraryListBox", recordingsNames);
        Assert.Contains("DeleteSelectedRecordingButton", recordingsNames);
        Assert.Contains("RenameSelectedRecordingButton", recordingsNames);
        Assert.Contains("RecordingRenameTextBox", recordingsNames);
        Assert.Contains("RecordingLibraryFilterTextBox", recordingsNames);
        Assert.Contains("RecordingLibraryDetailText", recordingsNames);

        Assert.Contains("Recording And Replay", recordingsText);
        Assert.Contains(
            recordingsText,
            text => text.Contains("Rename to", StringComparison.Ordinal));
        Assert.Contains(
            recordingsText,
            text => text.Contains("Filter library", StringComparison.Ordinal));
        Assert.Contains(
            recordingsText,
            text => text.Contains("Recording captures raw F1 25 UDP packets.", StringComparison.Ordinal));
    }

    [Fact]
    public void TelemetryUdpViewContainsForwardingEditorAndTopLevelWorkflowConcepts()
    {
        var telemetryXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "TelemetryUdpView.xaml");
        var telemetryPanel = FindElementByXName(telemetryXaml, "TelemetryUdpPanel");
        var forwardingPanel = FindElementByXName(telemetryXaml, "ForwardingPanel");
        var telemetryText = GetTextValues(telemetryPanel);
        var forwardingNames = GetXNameValues(forwardingPanel);

        Assert.Contains("ForwardingNameTextBox", forwardingNames);
        Assert.Contains("ForwardingHostTextBox", forwardingNames);
        Assert.Contains("ForwardingPortTextBox", forwardingNames);
        Assert.Contains("ForwardingEnabledCheckBox", forwardingNames);
        Assert.Contains("ForwardingDestinationsListBox", forwardingNames);
        Assert.Contains(
            telemetryText,
            text => text.Contains("F1 25 UDP", StringComparison.Ordinal));
        Assert.Contains("Recording And Replay", telemetryText);
        Assert.Contains("Replay mode:", telemetryText);
        Assert.Contains("UDP Forwarding Destinations", telemetryText);
    }

    [Fact]
    public void RoutingMixerPageContainsOutputRouteAndPrioritySummaries()
    {
        var routingMixerXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "RoutingMixerView.xaml");
        var mixerPanel = FindElementByXName(routingMixerXaml, "MixerPanel");
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

    [Fact]
    public void ProfilesPageStatesLiveHardwareStateIsNotSaved()
    {
        var profilesXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "ProfilesView.xaml");
        var profilesPanel = FindElementByXName(profilesXaml, "ProfilesPanel");
        var profileText = GetTextValues(profilesPanel);

        Assert.Contains(
            profileText,
            text => text.Contains("Live output, emergency state, private device paths, and running hardware state are not saved.", StringComparison.Ordinal));
    }

    [Fact]
    public void ProfilesViewContainsExpectedWorkflowConceptsAndNamedControls()
    {
        var profilesMarkup = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "Views",
            "ProfilesView.xaml"));
        var profilesXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "ProfilesView.xaml");
        var profilesPanel = FindElementByXName(profilesXaml, "ProfilesPanel");
        var profileText = GetTextValues(profilesPanel);
        var profileNames = GetXNameValues(profilesPanel);

        Assert.Contains("ProfileNameTextBox", profileNames);
        Assert.Contains("ProfileStatusText", profileNames);
        Assert.Contains("ProfilePathText", profileNames);
        Assert.Contains("ProfilePhprStatusText", profileNames);
        Assert.Contains("ProfileValidationText", profileNames);

        Assert.Contains("Profiles", profileText);
        Assert.Contains("Profile name", profileText);
        Assert.Contains("Content=\"Save Profile\"", profilesMarkup, StringComparison.Ordinal);
        Assert.Contains("Content=\"Load Profile\"", profilesMarkup, StringComparison.Ordinal);
        Assert.Contains("Content=\"Reset Defaults\"", profilesMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalWorkflowPanelsDoNotExposeRawHidOrReportCopy()
    {
        var mainWindowXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "MainWindow.xaml");
        var devicesXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "DevicesView.xaml");
        var effectsXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "EffectsView.xaml");
        var routingMixerXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "RoutingMixerView.xaml");
        var telemetryXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "TelemetryUdpView.xaml");
        var profilesXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "ProfilesView.xaml");
        var testingXaml = LoadSourceXaml("src", "HapticDrive.Asio.App", "Views", "TestingValidationView.xaml");
        var normalText = GetTextValues(FindElementByXName(devicesXaml, "DevicesPanel"))
            .Concat(GetTextValues(FindElementByXName(effectsXaml, "EffectsPanel")))
            .Concat(GetTextValues(FindElementByXName(routingMixerXaml, "MixerPanel")))
            .Concat(GetTextValues(FindElementByXName(telemetryXaml, "TelemetryUdpPanel")))
            .Concat(GetTextValues(FindElementByXName(profilesXaml, "ProfilesPanel")))
            .Concat(GetTextValues(FindElementByXName(testingXaml, "TestingPanel")))
            .ToArray();

        Assert.DoesNotContain(normalText, text => text.Contains("Report ID", StringComparison.Ordinal));
        Assert.DoesNotContain(normalText, text => text.Contains("FeatureReport", StringComparison.Ordinal));
        Assert.DoesNotContain(normalText, text => text.Contains("HID device-interface", StringComparison.Ordinal));
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
