# Roadmap

## Current Stage

- Stage 00: Repo Setup complete.
- Stage 01: App Shell complete.
- Stage 02: Output abstractions and hardware-absent mode complete.
- Stage 03: F1 25 spec extraction complete.
- Stage 04: UDP listener complete.
- Stage 05: UDP forwarding complete.
- Stage 06: F1 25 packet header parser complete.
- Stage 07: F1 25 core packet parser complete.
- Stage 08: VehicleState model complete.
- Stage 09: Recording and replay complete.
- Stage 10: Audio mixer and safety chain complete.
- Stage 11: Test bench complete.
- Stage 12: Gear shift and engine effects complete.
- Stage 13: Kerb, impact, road texture, and slip effects complete.
- Stage 14: UI tuning, profiles, and diagnostics complete.
- Stage 15: First playable mock output milestone complete.
- Stage 16: Manual ASIO hardware readiness complete.
- Stage 17: Native ASIO streaming and low-latency pre-shaker hardening complete.
- Stage 18: Final pre-shaker readiness package complete.
- Phase 1 software/manual ASIO readiness milestone complete through the maximum safe pre-BT-1 scope.
- Stage 2A: Phase 2 readiness, Simagic P-HPR research intake, and data request complete.
- Stage 2B: Input and P-HPR abstractions complete.
- Stage 2C: Cached `DrivingArmed` state service complete.
- Stage 2D: Read-only wheel / paddle input discovery complete.
- Stage 2E: Raw paddle input listener and mapping complete.
- Stage 2F: Shift intent event layer complete.
- Stage 2G: Read-only P700 / P-HPR device inventory complete.
- Stage 2H: Capture workflow and metadata tooling complete.
- Stage 2I: Capture analysis framework complete.
- Stage 2J: P-HPR protocol hypotheses complete.
- Stage 2K: Mock P-HPR protocol and output complete.
- Stage 2L: P-HPR safety layer complete.
- Stage 2M: Mock gear pulse routing complete.
- Stage 2N: Mock road vibration, wheel slip, and wheel lock routing complete.
- Stage 2O: SimPro / SimHub coexistence detection complete.
- Stage 2P: Controlled write test plan complete.
- Stage 2Q: Gated minimal real P-HPR write implementation complete.
- Stage 2R: Controlled real P-HPR validation harness complete.
- Phase 3A: Production P-HPR output adapter hardening complete.
- Phase 3B: Instant paddle gear pulse production integration complete.
- Phase 3C: P-HPR road vibration production integration complete.
- Phase 3D: P-HPR wheel slip and wheel lock production integration complete.
- Phase 3E: P-HPR UI, profiles, diagnostics, and user workflow complete.
- Phase 3F: Integrated replay validation complete.
- Phase 3G: Manual live F1 25 validation workflow complete.
- Phase 3H: Final P-HPR acceptance package complete.
- Phase 3I: Simplified P-HPR controls and routing UI complete.
- Phase 3J: Final controlled P-HPR hardware readiness and zero-skip test reporting complete.
- Stage 18 follow-up: Manual ASIO hardware test and paddle gear bench validation complete.
- Stage 18b: Simplified P-HPR direct bench startup and stop scheduling complete.
- Stage 18c: Paddle device auto-selection and direct bench stop hardening complete.
- Stage 18d: Direct Paddle Bench runaway and emergency stop hotfix complete.
- Stage 18e: P-HPR direct runtime bench crash recovery complete.
- Stage 18f: Direct Paddle Bench UI thread crash hotfix complete.
- Stage 18g: Rapid paddle gear-pulse retriggering complete.
- Stage 18i: BST-1 ASIO manual controls and synchronized paddle gear pulse output complete.
- Stage 18j: BST-1 local gear test and shared duration sync complete.
- Stage 18k: BST-1 standalone ASIO local pulse, startup defaults, compact status, and output trim complete.
- Stage 18l: BST-1 standalone ASIO pulse queue/drop and app shutdown cleanup complete.
- Stage 18m: BST-1 ASIO state hydration, pulse completion correctness, haptics-on/off pulse consistency, and close cleanup complete.
- Stage 18n-B: Persistent BST-1 local ASIO engine, pulse-owned completion proof, Direct Bench retrigger limiter, and tray placeholder cleanup complete.
- Stage 18o-B: Shared road texture signal and gear ducking complete.
- Stage 18p-A: Product UI architecture and replay timing diagnostic complete.

