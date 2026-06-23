namespace HapticDrive.Asio.App.Tests;

public sealed class PackagingGovernanceTests
{
    [Fact]
    public void RunScript_DefaultConfigurationIsRelease()
    {
        var runScript = MainWindowSourceTestHelper.ReadRepositoryFile("Run-HapticDrive.ps1");

        Assert.Contains("[string]$Configuration = \"Release\"", runScript, StringComparison.Ordinal);
    }

    [Fact]
    public void RunScript_CheckOnlyUsesSelectedConfiguration()
    {
        var runScript = MainWindowSourceTestHelper.ReadRepositoryFile("Run-HapticDrive.ps1");

        Assert.Contains("Join-Path $repoRoot \"src\\HapticDrive.Asio.App\\bin\\$Configuration\\net8.0-windows\\HapticDrive.Asio.App.exe\"", runScript, StringComparison.Ordinal);
        Assert.Contains("Executable: $appExe", runScript, StringComparison.Ordinal);
        Assert.Contains("Expected path: $appExe", runScript, StringComparison.Ordinal);
    }

    [Fact]
    public void CmdWrapper_ForwardsArguments()
    {
        var runCmd = MainWindowSourceTestHelper.ReadRepositoryFile("Run-HapticDrive.cmd");

        Assert.Contains("powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%~dp0Run-HapticDrive.ps1\" %*", runCmd, StringComparison.Ordinal);
    }

    [Fact]
    public void PrepareRelease_InvokesRunScriptWithRelease()
    {
        var prepareScript = MainWindowSourceTestHelper.ReadRepositoryFile("Prepare-ReleaseArtifact.ps1");

        Assert.Contains("& $runScript -Configuration $Configuration -NoBuild -CheckOnly", prepareScript, StringComparison.Ordinal);
        Assert.Contains("& $smokeScript -Configuration $Configuration -Runtime $Runtime", prepareScript, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageWorkflow_UsesReleasePreflight()
    {
        var packageWorkflow = MainWindowSourceTestHelper.ReadRepositoryFile(".github", "workflows", "package.yml");

        Assert.Contains(@".\Run-HapticDrive.cmd -Configuration Release -NoBuild -CheckOnly", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains(@".\Test-ReleaseArtifact.ps1 -Configuration Release -Runtime win-x64", packageWorkflow, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseArtifactTest_DoesNotPassDebugExe()
    {
        var smokeScript = MainWindowSourceTestHelper.ReadRepositoryFile("Test-ReleaseArtifact.ps1");

        Assert.DoesNotContain(@"bin\Debug", smokeScript, StringComparison.Ordinal);
        Assert.Contains("[string]$Configuration = \"Release\"", smokeScript, StringComparison.Ordinal);
        Assert.Contains("$packagedExecutable = Join-Path $extractDirectory \"HapticDrive.Asio.App.exe\"", smokeScript, StringComparison.Ordinal);
        Assert.Contains("Release manifest configuration", smokeScript, StringComparison.Ordinal);
    }

    [Fact]
    public void NoDuplicateZipSizeOutput()
    {
        var publishScript = MainWindowSourceTestHelper.ReadRepositoryFile("Publish-HapticDrive.ps1");

        Assert.Equal(1, CountOccurrences(publishScript, "- Zip size (bytes): $($manifest.ZipSizeBytes)"));
    }

    [Fact]
    public void ReleaseScriptsDoNotDisableNuGetAudit()
    {
        var prepareScript = MainWindowSourceTestHelper.ReadRepositoryFile("Prepare-ReleaseArtifact.ps1");
        var publishScript = MainWindowSourceTestHelper.ReadRepositoryFile("Publish-HapticDrive.ps1");

        Assert.DoesNotContain("NuGetAudit=false", prepareScript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NuGetAudit=false", publishScript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleaseManifestIncludesCommitHashConfigurationRidAndPackageHash()
    {
        var publishScript = MainWindowSourceTestHelper.ReadRepositoryFile("Publish-HapticDrive.ps1");

        Assert.Contains("RuntimeIdentifier = $Runtime", publishScript, StringComparison.Ordinal);
        Assert.Contains("Configuration = $Configuration", publishScript, StringComparison.Ordinal);
        Assert.Contains("PackageSha256 = $zipHash.Hash", publishScript, StringComparison.Ordinal);
        Assert.Contains("$manifest.CommitHash = $commitHash", publishScript, StringComparison.Ordinal);
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
        Assert.Contains(@".\Test-CodeCoverage.ps1 -SearchRoot artifacts\TestResults -MinimumLineCoverage 80", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("dotnet format HapticDrive.Asio.sln --verify-no-changes --no-restore", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains(@".\Run-HapticDrive.cmd -Configuration Release -NoBuild -CheckOnly", ciWorkflow, StringComparison.Ordinal);

        Assert.Contains("permissions:", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains("contents: read", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains(@".\Test-PackageVulnerabilities.ps1 -FailOnMinimumSeverity High", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains(@".\Test-CodeCoverage.ps1 -SearchRoot artifacts\TestResults -MinimumLineCoverage 80", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains(@".\Publish-HapticDrive.ps1 -Configuration Release -Runtime win-x64", packageWorkflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/release/HapticDrive.Asio-win-x64.package-manifest.json", packageWorkflow, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var searchIndex = 0;

        while (true)
        {
            var foundIndex = source.IndexOf(value, searchIndex, StringComparison.Ordinal);
            if (foundIndex < 0)
            {
                return count;
            }

            count++;
            searchIndex = foundIndex + value.Length;
        }
    }
}
