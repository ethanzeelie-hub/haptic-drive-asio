# Simagic P-HPR Safety Plan

This plan governs all Simagic P-HPR work. Stage 2A is documentation and readiness only. Stage 2B adds P-HPR command/safety/output abstractions and a mock-only output skeleton. Stage 2C adds cached driving-state gating. Stage 2D adds read-only wheel / paddle input discovery. Stage 2E adds read-only Windows game-controller paddle listening and manual mapping diagnostics. Stage 2F adds shift-intent accepted/suppressed diagnostics from mapped paddle input and cached `DrivingArmed` state. Stage 2G adds read-only P700 / P-HPR inventory tooling and sanitized exports. Stage 2H adds capture workflow and metadata tooling only. Stage 2I adds read-only capture analysis and sanitized summary export only. Stage 2J adds protocol hypotheses and sanitized hypothesis export only. Stage 2K adds mock-only protocol/output modelling and mock diagnostics only. Stage 2L adds safety limiting, diagnostics, emergency-stop latching, context gates, and a safety-limited mock output wrapper. Stage 2M adds mock-only gear pulse routing from accepted shift intents through that safety-limited mock output wrapper. Stage 2N adds mock-only road vibration, wheel slip, and wheel lock routing from existing `VehicleState` data through the same safety-limited mock output wrapper. Stage 2O adds read-only SimPro Manager / SimHub process detection and safety-context conflict warnings. Stage 2P adds the controlled write test plan, manual validation runbook, no-write readiness model, and disabled direct-write readiness diagnostics. Stage 2Q adds a gated write-capable Windows HID adapter for later manual testing, disabled and unarmed by default. Stage 2R adds a controlled validation harness and private local result export. Phase 3A hardens the direct-output adapter lifecycle and diagnostics. Phase 3B completes instant paddle gear-pulse production integration with safe per-pedal settings persistence and latency trace diagnostics. Phase 3C completes real road-vibration production routing with safe per-pedal road settings, route-interval suppression, and the same real-output gates. Phase 3D completes real wheel-slip and wheel-lock production routing with safe per-effect settings, route-interval suppression, priority above road and below gear pulse, and the same real-output gates. Phase 3E adds P-HPR workflow UI, safe P-HPR effect profiles, diagnostics report coverage, and user-guide coverage. Phase 3F adds replay-driven road/slip/lock validation with mock/fake output only. Phase 3G adds a passive manual live F1 25 validation checklist and diagnostics line. Phase 3H adds final quick-start, troubleshooting, acceptance, and safety documentation. Phase 3I simplifies normal P-HPR controls, moves research internals behind persisted Advanced diagnostics, and updates the user-facing P-HPR range to 0-100% strength, 1-50 Hz, and 10-1000 ms. Phase 3J adds the explicit `controlled-write-test` CLI after Ethan supplied the approval phrase, plus zero-skip readiness test reporting. No stage through Phase 3J claims physical hardware validation.

## Required Approval Phrase

No unattended real P-HPR USB writes, output reports, write-capable feature reports, or real vibration commands may be executed until the user says exactly:

```text
I approve Phase 2 controlled P-HPR write testing
```

The extended Phase 2 / Phase 3 work now includes Ethan's exact approval phrase, so controlled P-HPR write testing may be performed only through explicit manual gates such as the app's armed Direct mode or the Phase 3J `controlled-write-test --execute` command. It does not authorize unattended hardware vibration, automated real writes, automatic startup pulses, persisted arming, or claims of physical validation.

Stage 2B keeps `PHprSafetyLimits.AllowRealDeviceWrites` false by default, and `MockPhprOutputDevice` only records mock commands in memory.

Stage 2F does not call `MockPhprOutputDevice`, `IPHprOutputDevice`, or `PHprCommand`; accepted shift-intent events are diagnostics only.

Stage 2G does not reference the P-HPR output abstraction project, call `MockPhprOutputDevice`, call `IPHprOutputDevice`, create `PHprCommand`, send output reports, send feature writes, or open P700/P-HPR device handles for control.

Stage 2H does not parse USB captures, analyze USB transfers, hypothesize protocol bytes, call `MockPhprOutputDevice`, call `IPHprOutputDevice`, create `PHprCommand`, send output reports, send feature reports, send HID writes, or open P700/P-HPR device handles for control. Its CLI commands create templates, validate metadata, and export sanitized manifests only.

