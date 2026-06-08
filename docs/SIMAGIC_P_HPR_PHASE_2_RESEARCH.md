# Simagic P-HPR Phase 2 Research

Stage 2A starts the Simagic P-HPR and GT Neo paddle-input phase as research, documentation, and safety intake only. Stage 2B adds safe abstraction projects and a mock-only output skeleton. Stage 2C adds cached driving-state evaluation. Stage 2D adds read-only wheel / paddle input discovery and candidate scoring. Stage 2E adds read-only Windows game-controller paddle listening and manual mapping diagnostics. Stage 2F adds the Shift Intent Event Layer for cached `DrivingArmed` evaluation and accepted/suppressed diagnostics. Stage 2G adds read-only P700 / P-HPR device inventory tooling and sanitized exports. Stage 2H adds capture workflow documentation and metadata tooling. Stage 2I adds read-only capture analysis tooling and sanitized summary export. Stage 2J adds formal protocol hypotheses and sanitized hypothesis export. Stage 2K adds mock-only protocol/output modelling. Stage 2L adds mock-only P-HPR safety limiting. Stage 2M adds mock-only gear pulse routing from accepted shift intents. Stage 2N adds mock-only road vibration, wheel slip, and wheel lock routing from `VehicleState`. Stage 2O adds read-only SimPro Manager / SimHub coexistence detection and safety-context warning integration. Stage 2P adds the controlled write test plan, manual validation runbook, no-write readiness model, and disabled direct-write diagnostics. These stages do not add USB writes, real P-HPR output, protocol control, or real P-HPR vibration.

## Current Repository Baseline

- Phase 1 is complete through Stage 18.
- The WPF app, F1 25 UDP listener/parser, raw forwarding, recording/replay, `VehicleState`, ASIO readiness, native ASIO streaming backend, haptic effects, mixer, safety chain, emergency mute, stale telemetry mute, profiles, diagnostics, and launch wrapper already exist.
- `NullAudioOutputDevice` remains the automated-test and startup default.
- Stage 18 remains the final pre-Dayton-shaker software package.
- Searches of `src/` and `tests/` during Stage 2A found no Simagic, P-HPR, P700, GT Neo, `ShiftIntent`, or `DrivingArmed` implementation code.
- Stage 2B now defines input and P-HPR contracts without adding real hardware access.
- Stage 2C now defines `DrivingArmedStateService` in `HapticDrive.Actuation` without connecting it to paddle input or P-HPR output.
- Stage 2D now defines richer input discovery snapshots and implements read-only Windows Raw Input plus Windows game-controller capability discovery.
- Stage 2E now defines read-only paddle listener diagnostics, manual left/right mapping, rising-edge/debounce processing, and safe mapping persistence.
- Stage 2F now defines `ShiftIntentProcessor`, `ShiftIntentMode`, `ShiftIntentDirection`, `ShiftIntentSource`, accepted/suppressed shift-intent diagnostics, safe in-memory accepted-event storage, and WPF diagnostics/settings for shift intent enabled state and mode.
- Stage 2G now defines `HapticDrive.Simagic.PHPR.Research` for read-only P700 / P-HPR inventory, sanitized local JSON/Markdown exports, redaction, candidate classification, and hardware-free tests.
- Stage 2H now extends `HapticDrive.Simagic.PHPR.Research` with capture scenario definitions, metadata templates, filename building, validation, sanitization, sanitized manifest export, CLI commands, and hardware-free tests.
- Stage 2I now extends `HapticDrive.Simagic.PHPR.Research` with read-only capture analysis for Wireshark CSV/text exports, payload fingerprints, byte-diff observations, pcap/pcapng container summaries, sanitized JSON export, CLI commands, and hardware-free tests.
- Stage 2J now extends `HapticDrive.Simagic.PHPR.Research` with analysis-only protocol hypothesis records, sanitized JSON/Markdown export commands, Stage 2K mock-only boundary definition, real-write blockers, and hardware-free tests.
- Stage 2K now extends `HapticDrive.Simagic.PHPR.Abstractions` with mock-only protocol records, SimHub F1 EC mock encoding/decoding, deterministic duration scheduling, SimProUnknownMock classification, enhanced `MockPhprOutputDevice` diagnostics, and safe research CLI examples.
- Stage 2L now extends `HapticDrive.Simagic.PHPR.Abstractions` with `PHprSafetyLimiter`, safety decision/context/snapshot models, deterministic command-rate and continuous-duration limiting, emergency-stop latching/clear behavior, real-write blocking diagnostics, and a safety-limited mock output wrapper.
- Stage 2M now extends `HapticDrive.Actuation` with `PHprGearPulseRouter`, conservative mock gear pulse defaults, safety-limited mock routing from accepted `ShiftIntentEvent` values, WPF mock routing diagnostics, and hardware-free tests.
- Stage 2N now extends `HapticDrive.Actuation` with `PHprPedalEffectsRouter`, mock road/slip/lock defaults, priority/interval routing, shared WPF mock output diagnostics, safe settings persistence, and hardware-free tests.
- Stage 2O now extends `HapticDrive.Simagic.PHPR.Abstractions` with read-only SimPro Manager / SimHub process detection, coexistence snapshots, WPF diagnostics, safety-context status wiring, and hardware-free tests.
- Stage 2P now extends `HapticDrive.Simagic.PHPR.Abstractions` with a no-write direct-control readiness model, WPF disabled readiness diagnostics, controlled write test plan, manual validation runbook, and hardware-free tests.

