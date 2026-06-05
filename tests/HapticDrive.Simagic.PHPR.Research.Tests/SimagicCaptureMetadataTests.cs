using System.Text.Json;
using HapticDrive.Simagic.PHPR.Research;
using HapticDrive.Simagic.PHPR.Research.Capture;

namespace HapticDrive.Simagic.PHPR.Research.Tests;

public sealed class SimagicCaptureMetadataTests
{
    [Fact]
    public void ScenarioList_ContainsAllRequiredStage2HScenarios()
    {
        var expected = Enum.GetValues<SimagicCaptureScenarioId>();

        var actual = SimagicCaptureScenarios.RequiredScenarios.Select(scenario => scenario.Id).ToArray();

        Assert.Equal(expected.OrderBy(id => id), actual.OrderBy(id => id));
        Assert.Equal(13, actual.Length);
    }

    [Fact]
    public void TemplateFactory_CreatesMetadataForEveryRequiredScenario()
    {
        var startedAt = new DateTimeOffset(2026, 6, 5, 20, 15, 30, TimeSpan.Zero);

        foreach (var scenario in SimagicCaptureScenarios.RequiredScenarios)
        {
            var template = SimagicCaptureTemplateFactory.Create(scenario.Id, scenario.RecommendedTarget, startedAt);

            Assert.Equal(scenario.Id, template.ScenarioId);
            Assert.Equal(scenario.Name, template.ScenarioName);
            Assert.Equal(scenario.RecommendedTarget, template.Device.TargetModule);
            Assert.Contains(scenario.Slug, template.CaptureFileName, StringComparison.OrdinalIgnoreCase);
            Assert.False(template.Software.HapticDriveRunning);
        }
    }