## Planned Stages

1. Stage 01: App shell. Complete.
2. Stage 02: Output abstractions and hardware-absent mode. Complete.
3. Stage 03: F1 25 spec extraction. Complete.
4. Stage 04: UDP listener. Complete.
5. Stage 05: UDP forwarding. Complete.
6. Stage 06: F1 25 packet header parser. Complete.
7. Stage 07: F1 25 core packet parser. Complete.
8. Stage 08: VehicleState model. Complete.
9. Stage 09: Recording and replay. Complete.
10. Stage 10: Audio mixer and safety chain. Complete.
11. Stage 11: Test bench. Complete.
12. Stage 12: Gear shift and engine effects. Complete.
13. Stage 13: Kerb, impact, road texture, and slip effects. Complete.
14. Stage 14: UI tuning, profiles, and diagnostics. Complete.
15. Stage 15: First playable mock output milestone. Complete.
16. Stage 16: Manual ASIO hardware readiness. Complete.
17. Stage 17: Native ASIO streaming and low-latency pre-shaker hardening. Complete.
18. Stage 18: Final pre-shaker readiness package. Complete.
19. Stage 18 follow-up: Manual ASIO hardware test and paddle gear bench validation. Complete.
20. Stage 18b: Simplified P-HPR direct bench startup and stop scheduling. Complete.
21. Stage 18c: Paddle device auto-selection and direct bench stop hardening. Complete.
22. Stage 18d: Direct Paddle Bench runaway and emergency stop hotfix. Complete.
23. Stage 18e: P-HPR direct runtime bench crash recovery. Complete.
24. Stage 18f: Direct Paddle Bench UI thread crash hotfix. Complete.
25. Stage 18g: Rapid paddle gear-pulse retriggering. Complete.
26. Stage 18i: BST-1 ASIO manual controls and synchronized paddle gear pulse output. Complete.
27. Stage 18j: BST-1 local gear test and shared duration sync. Complete.
28. Stage 18k: BST-1 standalone ASIO local pulse, startup defaults, compact status, and output trim. Complete.
29. Stage 18l: BST-1 standalone ASIO pulse queue/drop and app shutdown cleanup. Complete.
30. Stage 18m: BST-1 ASIO state hydration, pulse completion correctness, haptics-on/off pulse consistency, and close cleanup. Complete.
31. Stage 18n-B: Persistent BST-1 local ASIO engine, pulse-owned completion proof, Direct Bench retrigger limiter, and tray placeholder cleanup. Complete.
32. Stage 18o-B: Shared road texture signal and gear ducking. Complete.
33. Stage 18p-A: Product UI architecture and replay timing diagnostic. Complete.
34. Stage 18p-B: Telemetry / UDP recording library cleanup, real-time replay timing fix, and delete-selected recording. Planned.
35. Stage 18p-C: App shell, dark theme, sidebar, and cards. Planned.
36. Stage 18p-D: Effects page hardware/effect restructure. Planned.
37. Stage 18p-E: Devices and Advanced cleanup. Planned.
38. Stage 18p-F: Routing / Mixer polish and final visual pass. Planned.

## Phase 2 / 3 Simagic P-HPR Plan

Phase 2 begins while preserving the Stage 18 ASIO/BST-1 package. P-HPR is a separate non-audio actuator path and must not be routed through ASIO or `IAudioOutputDevice`.

Phase 2 safe sequence:

1. Stage 2A: Phase 2 readiness, research intake, and data request. Complete.
2. Stage 2B: Input and P-HPR abstractions. Complete.
3. Stage 2C: Cached `DrivingArmed` state service. Complete.
4. Stage 2D: Read-only wheel / paddle input discovery. Complete.
5. Stage 2E: Raw paddle input listener and mapping. Complete.
6. Stage 2F: Shift intent event layer. Complete.
7. Stage 2G: Read-only P700 / P-HPR device inventory. Complete.
8. Stage 2H: Capture workflow and metadata tooling. Complete.
9. Stage 2I: Capture analysis framework. Complete.
10. Stage 2J: P-HPR protocol hypotheses. Complete.
11. Stage 2K: Mock P-HPR protocol and output. Complete.
12. Stage 2L: P-HPR safety layer. Complete.
13. Stage 2M: Mock gear pulse routing. Complete.
14. Stage 2N: Mock road vibration, wheel slip, and wheel lock routing. Complete.
15. Stage 2O: SimPro / SimHub coexistence detection. Complete.
16. Stage 2P: Controlled write test plan. Complete.
17. Stage 2Q: Gated minimal real P-HPR write implementation. Complete.
18. Stage 2R: Controlled real P-HPR validation harness. Complete.
19. Phase 3A: Production P-HPR output adapter hardening. Complete.
20. Phase 3B: Instant paddle gear pulse production integration. Complete.
21. Phase 3C: P-HPR road vibration production integration. Complete.
22. Phase 3D: P-HPR wheel slip and wheel lock production integration. Complete.
23. Phase 3E: P-HPR UI, profiles, diagnostics, and user workflow. Complete.
24. Phase 3F: Integrated replay validation. Complete.
25. Phase 3G: Manual live F1 25 validation workflow. Complete.
26. Phase 3H: Final P-HPR acceptance package. Complete.
27. Phase 3I: Simplified P-HPR controls and routing UI. Complete.
28. Phase 3J: Final controlled P-HPR hardware readiness and zero-skip test reporting. Complete.

The extended Phase 2 / Phase 3 master prompt authorizes implementing the gated Stage 2Q code path, controlled validation harness, adapter hardening, instant gear-pulse route, road-vibration route, wheel-slip/wheel-lock route, P-HPR UI/profile/diagnostics workflow, integrated replay validation, manual live F1 validation workflow, final P-HPR acceptance package, and the final controlled-write CLI after Ethan supplied the exact approval phrase. It does not authorize unattended hardware vibration, automated real writes, startup pulses, persisted arming, or physical validation claims.

## Post-BT-1 Hardware Phases

1. BT-1 chain bring-up and silent-to-low-gain ASIO validation.
2. Physical gain envelope, limiter ceiling, and emergency-stop validation.
3. Physical latency and callback stability validation with the full M-Audio -> Fosi -> Dayton BST-1 chain.
4. Effect-by-effect shaker tuning for engine, gear, kerb, impact, road texture, and slip/brake-lock.
5. Final ASIO V1 acceptance pass, documentation, and manual hardware test updates.

## V1 Boundary