Stage 2I analyzes local captures or sanitized Wireshark exports read-only and exports sanitized summaries only. It does not hypothesize protocol fields, classify commands, create encoders/decoders, call `MockPhprOutputDevice`, call `IPHprOutputDevice`, create `PHprCommand`, send output reports, send feature reports, send HID writes, or open P700/P-HPR device handles for control.

Stage 2J documents hypotheses from sanitized evidence and exports sanitized hypothesis records only. It does not create production encoders/decoders, call `MockPhprOutputDevice`, call `IPHprOutputDevice`, create `PHprCommand`, send output reports, send feature reports, send HID writes, open P700/P-HPR device handles for control, or route `ShiftIntentEvent` values to haptic output.

Stage 2K implements mock-only SimHub F1 EC frame modelling, mock encode/decode tests, deterministic duration scheduling, SimProUnknownMock classification, mock output frame diagnostics, and safe CLI mock examples. It does not create a production encoder/decoder, send output reports, send feature reports, send HID writes, open P700/P-HPR device handles for control, control SimPro Manager or SimHub, route `ShiftIntentEvent` values, route `VehicleState`, or touch the ASIO/BST-1 output path. Nothing in the Stage 2K mock protocol may be sent to real hardware.

Stage 2L implements `PHprSafetyLimiter`, safety result/context/snapshot models, command clamping/rejection, command-rate limiting, continuous-duration limiting, synthetic telemetry/haptics/emergency-mute/driving/conflict context gates, real-write blocking diagnostics, emergency-stop latching/clear behavior, and a `SafetyLimitedPhprOutputDevice` wrapper for `MockPhprOutputDevice`. It does not create a production encoder/decoder, send output reports, send feature reports, send HID writes, open P700/P-HPR device handles for control, control SimPro Manager or SimHub, route `ShiftIntentEvent` values, route `VehicleState`, route telemetry effects, or touch the ASIO/BST-1 output path.

Stage 2M implements `PHprGearPulseRouter`, conservative mock gear pulse defaults, accepted-route/ignored/safety-rejected diagnostics, WPF mock gear routing diagnostics, and mock routing preferences. It routes only accepted `ShiftIntentEvent` values to `SafetyLimitedPhprOutputDevice` wrapping `MockPhprOutputDevice`. It does not create a production encoder/decoder, send output reports, send feature reports, send HID writes, open P700/P-HPR device handles for control, control SimPro Manager or SimHub, route `VehicleState`, route road/slip/lock effects, or touch the ASIO/BST-1 output path.

Stage 2N implements `PHprPedalEffectsRouter`, conservative mock road/slip/lock defaults, priority and interval suppression, WPF mock pedal-effect diagnostics, and mock pedal-effect preferences. It routes only existing `VehicleState` / `HapticPipelineSnapshot` effect interpretations to `SafetyLimitedPhprOutputDevice` wrapping `MockPhprOutputDevice`. It does not create a production encoder/decoder, send output reports, send feature reports, send HID writes, open P700/P-HPR device handles for control, control SimPro Manager or SimHub, implement coexistence detection, implement controlled write testing, add new F1 25 packet parsing, or touch the ASIO/BST-1 output path.

Stage 2O implements read-only SimPro Manager / SimHub process detection, coexistence snapshots, WPF diagnostics, and safety-context conflict status wiring. It does not kill, hook, inject into, patch, inspect memory, IPC-control, or modify either application. It does not create a production encoder/decoder, send output reports, send feature reports, send HID writes, open P700/P-HPR device handles for control, implement controlled write testing, or touch the ASIO/BST-1 output path.

Stage 2P implements `PHprControlledWriteChecklist`, `PHprControlledWriteReadiness`, the controlled write test plan, manual validation runbook, manual result template, evidence map, and disabled WPF direct-write readiness diagnostics. It does not create a real adapter, create a HID writer, add pulse buttons, send output reports, send feature reports, send HID writes, open P700/P-HPR device handles for control, execute controlled write testing, persist arming, or touch the ASIO/BST-1 output path.

