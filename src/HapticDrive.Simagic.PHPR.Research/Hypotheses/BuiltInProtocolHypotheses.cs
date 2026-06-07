namespace HapticDrive.Simagic.PHPR.Research.Hypotheses;

public static class BuiltInProtocolHypotheses
{
    public static SimagicProtocolHypothesisSet Create()
    {
        return new SimagicProtocolHypothesisSet
        {
            EvidenceSourcesReviewed =
            [
                "AGENTS.md",
                "docs/SIMAGIC_CAPTURE_ANALYSIS.md",
                "docs/SIMAGIC_CAPTURE_GUIDE.md",
                "docs/SIMAGIC_P_HPR_PHASE_2_RESEARCH.md",
                "docs/SIMAGIC_P_HPR_SAFETY_PLAN.md",
                "docs/SIMAGIC_SHIFT_INTENT_DESIGN.md",
                "docs/SIMAGIC_USB_DEVICE_INVENTORY.md",
                "docs/SIMAGIC_USER_DATA_REQUEST.md",
                "docs/SIMAGIC_WHEEL_INPUT_RESEARCH.md",
                "sanitized P-HPR evidence bundle: docs/research/simagic/SIMAGIC_P_HPR_PROTOCOL_VERIFICATION.md",
                "sanitized P-HPR evidence bundle: evidence/simhub/simhub_f1ec_active_stop_records.csv",
                "sanitized P-HPR evidence bundle: evidence/simhub/simhub_f1ec_duration_summary.csv",
                "sanitized P-HPR evidence bundle: evidence/simhub/simhub_f1ec_validation_report.txt",
                "sanitized P-HPR evidence bundle: evidence/simpro/phpr_setreport_summary.txt",
                "sanitized GT Neo paddle evidence bundle",
                "sanitized P700 throttle/brake evidence bundles"
            ],
            Hypotheses =
            [
                CreateSimHubActiveStart(),
                CreateSimHubStopIdle(),
                CreateSimHubDurationTiming(),
                CreateSimProFamily(),
                CreateInputMappingSeparation(),
                CreateRuntimeIdentity()
            ],
            Unknowns =
            [
                new()
                {
                    Id = "unknown-report-id-interface",
                    Description = "The exact report ID, endpoint, and Windows HID interface for future writes are not validated in committed sanitized docs.",
                    EvidenceNeeded =
                    [
                        "USBView or USB Device Tree Viewer descriptor export.",
                        "HID report descriptor if available.",
                        "Endpoint, interface, report ID, and report length confirmation."
                    ]
                },
                new()
                {
                    Id = "unknown-real-stop-behaviour",
                    Description = "Observed stop/idle payloads have not been validated as a safe real emergency stop command from Haptic Drive.",
                    EvidenceNeeded =
                    [
                        "Explicit approval phrase.",
                        "Manual controlled write plan.",
                        "Low-strength single-pedal stop validation after approval."
                    ]
                },
                new()
                {
                    Id = "unknown-simpro-simhub-coexistence",
                    Description = "Direct-control coexistence with SimPro Manager and SimHub is not validated.",
                    EvidenceNeeded =
                    [
                        "Confirmation whether SimPro must be closed to access the P700.",
                        "Confirmation whether SimHub and SimPro can both see/control P-HPR at the same time.",
                        "Process-status diagnostics in a later stage."
                    ]
                }
            ],
            Stage2KAllowedMockSurface =
            [
                "Create mock protocol objects from Stage 2J hypotheses.",
                "Create mock start/stop packet representations.",
                "Create mock SimHub F1 EC command structures.",
                "Create mock-only PHprCommand mapping.",
                "Feed MockPhprOutputDevice in mock tests only.",
                "Model duration as mock start plus scheduled mock stop.",
                "Use TargetModule values Brake, Throttle, and Both.",
                "Use State values Start, Stop, and EmergencyStop.",
                "Use FrequencyHz and StrengthPercent or Strength01.",
                "Include DurationMs as app-side timing.",
                "Include SourceProtocolFamily values SimHubF1EcMock and SimProUnknownMock.",
                "Include EvidenceConfidence and MockOnly flags.",
                "Do not write to hardware, open Simagic device handles for write, send output reports, send feature reports, or vibrate real P-HPR modules."
            ],
            RealWriteBlockers =
            [
                "The exact approval phrase has not been provided.",
                "No controlled write test plan has been executed.",
                "No real hardware write safety validation exists.",
                "Stop command behavior has not been validated on real hardware.",
                "SimPro/SimHub coexistence has not been validated for direct control.",
                "Device ownership and exclusive access behavior are not validated.",
                "Report ID, endpoint, and interface selection must be confirmed.",
                "Any checksum, sequence, or keepalive behavior must be confirmed if present.",
                "Behavior when SimPro Manager is running must be understood.",
                "Emergency stop path must exist before real writes.",
                "PHprSafetyLimiter must exist before real writes.",
                "First real test must be manual, low strength, short duration, one pedal, and no loop."
            ],
            OptionalUserData =
            [
                "Additional SimPro Manager captures or summaries for brake/throttle test vibration.",
                "SimPro strength, frequency, and duration change summaries.",
                "Exact P700 interface and report IDs from USBView.",
                "Confirmation whether SimPro must be closed to access the P700.",
                "Confirmation whether SimHub and SimPro can both see/control P-HPR at the same time.",
                "Confirmation whether SimHub F1 EC commands were captured against the same P700/P-HPR hardware path.",
                "Evidence of any report ID separate from payload bytes.",
                "Endpoint and interface details still missing from committed sanitized docs."
            ]
        };
    }

