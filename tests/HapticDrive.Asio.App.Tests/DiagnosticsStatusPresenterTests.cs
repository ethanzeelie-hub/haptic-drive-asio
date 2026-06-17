using HapticDrive.Asio.App;

namespace HapticDrive.Asio.App.Tests;

public sealed class DiagnosticsStatusPresenterTests
{
    [Fact]
    public void Build_FormatsDiagnosticsSummaryItemsAndClipboardReport()
    {
        var generatedAt = new DateTimeOffset(2026, 6, 17, 10, 30, 0, TimeSpan.Zero);
        var snapshot = DiagnosticsStatusSnapshotBuilder.Build(new DiagnosticsStatusBuildInputs(
            GeneratedAt: generatedAt,
            FlightRecorderActive: true,
            FlightRecorderPath: "local-validation-results\\road-flight.jsonl",
            FlightRecorderLastFallbackStatus: "none",
            UdpPacketCount: 42,
            ParserSuccessCount: 40,
            ParserFailureCount: 1,
            ActiveEffectCount: 5,
            OutputPeakLevel: 0.42f,
            RenderCallbackCount: 128,
            PipelineText: "running; source Replay; rendered 20 buffer(s); telemetry age 2 ms; stale mute False; last error none.",
            UdpListenerText: "running on port 20777; rate 60.00/s; last packet 0.1s ago.",
            UdpForwardingText: "1/2 destination(s) enabled; 100 datagrams; 0 error(s).",
            UdpForwardingDestinationsText: "127.0.0.1:20778 enabled",
            ParserText: "40 valid, 1 ignored, 1 failed. Replay active.",
            PacketIdsText: "Motion#0: 40",
            VehicleStateText: "40 update(s). Vehicle state current.",
            RecordingText: "inactive; 0 packet(s); file none.",
            ReplayText: "active; source test-session.hdrec; 40 packet(s); Replay active.",
            EffectsText: "enabled engine True, gear True, kerb False, impact False, road True, slip True, lock False; overall slip/lock True; peak 0.420.",
            Bst1SlipLockText: "source Slip; reason active; slip intensity 0.50; lock intensity 0.00; slip ratio 0.10; slip angle 0.05 rad; wheel-speed ratio 0.95; frequency 42.0 Hz; roughness 30%; peak 0.200.",
            MixerSafetyText: "mixer peak 0.300; output peak 0.420; limited 0; clipped 0; emergency mute False.",
            RoadDiagnosticsLines:
            [
                "Road signal: sharedRoadSignalEnabled True; raw 0.200; smoothed 0.150; output 0.120.",
                "BST-1 road proof: bst1RoadOutputEnabled True; gain 50%; total output peak 0.420.",
                "P-HPR road proof: enabled True; attempts 4; routed commands 2.",
                "Road flight recorder: active True; path local-validation-results\\road-flight.jsonl; source Replay; replay test-session.hdrec; telemetry age 2 ms; recommended event replay."
            ],
            PhprSlipLockText: "runtime Active; active Brake; attempts 4; routed 2.",
            TestBenchText: "inactive; signal Silence; output Null Output; peak 0.000.",
            OutputText: "Null Output (Started); streaming True; hardware required False; manual debug False; hardware-absent mode True; null buffers 20; render callbacks 128; backend callbacks 128; output buffers 20; drops 0; underruns 0; render 0.1 ms; jitter 0.0 ms.",
            InputDiscoveryText: "2 device(s); methods RawInput; wheelbase 1; GT Neo/wheel 1; P700 0; unknown HID/game-controller 0; errors none; read-only discovery.",
            PaddleInputListenerText: "Running; selected GT Neo (device-1); method WindowsGameController; left 4 state Released; right 5 state Released; last raw none; last mapped none; count 0; debounce 5 ms; debounce suppressed 0; error none; diagnostics only, no haptic output.",
            ShiftIntentText: "enabled; mode InstantPaddleOnly; DrivingArmed True reason DrivingArmed; menu safe True; require recent telemetry True; accepted 3; suppressed 1; last accepted Downshift seq 2 utc 2026-06-17T10:29:59.0000000+00:00 button 5 gear 4; last suppressed Right seq 3 reason MenuUnsafe; pending confirmations 0; accepted events may feed mock-only P-HPR gear routing.",
            ProfilePersistenceText: "Profiles: audio default.hdprofile.json auto-saves current rig tuning/defaults; P-HPR p-hpr.hdphprprofile.json is a manual effect-preferences snapshot only.",
            WorkflowText: "Workflow: mode Real Direct Control; input Replay; replay source test-session.hdrec; replay packets 40; direct control enabled True armed True; selected output True; mock gear False; mock pedal False; road True; slip/lock True.",
            LiveValidationText: "Live F1 validation: replay input active; telemetry fresh; gear pulses require live paddles.",
            PhprSoftwareCoexistenceText: "status Clear; SimPro False; SimHub False; last scan 2026-06-17 10:29:58Z; supported True; direct control blocked False; read-only process detection only; error none.",
            PhprDirectWriteReadinessText: "Ready; no-write stage False; enable True; arm True; manual pulse True; issues none",
            PhprRealDirectControlText: "enabled; selected True; candidates 1; source HidDevices; raw-input-only False; openable True; transport FeatureReport; output report known False; feature report known True; report-shape attempted True; succeeded True; failed False; shape message none; expected first bytes F1-EC; open-check attempted True; succeeded True; failed False; open error none; connection Open; writer open True; interface HID; report ID 0xF1; report length 64; private path held in memory only; timeout 25 ms; can pulse True; brake on 55%/52 Hz/45 ms; throttle off 0%/0 Hz/0 ms; road enabled brake on strength 20-60%; freq 40-80 Hz; duration 60 ms throttle off strength 0-0%; freq 0-0 Hz; duration 0 ms last road routed; slip/lock enabled slip on target Brake; strength 10-70%; freq 30-90 Hz; duration 55 ms lock off target Throttle; strength 0-0%; freq 0-0 Hz; duration 0 ms last slip/lock active; gear latency routed true; writes 8; failures 1; opens 1/1; closes 1/1; stops 2; disconnects 0; timeouts 0; invalid reports 0; last target Brake; last report Active 64 bytes; last status Accepted; write Succeeded; stop Succeeded; open Succeeded; close Succeeded; last error none; runtime-only enable/open-check/device not persisted; safe gear-pulse, road, and slip/lock settings persisted.",
            PhprValidationHarnessText: "Ready; brake True; throttle True; gear True; issues none; pass requested False; can mark pass False; result issues none; confirmations user False, P700 False, brake False, throttle False; last export none; no hardware output triggered by harness.",
            PaddleGearBenchText: "enabled/auto-armed; output Direct; target Both; runtime Active; route service SharedPathProven; pulse id 5; brake on 55%/52 Hz/45 ms; throttle off 0%/0 Hz/0 ms; accepted 3; suppressed 1; left 2; right 1; source none; decision none; active pulse False; pending stops 0; generations brake 3 throttle 0; retrigger 1; stale stop ignored 0; stale output dropped 0; marker False; last start none target none; scheduled stop none; last stop none target none; stop result Succeeded none; emergency stop none none none; watchdog stop-all 0 none none; latency paddle-to-write 1.2 ms; flight recorder disabled; last suppression none; last output Accepted; runtime-only enable.",
            MockGearRoutingText: "enabled; target Brake; strength 55%; frequency 52 Hz; duration 45 ms; routed 12; ignored 1; safety rejected 0; last result Accepted; safety violation none; mock commands 12; mock frames 48; pending stops 0; emergency stop False; mock only, no hardware output.",
            MockPedalEffectsText: "enabled; road enabled target Brake; slip enabled target Brake; lock disabled target Throttle; last result Accepted; safety violation none; mock commands 18; mock frames 72; pending stops 0; emergency stop False; mock only, no hardware output.",
            ManualAsioHardwareTestText: "mode Null; ASIO status not armed; active False; haptics False; emergency False; normal mute False; last source none; last signal none; frequency none; duration none; duration mode none; BST-1 duration mode Sync; effective BST-1 duration 45 ms; P-HPR gear duration 45 ms; custom BST-1 duration 45 ms; flight recorder disabled.",
            AsioReadinessText: "M-Audio ready. Drivers reported 1; M-Audio match yes; channel 1; armed True; Windows sound output proves ASIO False.",
            RuntimePrerequisitesText: ".NET 8.0.0; WPF desktop runtime is present because the app is running; launch script sets DOTNET_ROOT to the repo-local runtime before starting the executable.",
            AppSettingsText: "C:\\repo\\appsettings.json; loaded; theme dark; output mode Asio; replay Real time; persisted ASIO driver M-Audio; persisted ASIO channel 1; persisted Arm ASIO preference True; persisted paddle mapping device device-1 left 4 right 5 debounce 5 ms; shift intent enabled mode InstantPaddleOnly; BST-1 local gear enabled 55% 52 Hz 45 ms; mock gear routing enabled target Brake; mock pedal effects enabled; real road vibration enabled; real slip/lock enabled; haptics running state, emergency mute, active pulses, pending stops, P-HPR real direct-control enabled/selected private device, P-HPR emergency stop state, safety latch state, paddle bench enable state, manual ASIO test active state, flight-recorder history, and mock histories are not persisted."));

        var presentation = DiagnosticsStatusPresenter.Build(snapshot);

        Assert.Equal(
            "Road recorder: active; path local-validation-results\\road-flight.jsonl; last fallback none.",
            presentation.RoadRecorderStatusText);
        Assert.Equal(
            "UDP 42 packet(s), parser 40 valid / 1 failed, effects 5, output peak 0.420, callbacks 128.",
            presentation.SummaryText);
        Assert.Equal(36, presentation.Items.Count);
        Assert.Equal("Pipeline: running; source Replay; rendered 20 buffer(s); telemetry age 2 ms; stale mute False; last error none.", presentation.Items[0]);
        Assert.Equal("UDP forwarding destinations: 127.0.0.1:20778 enabled", presentation.Items[3]);
        Assert.Equal("Road signal: sharedRoadSignalEnabled True; raw 0.200; smoothed 0.150; output 0.120.", presentation.Items[12]);
        Assert.Equal("Road flight recorder: active True; path local-validation-results\\road-flight.jsonl; source Replay; replay test-session.hdrec; telemetry age 2 ms; recommended event replay.", presentation.Items[15]);
        Assert.Equal("Profiles: audio default.hdprofile.json auto-saves current rig tuning/defaults; P-HPR p-hpr.hdphprprofile.json is a manual effect-preferences snapshot only.", presentation.Items[22]);
        Assert.Equal("Workflow: mode Real Direct Control; input Replay; replay source test-session.hdrec; replay packets 40; direct control enabled True armed True; selected output True; mock gear False; mock pedal False; road True; slip/lock True.", presentation.Items[23]);
        Assert.Equal("App settings: C:\\repo\\appsettings.json; loaded; theme dark; output mode Asio; replay Real time; persisted ASIO driver M-Audio; persisted ASIO channel 1; persisted Arm ASIO preference True; persisted paddle mapping device device-1 left 4 right 5 debounce 5 ms; shift intent enabled mode InstantPaddleOnly; BST-1 local gear enabled 55% 52 Hz 45 ms; mock gear routing enabled target Brake; mock pedal effects enabled; real road vibration enabled; real slip/lock enabled; haptics running state, emergency mute, active pulses, pending stops, P-HPR real direct-control enabled/selected private device, P-HPR emergency stop state, safety latch state, paddle bench enable state, manual ASIO test active state, flight-recorder history, and mock histories are not persisted.", presentation.Items[^1]);
        Assert.StartsWith(
            "Haptic Drive ASIO diagnostics" + Environment.NewLine +
            $"Generated: {generatedAt:g}" + Environment.NewLine +
            "UDP 42 packet(s), parser 40 valid / 1 failed, effects 5, output peak 0.420, callbacks 128." + Environment.NewLine,
            presentation.ClipboardReportText,
            StringComparison.Ordinal);
        Assert.Contains("Road flight recorder: active True; path local-validation-results\\road-flight.jsonl;", presentation.ClipboardReportText, StringComparison.Ordinal);
        Assert.Contains("P-HPR real direct control: enabled; selected True;", presentation.ClipboardReportText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NullSnapshot_ReturnsSafeFallbackPresentation()
    {
        var presentation = DiagnosticsStatusPresenter.Build(null);

        Assert.Equal("Road recorder: disabled; path disabled; last fallback none.", presentation.RoadRecorderStatusText);
        Assert.Equal(
            "UDP 0 packet(s), parser 0 valid / 0 failed, effects 0, output peak 0.000, callbacks 0.",
            presentation.SummaryText);
        Assert.Equal(32, presentation.Items.Count);
        Assert.Contains(presentation.Items, item => string.Equals(item, "Pipeline: unknown", StringComparison.Ordinal));
        Assert.Contains(presentation.Items, item => string.Equals(item, "P-HPR workflow: unavailable.", StringComparison.Ordinal));
        Assert.StartsWith("Haptic Drive ASIO diagnostics", presentation.ClipboardReportText, StringComparison.Ordinal);
    }
}
