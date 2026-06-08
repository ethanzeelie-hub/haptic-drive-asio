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
21. Phase 3C: P-HPR road vibration production integration. Next.

The extended Phase 2 / Phase 3 master prompt authorizes implementing the gated Stage 2Q code path. It does not authorize unattended hardware vibration, automated real writes, startup pulses, persisted arming, or physical validation claims.

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