## User Hardware Context

- Simagic P700 2-pedal set connected directly to the PC by USB.
- Brake and throttle pedals.
- Two Simagic P-HPR modules, one on brake and one on throttle.
- P-HPR modules connected through the built-in P700 haptic controller.
- Simagic Alpha Evo 12 Nm wheelbase connected separately to the PC by USB.
- Simagic GT Neo wheel connected to or through the Alpha Evo wheelbase.
- SimPro Manager V3 is currently used.
- P-HPR modules are visible/configurable in SimPro Manager V3.
- P-HPR modules are visible/configurable in SimHub, but SimHub P-HPR control feels noticeably laggier.
- Haptic Drive ASIO must not require SimHub at runtime.

## Phase 2 Objective

Phase 2 adds a separate non-audio actuator architecture for Simagic P-HPR pedal modules while preserving the existing ASIO/BST-1 audio path.

The P-HPR modules are not audio devices. They must not be routed through ASIO and must not be forced into `IAudioOutputDevice`.

Planned future boundaries:

```text
F1 25 telemetry
-> VehicleState
-> audio haptic effects
-> mixer/safety
-> ASIO/BST-1 audio output

GT Neo paddle input and VehicleState
-> shift/effect routing
-> actuator safety
-> P-HPR pedal output
```

The two paths may share event timestamps and diagnostics later, but neither path should block the other.

## Highest-Priority Feature

The most important new behavior is an instant pedal gear pulse from GT Neo paddle input:

```text
GT Neo paddle press
-> read-only Raw Input / DirectInput / HID event
-> ShiftIntentEvent
-> cached DrivingArmed gate
-> immediate P-HPR gear pulse
```

Default future mode:

- `InstantPaddleOnly`
- Fire on left or right paddle press immediately when `DrivingArmed` is true.
- Do not wait for F1 25 telemetry at event time.
- Do not fire a second normal telemetry-confirmed pulse by default.

Telemetry is still required for cached menu-safe driving state, diagnostics, optional telemetry-confirmed mode, optional rejected-shift detection, road vibration, slip, and lock effects.

## Stage 2A Scope

Implemented in Stage 2A:

- Confirmed Stage 18 completion from project docs and development log.
- Confirmed no current Simagic/P-HPR implementation exists in `src/` or `tests/`.
- Created the Phase 2 research and safety documentation set.
- Updated durable project guidance for no real P-HPR writes before explicit approval.
- Updated ignore rules for raw/private capture artifacts.

Not implemented in Stage 2A:

- No input listener.
- No DirectInput, Raw Input, or HID code.
- No P-HPR output abstractions.
- No mock P-HPR output.
- No protocol encoder/decoder.
- No real USB write path.
- No Simagic device output reports.
- No feature reports that can write or change device state.

## Stage 2B Scope

Implemented in Stage 2B:

- `HapticDrive.Input.Abstractions`
- `HapticDrive.Input.Windows` placeholder project for later read-only Windows input work.
- `HapticDrive.Simagic.PHPR.Abstractions`
- `IShiftIntentSource`
- `IWheelPaddleInputSource`
- `IInputDeviceDiscovery`
- `ShiftIntentEvent`
- `PaddleSide`
- `DrivingArmedState`
- `IDrivingArmedStateProvider`
- `IPHprOutputDevice`
- `PHprCommand`
- `PHprModuleId`
- `PHprCommandSource`
- `PHprSafetyLimits`
- `PHprOutputSnapshot`
- `MockPhprOutputDevice` skeleton.

Not implemented in Stage 2B:

- No Windows Raw Input implementation.
- No DirectInput implementation.
- No HID input-report reader.
- No P700/P-HPR device discovery implementation.
- No shift-intent router.
- No telemetry-backed `DrivingArmed` service.
- No protocol encoder/decoder.
- No real USB writes.
- No real P-HPR output.

