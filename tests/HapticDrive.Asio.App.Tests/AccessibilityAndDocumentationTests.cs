namespace HapticDrive.Asio.App.Tests;

public sealed class AccessibilityMetadataTests
{
    [Fact]
    public void CriticalButtonsHaveAutomationNames()
    {
        var mainWindowMarkup = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml");
        var telemetryMarkup = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "Views",
            "TelemetryUdpView.xaml");
        var devicesMarkup = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "Views",
            "DevicesView.xaml");

        Assert.Contains("AutomationProperties.Name=\"Emergency mute\"", mainWindowMarkup, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Reset output interlock\"", mainWindowMarkup, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Telemetry listener status\"", mainWindowMarkup, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Global safety interlock status\"", mainWindowMarkup, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Allow LAN telemetry\"", telemetryMarkup, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Recording library status\"", telemetryMarkup, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"P-HPR emergency stop\"", devicesMarkup, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Start paddle input listener\"", devicesMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void EmergencyActionsHaveKeyboardShortcuts()
    {
        var mainWindowMarkup = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml");
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("AutomationProperties.AccessKey=\"Ctrl+Shift+M\"", mainWindowMarkup, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AccessKey=\"Ctrl+Shift+R\"", mainWindowMarkup, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"Emergency mute all outputs (Ctrl+Shift+M)\"", mainWindowMarkup, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"Reset the global output interlock (Ctrl+Shift+R)\"", mainWindowMarkup, StringComparison.Ordinal);
        Assert.Contains("if (e.Key == Key.M)", source, StringComparison.Ordinal);
        Assert.Contains("if (e.Key == Key.R)", source, StringComparison.Ordinal);
    }
}

public sealed class DocumentationConsistencyTests
{
    [Fact]
    public void ReadmeDoesNotClaimProductionWasapiIfPlaceholder()
    {
        var readme = MainWindowSourceTestHelper.ReadRepositoryFile("README.md");

        Assert.Contains("WASAPI debug output remains manual/experimental only", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("production WASAPI", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KnownIssuesDoesNotListResolvedFreshnessBug()
    {
        var knownIssues = MainWindowSourceTestHelper.ReadRepositoryFile("KNOWN_ISSUES.md");

        Assert.Contains("## True blockers", knownIssues, StringComparison.Ordinal);
        Assert.Contains("## Active engineering", knownIssues, StringComparison.Ordinal);
        Assert.Contains("## Hardware-later tuning and validation", knownIssues, StringComparison.Ordinal);
        Assert.Contains("## Owner and legal decisions", knownIssues, StringComparison.Ordinal);
        Assert.DoesNotContain("## Stage 00", knownIssues, StringComparison.Ordinal);
        Assert.DoesNotContain("## Stage 26B", knownIssues, StringComparison.Ordinal);
        Assert.DoesNotContain("high-remediation program is still in progress", knownIssues, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArchitectureDocMentionsCanonicalHapticFrame()
    {
        var architecture = MainWindowSourceTestHelper.ReadRepositoryFile("ARCHITECTURE.md");

        Assert.Contains("canonical `HapticFrame`", architecture, StringComparison.Ordinal);
        Assert.Contains("`HapticRenderFrame`", architecture, StringComparison.Ordinal);
        Assert.Contains("output interlock", architecture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("recording and replay", architecture, StringComparison.OrdinalIgnoreCase);
    }
}
