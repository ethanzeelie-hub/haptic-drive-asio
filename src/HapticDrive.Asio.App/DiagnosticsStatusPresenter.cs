using System.Text;

namespace HapticDrive.Asio.App;

internal sealed record DiagnosticsStatusSnapshot(
    DateTimeOffset GeneratedAt,
    bool FlightRecorderActive,
    string FlightRecorderPath,
    string FlightRecorderLastFallbackStatus,
    long UdpPacketCount,
    long ParserSuccessCount,
    long ParserFailureCount,
    int ActiveEffectCount,
    float OutputPeakLevel,
    long RenderCallbackCount,
    string PipelineText,
    string UdpListenerText,
    string UdpForwardingText,
    string UdpForwardingDestinationsText,
    string ParserText,
    string PacketIdsText,
    string VehicleStateText,
    string RecordingText,
    string ReplayText,
    string EffectsText,
    string Bst1SlipLockText,
    string MixerSafetyText,
    IReadOnlyList<string> RoadDiagnosticsLines,
    string PhprSlipLockText,
    string TestBenchText,
    string OutputText,
    string InputDiscoveryText,
    string PaddleInputListenerText,
    string ShiftIntentText,
    string ProfilePersistenceText,
    string WorkflowText,
    string LiveValidationText,
    string PhprSoftwareCoexistenceText,
    string PhprDirectWriteReadinessText,
    string PhprRealDirectControlText,
    string PhprValidationHarnessText,
    string PaddleGearBenchText,
    string MockGearRoutingText,
    string MockPedalEffectsText,
    string ManualAsioHardwareTestText,
    string AsioReadinessText,
    string RuntimePrerequisitesText,
    string AppSettingsText);

internal sealed record DiagnosticsStatusBuildInputs(
    DateTimeOffset GeneratedAt,
    bool FlightRecorderActive,
    string FlightRecorderPath,
    string FlightRecorderLastFallbackStatus,
    long UdpPacketCount,
    long ParserSuccessCount,
    long ParserFailureCount,
    int ActiveEffectCount,
    float OutputPeakLevel,
    long RenderCallbackCount,
    string PipelineText,
    string UdpListenerText,
    string UdpForwardingText,
    string UdpForwardingDestinationsText,
    string ParserText,
    string PacketIdsText,
    string VehicleStateText,
    string RecordingText,
    string ReplayText,
    string EffectsText,
    string Bst1SlipLockText,
    string MixerSafetyText,
    IReadOnlyList<string> RoadDiagnosticsLines,
    string PhprSlipLockText,
    string TestBenchText,
    string OutputText,
    string InputDiscoveryText,
    string PaddleInputListenerText,
    string ShiftIntentText,
    string ProfilePersistenceText,
    string WorkflowText,
    string LiveValidationText,
    string PhprSoftwareCoexistenceText,
    string PhprDirectWriteReadinessText,
    string PhprRealDirectControlText,
    string PhprValidationHarnessText,
    string PaddleGearBenchText,
    string MockGearRoutingText,
    string MockPedalEffectsText,
    string ManualAsioHardwareTestText,
    string AsioReadinessText,
    string RuntimePrerequisitesText,
    string AppSettingsText);

internal sealed record DiagnosticsStatusPresentation(
    string RoadRecorderStatusText,
    string SummaryText,
    IReadOnlyList<string> Items,
    string ClipboardReportText);