Stage 2Q implements `SimagicPhprOutputDevice`, `WindowsHidReportWriter`, `SimHubF1EcRealReportEncoder`, runtime-only WPF direct-control controls, fake-writer tests, and direct gear-pulse routing behind enable/arm/device/coexistence/safety gates. It does not auto-run hardware writes, does not run real P-HPR vibration tests unattended, does not add CI hardware pulses, does not persist enable/arm/device selection, does not implement SimPro `80 1E 89` writes, and does not touch the ASIO/BST-1 output path.

Stage 2R implements checklist/readiness models, manual validation result models, private local Markdown export, and WPF validation-harness diagnostics. It does not send P-HPR commands, does not call the HID writer, does not trigger brake/throttle/paddle pulses, does not mark validation passed without required fields, and does not touch the ASIO/BST-1 output path.

Phase 3A implements explicit P-HPR HID writer open/close lifecycle, timeout-wrapped writes, selected-interface/report validation, disconnect classification, dispose close behavior, and richer diagnostics. It does not auto-open devices at startup, does not auto-trigger pulses, does not persist enable/arm/device selection, does not add automated hardware writes, and does not touch the ASIO/BST-1 output path.

Phase 3B implements instant paddle gear-pulse production integration using accepted `ShiftIntentEvent` values, independent brake/throttle settings, safe settings persistence, and software latency traces. It does not wait for telemetry gear confirmation, does not persist direct-control enable/arm/device state, does not route real road/slip/lock effects, does not auto-run hardware writes, and does not touch the ASIO/BST-1 output path.

Phase 3C implements real road-vibration production routing using existing `VehicleState` / `HapticPipelineSnapshot` data, independent brake/throttle road settings, safe settings persistence, telemetry freshness and cached `DrivingArmed` gates, SimPro/SimHub conflict blocking, and deterministic route-interval suppression. It does not persist direct-control enable/arm/device state, does not route real wheel slip or wheel lock, does not auto-run hardware writes, and does not touch the ASIO/BST-1 road texture path.

Phase 3D implements real wheel-slip and wheel-lock production routing using existing `VehicleState` / `HapticPipelineSnapshot` data, per-effect target/strength/frequency/duration settings, safe settings persistence, telemetry freshness and cached `DrivingArmed` gates, SimPro/SimHub conflict blocking, deterministic route-interval suppression, and priority above road but below gear pulse. It does not persist direct-control enable/arm/device state, does not auto-run hardware writes, and does not touch the ASIO/BST-1 slip/brake-lock path.

Phase 3E implements P-HPR workflow summaries, safe P-HPR effect profile save/load, diagnostics report coverage, and user-guide coverage. It persists effect preferences only and does not persist direct-control enable/arm/device state, emergency-stop state, command history, write history, private validation data, or raw hardware identifiers.

Phase 3F implements replay validation for P-HPR road vibration, wheel slip, and wheel lock using deterministic synthetic replay packets, `VehicleState`, `DrivingArmed`, safety-limited mock output, and diagnostics report source summaries. It does not synthesize GT Neo paddle events, run real writes, validate live F1 25 behavior, or prove physical P-HPR behavior.

Phase 3G implements a passive live F1 25 validation checklist and diagnostics line for telemetry, `DrivingArmed`, paddle listener, shift intent, P-HPR output mode, selected output readiness, coexistence, emergency stop, mock gear pulse, real manual arming, brake/throttle gear tests, road, slip/lock, menu suppression, and conflict warnings. It does not trigger output, open HID devices, run real writes, validate physical P-HPR behavior, or touch the ASIO/BST-1 path.

Phase 3H implements final quick-start, troubleshooting, final acceptance, and final user-guide documentation. It does not change runtime behavior, trigger output, open HID devices, run real writes, validate physical P-HPR behavior, or touch the ASIO/BST-1 path.

Phase 3J implements the `controlled-write-test` CLI. It defaults to dry-run and only sends real P-HPR HID reports when `--execute`, exact approval phrase, selected private HID path, clear SimPro/SimHub coexistence, and local supervision are present. It also converts prior skipped ASIO hardware tests into readiness/pending checks. It does not add unattended output loops, startup writes, persisted arming, physical validation claims, or ASIO/BST-1 changes.

## Allowed Before Approval