## Stage 2C Scope

Implemented in Stage 2C:

- `HapticDrive.Actuation`
- `DrivingArmedStateService`
- `DrivingArmedStateServiceOptions`
- `DrivingArmedEvaluationContext`
- `DrivingArmedSuppressionReason`
- `DrivingArmedStateServiceSnapshot`
- Update from existing `VehicleState`
- Update from existing `HapticPipelineSnapshot`
- Default false until recent valid telemetry is observed
- Menu-safe mode enabled by default
- Require recent telemetry enabled by default
- Telemetry freshness threshold
- Allow zero-speed active driving enabled by default
- Diagnostics-only unsafe override option
- Suppression reasons for no telemetry, stale telemetry, paused, network-paused, garage/menu/result state, invalid state, not moving/inactive, emergency mute, and haptics stopped

Not implemented in Stage 2C:

- No paddle input listener.
- No shift intent router.
- No P-HPR routing.
- No UI wiring.
- No real USB writes.
- No real P-HPR output.

## Stage 2D Scope

Implemented in Stage 2D:

- `InputDeviceInfo`
- `InputDeviceKind`
- `InputDiscoveryMethod`
- `InputControlInfo`
- `InputDeviceDiscoverySnapshot`
- `IWheelInputCandidateProvider`
- Raw Input metadata discovery
- Windows game-controller capability discovery
- Safe redaction for normal device path display
- Likely-device scoring for Simagic wheelbase, GT Neo / wheel input path, P700 pedals, and unknown HID/game-controller devices
- WPF Devices page Refresh Input Devices diagnostics
- Hardware-free tests for models, zero devices, exceptions, scoring, deterministic fake discovery, safe empty snapshots, and no write-like discovery interface methods

Not implemented in Stage 2D:

- No live paddle input listener.
- No rising-edge detection.
- No left/right paddle mapping.
- No `ShiftIntentEvent` routing from actual hardware input.
- No P-HPR routing.
- No DirectInput-specific dependency.
- No HID input-report reader.
- No USB output reports or write-capable feature reports.
- No real P-HPR output.

## Stage 2E Scope

Implemented in Stage 2E:

- `InputDeviceSelection`
- `WheelPaddleMapping`
- `InputButtonState`
- `InputEventTimestamp`
- `InputListenerStatus`
- `WheelPaddleRawButtonEvent`
- `WheelPaddleInputEvent`
- `WheelPaddleInputSnapshot`
- `IInputButtonStateReader`
- `PollingWheelPaddleInputSource`
- `WheelPaddleInputProcessor`
- `WindowsGameControllerButtonStateReader`
- Devices-page Start/Stop Listener controls
- last-changed raw button diagnostics
- Set Left/Right From Last Button mapping workflow
- left/right current state diagnostics
- mapped paddle press count and timestamp diagnostics
- safe app-settings persistence for selected input device, method, left/right button IDs, and debounce only
- hardware-free listener and processor tests

Not implemented in Stage 2E:

- No hardware-derived `ShiftIntentEvent` routing.
- No cached `DrivingArmed` gate connection.
- No P-HPR routing.
- No real USB writes.
- No real P-HPR output.
- No audio/ASIO gear-pulse routing.
- No Raw Input live HID report decoding.
- No HID input-report reader.

## Stage 2F Scope

Implemented in Stage 2F:

- `ShiftIntentMode` with `InstantPaddleOnly` as the default, plus `TelemetryConfirmedOnly` and `InstantWithRejectedShiftFeedback`.
- `ShiftIntentDirection` with left paddle mapped to `Downshift` and right paddle mapped to `Upshift`.
- `ShiftIntentSource` for wheel-paddle, telemetry-gear-change, and test diagnostics.
- Extended `ShiftIntentEvent` diagnostics for direction, source, mode, stopwatch ticks, source button, last known gear/speed/RPM/session/frame, and a correlation ID.
- `ShiftIntentProcessor` in `HapticDrive.Actuation.Shift`.
- Immediate accepted intent when a mapped paddle press arrives while cached `DrivingArmed` is true and mode allows instant paddle intent.
- Suppressed diagnostics when the layer is disabled, `DrivingArmed` is false, or `TelemetryConfirmedOnly` is active.
- Suppression messages preserve the cached `DrivingArmed` reason when the gate is false.
- Safe in-memory accepted-event sink for diagnostics and tests only.
- Devices-page shift-intent diagnostics and controls for enabled state, mode, and counter clearing.
- App-settings persistence for shift-intent enabled state and selected mode only.
- Hardware-free tests for default mode, direction mapping, accepted/suppressed behavior, diagnostics counters, error capture, telemetry-confirmed mode, future rejected-feedback mode, and no output-facing processor surface.

