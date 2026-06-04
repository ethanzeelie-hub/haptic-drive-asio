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
7. Stage 2G: Read-only P700 / P-HPR device inventory. Next.
8. Stage 2H: Capture workflow and metadata tooling.
9. Stage 2I: Capture analysis framework.
10. Stage 2J: P-HPR protocol hypotheses.
11. Stage 2K: Mock P-HPR protocol and output.
12. Stage 2L: P-HPR safety layer.
13. Stage 2M: Mock gear pulse routing.
14. Stage 2N: Mock road vibration, wheel slip, and wheel lock routing.
15. Stage 2O: SimPro / SimHub coexistence detection.
16. Stage 2P: Controlled write test plan.

Stage 2Q and later real P-HPR write work is gated and must not start unless the user says exactly: `I approve Phase 2 controlled P-HPR write testing`.

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
