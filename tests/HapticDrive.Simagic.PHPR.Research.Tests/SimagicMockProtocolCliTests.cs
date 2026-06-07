using System.Text.Json;
using HapticDrive.Simagic.PHPR.Research;
using HapticDrive.Simagic.PHPR.Research.MockProtocol;
using HapticDrive.Simagic.PHPR.Research.Safety;

namespace HapticDrive.Simagic.PHPR.Research.Tests;

public sealed class SimagicMockProtocolCliTests
{
    [Fact]
    public void Examples_ContainExpectedMockPayloadsAndNoPrivatePaths()
    {
        var examples = SimagicMockProtocolExamples.Create();
        var json = JsonSerializer.Serialize(examples);

        Assert.Contains(examples, example => example.Id == "simhub-f1ec-brake-50hz-10pct-start"
            && example.Frames.Any(frame => frame.PayloadHex.StartsWith("F1 EC 01 01 32 0A", StringComparison.Ordinal)));
        Assert.Contains(examples, example => example.Id == "simhub-f1ec-duration-500ms"
            && example.Frames.Any(frame => frame.State == "Stop" && frame.ScheduledOffsetMs == 500));
        Assert.DoesNotContain("C:\\Users", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ethan", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("2E9C21CD4D401000", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cli_PrintsMockProtocolExamplesWithSafetyBanner()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await SimagicResearchCli.RunAsync(["mock-protocol-examples"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("STAGE 2K MOCK P-HPR PROTOCOL SAFETY", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("No USB writes", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("simhub-f1ec-brake-50hz-10pct-start", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("F1 EC 01 01 32 0A", output.ToString(), StringComparison.Ordinal);
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public async Task Cli_ExportsMockProtocolExamples()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"haptic-drive-stage-2k-{Guid.NewGuid():N}");
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var outputPath = Path.Combine(tempDirectory, "simagic-mock-protocol-examples.json");

            var exitCode = await SimagicResearchCli.RunAsync(["mock-protocol-export", "--output", outputPath], output, error);
            var json = await File.ReadAllTextAsync(outputPath);

            Assert.Equal(0, exitCode);
            Assert.Contains("Sanitized mock protocol export", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Stage 2K", json, StringComparison.Ordinal);
            Assert.Contains("BlockedForRealWrite", json, StringComparison.Ordinal);
            Assert.Contains("F1 EC 01 01 32 0A", json, StringComparison.Ordinal);
            Assert.DoesNotContain("C:\\Users", json, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("", error.ToString());
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CliHelp_ListsMockProtocolCommands()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await SimagicResearchCli.RunAsync(["--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("mock-protocol-examples", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("mock-protocol-export", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("safety-examples", output.ToString(), StringComparison.Ordinal);
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public void SafetyExamples_ContainClampRejectionAndEmergencyStopCases()
    {
        var examples = SimagicPhprSafetyExamples.Create();
        var json = JsonSerializer.Serialize(examples);

        Assert.Contains(examples, example => example.Id == "clamped-strength-duration-frequency"
            && example.DecisionKind == "AcceptedWithClamp");
        Assert.Contains(examples, example => example.Id == "telemetry-stale-rejects-start"
            && example.Violation == "TelemetryStale");
        Assert.Contains(examples, example => example.Id == "real-writes-blocked"
            && example.Violation == "RealWritesNotAllowed");
        Assert.Contains(examples, example => example.Id == "emergency-stop-latches"
            && example.DecisionKind == "EmergencyStopped");
        Assert.DoesNotContain("C:\\Users", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ethan", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cli_PrintsSafetyExamplesWithSafetyBanner()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await SimagicResearchCli.RunAsync(["safety-examples"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("STAGE 2L P-HPR SAFETY LAYER", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("No USB writes", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("clamped-strength-duration-frequency", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("real-writes-blocked", output.ToString(), StringComparison.Ordinal);
        Assert.Equal("", error.ToString());
    }
}