Not implemented in Stage 2F:

- No P700/P-HPR USB discovery or inventory.
- No capture workflow or capture analysis.
- No mock P-HPR gear-pulse routing.
- No real P-HPR output.
- No `IPHprOutputDevice` or `MockPhprOutputDevice` calls.
- No `PHprCommand` creation.
- No ASIO gear pulse from paddle input.
- No `GearShiftEffect` call from paddle input.
- No rejected-shift feedback output.
- No telemetry wait, disk IO, network IO, or audio rendering work in the paddle event path.

## Stage 2G Scope

Implemented in Stage 2G:

- `HapticDrive.Simagic.PHPR.Research` console/reusable research utility.
- `SimagicDeviceInventorySnapshot`, `SimagicDeviceInventoryItem`, inventory method/error/export models, candidate kinds, sanitizer, provider, exporter, and summary formatter.
- Reuse of existing Stage 2D read-only input discovery metadata.
- Read-only Windows HID registry metadata enumeration.
- Read-only Windows USB registry metadata enumeration.
- Candidate classification for P700 pedal controller, P-HPR module/controller, Alpha Evo wheelbase, GT Neo wheel input, Simagic unknown, generic HID, and generic USB input.
- Redaction of serial-like path segments and Windows usernames while preserving VID/PID and useful non-sensitive class/manufacturer/product data.
- Sanitized JSON and Markdown export support under ignored `local-device-inventory/`.
- Console safety banner and help/inventory commands.
- A read-only winmm entry-point fix for the existing Windows game-controller discovery/listener path.
- Hardware-free tests for model construction, empty snapshots, classification, redaction, sanitized export, provider failure capture, interface no-write surface, assembly reference boundaries, JSON round-trip, and summary formatting.

Local Stage 2G inventory result:

- Total local inventory items observed by the tool: 168.
- Generic HID/USB candidates: 166.
- Specific Simagic P700/P-HPR/Alpha/GT Neo candidates: 0.
- Discovery errors after the winmm entry-point fix: 0.
- Real P700/P-HPR inventory is still awaiting user-provided Device Manager / USBView / tool output.

Not implemented in Stage 2G:

- No USB capture workflow.
- No capture analysis.
- No protocol hypotheses.
- No mock P-HPR gear-pulse routing.
- No real P-HPR output.
- No `IPHprOutputDevice` or `MockPhprOutputDevice` calls.
- No `PHprCommand` creation.
- No output reports, feature writes, HID writes, driver changes, SimPro/SimHub control, or controlled write testing.

## Stage 2H Scope

Implemented in Stage 2H:

- Safe capture workflow documentation in `docs/SIMAGIC_CAPTURE_GUIDE.md`.
- Required scenario list for SimPro and SimHub P700 / P-HPR captures.
- `SimagicCaptureScenario` and `SimagicCaptureScenarioId`.
- `SimagicCaptureMetadata`, software/device/action contexts, and setting snapshots.
- `SimagicCaptureFilenameBuilder` using the Stage 2H naming convention.
- `SimagicCaptureMetadataValidator` with required-field checks, scenario-specific setting warnings, private-path warnings, and redaction warnings.
- `SimagicCaptureSanitizer` for serial-like strings, Windows user paths, raw capture paths, and pasted raw-transfer byte snippets.
- `SimagicCaptureManifest` and `SimagicCaptureManifestExporter` for sanitized metadata-only manifests.
- CLI commands for `capture-scenarios`, `capture-template`, `validate-capture-metadata`, and `capture-manifest`.
- Ignored `capture-metadata/` output path.
- Hardware-free tests for scenarios, templates, filenames, validation, sanitization, manifest export, and CLI help.

Not implemented in Stage 2H:

- No `.pcap` or `.pcapng` parsing.
- No USB transfer analysis.
- No protocol byte inference.
- No report ID, checksum, field, endpoint, or command classification.
- No protocol hypotheses.
- No mock protocol or mock P-HPR output.
- No mock gear-pulse routing.
- No real P-HPR output.
- No `IPHprOutputDevice` or `MockPhprOutputDevice` calls.
- No `PHprCommand` creation.
- No output reports, feature reports, HID writes, driver changes, SimPro/SimHub control, or controlled write testing.

Real raw captures remain private and uncommitted. Stage 2I can use actual local captures or sanitized transfer summaries, and the local `Complete Files Required` bundle provides sanitized Wireshark-derived evidence for private analysis.