    private static SimagicProtocolHypothesis CreateSimHubActiveStart()
    {
        return new SimagicProtocolHypothesis
        {
            Id = "simhub-f1ec-active-start",
            Title = "SimHub F1 EC active/start packet hypothesis",
            ProtocolFamily = SimagicProtocolFamily.SimHubF1EcSetReport,
            SoftwareSource = SimagicProtocolSource.SimHub,
            TransportObservation = "USBHID SET_REPORT / usb.data_fragment host-to-device observation.",
            PayloadPrefixHex = "F1 EC",
            ReportLengthBytes = 64,
            Summary = "SimHub P-HPR active/start reports appear to use F1 EC [module] 01 [frequency_hz] [strength_percent] 00 ...",
            Confidence = SimagicProtocolHypothesisConfidence.High,
            Status = SimagicProtocolHypothesisStatus.ReadyForMockProtocol,
            IsOutputCommand = true,
            Fields =
            [
                Field("prefix", 0, 2, "constant bytes", ["F1 EC"], "SimHub F1 EC family prefix.", SimagicProtocolHypothesisConfidence.ConfirmedObservation),
                Field(
                    "module selector",
                    2,
                    1,
                    "u8",
                    ["00 = all/neutral/init/baseline candidate", "01 = brake", "02 = throttle"],
                    "01 and 02 track brake/throttle captures. 00 appears in idle/baseline traffic, but its exact semantic meaning is unresolved.",
                    SimagicProtocolHypothesisConfidence.High,
                    ["00 remains lower confidence than the brake/throttle values."]),
                Field("state", 3, 1, "u8", ["01 = active/on"], "Active/start marker.", SimagicProtocolHypothesisConfidence.ConfirmedObservation),
                Field("frequency", 4, 1, "u8 direct Hz", ["10 Hz = 0A", "20 Hz = 14", "30 Hz = 1E", "40 Hz = 28", "50 Hz = 32"], "Frequency appears to be encoded directly as Hz.", SimagicProtocolHypothesisConfidence.High),
                Field("strength", 5, 1, "u8 direct percent", ["10% = 0A", "20% = 14", "40% = 28", "60% = 3C", "80% = 50", "100% = 64"], "Strength appears to be encoded directly as percent.", SimagicProtocolHypothesisConfidence.High, ["One 10% throttle observation was 09 and remains noted as a minor anomaly."]),
                Field("trailing bytes", 6, null, "zero/unknown", ["00 ..."], "Observed as zero-filled in tested active/start payloads; exact padding/report semantics remain unresolved.", SimagicProtocolHypothesisConfidence.Low)
            ],
            EvidenceReferences =
            [
                "evidence/simhub/simhub_f1ec_active_stop_records.csv",
                "evidence/simhub/simhub_f1ec_validation_report.txt",
                "docs/research/simagic/PHPR_OUTPUT_CAPTURE_EVIDENCE.md"
            ],
            Risks = SharedOutputRisks(),
            MissingData =
            [
                "Report ID separate from payload bytes, if any.",
                "Exact Windows HID interface and endpoint used for future write-gated control.",
                "Real stop behavior validation after explicit approval."
            ],
            ValidationNeeded =
            [
                "Stage 2K mock-only packet modelling.",
                "Later manual controlled write validation only after exact approval phrase."
            ],
            StageAllowedForNextAction = "Stage 2K mock protocol only.",
            NoWriteSafetyNote = "This hypothesis is approved for Stage 2K mock protocol only. It is not approved for real USB writes."
        };
    }

