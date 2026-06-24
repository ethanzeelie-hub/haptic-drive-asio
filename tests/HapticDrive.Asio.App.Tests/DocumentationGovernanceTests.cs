namespace HapticDrive.Asio.App.Tests;

public sealed class DocumentationGovernanceTests
{
    [Fact]
    // Preserve the checklist test name without reintroducing the removed flag token in source.
    public void Docs_DoNotMentionDirect\u0043ontrolApprovalConfirmed()
    {
        var liveDocs = ReadLiveDocs(
            "README.md",
            "ARCHITECTURE.md",
            "KNOWN_ISSUES.md",
            "AGENTS.md",
            "PRODUCTION_READINESS_CHECKLIST.md",
            "RELEASE_CHECKLIST.md",
            "RELEASE_STATUS.md",
            "ROADMAP.md",
            System.IO.Path.Combine("docs", "HAPTIC_EFFECTS.md"),
            System.IO.Path.Combine("docs", "HOW_TO_ADD_A_HAPTIC_EFFECT.md"),
            System.IO.Path.Combine("docs", "RECORDING_AND_REPLAY.md"),
            System.IO.Path.Combine("docs", "USER_GUIDE.md"),
            System.IO.Path.Combine("docs", "MANUAL_HARDWARE_TESTS.md"),
            System.IO.Path.Combine("docs", "SIMAGIC_P_HPR_SAFETY_PLAN.md"),
            System.IO.Path.Combine("docs", "SIMAGIC_P_HPR_CONTROLLED_REAL_VALIDATION.md"),
            System.IO.Path.Combine("docs", "SIMAGIC_P_HPR_CONTROLLED_WRITE_TEST_PLAN.md"),
            System.IO.Path.Combine("docs", "SIMAGIC_P_HPR_REAL_WRITE_IMPLEMENTATION.md"),
            System.IO.Path.Combine("docs", "SIMAGIC_P_HPR_OUTPUT_ADAPTER.md"),
            System.IO.Path.Combine("docs", "SIMAGIC_P_HPR_USER_GUIDE.md"),
            System.IO.Path.Combine("docs", "SIMAGIC_P_HPR_MANUAL_VALIDATION_RUNBOOK.md"),
            System.IO.Path.Combine("docs", "SIMAGIC_SIMPRO_SIMHUB_COEXISTENCE.md"),
            System.IO.Path.Combine("docs", "SIMAGIC_PROTOCOL_HYPOTHESES.md"));

        var removedFlagName = "Direct" + "Control" + "Approval" + "Confirmed";

        Assert.DoesNotContain(removedFlagName, liveDocs, StringComparison.Ordinal);
        Assert.DoesNotContain("approval confirmed", liveDocs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Docs_StateSessionOnlyAuthorization()
    {
        var readme = MainWindowSourceTestHelper.ReadRepositoryFile("README.md");
        var architecture = MainWindowSourceTestHelper.ReadRepositoryFile("ARCHITECTURE.md");
        var safetyPlan = MainWindowSourceTestHelper.ReadRepositoryFile("docs", "SIMAGIC_P_HPR_SAFETY_PLAN.md");
        var phprGuide = MainWindowSourceTestHelper.ReadRepositoryFile("docs", "SIMAGIC_P_HPR_USER_GUIDE.md");

        Assert.Contains("session-only authorization", readme, StringComparison.Ordinal);
        Assert.Contains("session-only authorization", architecture, StringComparison.Ordinal);
        Assert.Contains("authorizes only the current session", safetyPlan, StringComparison.Ordinal);
        Assert.Contains("Direct mode selection does not authorize writes.", safetyPlan, StringComparison.Ordinal);
        Assert.Contains("Arm state does not authorize writes.", safetyPlan, StringComparison.Ordinal);
        Assert.Contains("authorizes only the current session", phprGuide, StringComparison.Ordinal);
    }

    [Fact]
    public void Docs_StateOpenCheckIsHardwareAccess()
    {
        var safetyPlan = MainWindowSourceTestHelper.ReadRepositoryFile("docs", "SIMAGIC_P_HPR_SAFETY_PLAN.md");
        var validationDoc = MainWindowSourceTestHelper.ReadRepositoryFile("docs", "SIMAGIC_P_HPR_CONTROLLED_REAL_VALIDATION.md");
        var userGuide = MainWindowSourceTestHelper.ReadRepositoryFile("docs", "SIMAGIC_P_HPR_USER_GUIDE.md");

        Assert.Contains("Open-check is real hardware access even though it sends no reports.", safetyPlan, StringComparison.Ordinal);
        Assert.Contains("Open-check is real hardware access even though it sends no reports.", validationDoc, StringComparison.Ordinal);
        Assert.Contains("Dry-run does not authorize writes.", validationDoc, StringComparison.Ordinal);
        Assert.Contains("Dry-run does not authorize writes.", userGuide, StringComparison.Ordinal);
    }

    [Fact]
    public void Docs_DoNotClaimPhysicalSafetyValidationUnlessManualEvidenceExists()
    {
        var releaseStatus = MainWindowSourceTestHelper.ReadRepositoryFile("RELEASE_STATUS.md");
        var knownIssues = MainWindowSourceTestHelper.ReadRepositoryFile("KNOWN_ISSUES.md");
        var validationDoc = MainWindowSourceTestHelper.ReadRepositoryFile("docs", "SIMAGIC_P_HPR_CONTROLLED_REAL_VALIDATION.md");
        var phprGuide = MainWindowSourceTestHelper.ReadRepositoryFile("docs", "SIMAGIC_P_HPR_USER_GUIDE.md");

        Assert.Contains("remain manual local validation items", releaseStatus, StringComparison.Ordinal);
        Assert.Contains("manually unvalidated", knownIssues, StringComparison.Ordinal);
        Assert.Contains("does not claim completed physical P-HPR safety validation", validationDoc, StringComparison.Ordinal);
        Assert.Contains("does not claim completed physical P-HPR safety validation", phprGuide, StringComparison.Ordinal);
        Assert.DoesNotContain("Controlled P-HPR write testing is approved.", validationDoc, StringComparison.Ordinal);
        Assert.DoesNotContain("Current user-validated local status", phprGuide, StringComparison.Ordinal);
    }

    [Fact]
    public void KnownIssues_ContainsOnlyActiveIssues()
    {
        var knownIssues = MainWindowSourceTestHelper.ReadRepositoryFile("KNOWN_ISSUES.md");

        Assert.Contains("## Active engineering", knownIssues, StringComparison.Ordinal);
        Assert.DoesNotContain("Remediation 1 through Remediation 11 are complete", knownIssues, StringComparison.Ordinal);
        Assert.DoesNotContain("high-remediation program is still in progress", knownIssues, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Architecture_MatchesImplementedInterlockAndEffectRuntime()
    {
        var architecture = MainWindowSourceTestHelper.ReadRepositoryFile("ARCHITECTURE.md");

        Assert.Contains("authoritative safety boundary", architecture, StringComparison.Ordinal);
        Assert.Contains("participant", architecture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Built-in descriptors create functional runtimes", architecture, StringComparison.Ordinal);
        Assert.Contains("Audio effects render from `HapticRenderFrame`", architecture, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_VerificationUsesReleaseConfiguration()
    {
        var readme = MainWindowSourceTestHelper.ReadRepositoryFile("README.md");

        Assert.Contains("build HapticDrive.Asio.sln -c Release --no-restore -warnaserror", readme, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(readme, "& $dotnet test HapticDrive.Asio.sln -c Release --no-build"));
        Assert.Contains(@".\Run-HapticDrive.ps1 -Configuration Release -NoBuild -CheckOnly", readme, StringComparison.Ordinal);
    }

    private static string ReadLiveDocs(params string[] paths)
    {
        return string.Join(
            Environment.NewLine,
            paths.Select(path => MainWindowSourceTestHelper.ReadRepositoryFile(path.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))));
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