## Stage 2I Scope

Implemented in Stage 2I:

- `SimagicCaptureAnalysisReport`, file summaries, payload observations, payload summaries, byte-diff observations, pcap summaries, and warnings.
- Wireshark CSV import for payload columns such as `payload_spaced`, `usb.data_fragment`, and `usbhid.data`.
- Wireshark text-summary import for payload counts and `payload=` records.
- Compare-summary import for byte-diff observations.
- `SimagicPayloadDiffAnalyzer` for closest-pair byte comparisons between two capture/export sources.
- pcap/pcapng container summaries for sections, interfaces, packets, link types, and captured-byte totals.
- `SimagicCaptureAnalysisExporter` for sanitized JSON reports under ignored `capture-metadata/generated/`.
- CLI commands `capture-analysis` and `capture-diff`.
- `docs/SIMAGIC_CAPTURE_ANALYSIS.md`.
- Hardware-free tests using synthetic CSV, text summary, compare summary, and pcapng fixtures.

Not implemented in Stage 2I:

- No protocol hypotheses.
- No protocol field naming.
- No report ID, checksum, endpoint semantic, module, strength, frequency, duration, active, stop, or command classification.
- No protocol decoder or encoder.
- No mock P-HPR protocol or output.
- No mock gear-pulse routing.
- No real P-HPR output.
- No `IPHprOutputDevice` or `MockPhprOutputDevice` calls.
- No `PHprCommand` creation.
- No output reports, feature reports, HID writes, driver changes, SimPro/SimHub control, or controlled write testing.
- No raw/private captures, screenshots, serial numbers, or unsanitized hardware data are committed.

## Stage 2J Scope

Implemented in Stage 2J:

- `docs/SIMAGIC_PROTOCOL_HYPOTHESES.md`.
- Sanitized evidence notes for P700 pedal input, GT Neo shift paddles, and P-HPR output captures.
- Analysis-only hypothesis models under `HapticDrive.Simagic.PHPR.Research.Hypotheses`.
- Built-in hypotheses for confirmed input mappings, SimHub `F1 EC` active/start, SimHub stop/idle, SimHub duration timing, SimPro `80 1E 89`, and runtime identity.
- Confidence, status, protocol family, software source, and risk classifications.
- CLI commands `hypotheses-list` and `hypotheses-export`.
- Hardware-free tests for hypothesis construction, safety wording, mock-only readiness, SimPro conservatism, input/output separation, and sanitized export.

Not implemented in Stage 2J:

- No production protocol encoder.
- No production protocol decoder.
- No generated bytes for live hardware.
- No mock P-HPR output integration.
- No mock gear-pulse routing.
- No real P-HPR output.
- No `IPHprOutputDevice` or `MockPhprOutputDevice` calls.
- No `PHprCommand` creation.
- No output reports, feature reports, HID writes, driver changes, SimPro/SimHub control, or controlled write testing.
- No haptic routing from paddle input or `ShiftIntentEvent` values.

## Stage 2K Scope

Implemented in Stage 2K:

- `PHprMockProtocolCommand`, `PHprMockProtocolFrame`, mock family/state/support-status models.
- `SimHubF1EcMockEncoder` and `SimHubF1EcMockDecoder`.
- Mock SimHub F1 EC 64-byte payload representation:
  - brake module byte `01`,
  - throttle module byte `02`,
  - start state byte `01`,
  - stop state byte `00`,
  - direct frequency-Hz byte,
  - direct strength-percent byte.
- `Both` target expansion into explicit brake and throttle mock frames without using module `00`.
- `PHprMockDurationScheduler` for deterministic start-at-0 plus stop-at-duration frame plans.
- Zero-duration start requests produce stop-only mock frames.
- Emergency stop produces immediate stop frames for brake and throttle.
- `SimProUnknownMockFrame` and `SimProUnknownMockEncoder` that classify `80 1E 89` payloads and refuse detailed encoding as `NeedsMoreCaptures`.
- `MockPhprOutputDevice` frame history, generated-frame diagnostics, module availability simulation, disconnect simulation, rejection simulation, emergency-stop count, and pending scheduled stop count.
- Research CLI commands `mock-protocol-examples` and `mock-protocol-export`.
- `docs/SIMAGIC_P_HPR_MOCK_PROTOCOL.md`.
- Hardware-free tests for encoding, decoding, duration modelling, SimProUnknownMock, mock output diagnostics, CLI export, and no write-capable mock protocol API names.

Not implemented in Stage 2K:

