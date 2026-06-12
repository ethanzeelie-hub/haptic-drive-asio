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
