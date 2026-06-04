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