    private static SimagicProtocolHypothesis CreateSimHubStopIdle()
    {
        return new SimagicProtocolHypothesis
        {
            Id = "simhub-f1ec-stop-idle",
            Title = "SimHub F1 EC stop/idle packet hypothesis",
            ProtocolFamily = SimagicProtocolFamily.SimHubF1EcSetReport,
            SoftwareSource = SimagicProtocolSource.SimHub,
            TransportObservation = "USBHID SET_REPORT / usb.data_fragment host-to-device observation.",
            PayloadPrefixHex = "F1 EC",
            ReportLengthBytes = 64,
            Summary = "SimHub P-HPR stop/idle reports appear to use F1 EC [module] 00 0A 00 00 00 ...",
            Confidence = SimagicProtocolHypothesisConfidence.High,
            Status = SimagicProtocolHypothesisStatus.ReadyForMockProtocol,
            IsOutputCommand = true,
            Fields =
            [
                Field("prefix", 0, 2, "constant bytes", ["F1 EC"], "SimHub F1 EC family prefix.", SimagicProtocolHypothesisConfidence.ConfirmedObservation),
                Field("module selector", 2, 1, "u8", ["00 = all/neutral/init/baseline candidate", "01 = brake", "02 = throttle"], "Target selector mirrors observed SimHub module values.", SimagicProtocolHypothesisConfidence.High),
                Field("state", 3, 1, "u8", ["00 = stop/off/idle"], "Stop/off/idle marker.", SimagicProtocolHypothesisConfidence.ConfirmedObservation),
                Field("byte 4 stop value", 4, 1, "u8 meaning unresolved", ["0A"], "May be default/min frequency, neutral value, or retained baseline. Do not assume final meaning.", SimagicProtocolHypothesisConfidence.Low),
                Field("byte 5 stop value", 5, 1, "u8", ["00"], "Observed zero value in stop/idle payloads.", SimagicProtocolHypothesisConfidence.Medium),
                Field("remaining bytes", 6, null, "zero/unknown", ["00 ..."], "Observed as zero-filled in tested stop/idle payloads; exact padding/report semantics remain unresolved.", SimagicProtocolHypothesisConfidence.Low)
            ],
            EvidenceReferences =
            [
                "evidence/simhub/simhub_f1ec_active_stop_records.csv",
                "docs/research/simagic/PHPR_OUTPUT_CAPTURE_EVIDENCE.md"
            ],
            Risks = SharedOutputRisks(),
            MissingData =
            [
                "Real hardware stop behavior.",
                "Whether byte 4 has required stop semantics beyond observed 0A.",
                "Emergency-stop behavior under direct Haptic Drive ownership."
            ],
            ValidationNeeded =
            [
                "Stage 2K mock-only stop representation.",
                "Later manual low-strength stop validation only after exact approval phrase."
            ],
            StageAllowedForNextAction = "Stage 2K mock protocol only.",
            NoWriteSafetyNote = "Stop/idle payload is an observation. Real stop command behavior must not be trusted until controlled write validation after explicit approval."
        };
    }

