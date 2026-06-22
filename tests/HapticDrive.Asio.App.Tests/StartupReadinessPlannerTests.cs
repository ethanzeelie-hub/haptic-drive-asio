using HapticDrive.Asio.App;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App.Tests;

public sealed class StartupReadinessPlannerTests
{
    [Fact]
    public void BuildAsioSelectionPlan_UsesResolvedDefaultsWhenNoPersistedPreferenceExists()
    {
        var plan = StartupReadinessPlanner.BuildAsioSelectionPlan(
            hasPersistedOutputModePreference: false,
            selectedOutputKind: AudioOutputDeviceKind.Null,
            selectedAsioDriverName: null,
            selectedAsioOutputChannel: null,
            armAsioPreference: false,
            visibleDriverNames: [AsioAudioOutputDevice.PreferredDriverName]);

        Assert.Equal(AudioOutputDeviceKind.Asio, plan.SelectedOutputKind);
        Assert.Equal(AsioAudioOutputDevice.PreferredDriverName, plan.SelectedAsioDriverName);
        Assert.Equal(Bst1AsioStartupDefaults.ValidatedBst1Channel, plan.SelectedAsioOutputChannel);
        Assert.True(plan.ArmAsioPreference);
        Assert.Contains("without starting output", plan.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildAsioSelectionPlan_ClearsMissingPersistedDriverAndDisarmsWithoutStartingOutput()
    {
        var plan = StartupReadinessPlanner.BuildAsioSelectionPlan(
            hasPersistedOutputModePreference: true,
            selectedOutputKind: AudioOutputDeviceKind.Asio,
            selectedAsioDriverName: "Missing Driver",
            selectedAsioOutputChannel: 1,
            armAsioPreference: true,
            visibleDriverNames: ["Other Driver"]);

        Assert.Equal(AudioOutputDeviceKind.Asio, plan.SelectedOutputKind);
        Assert.Null(plan.SelectedAsioDriverName);
        Assert.Equal(1, plan.SelectedAsioOutputChannel);
        Assert.False(plan.ArmAsioPreference);
        Assert.Contains("saved driver is unavailable", plan.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildStartupPhprAutoReadySelection_SelectsPreferredCandidateButKeepsDirectControlDisabled()
    {
        var selection = StartupReadinessPlanner.BuildStartupPhprAutoReadySelection(
            [
                Candidate("generic-output-first", vendorId: 0x1234, productId: 0x5678, outputLength: 64, featureLength: null, featureIds: []),
                Candidate("preferred-second", vendorId: 0x3670, productId: 0x0905, outputLength: null, featureLength: 64, featureIds: [PHprDirectOutputCandidate.F1EcFeatureReportId])
            ],
            PHprRealOutputOptions.Disabled);

        Assert.True(selection.HasPreferredCandidate);
        Assert.Equal("preferred-second", selection.Candidate?.CandidateId);
        Assert.True(selection.Selector.IsSelected);
        Assert.Equal(PHprHidReportTransport.FeatureReport, selection.Selector.Transport);
        Assert.False(selection.Options.DirectControlEnabled);
        Assert.False(selection.Options.DirectControlArmed);
        Assert.True(selection.Options.ReportShapeValidationSucceeded);
        Assert.Contains("no-output readiness checks", selection.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildStartupPhprAutoReadySelection_ClearsRuntimeOnlySelectionWhenNoPreferredCandidateExists()
    {
        var current = PHprRealOutputOptions.Disabled with
        {
            DirectControlEnabled = true,
            DirectControlArmed = true,
            Selector = new PHprHidDeviceSelector(
                "private-device-path",
                "Selected device",
                "HID",
                PHprDirectOutputCandidate.F1EcFeatureReportId,
                SimHubF1EcRealReportEncoder.PayloadLengthBytes,
                PHprHidReportTransport.FeatureReport)
        };

        var selection = StartupReadinessPlanner.BuildStartupPhprAutoReadySelection(
            [Candidate("generic", vendorId: 0x1234, productId: 0x5678, outputLength: 64, featureLength: null, featureIds: [])],
            current);

        Assert.False(selection.HasPreferredCandidate);
        Assert.False(selection.Options.DirectControlEnabled);
        Assert.False(selection.Options.DirectControlArmed);
        Assert.False(selection.Options.Selector.IsSelected);
        Assert.Equal(PHprHidDeviceSelector.None, selection.Selector);
    }

    private static PHprDirectOutputCandidate Candidate(
        string candidateId,
        int vendorId,
        int productId,
        int? outputLength,
        int? featureLength,
        IReadOnlyList<byte> featureIds)
    {
        return new PHprDirectOutputCandidate
        {
            CandidateId = candidateId,
            DevicePath = outputLength is null
                ? $@"\\?\hid#vid_{vendorId:X4}&pid_{productId:X4}#{candidateId}"
                : candidateId,
            SourceMethod = outputLength is null
                ? PHprDirectOutputCandidateSourceMethod.HidDeviceInterface
                : PHprDirectOutputCandidateSourceMethod.HidRegistryMetadata,
            DisplayName = candidateId,
            DeviceClass = "HID",
            VendorId = (ushort)vendorId,
            ProductId = (ushort)productId,
            HidUsagePage = 0xFF00,
            HidUsage = 0x0001,
            OutputReportByteLength = outputLength,
            FeatureReportByteLength = featureLength,
            FeatureReportIds = featureIds,
            Confidence = PHprDirectOutputCandidateConfidence.GenericHid,
            ConfidenceReason = "test fixture"
        };
    }
}