- Research.
- Documentation.
- Screenshots request.
- Device inventory planning.
- Read-only HID/USB discovery.
- Read-only PnP/registry/input inventory.
- Read-only Raw Input / DirectInput discovery.
- SimPro/SimHub behavior documentation.
- USB capture checklist.
- Capture workflow and metadata tooling.
- Capture analysis tooling using synthetic fixtures and private local captures.
- Protocol hypotheses.
- Mock P-HPR output.
- Mock protocol.
- Mock P-HPR safety limiting.
- Mock routing.
- Controlled write test planning.
- Disabled direct-write readiness diagnostics.
- Gated Stage 2Q implementation with fake-writer automated tests.
- Controlled validation harness and private local result templates.
- Phase 3A output-adapter hardening with fake-writer automated tests.
- Phase 3B instant gear-pulse integration with fake-writer automated tests and safe settings persistence.
- Phase 3C road-vibration integration with mock/fake-writer automated tests and safe settings persistence.
- Phase 3D wheel-slip/wheel-lock integration with mock/fake-writer automated tests and safe settings persistence.
- Phase 3E UI/profile/diagnostics workflow with model tests and safe effect-profile persistence.
- Phase 3F replay validation with deterministic synthetic replay packets, safety-limited mock output, and source diagnostics.
- Phase 3G passive live F1 25 validation workflow checklist and diagnostics with fake-only automated tests.
- Phase 3H final quick-start, troubleshooting, acceptance, and safety documentation.
- Phase 3J controlled-write CLI implementation, dry-run output, fake-writer tests, and zero-skip readiness reporting.
- UI placeholders.
- Diagnostics.
- Tests.

## Forbidden Before Manual Execution Approval

- Automated or unattended real P-HPR USB writes.
- Automated or unattended real P-HPR vibration commands.
- Automated or unattended real Simagic device output reports.
- Real Simagic device feature reports that write or change state.
- Taking control from SimPro Manager.
- Firmware flashing.
- Driver replacement.
- Kernel-mode drivers.
- Permanent USB filter drivers.
- Unsafe libusb/WinUSB takeover.
- High-amplitude tests.
- Continuous vibration tests.
- Automated hardware loops.
- Any command that could modify calibration, firmware, or persistent device state.

## Mandatory Runtime Safety Principles

All future P-HPR output must be treated as hardware-actuator output.

Mandatory safeguards:

- Emergency stop.
- Stop on telemetry stale.
- Stop on device disconnect.
- Stop on protocol error.
- Stop on SimPro conflict if relevant.
- Max strength cap.
- Max pulse duration.
- Max command rate.
- Max continuous vibration duration.
- Per-pedal limiter.
- Per-effect limiter.
- Low strength first.
- Short duration first.
- Manual trigger first.
- Hardware tests and real hardware commands are manual and hardware-safe by default. Automated readiness tests can run with zero skips, but they must not vibrate hardware without explicit local flags or `controlled-write-test --execute`.

No telemetry-driven real P-HPR output may occur until manual low-amplitude pulse testing has passed.

## First Controlled Write Limits After Approval

The Stage 2P plan for the first controlled write test requires:

- Strength <= 10%.
- Default first pulse is 10% strength, 50 Hz, and 50 ms.
- User-facing pulse duration range is 10-1000 ms.
- One pedal at a time.
- Frequency based on a known safe SimPro setting.
- Manual trigger only.
- Emergency stop visible.
- No loops.
- No continuous vibration.
- Command/result logging.
- Stop command or safe end state after the pulse.

If protocol confidence is insufficient, document blockers instead of writing to hardware.

## SimPro / SimHub Coexistence

Initial target: coexist with SimPro Manager where safe.

Future behavior:

- Detect if SimPro Manager V3 is running. Stage 2O implements read-only process detection.
- Detect if SimHub is running. Stage 2O implements read-only process detection.
- Display process status in diagnostics.
- Default to read-only/mock mode when SimPro Manager is running.
- Warn before any direct P-HPR control.
- Refuse direct control when conflict risk exists unless a later explicit advanced override is created and manually selected.

Never:

- Kill SimPro Manager.
- Hook SimPro Manager.
- Inject into SimPro Manager.
- Modify SimPro Manager settings automatically.
- Require SimHub.
- Use SimHub as a runtime dependency.

## Legal Boundaries

SimHub may be used for behavior, effect, and latency comparison only.

Do not copy SimHub code or UI assets unless license compatibility is checked and documented. Do not copy, hook, patch, or bypass SimPro Manager or Simagic software.