    private static SimagicProtocolHypothesis CreateSimHubDurationTiming()
    {
        return new SimagicProtocolHypothesis
        {
            Id = "simhub-duration-timing",
            Title = "SimHub duration timing hypothesis",
            ProtocolFamily = SimagicProtocolFamily.SimHubF1EcSetReport,
            SoftwareSource = SimagicProtocolSource.SimHub,
            TransportObservation = "USBHID SET_REPORT active/start followed by later stop/idle observation.",
            PayloadPrefixHex = "F1 EC",
            ReportLengthBytes = 64,
            Summary = "For tested SimHub captures, duration appears software-timed by active/start plus delayed stop/idle rather than encoded in the active payload.",
            Confidence = SimagicProtocolHypothesisConfidence.High,
            Status = SimagicProtocolHypothesisStatus.ReadyForMockProtocol,
            IsOutputCommand = true,
            Fields =
            [
                Field(
                    "duration field in active payload",
                    null,
                    null,
                    "none observed",
                    ["100 ms and 500 ms tests can share identical active payloads", "Stop packet occurs approximately 0.1 s or 0.5 s later"],
                    "Duration should be represented in Stage 2K mocks as app-side start plus scheduled stop.",
                    SimagicProtocolHypothesisConfidence.High)
            ],
            EvidenceReferences =
            [
                "evidence/simhub/simhub_f1ec_duration_summary.csv",
                "docs/research/simagic/PHPR_OUTPUT_CAPTURE_EVIDENCE.md"
            ],
            Risks = SharedOutputRisks(),
            MissingData =
            [
                "Other SimHub effects not captured.",
                "Real hardware response to direct start/stop timing."
            ],
            ValidationNeeded =
            [
                "Mock scheduler tests in Stage 2K.",
                "Real timing validation only after explicit approval phrase."
            ],
            StageAllowedForNextAction = "Stage 2K mock protocol only.",
            NoWriteSafetyNote = "A future mock protocol can model duration as start plus delayed stop. Real hardware write implementation remains blocked."
        };
    }

    private static SimagicProtocolHypothesis CreateSimProFamily()
    {
        return new SimagicProtocolHypothesis
        {
            Id = "simpro-801e89-family",
            Title = "SimPro Manager 80 1E 89 family hypothesis",
            ProtocolFamily = SimagicProtocolFamily.SimPro801E89SetReport,
            SoftwareSource = SimagicProtocolSource.SimProManager,
            TransportObservation = "USBHID SET_REPORT / usb.data_fragment host-to-device observation.",
            PayloadPrefixHex = "80 1E 89",
            ReportLengthBytes = 64,
            Summary = "SimPro Manager appears to use a distinct SET_REPORT family beginning with 80 1E 89. Field meanings remain below Stage 2K mock-ready confidence.",
            Confidence = SimagicProtocolHypothesisConfidence.Medium,
            Status = SimagicProtocolHypothesisStatus.NeedsMoreCaptures,
            IsOutputCommand = true,
            Fields =
            [
                Field("prefix/family", 0, 3, "constant bytes", ["80 1E 89"], "Distinct SimPro Manager payload family.", SimagicProtocolHypothesisConfidence.ConfirmedObservation),
                Field("module selector", null, null, "unresolved", ["candidate bytes exist in sanitized compare summaries"], "Do not promote to a write or mock-compatible selector until the SimPro family is deliberately modelled.", SimagicProtocolHypothesisConfidence.Low),
                Field("strength field", null, null, "unresolved", ["candidate strength changes exist in sanitized summaries"], "Byte meaning remains a hypothesis, not a production control field.", SimagicProtocolHypothesisConfidence.Low),
                Field("frequency field", null, null, "unresolved", ["candidate frequency changes exist in sanitized summaries"], "Byte meaning remains a hypothesis, not a production control field.", SimagicProtocolHypothesisConfidence.Low),
                Field("duration field", null, null, "unknown", ["none confirmed"], "Duration may be software-timed, but SimPro duration semantics are unresolved.", SimagicProtocolHypothesisConfidence.Unknown),
                Field("checksum/counter/keepalive", null, null, "unknown", ["not proven"], "Do not assume no checksum, sequence, or keepalive behavior.", SimagicProtocolHypothesisConfidence.Unknown)
            ],
            EvidenceReferences =
            [
                "evidence/simpro/phpr_setreport_summary.txt",
                "evidence/simpro/compare_002_brake_50hz_50pct_250ms_tes_usb_packets__VS__003_throttle_50hz_50pct_250ms_tes_usb_packets.txt",
                "evidence/simpro/compare_016_brake_10hz_50pct_250ms_tes_usb_packets__VS__017_brake_20hz_50pct_250ms_tes_usb_packets.txt",
                "docs/research/simagic/PHPR_OUTPUT_CAPTURE_EVIDENCE.md"
            ],
            Risks = SharedOutputRisks(),
            MissingData =
            [
                "Descriptor-backed report ID and interface selection.",
                "A deliberate Stage 2K decision before any SimPro mock surface beyond SimProUnknownMock.",
                "Confirmation whether repeated background traffic is keepalive, state sync, or configuration.",
                "Stop/release semantics and emergency stop semantics."
            ],
            ValidationNeeded =
            [
                "More sanitized SimPro summaries if later stages need SimPro-compatible mock modelling.",
                "No real write validation before approval phrase and safety limiter."
            ],
            StageAllowedForNextAction = "Stage 2K may represent this as SimProUnknownMock only.",
            NoWriteSafetyNote = "Do not implement SimPro-compatible write behavior from this hypothesis in Stage 2J or 2K unless a later mock-only stage deliberately scopes it."
        };
    }

