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