    [Fact]
    public void FilenameBuilder_SanitizesUnsafeCharactersAndPrivateTokens()
    {
        var fileName = SimagicCaptureFilenameBuilder.Build(
            new DateTimeOffset(2026, 6, 5, 20, 15, 30, TimeSpan.Zero),
            @"SimPro C:\Users\simuser\Secret",
            "P700 SN=ABC123456789",
            SimagicCaptureScenarioId.BrakeTestVibration,
            SimagicCaptureTargetModule.Brake,
            new SimagicCaptureSettingSnapshot { FrequencyHz = 24d, StrengthPercent = 10d, DurationMs = 80 },
            new SimagicCaptureSettingSnapshot { FrequencyHz = 30d, StrengthPercent = 20d, DurationMs = 100 });

        Assert.StartsWith("2026-06-05_201530_", fileName, StringComparison.Ordinal);
        Assert.EndsWith(".pcapng", fileName, StringComparison.Ordinal);
        Assert.DoesNotContain("simuser", fileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ABC123456789", fileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\\', fileName);
        Assert.DoesNotContain(':', fileName);
        Assert.Contains("24-to-30hz", fileName, StringComparison.Ordinal);
        Assert.Contains("10-to-20pct", fileName, StringComparison.Ordinal);
        Assert.Contains("80-to-100ms", fileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_AcceptsCompleteSyntheticCaptureMetadata()
    {
        var metadata = CreateCompleteMetadata();

        var result = new SimagicCaptureMetadataValidator().Validate(metadata);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
    }

    [Fact]
    public void Validator_WarnsWhenRequiredSettingsAreMissing()
    {
        var metadata = CreateCompleteMetadata() with
        {
            ScenarioId = SimagicCaptureScenarioId.BrakeTestVibration,
            Action = CreateCompleteMetadata().Action with
            {
                SettingBefore = new SimagicCaptureSettingSnapshot(),
                SettingAfter = new SimagicCaptureSettingSnapshot()
            }
        };

        var result = new SimagicCaptureMetadataValidator().Validate(metadata);

        Assert.True(result.IsValid);
        Assert.Contains(result.Messages, message => message.Field.Contains("StrengthPercent", StringComparison.Ordinal));
        Assert.Contains(result.Messages, message => message.Field.Contains("FrequencyHz", StringComparison.Ordinal));
        Assert.Contains(result.Messages, message => message.Field.Contains("DurationMs", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_WarnsWhenRawCapturePathIsNotPrivateOrIgnored()
    {
        var metadata = CreateCompleteMetadata() with
        {
            RawCapturePath = @"C:\Users\simuser\Desktop\public-capture.pcapng"
        };

        var result = new SimagicCaptureMetadataValidator().Validate(metadata);

        Assert.True(result.IsValid);
        Assert.Contains(result.Messages, message => message.Field == nameof(SimagicCaptureMetadata.RawCapturePath));
    }

    [Fact]
    public void Sanitizer_RedactsSerialsAndPreservesScenarioSettings()
    {
        var metadata = CreateCompleteMetadata() with
        {
            Notes = @"Captured from HID\VID_0483&PID_A700&MI_00\8&2ff34217&0&0000 with serial SN=ABC123456789",
            RawCapturePath = @"C:\Users\simuser\captures\private\simagic\ABC123456789\brake-test.pcapng",
            ContainsSerialNumbers = true,
            ContainsPrivatePaths = true
        };

        var sanitized = SimagicCaptureSanitizer.Sanitize(metadata);

        Assert.DoesNotContain("ABC123456789", JsonSerializer.Serialize(sanitized, SimagicCaptureJson.Options), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("simuser", JsonSerializer.Serialize(sanitized, SimagicCaptureJson.Options), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SimagicCaptureScenarioId.BrakeFrequencyChanged, sanitized.ScenarioId);
        Assert.Equal(40d, sanitized.Action.SettingAfter.FrequencyHz);
        Assert.Equal(SimagicCaptureRedactionStatus.Redacted, sanitized.RedactionStatus);
        Assert.False(sanitized.ContainsSerialNumbers);
        Assert.False(sanitized.ContainsPrivatePaths);
    }

    [Fact]
    public async Task ManifestExporter_IncludesOnlySanitizedMetadataAndNoRawCaptureBytes()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"haptic-drive-stage-2h-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            var metadataPath = Path.Combine(tempDirectory, "metadata.json");
            var outputDirectory = Path.Combine(tempDirectory, "generated");
            var metadata = CreateCompleteMetadata() with
            {
                Notes = "Raw bytes: DE AD BE EF should not be treated as capture content.",
                RawCapturePath = @"C:\Users\simuser\captures\private\simagic\SN=ABC123456789\raw-capture.pcapng",
                ContainsSerialNumbers = true,
                ContainsPrivatePaths = true
            };
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, SimagicCaptureJson.Options));

            var exporter = new SimagicCaptureManifestExporter();
            var manifest = await exporter.LoadManifestFromFolderAsync(tempDirectory);
            var path = await exporter.ExportJsonAsync(manifest, outputDirectory);
            var json = await File.ReadAllTextAsync(path);

            Assert.Equal(1, manifest.SourceMetadataCount);
            Assert.Contains("BrakeFrequencyChanged", json, StringComparison.Ordinal);
            Assert.DoesNotContain("ABC123456789", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:\\Users\\simuser", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".pcapng bytes", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DE AD BE EF", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Raw bytes", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("redacted", json, StringComparison.OrdinalIgnoreCase);
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
    public async Task CliHelp_ListsCaptureMetadataCommands()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await SimagicResearchCli.RunAsync(["--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("capture-scenarios", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("capture-template", output.ToString(), StringComparison.Ordinal);
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public void ResearchAssembly_StillDoesNotReferencePhrOutputOrAudioProjects()
    {
        var referencedAssemblyNames = typeof(SimagicCaptureMetadata).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("HapticDrive.Simagic.PHPR.Abstractions", referencedAssemblyNames);
        Assert.DoesNotContain("HapticDrive.Asio.Audio", referencedAssemblyNames);
    }

    private static SimagicCaptureMetadata CreateCompleteMetadata()
    {
        return new SimagicCaptureMetadata
        {
            CaptureId = "capture-synthetic-brake-frequency",
            ScenarioId = SimagicCaptureScenarioId.BrakeFrequencyChanged,
            ScenarioName = "Brake P-HPR frequency changed only",
            CaptureFileName = "2026-06-05_201530_simpro_p700_brake-frequency_brake_24-to-40hz.pcapng",
            CaptureStartedAtUtc = new DateTimeOffset(2026, 6, 5, 20, 15, 30, TimeSpan.Zero),
            CaptureDuration = TimeSpan.FromSeconds(8),
            Software = new SimagicCaptureSoftwareContext
            {
                CaptureTool = "USBPcap/Wireshark",
                CaptureToolVersion = "synthetic",
                SoftwareUnderTest = "SimPro Manager",
                SoftwareUnderTestVersion = "synthetic",
                SimProVersion = "V3 synthetic",
                SimHubVersion = "not running",
                SimProRunning = true,
                SimHubRunning = false,
                HapticDriveRunning = false
            },
            Device = new SimagicCaptureDeviceContext
            {
                P700FirmwareVersion = "synthetic",
                DeviceInventoryReference = "local-device-inventory/simagic-device-inventory-summary.md",
                TargetModule = SimagicCaptureTargetModule.Brake
            },
            Action = new SimagicCaptureActionContext
            {
                ActionPerformed = "Changed brake P-HPR frequency from 24 Hz to 40 Hz only.",
                SettingBefore = new SimagicCaptureSettingSnapshot
                {
                    StrengthPercent = 20d,
                    FrequencyHz = 24d,
                    DurationMs = 100
                },
                SettingAfter = new SimagicCaptureSettingSnapshot
                {
                    StrengthPercent = 20d,
                    FrequencyHz = 40d,
                    DurationMs = 100
                },
                ExpectedVibrationObserved = true,
                ActualObservedBehaviour = "Brake module vibrated as expected."
            },
            Notes = "Synthetic metadata for hardware-free Stage 2H tests.",
            RedactionStatus = SimagicCaptureRedactionStatus.ReviewedClean,
            ContainsSerialNumbers = false,
            ContainsPrivatePaths = false,
            RawCapturePath = "captures/private/simagic/2026-06-05/2026-06-05_201530_simpro_p700_brake-frequency_brake_24-to-40hz.pcapng",
            SanitizedSummaryPath = "capture-metadata/generated/capture-synthetic-brake-frequency-summary.json"
        };
    }
}