    private static SimagicProtocolHypothesis CreateInputMappingSeparation()
    {
        return new SimagicProtocolHypothesis
        {
            Id = "input-output-separation",
            Title = "P700 and GT Neo input mapping separation",
            ProtocolFamily = SimagicProtocolFamily.SafetyBoundary,
            SoftwareSource = SimagicProtocolSource.ArchitectureRule,
            TransportObservation = "HID input reports are device-to-host input observations, not P-HPR output commands.",
            Summary = "Confirmed P700 throttle/brake input mappings and GT Neo paddle mappings must remain separate from P-HPR output protocol hypotheses.",
            Confidence = SimagicProtocolHypothesisConfidence.ConfirmedObservation,
            Status = SimagicProtocolHypothesisStatus.EvidenceOnly,
            IsInputMapping = true,
            IsOutputCommand = false,
            MockOnly = false,
            Fields =
            [
                Field("P700 primary throttle input", 5, 2, "u16 little-endian", ["raw range 0..4095", "percent = raw / 4095 * 100"], "Confirmed read-only throttle input mapping.", SimagicProtocolHypothesisConfidence.ConfirmedObservation, ["Mirror throttle u16_le@15 is diagnostic only."]),
                Field("P700 primary brake input", 3, 2, "u16 little-endian", ["raw range expected 0..4095", "percent = raw / 4095 * 100"], "Confirmed read-only brake input mapping.", SimagicProtocolHypothesisConfidence.ConfirmedObservation, ["Mirror brake u16_le@13 is diagnostic only."]),
                Field("GT Neo left paddle input", 14, 1, "bit mask", ["report[14] & 0x02"], "Confirmed read-only left paddle input mapping.", SimagicProtocolHypothesisConfidence.ConfirmedObservation),
                Field("GT Neo right paddle input", 14, 1, "bit mask", ["report[14] & 0x01"], "Confirmed read-only right paddle input mapping.", SimagicProtocolHypothesisConfidence.ConfirmedObservation)
            ],
            EvidenceReferences =
            [
                "docs/research/simagic/P700_PEDAL_INPUT_EVIDENCE.md",
                "docs/research/simagic/GT_NEO_SHIFT_PADDLE_EVIDENCE.md"
            ],
            Risks =
            [
                SimagicProtocolRisk.InputOutputBoundary,
                SimagicProtocolRisk.ProductionEncoderForbidden
            ],
            MissingData =
            [
                "Runtime VID/PID and device-selection validation for the exact P700 and Alpha Evo / GT Neo paths.",
                "Any firmware variation that changes input report layout."
            ],
            ValidationNeeded =
            [
                "Keep input report parsing separate from output report modelling.",
                "Do not infer motor control from input reports."
            ],
            StageAllowedForNextAction = "Read-only diagnostics and future parser tests only.",
            NoWriteSafetyNote = "Input mappings do not imply write capability and must never be used to infer motor control by themselves."
        };
    }