- No full P-HPR safety limiter in Stage 2K itself; Stage 2L implements it separately.
- No mock gear-pulse routing.
- No mock road/slip/lock routing.
- No SimPro / SimHub coexistence detection.
- No controlled write test plan.
- No production encoder or decoder.
- No real P-HPR output.
- No output reports, feature reports, HID writes, device-handle writes, driver changes, SimPro/SimHub control, or controlled write testing.
- No haptic routing from paddle input, `ShiftIntentEvent`, `VehicleState`, audio effects, ASIO output, or the mixer.

## Stage 2L Scope

Implemented in Stage 2L:

- `PHprSafetyLimiter`, `IPHprSafetyLimiter`, `PHprSafetyContext`, `PHprSafetyDecision`, `PHprSafetySnapshot`, and `IPHprSafetyClock`.
- Strength, duration, and frequency clamping against conservative `PHprSafetyLimits`.
- Deterministic command-rate limiting with an injected fake clock for tests.
- Per-module continuous-duration estimation and rejection.
- Module availability and disconnected-device start rejection.
- Emergency-stop latching, pending mock stop clearing through `MockPhprOutputDevice`, continuous-duration reset, command-rate reset, and explicit clear behavior.
- Telemetry stale, haptics stopped, emergency mute active, driving not armed, SimPro/SimHub conflict placeholder, and real-write blocking context gates.
- `SafetyLimitedPhprOutputDevice` wrapping `MockPhprOutputDevice`.
- Safe research CLI command `safety-examples`.
- Hardware-free tests for clamps, rejections, context gates, rate limiting, continuous-duration limiting, emergency stop, disconnect/module behavior, diagnostics, fake clock behavior, and no HID/USB write API surface.
- `docs/SIMAGIC_P_HPR_SAFETY_LAYER.md`.

Not implemented in Stage 2L:

- No mock gear-pulse routing.
- No mock road/slip/lock routing.
- No SimPro / SimHub coexistence detection.
- No controlled write test plan.
- No production encoder or decoder.
- No real P-HPR output.
- No output reports, feature reports, HID writes, device-handle writes, driver changes, SimPro/SimHub control, or controlled write testing.
- No haptic routing from paddle input, `ShiftIntentEvent`, `VehicleState`, audio effects, ASIO output, or the mixer.

## Stage 2M Scope

Implemented in Stage 2M:

- `PHprGearPulseRouter`, `PHprGearPulseRouterOptions`, `PHprGearPulseProfile`, route result/status/snapshot models, and target mapping under `HapticDrive.Actuation.PHpr`.
- Conservative mock gear pulse defaults: enabled, target `Both`, strength `0.05`, frequency `50 Hz`, duration `50 ms`, priority `100`, and source `PaddleShiftIntent`.
- Routing only from accepted `ShiftIntentEvent` values to `SafetyLimitedPhprOutputDevice` wrapping `MockPhprOutputDevice`.
- Ignored route diagnostics for disabled, missing, not-armed, or unknown-direction events.
- Safety rejection diagnostics when Stage 2L context gates or limits block a command.
- Mock emergency-stop and clear pass-through.
- WPF Devices-page mock gear routing diagnostics and controls.
- App-settings persistence for mock routing preferences only.
- Hardware-free router tests and existing Stage 2L safety test coverage.
- `docs/SIMAGIC_P_HPR_MOCK_GEAR_ROUTING.md`.

Not implemented in Stage 2M:

- No mock road vibration, wheel slip, or wheel lock routing.
- No SimPro / SimHub coexistence detection.
- No controlled write test plan.
- No production encoder or decoder.
- No real P-HPR output.
- No USB writes, HID output reports, HID feature reports, device-handle writes, driver changes, SimPro/SimHub control, or controlled write testing.
- No `VehicleState`, audio effects, ASIO output, or mixer routing to P-HPR.

## Stage 2N Scope

Implemented in Stage 2N:

- `PHprPedalEffectsRouter`, `PHprPedalEffectsRouterOptions`, `PHprPedalEffectState`, `PHprPedalEffectProfile`, diagnostics, result/status/snapshot models, and effect kind mapping under `HapticDrive.Actuation.PHpr`.
- Mock-only routing for road vibration, wheel slip, and wheel lock from existing `VehicleState` / `HapticPipelineSnapshot` data.
- Conservative defaults:
  - road vibration targets both modules with strength `0.01` to `0.04`, frequency `25` to `45 Hz`, duration `50 ms`, and source `RoadTexture`;
  - wheel slip targets throttle with strength `0.03` to `0.08`, frequency `45` to `75 Hz`, duration `50 ms`, and source `WheelSlip`;
  - wheel lock targets brake with strength `0.04` to `0.10`, frequency `60` to `90 Hz`, duration `50 ms`, and source `WheelLock`.