- F1 25 first.
- Null output, WASAPI debug output, ASIO abstraction, telemetry replay, first playable mock software pipeline, and hardware-safe defaults.
- Stage 16 ASIO readiness includes discovery, explicit selection/arming/channel routing seams, diagnostics, fake-backend tests, and manual M-Audio/Fosi/BST-1 checklist.
- Stage 17 adds native ASIO streaming behind the backend seam, output-owned render cadence, stale telemetry mute, render/backend diagnostics, and fake-backend tests while keeping Null output as the automated-test default.
- Stage 18 adds launch/runtime prerequisite handling, persisted app settings, forwarding destination UI, recordings library, selected replay, packet-ID diagnostics, copyable diagnostics reports, and final pre-shaker documentation cleanup.
- Stage 18 follow-up adds a manual-only ASIO hardware test that injects short 40/50 Hz sine pulses through the selected real ASIO output after output mode, M-Audio / M-Track driver, channel, arming, mute, and channel gates pass. It also keeps the existing deterministic synthetic benchmark on Null output.
- Stage 18o-B consolidates BST-1 and P-HPR road texture around one shared `RoadTextureSignal`, keeps P-HPR road routing separate from `IAudioOutputDevice`, and gives accepted local gear pulses a short road-ducking priority window without changing F1 25 parsing, UDP forwarding, raw recording/replay preservation, or confirmed P-HPR report bytes.
- Stage 18p-A inspects the existing WPF shell, settings/profile persistence, and replay path before any broad UI rewrite. It identifies that `.hdrec` recordings already store relative timing and that normal WPF replay currently appears instant because the UI calls `TelemetryReplayOptions.Fast`; planned 18p-B should make real-time replay the default and add delete-selected recording before the staged UI visual work.
- Stage 18b simplifies the P-HPR Paddle Gear Bench direct workflow: startup may auto-refresh input/direct candidates, auto-select the known `VID_3670/PID_0905` FeatureReport `0xF1` / 64-byte HID device-interface candidate by capability, and run no-output readiness checks without sending startup vibration; the bench is enabled, auto-armed, Direct-mode by default, uses Devices brake/throttle gear-pulse values, and direct starts schedule matching stop reports after `DurationMs`.
- Stage 18c fixes Paddle Gear Bench follow-up blockers by selecting the usable 32-button `VID_3670/PID_0905` Windows game-controller over 0-button candidates, blocking 0-button listener starts, routing bench pulses only from visible mapped listener events, and surfacing active-pulse/start/stop diagnostics from the shared direct P-HPR output path.
- Stage 18d hotfixes Direct Paddle Gear Bench runaway-output risk by removing the bench-only pulse planner, routing direct bench starts through the same Devices-tab direct pulse service as the blue Test Brake/Throttle buttons, defaulting the bench target to Both, blocking release/retrigger events while a direct pulse is active or pending stop, adding `DurationMs + 100 ms` stop-all watchdog coverage, retrying per-module emergency stop-all writes, and writing sanitized local crash-state logs on unhandled failures.
- Stage 18e extracts the Direct Paddle Gear Bench route into an explicit `PHprDirectRuntimeCoordinator` state machine with one shared blue-button pulse service instance, serialized direct commands, stop-only startup cleanup, an unclean-shutdown marker, an immediate-flush local flight recorder, stop-all/clear-device-state recovery, shared-path proof diagnostics, and software latency snapshots. Stage 18f hotfixes WPF cross-thread status updates from the background paddle callback after a successful direct start write by self-marshaling status refreshes, awaiting the final UI post, and flight-recording/stop-all recovering paddle-path exceptions. Stage 18g adds Direct Bench latest-press-wins retriggering with per-module generation-guarded scheduled stops, stale observer/drop diagnostics, and 5 ms default per-button paddle debounce. Stage 18i adds BST-1 ASIO strength/frequency/duration controls, manual short ASIO pulses without Start Haptics, internal True ASIO diagnostics, an ignored BST-1 ASIO JSONL flight recorder, and off-by-default BST-1 Paddle Gear Bench pulses synchronized from the same accepted `Pressed` bench events as P-HPR. Stage 18j separates ASIO selected/armed readiness from stream-running status, records last manual and last gear ASIO proof independently, adds a local gear-test listener workflow that does not require Start Haptics or live F1 telemetry, and syncs brake P-HPR, throttle P-HPR, Direct Paddle Gear Bench, and BST-1 sync-mode duration through one shared gear-pulse duration. Stage 18k defaults to the validated M-Audio ASIO driver/channel 1/armed state when the driver is discoverable without starting output, primes standalone ASIO manual/local paddle pulses before playback, adds compact ASIO Ready/Active status with detailed diagnostics moved to Advanced, adds BST-1-only output trim, and makes channel selection non-vibrating. Stage 18m hydrates ASIO driver/channel capability without Start Haptics, prevents stale channel-count-0 readiness blocks, makes haptics-on local pulses complete through the running callback instead of a competing submit loop, records exact expected/accepted/rendered frame counts and completion reasons, and keeps unchecked close from being cancelled by tray-minimize logic. Stage 18n-B keeps local BST-1 pulses on a persistent output-owned callback path, requires pulse-owned post-limiter energy proof, restores rapid Direct Bench retrigger headroom, and removes the disabled tray checkbox placeholder. It still sends no startup output and does not alter confirmed P-HPR bytes, P-HPR paddle mappings, normal telemetry `DrivingArmed` routing, F1 25 parsing, UDP forwarding, or recording/replay raw-byte preservation.
- No Simagic P-HPR implementation in V1.
- Stage 2A adds Simagic Phase 2 documentation and safety gates only; it does not implement P-HPR output.
- Stage 2B adds contracts and mock-only P-HPR scaffolding only; it does not implement real input discovery, protocol control, or device writes.
- Stage 2C adds cached `DrivingArmed` evaluation only; it does not connect paddle events or route actuator commands.
- Stage 2D adds read-only input-device discovery and candidate scoring only; it does not implement live paddle listening, left/right mapping, haptic routing, or P-HPR output.
- Stage 2E adds read-only Windows game-controller paddle listening, manual left/right mapping, rising-edge/debounce diagnostics, and safe mapping persistence only; it does not raise `ShiftIntentEvent` from hardware input, route haptics, or implement P-HPR output.
- Stage 2F adds mapped paddle to `ShiftIntentEvent` evaluation, cached `DrivingArmed` gating, safe diagnostics, and safe enabled/mode persistence only; it does not call P-HPR output, create `PHprCommand`, route mock gear pulses, trigger ASIO gear effects, or implement P700/P-HPR discovery.
- Stage 2G adds read-only P700 / P-HPR inventory tooling, sanitized local exports, registry/input-discovery metadata collection, and candidate classification only; it does not capture USB traffic, analyze captures, hypothesize protocols, call P-HPR output, create `PHprCommand`, or route haptics.
- Stage 2H adds capture workflow documentation, scenario definitions, metadata templates, filename building, validation, sanitization, sanitized manifest export, and CLI commands only; it does not parse `.pcap/.pcapng`, analyze USB transfers, hypothesize protocols, call P-HPR output, create `PHprCommand`, send USB writes, or route haptics.
- Stage 2I adds read-only capture analysis tooling for Wireshark CSV/text summaries, payload fingerprints, byte-diff observations, pcap/pcapng container summaries, sanitized JSON exports, and synthetic tests only; it does not hypothesize protocol fields, create encoders/decoders, call P-HPR output, create `PHprCommand`, send USB writes, or route haptics.
- Stage 2J adds formal protocol hypothesis records, sanitized hypothesis docs, SimHub F1 EC mock-only readiness notes, conservative SimPro 80 1E 89 unknowns, Stage 2K mock-only surface definition, real-write blockers, and tests only; it does not create production encoders/decoders, call P-HPR output, create `PHprCommand`, send USB writes, or route haptics.
- Stage 2K adds mock-only protocol records, SimHub F1 EC mock encoding/decoding, deterministic duration scheduling, SimProUnknownMock classification, mock output frame diagnostics, safe CLI examples, and tests only; it does not add mock routing, production encoders/decoders, real output, USB writes, or haptic routing.
- Stage 2L adds reusable P-HPR safety limiter models, context gates, diagnostics, command clamping/rejection, command-rate limiting, continuous-duration limiting, emergency-stop latching/clear behavior, real-write blocking diagnostics, a safety-limited mock output wrapper, safe CLI examples, and tests only; it does not add mock gear-pulse routing, mock road/slip/lock routing, production encoders/decoders, real output, USB writes, or haptic routing.
- Stage 2M adds mock-only gear pulse routing from accepted `ShiftIntentEvent` values through `PHprGearPulseRouter`, `SafetyLimitedPhprOutputDevice`, and `MockPhprOutputDevice`; it does not add road/slip/lock routing, production encoders/decoders, real output, USB writes, HID reports, SimPro/SimHub detection, or ASIO/BST-1 routing.
- Stage 2N adds mock-only road vibration, wheel slip, and wheel lock routing from existing `VehicleState` / `HapticPipelineSnapshot` data through `PHprPedalEffectsRouter`, `SafetyLimitedPhprOutputDevice`, and `MockPhprOutputDevice`; it does not add real output, USB writes, HID reports, SimPro/SimHub detection, controlled write testing, new packet parsing, or ASIO/BST-1 routing.
- Stage 2O adds read-only SimPro Manager / SimHub process detection, coexistence diagnostics, and `PHprSafetyContext.SoftwareConflictStatus` wiring; it does not kill, hook, inject into, patch, control, or modify either process, and it does not add real output, USB writes, HID reports, controlled write testing, or ASIO/BST-1 routing.
- Stage 2P adds the controlled write test plan, manual validation runbook, no-write readiness model, WPF disabled direct-write readiness diagnostics, evidence mapping, and tests; it does not add a real adapter, HID writer, write-capable UI, USB writes, real vibration, or ASIO/BST-1 routing.
- Stage 2Q adds a gated write-capable Windows HID P-HPR adapter, SimHub F1 EC encoder, runtime-only direct-control UI, fake-writer tests, and accepted-paddle direct gear-pulse routing; it does not execute hardware writes automatically, persist enable/arm/device selection, validate physical P-HPR behavior, or touch ASIO/BST-1 routing.
- Stage 2R adds a controlled validation harness, checklist/readiness model, WPF manual result-entry/export surface, private local Markdown export, and fake-only tests; it does not run hardware validation, mark physical validation passed, or add automated hardware writes.
- Phase 3A hardens the real direct-output adapter with explicit writer open/write/close lifecycle, timeout handling, selected-interface/report validation, disconnect classification, close-on-dispose behavior, WPF diagnostics, and fake-writer tests; it does not auto-run hardware writes, persist arming, validate physical P-HPR behavior, or touch ASIO/BST-1 routing.
- Phase 3B completes instant paddle gear-pulse production integration with independent brake/throttle settings, safe settings persistence, default same up/down pulse, software latency trace diagnostics, and fake-writer tests; it does not persist real enable/arm/device state, route real road/slip/lock effects, validate physical P-HPR behavior, or touch ASIO/BST-1 routing.
- Phase 3C completes real road-vibration production integration with independent brake/throttle road scaling, safe settings persistence, deterministic route-interval suppression, stale telemetry and `DrivingArmed` gates, SimPro/SimHub conflict blocking, and mock/fake-real tests; it does not persist direct-control enable/arm/device state, route real wheel slip or wheel lock, validate physical P-HPR behavior, or touch ASIO/BST-1 routing.
- Phase 3D completes real wheel-slip and wheel-lock production integration with safe target/strength/frequency/duration persistence, deterministic route-interval suppression, priority above road and below gear pulse, stale telemetry and `DrivingArmed` gates, SimPro/SimHub conflict blocking, and mock/fake-real tests; it does not persist direct-control enable/arm/device state, validate physical P-HPR behavior, or touch ASIO/BST-1 routing.
- Phase 3E completes P-HPR workflow UI polish, safe P-HPR effect profile save/load, diagnostics report coverage, and user-guide coverage; it does not persist direct-control enable/arm/device state, validate physical P-HPR behavior, or touch ASIO/BST-1 routing.
- Phase 3F completes integrated replay validation for P-HPR road/slip/lock routing with deterministic synthetic replay tests, replay-source diagnostics, `DrivingArmed` replay checks, stale/emergency/profile-setting coverage, and no synthetic gear-paddle events; it does not run real writes, validate live F1 25 behavior, or prove physical P-HPR behavior.
- Phase 3G completes a passive manual live F1 25 P-HPR validation workflow with Devices-page checklist and diagnostics coverage for telemetry, `DrivingArmed`, paddle listener, output mode, coexistence, emergency stop, gear pulse, road, slip/lock, menu suppression, and conflict warnings; it does not execute hardware writes, validate physical P-HPR behavior, or touch ASIO/BST-1 routing.
- Phase 3H completes final quick-start, troubleshooting, acceptance, safety, and user-guide documentation; it does not change runtime output paths, execute hardware writes, validate physical P-HPR behavior, or touch ASIO/BST-1 routing.
- Phase 3I simplifies the WPF shell into Dashboard, Devices, Effects, Routing / Mixer, Telemetry / UDP, Profiles, and Advanced / Diagnostics; moves P-HPR research/direct internals behind persisted Advanced mode; changes P-HPR UI settings to 0-100% strength, 1-50 Hz frequency, and 10-1000 ms duration; it does not execute hardware writes, validate physical P-HPR behavior, or touch ASIO/BST-1 routing.
- Phase 3J adds a `controlled-write-test` CLI that defaults to dry-run and requires `--execute`, selected private HID path, successful no-report open-check, clear coexistence, and the exact approval phrase before real P-HPR writes; converts the prior skipped ASIO manual tests into zero-skip readiness checks; adds a local-only direct-output candidate picker, HID device-interface discovery, HID registry metadata surfacing for `VID_3670` family PIDs, dry-run gates, no-report open-check, read-only HID report-capability discovery, explicit OutputReport/FeatureReport transport selection, FeatureReport `0xF1` shape validation, and no-command report-shape validation so `VID_3670` HID candidates can be selected without printing private paths while Raw Input/registry metadata-only and invalid-shape candidates remain blocked; it does not add unattended hardware vibration, physical validation claims, persisted direct arming, or ASIO/BST-1 routing changes.
- Stage 18 follow-up adds a runtime-only Paddle Gear Bench Test that accepts mapped GT Neo paddles without recent telemetry for validation and reaches direct P-HPR output only through the strict FeatureReport `0xF1`, 64-byte, open-check, coexistence, emergency-stop, road, slip, and lock gates. Stage 18b makes that bench Direct-mode and auto-armed by default, uses the normal Devices brake/throttle P-HPR values, and fixes scheduled stop reports. Stage 18c hardens input auto-selection, blocks 0-button listener starts, requires direct bench events to originate from the visible mapped listener path, and exposes active-pulse/start/stop diagnostics. Stage 18d removes the bench-only direct pulse planner, reuses the Devices-tab blue-button direct pulse service, defaults bench target to Both, suppresses release/retrigger input, and adds watchdog/emergency-stop-all/crash-state hardening. Stage 18e owns the route through a deterministic runtime state machine, stop-only cleanup, unclean marker, flight recorder, manual Stop All / Clear Device State recovery, and shared-path proof diagnostics. Stage 18f fixes the Direct Bench WPF cross-thread crash by marshaling paddle-path UI updates and recovering exceptions without throwing to WPF/AppDomain. Stage 18g lets rapid Direct Bench paddle presses retrigger immediately with generation-guarded per-module stops and stale-output drops instead of queued late pulses. Stage 18j adds a local gear-test workflow for mapped paddles and explicit ASIO/P-HPR readiness without requiring Start Haptics, UDP, or `DrivingArmed`. Stage 18k makes that BST-1 local path truly standalone when Start Haptics is stopped by queue-priming bounded ASIO pulse buffers and stopping afterward. Stage 18m keeps haptics-on and haptics-off BST-1 local pulses on the same renderer/settings while using the appropriate standalone or running-callback path. Stage 18n-B moves stopped-haptics BST-1 local pulses to the persistent callback path, proves pulse-owned frames/energy, and gives Direct Bench rapid retriggers enough limiter headroom for the fake-backed 10-press downshift case. Stage 18o-B makes BST-1 and P-HPR road texture consume one shared road signal and briefly ducks road after accepted gear pulses. It does not alter normal `DrivingArmed` telemetry gating, confirmed P-HPR protocol bytes, or raw-packet preservation.
