using System.Text.Json;
using HapticDrive.Simagic.PHPR.Research;
using HapticDrive.Simagic.PHPR.Research.Hypotheses;

namespace HapticDrive.Simagic.PHPR.Research.Tests;

public sealed class SimagicProtocolHypothesisTests
{
    [Fact]
    public void BuiltInHypotheses_ContainSimHubActiveStartFieldMap()
    {
        var hypothesisSet = BuiltInProtocolHypotheses.Create();
        var active = Find(hypothesisSet, "simhub-f1ec-active-start");

        Assert.Equal(SimagicProtocolFamily.SimHubF1EcSetReport, active.ProtocolFamily);
        Assert.Equal(SimagicProtocolHypothesisStatus.ReadyForMockProtocol, active.Status);
        Assert.Equal(SimagicProtocolHypothesisStatus.BlockedForRealWrite, active.RealWriteStatus);
        Assert.True(active.IsOutputCommand);
        Assert.True(active.MockOnly);

        AssertField(active, "module selector", 2, "01 = brake");
        AssertField(active, "module selector", 2, "02 = throttle");
        AssertField(active, "frequency", 4, "50 Hz = 32");
        AssertField(active, "strength", 5, "100% = 64");
        Assert.Contains("not approved for real USB writes", active.NoWriteSafetyNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuiltInHypotheses_ContainSimHubStopAndDurationTiming()
    {
        var hypothesisSet = BuiltInProtocolHypotheses.Create();
        var stop = Find(hypothesisSet, "simhub-f1ec-stop-idle");
        var duration = Find(hypothesisSet, "simhub-duration-timing");

        Assert.Equal(SimagicProtocolHypothesisStatus.ReadyForMockProtocol, stop.Status);
        AssertField(stop, "state", 3, "00 = stop/off/idle");
        AssertField(stop, "byte 4 stop value", 4, "0A");

        var durationField = Assert.Single(duration.Fields);
        Assert.Null(durationField.ByteOffset);
        Assert.Equal("none observed", durationField.Encoding);
        Assert.Contains("scheduled stop", durationField.Interpretation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(duration.Fields, field => string.Equals(field.CandidateFieldName, "duration", StringComparison.OrdinalIgnoreCase) && field.ByteOffset is not null);
    }

    [Fact]
    public void SimProFamily_IsSeparateAndConservative()
    {
        var hypothesisSet = BuiltInProtocolHypotheses.Create();
        var simPro = Find(hypothesisSet, "simpro-801e89-family");

        Assert.Equal(SimagicProtocolFamily.SimPro801E89SetReport, simPro.ProtocolFamily);
        Assert.NotEqual(SimagicProtocolFamily.SimHubF1EcSetReport, simPro.ProtocolFamily);
        Assert.Equal("80 1E 89", simPro.PayloadPrefixHex);
        Assert.Equal(SimagicProtocolHypothesisStatus.NeedsMoreCaptures, simPro.Status);
        Assert.Equal("Stage 2K may represent this as SimProUnknownMock only.", simPro.StageAllowedForNextAction);

        var unresolvedFields = simPro.Fields.Where(field =>
            !string.Equals(field.CandidateFieldName, "prefix/family", StringComparison.OrdinalIgnoreCase));
        Assert.All(unresolvedFields, field =>
            Assert.True(field.Confidence is SimagicProtocolHypothesisConfidence.Unknown or SimagicProtocolHypothesisConfidence.Low));
    }

    [Fact]
    public void InputMappings_AreNotClassifiedAsOutputCommands()
    {
        var hypothesisSet = BuiltInProtocolHypotheses.Create();
        var inputSeparation = Find(hypothesisSet, "input-output-separation");

        Assert.True(inputSeparation.IsInputMapping);
        Assert.False(inputSeparation.IsOutputCommand);
        Assert.False(inputSeparation.MockOnly);
        Assert.Equal(SimagicProtocolHypothesisStatus.EvidenceOnly, inputSeparation.Status);

        AssertField(inputSeparation, "P700 primary throttle input", 5, "raw range 0..4095");
        AssertField(inputSeparation, "P700 primary brake input", 3, "raw range expected 0..4095");
        AssertField(inputSeparation, "GT Neo left paddle input", 14, "report[14] & 0x02");
        AssertField(inputSeparation, "GT Neo right paddle input", 14, "report[14] & 0x01");
    }

    [Fact]
    public void AllHypotheses_KeepRealWritesBlockedAndIncludeNoWriteNotes()
    {
        var hypothesisSet = BuiltInProtocolHypotheses.Create();

        Assert.Contains(hypothesisSet.SafetyBoundary, item => item.Contains("Nothing in this document authorises real USB writes", StringComparison.Ordinal));
        Assert.All(hypothesisSet.Hypotheses, hypothesis =>
        {
            Assert.Equal(SimagicProtocolHypothesisStatus.BlockedForRealWrite, hypothesis.RealWriteStatus);
            Assert.Contains("write", hypothesis.NoWriteSafetyNote, StringComparison.OrdinalIgnoreCase);
            Assert.True(hypothesis.MockOnly || hypothesis.RealWriteStatus == SimagicProtocolHypothesisStatus.BlockedForRealWrite);
        });

        Assert.DoesNotContain(hypothesisSet.Hypotheses, hypothesis =>
            string.Equals(hypothesis.Status.ToString(), "ReadyForRealWrite", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Exporter_WritesSanitizedJsonWithoutRawPathsOrSerials()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var outputPath = Path.Combine(tempDirectory, "simagic-protocol-hypotheses.json");
            var hypothesisSet = BuiltInProtocolHypotheses.Create();

            var writtenPath = await new SimagicProtocolHypothesisExporter().ExportJsonAsync(hypothesisSet, outputPath);
            var json = await File.ReadAllTextAsync(writtenPath);

            Assert.Contains("Stage 2J", json, StringComparison.Ordinal);
            Assert.Contains("simhub-f1ec-active-start", json, StringComparison.Ordinal);
            Assert.Contains("BlockedForRealWrite", json, StringComparison.Ordinal);
            Assert.DoesNotContain("C:\\Users", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ethan", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("2E9C21CD4D401000", json, StringComparison.OrdinalIgnoreCase);

            using var document = JsonDocument.Parse(json);
            Assert.True(document.RootElement.TryGetProperty("Hypotheses", out _));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Cli_ListsAndExportsHypotheses()
    {
        using var listOutput = new StringWriter();
        using var listError = new StringWriter();
        var listExitCode = await SimagicResearchCli.RunAsync(["hypotheses-list"], listOutput, listError);

        Assert.Equal(0, listExitCode);
        Assert.Contains("STAGE 2J PROTOCOL HYPOTHESES SAFETY", listOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("simhub-f1ec-active-start", listOutput.ToString(), StringComparison.Ordinal);
        Assert.Equal("", listError.ToString());

        var tempDirectory = CreateTempDirectory();
        try
        {
            using var exportOutput = new StringWriter();
            using var exportError = new StringWriter();
            var outputPath = Path.Combine(tempDirectory, "simagic-protocol-hypotheses.json");

            var exportExitCode = await SimagicResearchCli.RunAsync(["hypotheses-export", "--output", outputPath], exportOutput, exportError);

            Assert.Equal(0, exportExitCode);
            Assert.True(File.Exists(outputPath));
            Assert.Contains("Sanitized hypothesis export", exportOutput.ToString(), StringComparison.Ordinal);
            Assert.Equal("", exportError.ToString());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static SimagicProtocolHypothesis Find(SimagicProtocolHypothesisSet hypothesisSet, string id)
    {
        return Assert.Single(hypothesisSet.Hypotheses, hypothesis => string.Equals(hypothesis.Id, id, StringComparison.Ordinal));
    }

    private static void AssertField(SimagicProtocolHypothesis hypothesis, string fieldName, int? byteOffset, string observedValue)
    {
        var fields = hypothesis.Fields.Where(field => string.Equals(field.CandidateFieldName, fieldName, StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(fields);
        Assert.Contains(fields, field => field.ByteOffset == byteOffset);
        Assert.Contains(fields, field => field.ObservedValues.Any(value => value.Contains(observedValue, StringComparison.Ordinal)));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"haptic-drive-stage-2j-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