    private static SimagicProtocolHypothesis CreateRuntimeIdentity()
    {
        return new SimagicProtocolHypothesis
        {
            Id = "runtime-identity",
            Title = "Runtime identity hypothesis",
            ProtocolFamily = SimagicProtocolFamily.RuntimeIdentity,
            SoftwareSource = SimagicProtocolSource.Stage2IAnalysis,
            TransportObservation = "USBPcap capture addresses are session-only observations.",
            Summary = "Runtime device selection must use stable Windows identity and configured selection, not temporary USB bus/device addresses from captures.",
            Confidence = SimagicProtocolHypothesisConfidence.High,
            Status = SimagicProtocolHypothesisStatus.EvidenceOnly,
            IsOutputCommand = false,
            MockOnly = false,
            Fields =
            [
                Field("capture USB address", null, null, "session-only", ["address 8 observed for wheelbase capture", "address 32 observed for one P700 input capture"], "Useful for capture filtering only. Must not be used at runtime.", SimagicProtocolHypothesisConfidence.ConfirmedObservation),
                Field("runtime identity", null, null, "Windows device identity", ["VID/PID", "interface/product strings", "usage page/usage", "configured device selection"], "Stable identity must be confirmed before later control paths.", SimagicProtocolHypothesisConfidence.High),
                Field("private hardware data", null, null, "redacted", ["serials and raw paths must not be committed"], "Committed docs and exports must stay sanitized.", SimagicProtocolHypothesisConfidence.High)
            ],
            EvidenceReferences =
            [
                "docs/SIMAGIC_USB_DEVICE_INVENTORY.md",
                "docs/research/simagic/GT_NEO_SHIFT_PADDLE_EVIDENCE.md",
                "docs/research/simagic/P700_PEDAL_INPUT_EVIDENCE.md"
            ],
            Risks =
            [
                SimagicProtocolRisk.DeviceIdentityUnvalidated,
                SimagicProtocolRisk.PrivateDataLeak,
                SimagicProtocolRisk.ReportIdOrInterfaceUnvalidated
            ],
            MissingData =
            [
                "USBView P700 descriptor export with serials redacted.",
                "Exact P700 report ID and interface details.",
                "Stable Alpha Evo / GT Neo-visible input identity."
            ],
            ValidationNeeded =
            [
                "Use Stage 2G inventory and user-selected device identity.",
                "Keep private paths and serial-like data out of committed artifacts."
            ],
            StageAllowedForNextAction = "Documentation and read-only inventory follow-up only.",
            NoWriteSafetyNote = "Runtime identity notes do not authorise device writes or direct control."
        };
    }

    private static SimagicProtocolHypothesisField Field(
        string name,
        int? byteOffset,
        int? byteLength,
        string encoding,
        IReadOnlyList<string> observedValues,
        string interpretation,
        SimagicProtocolHypothesisConfidence confidence,
        IReadOnlyList<string>? notes = null)
    {
        return new SimagicProtocolHypothesisField
        {
            CandidateFieldName = name,
            ByteOffset = byteOffset,
            ByteLength = byteLength,
            Encoding = encoding,
            ObservedValues = observedValues,
            Interpretation = interpretation,
            Confidence = confidence,
            Notes = notes ?? []
        };
    }

    private static IReadOnlyList<SimagicProtocolRisk> SharedOutputRisks()
    {
        return
        [
            SimagicProtocolRisk.NoWriteApproval,
            SimagicProtocolRisk.RealStopUnvalidated,
            SimagicProtocolRisk.DeviceIdentityUnvalidated,
            SimagicProtocolRisk.ReportIdOrInterfaceUnvalidated,
            SimagicProtocolRisk.SimProSimHubCoexistenceUnvalidated,
            SimagicProtocolRisk.ProductionEncoderForbidden
        ];
    }
}
