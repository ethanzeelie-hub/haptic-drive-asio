using System.Text.Json;
using HapticDrive.Simagic.PHPR.Research.Inventory;

namespace HapticDrive.Simagic.PHPR.Research.Tests;

public sealed class SimagicDeviceInventoryTests
{
    [Fact]
    public void InventoryItem_CapturesReadOnlyMetadata()
    {
        var timestamp = new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
        var item = new SimagicDeviceInventoryItem
        {
            DeviceId = "synthetic:p700",
            DisplayName = "Simagic P700 pedal controller",
            Manufacturer = "Simagic",
            ProductName = "P700",
            ServiceName = "HidUsb",
            DriverProvider = "Microsoft",
            DriverVersion = "10.0.1",
            DeviceClass = "HIDClass",
            ClassGuid = "{745A17A0-74D3-11D0-B6FE-00A0C90F57DA}",
            VendorId = 0x0483,
            ProductId = 0xA700,
            InterfaceNumber = "00",
            CollectionNumber = "01",
            HidUsagePage = 0x01,
            HidUsage = 0x04,
            InputReportByteLength = 64,
            OutputReportByteLength = 0,
            FeatureReportByteLength = 0,
            EndpointSummaries = ["interrupt in endpoint 0x81"],
            SafeInstanceId = @"HID\VID_0483&PID_A700&MI_00\<redacted>",
            SafeDevicePath = @"HID#VID_0483&PID_A700&MI_00#<redacted>",
            DiscoveryMethod = SimagicDeviceInventoryMethod.WindowsRegistryHid,
            DiscoveredAtUtc = timestamp
        };

        Assert.True(item.ReadOnlyDiscoverySucceeded);
        Assert.Equal("Simagic P700 pedal controller", item.DisplayName);
        Assert.Equal((ushort)0x0483, item.VendorId);
        Assert.Equal("VID_0483/PID_A700", item.VendorProductText);
        Assert.Equal("00", item.InterfaceNumber);
        Assert.Equal(timestamp, item.DiscoveredAtUtc);
    }

    [Fact]
    public void EmptySnapshot_IsSafeAndHasNoCandidates()
    {
        var snapshot = new SimagicDeviceInventorySnapshot
        {
            Items = [],
            Methods = [SimagicDeviceInventoryMethod.Synthetic],
            Errors = []
        };

        Assert.True(snapshot.HasRun);
        Assert.Equal(0, snapshot.DeviceCount);
        Assert.True(snapshot.ReadOnlyDiscoverySucceeded);
        Assert.Empty(snapshot.SpecificSimagicCandidates);
        Assert.Empty(snapshot.GenericHidOrUsbCandidates);
    }

    [Theory]
    [InlineData("Simagic P700 pedal set", SimagicDeviceInventoryMethod.WindowsRegistryUsb, SimagicDeviceCandidateKind.P700PedalController)]
    [InlineData("Simagic haptic controller P-HPR", SimagicDeviceInventoryMethod.WindowsRegistryHid, SimagicDeviceCandidateKind.PHprModuleOrController)]
    [InlineData("Simagic Alpha Evo 12Nm Wheelbase", SimagicDeviceInventoryMethod.WindowsGameController, SimagicDeviceCandidateKind.AlphaEvoWheelbase)]
    [InlineData("Simagic GT Neo wheel input", SimagicDeviceInventoryMethod.RawInputMetadata, SimagicDeviceCandidateKind.GtNeoWheelInput)]
    [InlineData("Generic HID-compliant game controller", SimagicDeviceInventoryMethod.WindowsRegistryHid, SimagicDeviceCandidateKind.GenericHid)]
    public void Classifier_ScoresSyntheticDevices(
        string displayName,
        SimagicDeviceInventoryMethod method,
        SimagicDeviceCandidateKind expectedKind)
    {
        var classifier = new SimagicDeviceCandidateClassifier();

        var scored = classifier.ScoreItem(CreateSyntheticItem(displayName, method));

        Assert.Equal(expectedKind, scored.CandidateKind);
        Assert.True(scored.CandidateScore > 0);
        Assert.NotEqual("No Simagic, HID, or USB input signals found.", scored.CandidateReason);
    }