internal static class DiagnosticsStatusSnapshotBuilder
{
    public static DiagnosticsStatusSnapshot Build(DiagnosticsStatusBuildInputs? inputs)
    {
        return inputs is null
            ? new DiagnosticsStatusSnapshot(
                DateTimeOffset.MinValue,
                FlightRecorderActive: false,
                FlightRecorderPath: "disabled",
                FlightRecorderLastFallbackStatus: "none",
                UdpPacketCount: 0,
                ParserSuccessCount: 0,
                ParserFailureCount: 0,
                ActiveEffectCount: 0,
                OutputPeakLevel: 0f,
                RenderCallbackCount: 0,
                PipelineText: string.Empty,
                UdpListenerText: string.Empty,
                UdpForwardingText: string.Empty,
                UdpForwardingDestinationsText: string.Empty,
                ParserText: string.Empty,
                PacketIdsText: string.Empty,
                VehicleStateText: string.Empty,
                RecordingText: string.Empty,
                ReplayText: string.Empty,
                EffectsText: string.Empty,
                Bst1SlipLockText: string.Empty,
                MixerSafetyText: string.Empty,
                RoadDiagnosticsLines: [],
                PhprSlipLockText: string.Empty,
                TestBenchText: string.Empty,
                OutputText: string.Empty,
                InputDiscoveryText: string.Empty,
                PaddleInputListenerText: string.Empty,
                ShiftIntentText: string.Empty,
                ProfilePersistenceText: string.Empty,
                WorkflowText: string.Empty,
                LiveValidationText: string.Empty,
                PhprSoftwareCoexistenceText: string.Empty,
                PhprDirectWriteReadinessText: string.Empty,
                PhprRealDirectControlText: string.Empty,
                PhprValidationHarnessText: string.Empty,
                PaddleGearBenchText: string.Empty,
                MockGearRoutingText: string.Empty,
                MockPedalEffectsText: string.Empty,
                ManualAsioHardwareTestText: string.Empty,
                AsioReadinessText: string.Empty,
                RuntimePrerequisitesText: string.Empty,
                AppSettingsText: string.Empty)
            : new DiagnosticsStatusSnapshot(
                inputs.GeneratedAt,
                inputs.FlightRecorderActive,
                inputs.FlightRecorderPath,
                inputs.FlightRecorderLastFallbackStatus,
                inputs.UdpPacketCount,
                inputs.ParserSuccessCount,
                inputs.ParserFailureCount,
                inputs.ActiveEffectCount,
                inputs.OutputPeakLevel,
                inputs.RenderCallbackCount,
                inputs.PipelineText,
                inputs.UdpListenerText,
                inputs.UdpForwardingText,
                inputs.UdpForwardingDestinationsText,
                inputs.ParserText,
                inputs.PacketIdsText,
                inputs.VehicleStateText,
                inputs.RecordingText,
                inputs.ReplayText,
                inputs.EffectsText,
                inputs.Bst1SlipLockText,
                inputs.MixerSafetyText,
                inputs.RoadDiagnosticsLines,
                inputs.PhprSlipLockText,
                inputs.TestBenchText,
                inputs.OutputText,
                inputs.InputDiscoveryText,
                inputs.PaddleInputListenerText,
                inputs.ShiftIntentText,
                inputs.ProfilePersistenceText,
                inputs.WorkflowText,
                inputs.LiveValidationText,
                inputs.PhprSoftwareCoexistenceText,
                inputs.PhprDirectWriteReadinessText,
                inputs.PhprRealDirectControlText,
                inputs.PhprValidationHarnessText,
                inputs.PaddleGearBenchText,
                inputs.MockGearRoutingText,
                inputs.MockPedalEffectsText,
                inputs.ManualAsioHardwareTestText,
                inputs.AsioReadinessText,
                inputs.RuntimePrerequisitesText,
                inputs.AppSettingsText);
    }
}

internal static class DiagnosticsStatusPresenter
{
    public static DiagnosticsStatusPresentation Build(DiagnosticsStatusSnapshot? snapshot)
    {
        var resolved = snapshot ?? DiagnosticsStatusSnapshotBuilder.Build(null);
        var items = BuildItems(resolved);
        var summary = $"UDP {resolved.UdpPacketCount:N0} packet(s), parser {resolved.ParserSuccessCount:N0} valid / {resolved.ParserFailureCount:N0} failed, effects {resolved.ActiveEffectCount}, output peak {resolved.OutputPeakLevel:0.000}, callbacks {resolved.RenderCallbackCount:N0}.";
        var roadRecorderStatus = $"Road recorder: {(resolved.FlightRecorderActive ? "active" : "disabled")}; path {Normalize(resolved.FlightRecorderPath, "disabled")}; last fallback {Normalize(resolved.FlightRecorderLastFallbackStatus, "none")}.";

        var report = new StringBuilder()
            .AppendLine("Haptic Drive ASIO diagnostics")
            .AppendLine($"Generated: {resolved.GeneratedAt:g}")
            .AppendLine(summary);
        foreach (var item in items)
        {
            report.AppendLine(item);
        }

        return new DiagnosticsStatusPresentation(roadRecorderStatus, summary, items, report.ToString());
    }