- Priority per target module: wheel lock, then wheel slip, then road vibration.
- Deterministic minimum interval suppression per effect/module to avoid command storms.
- Routing only through `SafetyLimitedPhprOutputDevice` wrapping `MockPhprOutputDevice`.
- Shared WPF mock output stack for Stage 2M gear routing and Stage 2N pedal effects.
- Devices-page controls and diagnostics for enabled state, road/slip/lock target, strength, frequency, duration, route counts, safety rejections, interval suppression, mock command/frame counts, pending scheduled stops, and emergency stop.
- App-settings persistence for safe mock pedal-effect preferences only.
- `docs/SIMAGIC_P_HPR_MOCK_PEDAL_EFFECTS_ROUTING.md`.

Not implemented in Stage 2N:

- No real P-HPR output.
- No USB writes, HID output reports, HID feature reports, device-handle writes, driver changes, SimPro/SimHub control, or controlled write testing.
- No SimPro / SimHub coexistence detection.
- No controlled write test plan.
- No production encoder or decoder.
- No ASIO/BST-1 audio path change.
- No new F1 25 packet parsing fields.

## Stage 2O Scope

Implemented in Stage 2O:

- `IPHprSoftwareCoexistenceDetector`, `PHprSoftwareCoexistenceDetector`, `IPHprSoftwareProcessProvider`, `WindowsProcessSnapshotProvider`, `PHprSoftwareCoexistenceSnapshot`, `PHprSoftwareProcessSnapshot`, `PHprDetectedSoftwareProcess`, and `PHprCoexistenceOptions`.
- Conservative process-name matching for SimPro Manager and SimHub.
- Status reporting for `Unknown`, `Clear`, `SimProRunning`, `SimHubRunning`, and `ActiveConflict`.
- Non-Windows and process-access-error safe fallback to `Unknown`.
- WPF Devices and Diagnostics coexistence status, process match summary, last scan time, read-only detection statement, and direct-control block warning.
- `PHprSafetyContext.SoftwareConflictStatus` wiring from the latest coexistence snapshot.
- Existing Stage 2L `ActiveConflict` safety rejection remains authoritative through `SimProConflict`.
- `docs/SIMAGIC_SIMPRO_SIMHUB_COEXISTENCE.md`.

Not implemented in Stage 2O:

- No process control, kill, hook, inject, patch, memory inspection, IPC, or settings modification.
- No controlled write test plan.
- No production encoder or decoder.
- No real P-HPR output.
- No USB writes, HID output reports, HID feature reports, device-handle writes, driver changes, SimPro/SimHub control, or controlled write testing.
- No ASIO/BST-1 audio path change.

## Stage 2P Scope

Implemented in Stage 2P:

- `PHprControlledWriteChecklist`, `PHprControlledWriteReadiness`, `PHprControlledWriteReadinessIssue`, `PHprControlledWriteReadinessIssueCode`, `PHprControlledWriteTestPlan`, and `PHprManualTestResultTemplate`.
- A readiness model that always blocks real output in Stage 2P.
- WPF Devices and Diagnostics direct-write readiness state that remains disabled.
- `docs/SIMAGIC_P_HPR_CONTROLLED_WRITE_TEST_PLAN.md`.
- `docs/SIMAGIC_P_HPR_MANUAL_VALIDATION_RUNBOOK.md`.
- Evidence map from the local `Complete Files Required` sanitized Markdown/CSV/TXT summaries.
- Hardware-free tests for readiness blockers, future checklist behavior, test plan coverage, and result-template privacy boundaries.

Not implemented in Stage 2P:

- No real output adapter.
- No HID writer.
- No write-capable UI or direct pulse buttons.
- No production encoder or decoder.
- No USB writes, HID output reports, HID feature reports, device-handle writes, driver changes, SimPro/SimHub control, or controlled write execution.
- No real P-HPR hardware validation.
- No ASIO/BST-1 audio path change.

## Stage 2Q Scope

Implemented in Stage 2Q:

- `HapticDrive.Simagic.PHPR.Output.Windows` with a gated real direct-output adapter.
- SimHub F1 EC 64-byte start/stop encoding for brake and throttle modules.
- Fakeable HID report writer boundary and Windows HID report writer.
- Runtime-only WPF direct-control enable, arm, device/interface/report selection, per-pedal brake/throttle settings, one-pulse buttons, emergency stop, and write diagnostics.
- Accepted-paddle direct gear-pulse routing behind enable, arm, selected device, coexistence, emergency-stop, and safety gates.
- Fake-writer tests covering startup, gating, coexistence blocks, safety rejection, report bytes, duration stop scheduling, emergency stop, dispose stop behavior, per-pedal settings, suppressed shift intent, ASIO isolation, and mock-path preservation.