    [Fact]
    public void Classifier_UsesSimagicUnknownWhenSpecificHardwareCannotBeInferred()
    {
        var classifier = new SimagicDeviceCandidateClassifier();

        var scored = classifier.ScoreItem(CreateSyntheticItem("Simagic USB device", SimagicDeviceInventoryMethod.WindowsRegistryUsb));

        Assert.Equal(SimagicDeviceCandidateKind.SimagicUnknown, scored.CandidateKind);
        Assert.True(scored.CandidateScore > 0);
    }

    [Theory]
    [InlineData(0x0500)]
    [InlineData(0x0905)]
    [InlineData(0xB500)]
    [InlineData(0xB905)]
    public void Classifier_TreatsVid3670AsSimagicFamilyCandidate(int productId)
    {
        var classifier = new SimagicDeviceCandidateClassifier();
        var item = CreateSyntheticItem("HID-compliant vendor-defined device", SimagicDeviceInventoryMethod.WindowsRegistryHid) with
        {
            Manufacturer = null,
            ProductName = "HID-compliant vendor-defined device",
            VendorId = 0x3670,
            ProductId = (ushort)productId,
            SafeInstanceId = $@"HID\VID_3670&PID_{productId:X4}\<redacted>",
            SafeDevicePath = $@"HID#VID_3670&PID_{productId:X4}#<redacted>"
        };

        var scored = classifier.ScoreItem(item);

        Assert.Equal(SimagicDeviceCandidateKind.SimagicUnknown, scored.CandidateKind);
        Assert.True(scored.CandidateScore > 0);
        Assert.Contains("VID_3670", scored.CandidateReason, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(SimagicDeviceCandidateKind.GenericHid, scored.CandidateKind);
    }

    [Fact]
    public void Sanitizer_RedactsSerialLikePathSegmentsAndPreservesVidPid()
    {
        const string raw = @"\\?\hid#vid_0483&pid_a700&mi_00#8&2ff34217&0&0000#{745A17A0-74D3-11D0-B6FE-00A0C90F57DA}";

        var sanitized = SimagicDeviceInventorySanitizer.SanitizeIdentifier(raw);

        Assert.NotNull(sanitized);
        Assert.Contains("vid_0483&pid_a700&mi_00", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("2ff34217", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0000", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<redacted>", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitizer_RedactsWindowsUsernames()
    {
        var sanitized = SimagicDeviceInventorySanitizer.SanitizeIdentifier(
            @"C:\Users\ethan\Documents\private-device-inventory\serial-123456.txt");

        Assert.NotNull(sanitized);
        Assert.Contains(@"C:\Users\<redacted>", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\ethan\", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exporter_ProducesSanitizedJsonWithoutRawPrivatePath()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"haptic-drive-stage-2g-{Guid.NewGuid():N}");
        try
        {
            var rawPath = @"HID\VID_0483&PID_A700&MI_00\8&2ff34217&0&0000";
            var snapshot = new SimagicDeviceInventorySnapshot
            {
                Items =
                [
                    CreateSyntheticItem("Simagic P700 pedal set", SimagicDeviceInventoryMethod.WindowsRegistryHid) with
                    {
                        SafeInstanceId = rawPath,
                        SafeDevicePath = rawPath
                    }
                ],
                Methods = [SimagicDeviceInventoryMethod.WindowsRegistryHid]
            };
            var exporter = new SimagicDeviceInventoryExporter();

            var path = await exporter.ExportJsonAsync(snapshot, tempDirectory);
            var json = await File.ReadAllTextAsync(path);

            Assert.DoesNotContain("2ff34217", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("VID_0483", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("PID_A700", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Read-only inventory only", json, StringComparison.Ordinal);
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
    public async Task Provider_CapturesSourceFailureWithoutThrowing()
    {
        var provider = new CompositeSimagicDeviceInventoryProvider([new ThrowingSource()]);

        var snapshot = await provider.DiscoverAsync();

        Assert.Empty(snapshot.Items);
        var error = Assert.Single(snapshot.Errors);
        Assert.Equal(SimagicDeviceInventoryMethod.Synthetic, error.Method);
        Assert.Contains("synthetic inventory failure", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(snapshot.ReadOnlyDiscoverySucceeded);
    }

    [Fact]
    public void InventoryInterfaces_DoNotExposeDeviceWriteCapableMethodNames()
    {
        var forbiddenTerms = new[] { "Write", "Send", "Output", "Feature", "Vibrate", "Command", "Report" };
        var methodNames = new[]
            {
                typeof(ISimagicDeviceInventoryProvider),
                typeof(ISimagicDeviceInventorySource),
                typeof(ISimagicDeviceInventoryExporter)
            }
            .SelectMany(type => type.GetMethods())
            .Where(method => method.DeclaringType != typeof(object))
            .Select(method => method.Name)
            .ToArray();

        foreach (var methodName in methodNames)
        {
            Assert.DoesNotContain(forbiddenTerms, term => methodName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void ResearchAssembly_DoesNotReferenceAudioRuntimeOrLiveRoutingProjects()
    {
        var referencedAssemblyNames = typeof(SimagicDeviceInventoryItem).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.Contains("HapticDrive.Simagic.PHPR.Abstractions", referencedAssemblyNames);
        Assert.DoesNotContain("HapticDrive.Asio.Audio", referencedAssemblyNames);
        Assert.DoesNotContain("HapticDrive.Asio.Runtime", referencedAssemblyNames);
        Assert.DoesNotContain("HapticDrive.Asio.App", referencedAssemblyNames);
        Assert.DoesNotContain("HapticDrive.Actuation", referencedAssemblyNames);
    }

    [Fact]
    public void Export_JsonRoundTrips()
    {
        var snapshot = new SimagicDeviceInventorySnapshot
        {
            Items = [new SimagicDeviceCandidateClassifier().ScoreItem(CreateSyntheticItem("Simagic GT Neo wheel input", SimagicDeviceInventoryMethod.RawInputMetadata))],
            Methods = [SimagicDeviceInventoryMethod.RawInputMetadata]
        };
        var export = SimagicDeviceInventoryExport.FromSnapshot(snapshot);

        var json = JsonSerializer.Serialize(export, SimagicDeviceInventoryExporter.JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<SimagicDeviceInventoryExport>(json, SimagicDeviceInventoryExporter.JsonOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal("Stage 2G", roundTripped.Stage);
        Assert.Single(roundTripped.Snapshot.Items);
        Assert.Equal(SimagicDeviceCandidateKind.GtNeoWheelInput, roundTripped.Snapshot.Items[0].CandidateKind);
    }

    [Fact]
    public void SummaryFormatter_IncludesSafetyAndAwaitingInventoryWhenNoSimagicCandidatesExist()
    {
        var snapshot = new SimagicDeviceInventorySnapshot
        {
            Items = [new SimagicDeviceCandidateClassifier().ScoreItem(CreateSyntheticItem("Generic HID", SimagicDeviceInventoryMethod.WindowsRegistryHid))],
            Methods = [SimagicDeviceInventoryMethod.WindowsRegistryHid]
        };

        var summary = SimagicDeviceInventorySummaryFormatter.FormatConsole(snapshot);

        Assert.Contains("No Simagic-specific local candidates", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("P-HPR visibility is not validated", summary, StringComparison.OrdinalIgnoreCase);
    }

    private static SimagicDeviceInventoryItem CreateSyntheticItem(
        string displayName,
        SimagicDeviceInventoryMethod method)
    {
        return new SimagicDeviceInventoryItem
        {
            DeviceId = $"synthetic:{displayName}",
            DisplayName = displayName,
            ProductName = displayName,
            Manufacturer = displayName.Contains("Simagic", StringComparison.OrdinalIgnoreCase) ? "Simagic" : null,
            VendorId = 0x0483,
            ProductId = 0xA700,
            DeviceClass = method == SimagicDeviceInventoryMethod.WindowsRegistryUsb ? "USB Input Device" : "HIDClass",
            HidUsagePage = method == SimagicDeviceInventoryMethod.WindowsRegistryHid ? (ushort)0x01 : null,
            HidUsage = method == SimagicDeviceInventoryMethod.WindowsRegistryHid ? (ushort)0x04 : null,
            SafeInstanceId = @"HID\VID_0483&PID_A700&MI_00\<redacted>",
            DiscoveryMethod = method,
            DiscoveredAtUtc = new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero)
        };
    }

    private sealed class ThrowingSource : ISimagicDeviceInventorySource
    {
        public SimagicDeviceInventoryMethod Method => SimagicDeviceInventoryMethod.Synthetic;

        public ValueTask<SimagicDeviceInventorySourceResult> EnumerateAsync(
            DateTimeOffset discoveredAtUtc,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("synthetic inventory failure");
        }
    }
}