    private static IReadOnlyList<string> BuildItems(DiagnosticsStatusSnapshot snapshot)
    {
        var items = new List<string>
        {
            $"Pipeline: {Normalize(snapshot.PipelineText, "unknown")}",
            $"UDP listener: {Normalize(snapshot.UdpListenerText, "unknown")}",
            $"UDP forwarding: {Normalize(snapshot.UdpForwardingText, "unknown")}",
            $"UDP forwarding destinations: {Normalize(snapshot.UdpForwardingDestinationsText, "none")}",
            $"Parser: {Normalize(snapshot.ParserText, "unknown")}",
            $"Packet IDs: {Normalize(snapshot.PacketIdsText, "none")}",
            $"VehicleState: {Normalize(snapshot.VehicleStateText, "unknown")}",
            $"Recording: {Normalize(snapshot.RecordingText, "unknown")}",
            $"Replay: {Normalize(snapshot.ReplayText, "unknown")}",
            $"Effects: {Normalize(snapshot.EffectsText, "unknown")}",
            $"BST-1 slip/lock: {Normalize(snapshot.Bst1SlipLockText, "none")}",
            $"Mixer / safety: {Normalize(snapshot.MixerSafetyText, "unknown")}"
        };

        items.AddRange(snapshot.RoadDiagnosticsLines.Where(item => !string.IsNullOrWhiteSpace(item)));
        items.Add($"P-HPR slip/lock: {Normalize(snapshot.PhprSlipLockText, "none")}");
        items.Add($"Test bench: {Normalize(snapshot.TestBenchText, "unknown")}");
        items.Add($"Output: {Normalize(snapshot.OutputText, "unknown")}");
        items.Add($"Input discovery: {Normalize(snapshot.InputDiscoveryText, "unknown")}");
        items.Add($"Paddle input listener: {Normalize(snapshot.PaddleInputListenerText, "unknown")}");
        items.Add($"Shift intent layer: {Normalize(snapshot.ShiftIntentText, "unknown")}");
        items.Add(Normalize(snapshot.ProfilePersistenceText, "Profile persistence: unavailable."));
        items.Add(Normalize(snapshot.WorkflowText, "P-HPR workflow: unavailable."));
        items.Add(Normalize(snapshot.LiveValidationText, "P-HPR live F1 validation: unavailable."));
        items.Add($"P-HPR software coexistence: {Normalize(snapshot.PhprSoftwareCoexistenceText, "unknown")}");
        items.Add($"P-HPR direct write readiness: {Normalize(snapshot.PhprDirectWriteReadinessText, "unknown")}");
        items.Add($"P-HPR real direct control: {Normalize(snapshot.PhprRealDirectControlText, "unknown")}");
        items.Add($"P-HPR validation harness: {Normalize(snapshot.PhprValidationHarnessText, "unknown")}");
        items.Add($"Paddle gear bench test: {Normalize(snapshot.PaddleGearBenchText, "unknown")}");
        items.Add($"Mock P-HPR gear routing: {Normalize(snapshot.MockGearRoutingText, "unknown")}");
        items.Add($"Mock P-HPR pedal effects: {Normalize(snapshot.MockPedalEffectsText, "unknown")}");
        items.Add($"Manual ASIO Hardware Test: {Normalize(snapshot.ManualAsioHardwareTestText, "unknown")}");
        items.Add($"ASIO readiness: {Normalize(snapshot.AsioReadinessText, "unknown")}");
        items.Add($"Runtime prerequisites: {Normalize(snapshot.RuntimePrerequisitesText, "unknown")}");
        items.Add($"App settings: {Normalize(snapshot.AppSettingsText, "unknown")}");

        return items;
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }
}
