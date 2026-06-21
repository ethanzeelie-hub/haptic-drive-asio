namespace HapticDrive.Asio.App.Tests;

public sealed class PackagingScriptTests
{
    [Fact]
    public void ReleaseScriptsDoNotDisableNuGetAudit()
    {
        var prepareScript = MainWindowSourceTestHelper.ReadRepositoryFile("Prepare-ReleaseArtifact.ps1");
        var publishScript = MainWindowSourceTestHelper.ReadRepositoryFile("Publish-HapticDrive.ps1");

        Assert.DoesNotContain("NuGetAudit=false", prepareScript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NuGetAudit=false", publishScript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleaseManifestDoesNotContainAbsoluteWorkspacePath()
    {
        var publishScript = MainWindowSourceTestHelper.ReadRepositoryFile("Publish-HapticDrive.ps1");
        var smokeScript = MainWindowSourceTestHelper.ReadRepositoryFile("Test-ReleaseArtifact.ps1");

        Assert.DoesNotContain("PublishDirectory =", publishScript, StringComparison.Ordinal);
        Assert.DoesNotContain("ZipPath =", publishScript, StringComparison.Ordinal);
        Assert.DoesNotContain("ChecksumPath =", publishScript, StringComparison.Ordinal);
        Assert.Contains("ManifestFileName =", publishScript, StringComparison.Ordinal);
        Assert.Contains("if ($manifestJsonText.IndexOf($repoRoot", smokeScript, StringComparison.Ordinal);
        Assert.Contains("if ($packageManifestJsonText.IndexOf($repoRoot", smokeScript, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultArtifactExcludesPdbs()
    {
        var publishScript = MainWindowSourceTestHelper.ReadRepositoryFile("Publish-HapticDrive.ps1");
        var smokeScript = MainWindowSourceTestHelper.ReadRepositoryFile("Test-ReleaseArtifact.ps1");

        Assert.Contains("if ($file.Extension -ieq \".pdb\")", publishScript, StringComparison.Ordinal);
        Assert.Contains("$zipPdbs = Get-ChildItem -LiteralPath $extractDirectory -Recurse -Filter *.pdb", smokeScript, StringComparison.Ordinal);
        Assert.Contains("IncludesPortablePdbs = $false", publishScript, StringComparison.Ordinal);
    }
}

public sealed class DependencyGovernanceTests
{
    [Fact]
    public void CentralPackageManagementIsEnabled()
    {
        var packagesProps = MainWindowSourceTestHelper.ReadRepositoryFile("Directory.Packages.props");
        var buildProps = MainWindowSourceTestHelper.ReadRepositoryFile("Directory.Build.props");
        var globalJson = MainWindowSourceTestHelper.ReadRepositoryFile("global.json");

        Assert.Contains("<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>", packagesProps, StringComparison.Ordinal);
        Assert.Contains("coverlet.collector\" Version=\"10.0.1\"", packagesProps, StringComparison.Ordinal);
        Assert.Contains("Microsoft.NET.Test.Sdk\" Version=\"18.6.0\"", packagesProps, StringComparison.Ordinal);
        Assert.Contains("xunit\" Version=\"2.9.3\"", packagesProps, StringComparison.Ordinal);
        Assert.Contains("xunit.runner.visualstudio\" Version=\"3.1.5\"", packagesProps, StringComparison.Ordinal);
        Assert.Contains("<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>", buildProps, StringComparison.Ordinal);
        Assert.Contains("\"rollForward\": \"latestFeature\"", globalJson, StringComparison.Ordinal);
    }

    [Fact]
    public void CiWorkflowUsesMinimalPermissionsAndRequiredValidationSteps()
    {
        var ciWorkflow = MainWindowSourceTestHelper.ReadRepositoryFile(".github", "workflows", "ci.yml");
        var packageWorkflow = MainWindowSourceTestHelper.ReadRepositoryFile(".github", "workflows", "package.yml");

        Assert.Contains("permissions:", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("contents: read", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("dotnet restore HapticDrive.Asio.sln --locked-mode", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains(@".\Test-PackageVulnerabilities.ps1 -FailOnMinimumSeverity High", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("dotnet build HapticDrive.Asio.sln -c Release --no-restore -warnaserror", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test HapticDrive.Asio.sln -c Release --no-build --collect:\"XPlat Code Coverage\"", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains(@".\Test-CodeCoverage.ps1 -SearchRoot artifacts\TestResults -MinimumLineCoverage 75", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("dotnet format HapticDrive.Asio.sln --verify-no-changes --no-restore", ciWorkflow, StringComparison.Ordinal);

        Assert.Contains("permissions:", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains("contents: read", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains(@".\Test-PackageVulnerabilities.ps1 -FailOnMinimumSeverity High", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains(@".\Publish-HapticDrive.ps1 -Configuration Release -Runtime win-x64", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/release/HapticDrive.Asio-win-x64.package-manifest.json", packageWorkflow, StringComparison.Ordinal);
    }
}