Not implemented or not claimed in Stage 2Q:

- No unattended hardware writes.
- No automated/CI hardware writes.
- No physical P-HPR validation.
- No safe-gain, physical latency, stop behavior, or pedal mapping claim.
- No SimPro `80 1E 89` write support.
- No persisted real direct-control enable/arm/device selection.
- No ASIO/BST-1 audio path change.

## Stage 2R Scope

Implemented in Stage 2R:

- `PHprManualValidationChecklist`, `PHprManualValidationReadiness`, `PHprManualValidationResult`, `PHprManualValidationResultEvaluation`, and `PHprManualValidationResultExporter`.
- WPF Devices-page controlled validation harness below the real direct-control panel.
- Checklist readiness for user presence, P700 connection, P-HPR module installation, direct-control state, selected device/interface/report, coexistence, emergency stop, and pulse availability.
- Manual result entry fields for brake, throttle, emergency stop, upshift, downshift, wrong-pedal behavior, sustained-vibration behavior, notes, and pass/fail decision.
- Private local Markdown export under `local-validation-results/` when the repo root is available.
- Pass-decision blocking until required manual fields and hardware confirmations are complete.
- Fake/model-only tests for checklist gating, pass gating, export formatting, and local file writing.

Not implemented or not claimed in Stage 2R:

- No hardware validation run.
- No automatic brake/throttle/paddle pulses from the harness.
- No physical pass/fail claim.
- No raw/private validation result committed.
- No ASIO/BST-1 audio path change.

## Required Follow-Up Data

Stage 2A requests the hardware/software data listed in `docs/SIMAGIC_USER_DATA_REQUEST.md`.

The highest-value first items after Stage 2I are:

1. Stage 2G sanitized inventory output from the `inventory` command with the real P700 / Alpha Evo / GT Neo hardware connected.
2. Device Manager hardware IDs for the P700 and Alpha Evo / GT Neo-visible devices.
3. USBView or USB Device Tree Viewer exports for descriptors and HID report descriptors.
4. SimPro Manager V3 P700/P-HPR screenshots.
5. SimHub P-HPR detection and mapping screenshots.
6. Windows game controller / DirectInput button numbers for the GT Neo left and right paddles.
7. Haptic Drive ASIO Refresh Input Devices candidate output, especially device display names and discovery errors.
8. Haptic Drive ASIO Stage 2E last-changed button, mapped left/right paddle diagnostics, and Stage 2F accepted/suppressed shift-intent diagnostics.

USBPcap/Wireshark capture summaries can now be inspected with Stage 2I tooling. Stage 2J protocol hypotheses are complete and remain grounded in sanitized Stage 2I analysis outputs or reviewed local evidence. Stage 2K mock protocol/output is complete. Stage 2L P-HPR safety layer is complete. Stage 2M mock gear pulse routing is complete. Stage 2N mock road vibration, wheel slip, and wheel lock routing is complete. Stage 2O SimPro / SimHub coexistence detection is complete. Stage 2P controlled write test planning is complete. Stage 2Q gated minimal real-write implementation is complete. Stage 2R controlled real P-HPR validation harness is complete. Phase 3A production P-HPR output adapter hardening is next.

## Write Safety Gate

No unattended real P-HPR USB writes may be executed until the user says exactly:

```text
I approve Phase 2 controlled P-HPR write testing
```

The extended Phase 2 / Phase 3 master prompt authorizes implementing the gated Stage 2Q real-write code path and Stage 2R validation harness. It does not authorize unattended hardware vibration, automated real writes, automatic startup pulses, persisted arming, or claims of physical validation. Through Stage 2R, the real-write code path and validation harness are implemented but remain default-off, manually armed, fake/model tested, and not physically validated.

Before an explicit manual validation run, work is limited to read-only discovery, input observation, documentation, mock output, mock safety limiting, protocol hypotheses, gated implementation, fake-writer tests, and diagnostics.

## Legal and Coexistence Notes

- Do not copy SimHub code or UI assets unless license compatibility is checked and documented.
- Do not depend on hidden SimHub internals.
- Do not copy, hook, patch, or inject into SimPro Manager.
- Do not modify SimPro Manager settings automatically.
- Do not kill SimPro Manager.
- Read-only detection of whether SimPro Manager or SimHub is running is implemented in Stage 2O.
- Default future direct P-HPR control must remain disabled when a conflict risk exists.
