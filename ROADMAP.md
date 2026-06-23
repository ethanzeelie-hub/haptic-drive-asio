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
- Stage 18p-B: Telemetry / UDP real-time replay timing and delete-selected recording complete.
- Stage 18p-C: App shell, dark theme, sidebar, and cards complete.
- Stage 18p-D: Effects page hardware/effect restructure complete.
- Stage 18p-E: Devices and Advanced cleanup complete.
- Stage 18p-F: Routing / Mixer polish and final visual pass complete.
- Stage 18q-B: Road texture diagnostics and flight recorder complete.
- Stage 18q-C: BST-1 road scaling retune complete.
- Stage 18q-D: Shared road signal and output separation complete.
- Stage 18q-E: P-HPR continuous road cadence model complete.
- Stage 18q-F: Road validation checklist and documentation complete.
- Stage 18r-B: User settings persistence, defaults cleanup, UI safety simplification, P-HPR wording cleanup, and replay rename complete.
- Stage 18r-C: BST-1 road speed/frequency/grain tuning controls and Devices-tab persistence hotfix complete.
- Stage 18r-D: BST-1 wheel slip / wheel lock tuning controls and diagnostics complete.
- Stage 18r-E/F: P-HPR wheel slip / wheel lock continuous texture model and targeted priority validation complete.
- Stage 19A: Runtime ownership guardrails and extraction plan complete.
- Stage 19B: Runtime ownership dependency inversion and safe extraction complete.
- Stage 19C: Extract continuous real road/slip/lock runtime ownership out of `MainWindow` complete.
- Stage 19D: Extract paddle input routing ownership out of `MainWindow` complete.
- Stage 20: Shared slip/lock evaluator for BST-1 and P-HPR complete.
- Stage 21A: MainWindow residual orchestration audit and safe workflow-status extraction complete.
- Stage 21B: Diagnostics/status report extraction complete.
- Stage 21C: App/settings snapshot hydration and persisted-settings status extraction complete.
- Stage 21D: Remaining pure control-settings parsing and hydration-application extraction complete.
- Stage 21E: Audio-profile control parsing and application extraction complete.
- Stage 21F: Residual MainWindow orchestration audit and local gear readiness presentation extraction complete.
- Stage 21G: Startup/readiness orchestration audit and safe no-output startup planner extraction complete.
- Stage 21H: Shutdown/cleanup ordering audit and stop-only lifecycle planner extraction complete.
- Stage 21I: Safety-context builder audit and pure snapshot extraction complete.
- Stage 21J: Stop All / Emergency Stop ownership audit complete.
- Stage 21K: Start Haptics / Emergency Mute ownership audit complete.
- Stage 21L: Final residual MainWindow orchestration audit complete.
- Stage 22A: Post-Gemini P-HPR slip/lock feel retune and user controls complete.
- Stage 23A: Product workflow cleanup, Testing / Validation tab extraction, and safe normal-user P-HPR preference persistence complete.
- Stage 23B: Post-23A product UI polish, first-run workflow clarity, and normal-mode diagnostics reduction complete.
- Stage 23C: Dashboard view extraction and shell presentation seam complete.
- Stage 23D: Devices view extraction and hardware setup presentation seam complete.
- Stage 23E: Effects view extraction and effect tuning presentation seam complete.
- Stage 23F: Routing / Mixer view extraction and safety-routing presentation seam complete.
- Stage 23G: Telemetry / UDP view extraction and replay-forwarding presentation seam complete.
- Stage 23H: Profiles view extraction and profile workflow presentation seam complete.
- Stage 23I: Testing / Validation view extraction and manual-tools presentation seam complete.
- Stage 23J: Advanced / Diagnostics view extraction and raw-internals presentation seam complete.
- Stage 23K: MainWindow shell-composition audit and Gemini REC-01 closure complete.
- Stage 24A: Gemini REC-02 runtime-start ownership audit and closure complete.
- Stage 25A: Documentation baseline and audit closure complete.
- Stage 25B: Durable quality gates complete.
- Stage 25C: Runtime game-telemetry adapter seam complete.
- Stage 25D: App-side game telemetry catalog and selected-game persistence baseline complete.
- Stage 25E: Explicit telemetry adapter composition and runtime fallback removal complete.
- Stage 25F: Effect-engine extensibility seam complete.
- Stage 25G: Replay-file streaming seam complete.
- Stage 25H: Live recording queue/backpressure hardening complete.
- Stage 25I: Atomic persistence hardening complete.
- Stage 26A: Global output safety interlock complete.
- Stage 26B: Session-aware telemetry freshness hardening complete.
- Stage 26C: Bounded UDP ingress and safer listener defaults complete.
- Stage 26D: Runtime lifecycle serialization complete.
- Stage 26E: Game integration registry and canonical haptic frame complete.
- Stage 26F: Effect descriptor registry and profile schema v2 complete.
- Stage 26G: Real-time audio render path hardening complete.

## Current Hardening Program

- High remediation program active:
  - Remediation 1 complete: real P-HPR non-stop writes now require session-only authorization at the physical write boundary, the old persisted-style `DirectControlApprovalConfirmed` option flag is gone, and stop / stop-all / emergency-stop remain permitted when non-stop writes are blocked.
  - Remediation 2 complete: global output interlock state is observer-safe and now drives an authoritative supervisor across audio output, manual tests, mock P-HPR, continuous P-HPR, direct P-HPR authorization revocation, and reset-readiness blockers.
  - Remediation 3 complete: telemetry now carries structured source identity and typed packet-kind stamps, IPv4-mapped remote endpoints normalize correctly, participant freshness is part of canonical fail-closed driving context, and replay/receiver telemetry subscribers are isolated so one failing observer cannot collapse the flow.
  - Remediation 4 complete: `HapticFrame` is now the canonical live-path contract, the F1 25 normalizer emits typed signal stamps and canonical event/motion/status fields, and audio effects plus P-HPR routing no longer require raw `VehicleState` on the active path.
  - Remediation 5 complete: effect descriptors now create functional runtimes, schema-v2 effect documents drive runtime defaults/repair/enablement, unknown future effect keys round-trip without rendering, and shipped audio descriptors no longer use metadata-only runtimes.
  - Remediation 6 complete: the steady-state audio path now uses immutable effect-graph swaps, callback-time diagnostic publication/formatting is removed, render failures publish atomic failure state, and Stage 6 guardrail/performance coverage now enforces the no-lock real-time render contract.
  - Next remediation priority: Remediation 7 from the audited 12-stage plan.
- Stage 26A complete: global output safety interlock now owns emergency stop/mute latching across BST-1 audio, manual test rendering, and P-HPR routing/runtime surfaces.
- Stage 26B complete: telemetry now carries monotonic receive timestamps, F1 25 state resets on source/session/player identity changes, older same-session frames are ignored, per-signal freshness is centralized, and stale driving telemetry can latch the global output interlock.
- Stage 26C complete: live UDP telemetry now flows through one bounded ingress worker with dedicated haptic/forwarding/recording channels, loopback remains the default bind, LAN telemetry is explicit opt-in, and diagnostics now surface ignored remotes, oversized datagrams, and ingress drop counts.
- Stage 26D complete: shell-triggered runtime lifecycle work now runs through one serialized coordinator with generation guards, telemetry status ticks are single-flight, shutdown trips the global interlock first, and close cleanup now uses a bounded asynchronous shutdown path instead of overlapping fire-and-forget work.
- Stage 26E complete: the app now registers F1 25 through a formal game-integration registry, normalizes adapter output through `IVehicleStateNormalizer`, emits canonical `HapticFrame` snapshots, and routes audio/actuation live paths through canonical driving context and freshness instead of direct F1-specific enums on the active path.
- Stage 26F complete: shipped effects now register through `IHapticEffectRegistry`, profiles now save schema-v2 effect documents keyed by stable effect key, descriptor validation/defaulting repairs invalid settings safely, and unknown future effect keys round-trip without becoming runtime requirements.
- Stage 26G complete: the steady-state engine/pipeline render path now uses reusable buffers/value snapshots, only renders enabled effect runtimes, avoids per-buffer status-string construction, and the native ASIO callback now consumes from a fixed preallocated ring without the old shared callback lock.
- Stage 26H complete: shell status/workflow publication now runs through focused controllers/view-models, `MainWindow.xaml.cs` is back below the Stage 26H size guardrail, and profile tuning now persists through an async debounced controller path instead of synchronous inline save work.
- Stage 26I complete: new recordings now use the resilient `.hdrec` v2 format with per-record/footer CRC coverage, incomplete-capture metadata, selected game/profile-hash metadata, truncated-file recovery, and absolute-deadline replay timing with drift counters.
- Stage 26J complete: the actuation layer no longer references `HapticDrive.Asio.Runtime`, continuous/mock P-HPR routing now consumes canonical `HapticFrame` plus `ActuationDrivingContext` inputs instead of runtime snapshots, app defaults defer creation of the real HID writer until explicit direct-control use, and paddle polling no longer allocates through per-tick LINQ sorting.
- Next production-hardening priority: structured diagnostics, privacy redaction, and support-bundle hardening.
- Stage 25J: Recording library health summaries complete.
- Stage 25K: Release packaging automation complete.
- Stage 25L: Support bundle automation complete.
- Stage 25M: Persistence migration baseline complete.
- Stage 25N: Recording-library query baseline complete.
- Stage 25O: Persistence recovery baseline complete.
- Stage 25P: Effect-activity summary seam complete.
- Stage 25Q: Release artifact smoke baseline complete.
- Stage 25R: Release manifest and checksum baseline complete.
- Stage 25S: Release staging command baseline complete.
- Stage 25T: Release summary artifact baseline complete.
- Stage 25U: Selected-recording packet histogram baseline complete.
- Stage 25V: Selected-recording packet preview baseline complete.
- Stage 25W: Retained backup history baseline complete.
- Stage 25X: Selected-recording detail clipboard baseline complete.
- Stage 25Y: Support-bundle selected-recording detail baseline complete.
- Stage 25Z: Selected-recording detail export baseline complete.
- Stage 25AA: Structured recording inspection seam complete.
- Stage 25AB: Structured BST-1 effect summary seam complete.
- Stage 25AC: Effects-page status summary seam complete.
- Stage 25AD: Audio-profile BST-1 effect control seam complete.
- Stage 25AE: Audio-profile BST-1 effect input seam complete.
- Stage 25AF: Effects-status snapshot seam complete.
- Stage 25AG: Routing/mixer status snapshot seam complete.
- Stage 25AH: BST-1 diagnostics section seam complete.
- Stage 25AI: Shared BST-1 effect catalog seam complete.
- Stage 25AJ: Audio-profile view application seam complete.
- Stage 25AK: Audio-profile view input capture seam complete.
- Stage 25AL: MainWindow audio-profile control accessor cleanup seam complete.
- Stage 25AM: Audio-profile workflow feedback planner seam complete.
- Stage 25AN: Audio-profile view sync coordinator seam complete.

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
34. Stage 18p-B: Telemetry / UDP recording library cleanup, real-time replay timing fix, and delete-selected recording. Complete.
35. Stage 18p-C: App shell, dark theme, sidebar, and cards. Complete.
36. Stage 18p-D: Effects page hardware/effect restructure. Complete.
37. Stage 18p-E: Devices and Advanced cleanup. Complete.
38. Stage 18p-F: Routing / Mixer polish and final visual pass. Complete.
39. Stage 18q-B: Road texture diagnostics and flight recorder only. Complete.
40. Stage 18q-C: BST-1 road scaling retune. Complete.
41. Stage 18q-D: Shared road signal and output separation. Complete.
42. Stage 18q-E: P-HPR continuous road cadence model. Complete.
43. Stage 18q-F: Road validation checklist and documentation. Complete.
44. Stage 18r-B: User settings persistence, defaults cleanup, UI safety simplification, P-HPR wording cleanup, and replay rename. Complete.
45. Stage 18r-C: BST-1 road speed/frequency/grain tuning controls and Devices-tab persistence hotfix. Complete.
46. Stage 18r-D: BST-1 wheel slip / wheel lock tuning controls and diagnostics. Complete.
47. Stage 18r-E/F: P-HPR wheel slip / wheel lock continuous texture model and targeted priority validation. Complete.
48. Stage 19A: Runtime ownership guardrails and extraction plan. Complete.
49. Stage 19B: Runtime ownership dependency inversion and safe extraction. Complete.
50. Stage 19C: Extract continuous real road/slip/lock runtime ownership out of `MainWindow`. Complete.
51. Stage 19D: Extract paddle input routing ownership out of `MainWindow`. Complete.
52. Stage 20: Shared slip/lock evaluator for BST-1 and P-HPR. Complete.
53. Stage 21A: MainWindow residual orchestration audit and safe workflow-status extraction. Complete.
54. Stage 21B: Diagnostics/status report extraction. Complete.
55. Stage 21C: App/settings snapshot hydration and persisted-settings status extraction. Complete.
56. Stage 21D: Remaining pure control-settings parsing and hydration-application extraction. Complete.
57. Stage 21E: Audio-profile control parsing and application extraction. Complete.
58. Stage 21F: Residual MainWindow orchestration audit and local gear readiness presentation extraction. Complete.
59. Stage 21G: Startup/readiness orchestration audit and safe no-output startup planner extraction. Complete.
60. Stage 21H: Shutdown/cleanup ordering audit and stop-only lifecycle planner extraction. Complete.
61. Stage 21I: Safety-context builder audit and pure snapshot extraction. Complete.
62. Stage 21J: Stop All / Emergency Stop ownership audit. Complete.
63. Stage 21K: Start Haptics / Emergency Mute ownership audit. Complete.
64. Stage 21L: Final residual MainWindow orchestration audit. Complete.
65. Stage 22A: Post-Gemini P-HPR slip/lock feel retune and user controls. Complete.
66. Stage 22B: Local hardware validation and slip/lock fine-tune guidance. Planned.
67. Stage 23A: Product workflow cleanup, Testing / Validation tab extraction, and safe normal-user P-HPR preference persistence. Complete.
68. Stage 23B: Post-23A product UI polish, first-run workflow clarity, and normal-mode diagnostics reduction. Complete.
69. Stage 23C: Dashboard view extraction and shell presentation seam. Complete.
70. Stage 23D: Devices view extraction and hardware setup presentation seam. Complete.
71. Stage 23E: Effects view extraction and effect tuning presentation seam. Complete.
72. Stage 23F: Routing / Mixer view extraction and safety-routing presentation seam. Complete.
73. Stage 23G: Telemetry / UDP view extraction and replay-forwarding presentation seam. Complete.
74. Stage 23H: Profiles view extraction and profile workflow presentation seam. Complete.
75. Stage 23I: Testing / Validation view extraction and manual-tools presentation seam. Complete.
76. Stage 23J: Advanced / Diagnostics view extraction and raw-internals presentation seam. Complete.
77. Stage 23K: MainWindow shell-composition audit and Gemini REC-01 closure. Complete.
78. Stage 24A: Gemini REC-02 runtime-start ownership audit and closure. Complete.
79. Stage 25A: Documentation baseline and audit closure. Complete.
80. Stage 25B: Durable quality gates. Complete.
81. Stage 25C: Runtime game-telemetry adapter seam. Complete.
82. Stage 25D: App-side game telemetry catalog and selected-game persistence baseline. Complete.
83. Stage 25E: Explicit telemetry adapter composition and runtime fallback removal. Complete.
84. Stage 25F: Effect-engine extensibility seam. Complete.
85. Stage 25G: Replay-file streaming seam. Complete.
86. Stage 25H: Live recording queue/backpressure hardening. Complete.
87. Stage 25I: Atomic persistence hardening. Complete.
88. Stage 25J: Recording library health summaries. Complete.
89. Stage 25K: Release packaging automation. Complete.
90. Stage 25L: Support bundle automation. Complete.
91. Stage 25M: Persistence migration baseline. Complete.
92. Stage 25N: Recording-library query baseline. Complete.
93. Stage 25O: Persistence recovery baseline. Complete.
94. Stage 25P: Effect-activity summary seam. Complete.
95. Stage 25Q: Release artifact smoke baseline. Complete.
96. Stage 25R: Release manifest and checksum baseline. Complete.
97. Stage 25S: Release staging command baseline. Complete.
98. Stage 25T: Release summary artifact baseline. Complete.
99. Stage 25U: Selected-recording packet histogram baseline. Complete.
100. Stage 25V: Selected-recording packet preview baseline. Complete.
101. Stage 25W: Retained backup history baseline. Complete.
102. Stage 25X: Selected-recording detail clipboard baseline. Complete.
103. Stage 25Y: Support-bundle selected-recording detail baseline. Complete.
104. Stage 25Z: Selected-recording detail export baseline. Complete.
105. Stage 25AA: Structured recording inspection seam. Complete.
106. Stage 25AB: Structured BST-1 effect summary seam. Complete.
107. Stage 25AC: Effects-page status summary seam. Complete.
108. Stage 25AD: Audio-profile BST-1 effect control seam. Complete.
109. Stage 25AE: Audio-profile BST-1 effect input seam. Complete.
110. Stage 25AF: Effects-status snapshot seam. Complete.
111. Stage 25AG: Routing/mixer status snapshot seam. Complete.
112. Stage 25AH: BST-1 diagnostics section seam. Complete.
113. Stage 25AI: Shared BST-1 effect catalog seam. Complete.
114. Stage 25AJ: Audio-profile view application seam. Complete.
115. Stage 25AK: Audio-profile view input capture seam. Complete.
116. Stage 25AL: MainWindow audio-profile control accessor cleanup seam. Complete.
117. Stage 25AM: Audio-profile workflow feedback planner seam. Complete.
118. Stage 25AN: Audio-profile view sync coordinator seam. Complete.

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

## Stage 25 Quality Follow-Up

- Stage 25A aligned the repo documentation with the audited architecture baseline and explicitly recorded the remaining production-readiness gaps.
- Stage 25B makes the existing verification routine durable by turning warnings into build failures, documenting the exact serial verification path, and adding a Windows GitHub Actions workflow that restores, builds, tests, format-checks, and runs launch preflight.
- Stage 25C introduces a runtime-facing `IGameTelemetryAdapter` seam so parsing, packet descriptors, and `VehicleState` application now sit behind a game adapter contract with F1 25 as the default implementation.
- Stage 25D adds an app-side game telemetry catalog plus persisted `SelectedGameId`, so the production app path now composes the runtime adapter explicitly even though the shipped catalog still contains only F1 25.
- Stage 25E removes the runtime assembly's built-in F1 25 fallback and direct F1 telemetry project reference; callers now compose `HapticPipelineCoordinator` with an explicit adapter.
- Stage 25F replaces the effect engine's repeated fixed-list orchestration with an internal registered-slot seam, so reset/update/render/mixer-input flow is now shared even though the public effect options and diagnostics surfaces are still explicitly typed to the current BST-1 effect set.
- Stage 25G adds a streaming replay-file seam so `ReplayFileAsync` now reads `.hdrec` packets directly from disk through an open reader instead of first loading the whole recording into memory, while `LoadAsync` reuses the same reader path for whole-recording callers.
- Stage 25H replaces the live recording service's unbounded queue with a bounded queue plus explicit queue-capacity and dropped-packet diagnostics, keeping packet capture non-blocking while making overload visible to runtime/app callers.
- Stage 25I hardens app-settings, audio-profile, and P-HPR profile saves through shared atomic file replacement and adds an explicit app-settings schema version marker so future migrations have a stable footing.
- Stage 25J extends recording-library summaries with streamed duration, payload-size, and sequence-gap health diagnostics so the app can surface richer recording quality information without full in-memory loads.
- Stage 25K adds a repeatable local `Publish-HapticDrive.ps1` publish-and-zip path plus a GitHub Actions packaging workflow that uploads a `win-x64` release artifact after the same restore/build/test/format/preflight gate.
- Stage 25L adds a repo-native local support-bundle export path that packages the existing diagnostics report into a sanitized zip with a manifest and structured summary, keeping the export rooted in the existing diagnostics presentation seam instead of introducing a second reporting pipeline.
- Stage 25M adds a shared versioned-document migration planner used by app settings, audio profiles, and P-HPR profiles, so legacy version-0 documents now upgrade through one baseline path instead of store-specific ad hoc handling.
- Stage 25N adds a first recording-library query seam: streamed summaries now include sequence-range plus approximate packet-rate metadata, and the Telemetry / UDP page can filter the loaded library in-memory by filename, metadata, and health text.
- Stage 25O adds a single-file persistence recovery seam: app settings, audio profiles, and P-HPR profiles now refresh last-known-good backup snapshots after successful saves and can fall back to those backups when the primary document is missing, corrupt, or unsupported.
- Stage 25P adds a generic effect-activity summary seam from `HapticEffectEngineSnapshot` into the app presenters, so active-effect summary text no longer needs a presenter-local hardcoded list of the shipped BST-1 effects.
- Stage 25Q adds a release-artifact smoke seam: local and GitHub Actions packaging now verify that the produced publish folder and zipped release both contain the required launchable app payload before the workflow uploads the artifact.
- Stage 25R adds release checksum/manifest output: the publish script now emits SHA-256 plus JSON metadata alongside the release zip, the smoke script verifies that metadata against the actual artifact, and CI uploads those files together.
- Stage 25S adds a single local release-staging command: `Prepare-ReleaseArtifact.ps1` now runs the repo verification gate, performs the targeted runtime restore needed for `win-x64` publish, publishes, smoke-checks, and gathers the final zip/checksum/manifest into one staged-release folder.
- Stage 25T adds a release-summary artifact: the publish script now emits a Markdown summary alongside the zip/checksum/manifest, the smoke script validates that summary against the actual artifact metadata, CI uploads it, and local staged-release output carries the same handoff document.
- Stage 25U adds an on-demand selected-recording packet histogram seam in the app layer: F1 25 recordings can now surface cached packet-ID histograms from the Telemetry / UDP detail panel without coupling the generic recording assembly to game-specific packet analysis.
- Stage 25V extends that same app-side recording analysis seam with a first-pass packet preview, so the Telemetry / UDP detail panel now shows sample packet order, relative timing, packet kind/ID, and payload size for the selected F1 25 recording.
- Stage 25W extends the persistence recovery path with retained backup history: app settings, audio profiles, and P-HPR profiles now keep a small rolling snapshot set and fall back through primary, single backup, then retained history when corruption reaches more than one file copy.
- Stage 25X extends the selected-recording inspection path with a copyable clipboard detail report, so operators can lift the already-available summary, histogram, and preview text directly into bug reports or support notes without leaving the app.
- Stage 25Y extends the local support-bundle seam so export can optionally include the currently selected recording's sanitized detail report, connecting recording inspection to support artifacts without bundling raw captures.
- Stage 25Z adds a standalone local selected-recording detail export path under `local-validation-results/recording-inspections/`, so operators can save the current sanitized recording-inspection report without needing the clipboard or a full support bundle.
- Stage 25AA refactors that selected-recording inspection path onto a structured analysis model plus dedicated formatter, keeping today's output stable while giving later packet-browser/index work a stronger app-side seam.
- Stage 25AB adds a shared structured BST-1 effect-summary model plus formatter for diagnostics and routing/mixer presenters, keeping today's visible summaries stable while reducing one more fixed-list presenter seam before broader effect-surface generalization.
- Stage 25AC extends that same effect-extensibility cleanup into the Effects page by moving its active-summary fallback onto a typed app-side summary list, so future effect additions do not need another bespoke presenter-local status string.
- Stage 25AD extends the effect-extensibility cleanup into the audio-profile control path by grouping BST-1 effect control values/text behind one typed app-side snapshot, reducing profile-side flat-contract sprawl without changing the current WPF controls or persisted JSON shape.
- Stage 25AE extends that same profile-control cleanup one rung further by grouping BST-1 effect input capture behind one typed app-side input record, so profile-apply/save flow no longer threads a giant flat effect-input contract through the builder.
- Stage 25AF extends the effect-extensibility stream back into live status assembly by moving the full runtime/options-to-effects-status mapping behind a dedicated builder, so future effect additions do not have to grow another long `MainWindow` mapping block just to reach the existing presenter/view path.
- Stage 25AG extends that same status-assembly cleanup into the Routing / Mixer path by moving its runtime/effect-routing snapshot assembly behind a dedicated builder and by centralizing BST-1 effect-summary snapshot creation, so future effect additions do not have to maintain another `MainWindow`-local fixed-list mapping path across routing and diagnostics.
- Stage 25AH extends the same effect-extensibility cleanup into Advanced / Diagnostics by moving the BST-1-specific diagnostics section behind a dedicated builder, so future effect additions do not have to keep growing inline slip/lock and mixer-safety diagnostic text assembly inside `MainWindow`.
- Stage 25AI extends that same effect-extensibility stream from mapping cleanup into metadata cleanup by centralizing shipped BST-1 effect keys, labels, and ordering behind one shared catalog used by the app-side summary builders and formatters.
- Stage 25AJ extends that same cleanup into profile hydration by moving profile-name, BST-1 effect, and mixer/safety control application onto the extracted view seams, so future effect/control growth does not have to keep expanding another large `MainWindow` control-assignment block.
- Stage 25AK extends that same cleanup into profile input capture by moving profile-name, BST-1 effect, and mixer/safety reads onto the extracted view seams, so future effect/control growth no longer has to keep expanding another large `MainWindow` control-read block just to build the persisted profile input snapshot.
- Stage 25AL closes that mini-stream by removing the leftover dead profile-related control accessor strip from `MainWindow`, so the shell boundary now matches the extracted view seams instead of retaining stale direct-access paths that future edits could accidentally reuse.
- Stage 25AM extends the same profile-workflow cleanup into user-facing feedback by centralizing tuning/save/load/reset footer/profile-status message planning behind one pure helper, so later workflow changes do not have to keep hand-editing repeated shell branching for those message paths.
- Stage 25AN extends the same cleanup into cross-view control synchronization by centralizing profile input/value/text handoff behind a dedicated coordinator with narrow interfaces, so later profile-control changes do not have to keep editing `MainWindow` call choreography across three views.
- Remaining quality work still includes visible game selection UX when a second game exists, broader effect-surface generalization across profiles/tuning UI/detailed diagnostics, deeper recording-library seek/index tooling such as richer per-packet browse/index views, broader cross-file persistence repair/rollback orchestration across multiple documents, and installer/signing/release publication.

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
- Stage 18p-B makes normal WPF Replay Latest and Replay Selected pass explicit time-preserving replay options, keeps fast replay as an explicit `Fast debug` parser/debug mode, adds guarded Delete Selected recording behavior, and leaves `.hdrec` version 1, F1 25 parsing, UDP forwarding, ASIO, P-HPR HID/report bytes, gear routing, and road-effect logic unchanged.
- Stage 18p-C extracts the WPF visual system into `Resources/Theme.xaml` and `Resources/Styles.xaml`, applies the dark/red dashboard shell with sidebar navigation, top action/status bar, and shared card styles, and leaves haptic runtime logic, parser layouts, ASIO behavior, P-HPR HID/report bytes, and effect routing unchanged.
- Stage 18p-D restructures Effects into Shared / Global Effect Settings, BST-1 Seat Shaker, Brake P-HPR, and Throttle P-HPR card sections. It moves normal gear, road, brake lock, and throttle slip tuning to Effects using existing persisted settings and handlers, leaves manual hardware tests on Devices, keeps low-level P-HPR min/max ranges and raw diagnostics in Advanced, and does not change haptic runtime behavior, parser layouts, ASIO behavior, P-HPR HID/report bytes, or command-rate limiter logic.
- Stage 18p-E keeps Devices focused on ASIO/P-HPR/wheel hardware setup, readiness, emergency recovery, Stop All, and manual pulse checks, while moving Local Gear Test / Paddle Gear Bench validation internals behind the Advanced diagnostics gate alongside real direct-control, validation harness, mock routing, and low-level P-HPR diagnostics. It preserves existing named controls and handlers and does not change haptic runtime behavior, parser layouts, ASIO behavior, P-HPR HID/report bytes, or command-rate limiter logic.
- Stage 18p-F polishes Routing / Mixer as the output routing, gain, mute, limiter, priority, ducking, and active-effect summary page. Effects remains normal per-hardware tuning, Devices remains hardware readiness/manual testing, and Advanced remains diagnostics/validation. The stage adds presentational summaries from existing state only and does not change haptic runtime behavior, parser layouts, ASIO behavior, P-HPR HID/report bytes, gear/road routing logic, replay/delete behavior, or command-rate limiter logic.
- Stage 18q-B adds road-texture diagnostics and an off-by-default local JSONL flight recorder so BST-1 road signal, safety-chain estimates, and P-HPR routing/suppression counters can be captured from the same live/replay session. It does not tune road feel, alter F1 25 parsing, change ASIO output behavior, modify P-HPR HID/report bytes, redesign P-HPR road cadence, change gear priority, or claim physical validation.
- Stage 18q-C widens BST-1 / ASIO road output gain from the previous conservative 25% ceiling to 100% for local tuning while preserving the mixer, safety gain, limiter, emergency mute, and selected ASIO output chain. The default remains conservative and the stage does not claim a universal safe physical gain.
- Stage 18q-D separates the shared road signal enable from per-output road toggles. P-HPR road can consume the shared road signal even when BST-1 road output is disabled, while disabling the shared signal still suppresses road output everywhere.
- Stage 18q-E changes P-HPR road from sparse UI-timer pulse routing to a background bounded-cadence model with overlapping road commands, explicit stop commands, hold-timeout watchdog diagnostics, stale/haptics-stopped/disabled/gear-ducking stops, and gear/slip/lock priority preserved.
- Stage 18q-F updates the road validation guides, manual hardware checklist, roadmap, known issues, and development log for the Stage 18q-C/D/E behavior changes without claiming physical validation.
- Stage 18r-B persists normal user tuning across launches through `default.hdprofile.json` and app settings, keeps hardware-energising runtime states out of persistence, raises BST-1 gain headroom to the full `0-100%` UI range, simplifies Routing / Mixer safety controls to output gain only with limiter/ceiling retained internally, cleans up P-HPR road/slip/lock wording, and adds guarded Rename Selected recording support without changing gear runtime behavior, ASIO backend behavior, P-HPR HID/runtime behavior, or road/slip DSP logic.
- Stage 18r-C fixes the remaining safe Devices-tab persistence gaps by saving paddle debounce plus Arm ASIO readiness preference without auto-starting output, and extends BST-1 road tuning with low/high-speed frequency, speed-reference, speed-frequency influence, and grain controls while keeping one shared `RoadTextureSignal`, bounded intensity, and existing gear ducking/P-HPR separation.
- Stage 18r-D keeps one shared BST-1 slip/lock evaluator but splits normal-user wheel-slip and wheel-lock tuning into independent enabled/gain/frequency/roughness controls, persists those new fields safely in audio profiles, migrates older combined slip profiles conservatively, and expands BST-1 slip/lock diagnostics without changing P-HPR slip/lock routing, road tuning, gear timing, parser layouts, or ASIO backend behavior.
- Stage 18r-E/F moves real P-HPR wheel slip and wheel lock onto their own bounded continuous cadence runtime with explicit stops, hold-timeout watchdog protection, richer slip/lock diagnostics, and targeted road-yield plus gear-protection validation. BST-1 road/slip/lock behavior, P-HPR HID/report bytes, and gear timing remain intentionally unchanged.
- Stage 19A verifies the external runtime-ownership findings against the live code, confirms that `MainWindow` still owns the real P-HPR continuous loop startup and paddle routing, and adds project-graph plus shared direct-pulse-path guardrails instead of moving `PHprDirectRuntime.cs` directly into `HapticDrive.Asio.Runtime`. The direct move is blocked today because `HapticDrive.Actuation -> HapticDrive.Asio.Runtime` already exists while `PHprDirectRuntime.cs` still depends on `HapticDrive.Actuation.PHpr` bench and target types. Recommended Stage 19B is to invert or relocate that contract surface first, then extract non-UI runtime ownership out of the App layer without creating a cycle.
- Stage 19B moves the pure direct-runtime bench/target contracts into `HapticDrive.Simagic.PHPR.Abstractions.Routing`, then moves `PHprDirectRuntime.cs`, `PhprDeviceCardPulseService.cs`, and the hidden `PaddleGearBenchDirectGate.cs` helper out of `HapticDrive.Asio.App` into `HapticDrive.Simagic.PHPR.Output.Windows`. The stage intentionally does not move that runtime into `HapticDrive.Asio.Runtime`, because the direct runtime depends on concrete Windows P-HPR output code and should not pull that dependency into the generic runtime layer. Continuous road/slip/lock loop ownership remains Stage 19C, paddle input routing remains Stage 19D, and the shared BST-1/P-HPR slip/lock evaluator remains Stage 20.
- Stage 19C moves the continuous real P-HPR road/slip/lock loop ownership out of `MainWindow.xaml.cs` into `PHprContinuousEffectsRuntimeCoordinator` in `HapticDrive.Actuation.PHpr`. The coordinator owns the two `100 ms` background loops, cancellation/shutdown waits, in-flight suppression state, and road-yield bookkeeping while `MainWindow` now provides current pipeline/safety/readiness snapshots and keeps UI-only status formatting. P-HPR HID/report bytes, ASIO/BST-1 behavior, parser layouts, gear routing, road/slip/lock tuning, cadence, hold-timeout, and UI/XAML all remain intentionally unchanged. Stage 19D remains the paddle-input routing extraction, and Stage 20 remains the shared slip/lock evaluator.
- Stage 19D moves the remaining paddle input routing body out of `MainWindow.xaml.cs` into `PaddleInputRoutingCoordinator` in `HapticDrive.Asio.App`. The coordinator now owns `ShiftIntentProcessor`/bench evaluation, accepted live-shift notifications, mock and real direct gear-route calls, bench mock/direct routing, optional BST-1 local manual ASIO injection, and safe recovery through `IPHprDirectRuntime`, while `MainWindow` only forwards events, supplies current settings/safety delegates, and updates UI status text. The coordinator intentionally stays in App for now because the live route still spans internal direct-runtime APIs plus App-owned ASIO test injection, so moving it outward today would create a worse dependency direction. Stage 20 remains the shared slip/lock evaluator.
- Stage 20 introduces `SlipLockEvaluator` in `HapticDrive.Asio.Core.Haptics` and moves the shared slip/lock freshness, sanitization, threshold, speed-scale, and TC/ABS attenuation math out of `SlipEffect` and `PHprSlipLockRouter`. `SlipEffect` keeps BST-1 audio shaping, `PHprSlipLockRouter` keeps direct-routing ownership, and the older mock `PHprPedalEffectsRouter` also adopts the shared evaluator so BST-1, mock P-HPR, and real direct P-HPR stay aligned without changing UI/XAML, ASIO/BST-1 backends, P-HPR HID/report bytes, parser layouts, gear routing, road cadence, or slip/lock cadence.
- Stage 21A audits the remaining post-Stage-20 `MainWindow.xaml.cs` ownership, avoids a broad MVVM rewrite, and extracts the lowest-risk P-HPR workflow/status report assembly into `PhprWorkflowStatusSnapshotBuilder` and `PhprWorkflowStatusPresenter` inside `HapticDrive.Asio.App`. Startup/shutdown sequencing, settings parsing, safety-context building, recording/replay UI workflow, and the larger diagnostics-panel assembly intentionally remain in `MainWindow` for later stages.
- Stage 21B extracts the broader diagnostics/status report assembly around `UpdateDiagnosticsStatus()` into `DiagnosticsStatusSnapshotBuilder` and `DiagnosticsStatusPresenter` in `HapticDrive.Asio.App`, and extends `PhprWorkflowStatusPresenter` so diagnostics reuse the sanitized workflow/profile/live-validation lines instead of rebuilding them in `MainWindow.xaml.cs`. `MainWindow` still owns live snapshot gathering, helper subsection formatting, visibility gating, WPF control assignment, startup/shutdown sequencing, and settings/safety-context work. Recommended Stage 21C is app/settings snapshot-hydration extraction rather than a broad MVVM rewrite.
- Stage 21C extracts the safe app-settings hydration/save mapping and persisted-settings status/diagnostics text shaping into `AppSettingsSnapshotBuilder` and `PersistedSettingsStatusPresenter` in `HapticDrive.Asio.App`. `MainWindow` still owns WPF control assignment, live shell/runtime snapshot gathering, profile lifecycle, replay-control reads, startup/shutdown sequencing, safety-context builders, ASIO start/stop ownership, and P-HPR runtime coordination. Recommended Stage 21D is the remaining pure settings/control parsing and hydration-application helpers rather than a broad lifecycle move.
- Stage 21D extracts the remaining pure primitive control parsing, normalization, and plain control-value hydration plans into `ControlSettingsSnapshotBuilder` in `HapticDrive.Asio.App`. `MainWindow` still owns direct WPF reads/writes, candidate/item-list binding, runtime configure calls, local gear test readiness, profile lifecycle, startup/shutdown sequencing, safety-context builders, ASIO start/stop ownership, and P-HPR runtime coordination. Recommended Stage 21E is the remaining audio-profile control parsing/application helpers rather than lifecycle extraction.
- Stage 21E extracts the remaining deterministic audio-profile control parsing, validated profile application planning, and profile display-text formatting into `AudioProfileControlSnapshotBuilder` in `HapticDrive.Asio.App`. `MainWindow` still owns direct WPF reads/writes, profile file lifecycle, runtime apply/configure calls, local gear test readiness, startup/shutdown sequencing, safety-context builders, ASIO start/stop ownership, and P-HPR runtime coordination. Recommended Stage 21F is another residual-orchestration audit before any lifecycle move.
- Stage 21F re-audits the residual `MainWindow` ownership after the Stage 21A-21E builder/presenter extractions and finds that almost all remaining seams are lifecycle-heavy or hardware-adjacent. The only additional low-risk move is `LocalGearReadinessPresenter` in `HapticDrive.Asio.App`, which now owns the local gear readiness status/button/tooltip shaping while readiness evaluation and runtime snapshot gathering stay in `MainWindow`. Recommended Stage 21G is an explicitly higher-risk orchestration stage rather than more tiny mapping extractions.
- Stage 21G re-audits constructor/load/readiness ownership and extracts only `StartupReadinessPlanner` in `HapticDrive.Asio.App` for deterministic no-output startup planning. `MainWindow` still owns WPF assignment, pipeline rebuild/readiness hydration, input/candidate discovery, no-output HID open-check execution, startup cleanup, telemetry/timer/runtime start, and all Stop All / Emergency Stop, safety-context, and direct-output ownership. Recommended Stage 21H is a dedicated shutdown/cleanup ordering audit rather than a mixed lifecycle refactor.
- Stage 21H re-audits shutdown/cleanup ordering and extracts only `ShutdownCleanupPlanner` in `HapticDrive.Asio.App` for deterministic stop-only order metadata. `MainWindow` still owns actual close/stop/dispose execution, startup cleanup remains explicit in `PHprDirectRuntime`, and Stop All / Emergency Stop plus safety-context ownership remain untouched. Recommended Stage 21I is a separate safety-context-builder audit or Stop All / Emergency Stop audit, not both together.
- Stage 21I re-audits the remaining safety-context builders and extracts only `SafetyContextSnapshotBuilder` in `HapticDrive.Asio.App` for pure snapshot-to-context mapping. `MainWindow` still owns runtime snapshot gathering, actual Stop All / Emergency Stop execution, startup cleanup invocation, direct-control mutation, and all hardware/runtime call sites. Recommended Stage 21J is a dedicated Stop All / Emergency Stop ownership audit.
- Stage 21J re-audits Stop All / Emergency Stop ownership and finds no worthwhile pure extraction beyond guardrails. `MainWindow` still owns mock and real emergency-stop execution, direct-runtime Stop All, startup cleanup invocation, shutdown cleanup execution, bench block reset, and the surrounding WPF status/diagnostic refresh fan-out. The safe outcome is stronger guardrail coverage plus documentation, not a broad lifecycle coordinator. Recommended Stage 21K is a separate Start Haptics / Emergency Mute ownership audit rather than mixing those controls into Stop All handling.
- Stage 21K re-audits Start Haptics / Emergency Mute ownership and extracts only `HapticsControlStatePresenter` in `HapticDrive.Asio.App` for pure button/state/readiness presentation metadata. `MainWindow` still owns actual `_hapticPipeline.StartAsync()` / `_hapticPipeline.StopAsync()` execution, actual `_hapticPipeline.SetEmergencyMuteAsync(...)` and `_testBench.EmergencyMute` mutation, startup cleanup invocation, shutdown cleanup execution, and all Stop All / Emergency Stop ownership. Recommended Stage 21L is a final residual `MainWindow` orchestration audit to decide whether the Gemini review stream is complete or whether one last tiny dashboard/status presenter is still justified.
- Stage 21L performs the final residual `MainWindow` orchestration audit and closes the Gemini review stream without forcing another extraction. The remaining `MainWindow` code is now deliberately concentrated around WPF assignment/binding, event entry points, startup/shutdown sequencing, runtime snapshot gathering, and execution-heavy lifecycle/safety work. One final aggregate guardrail proves the Stage 21 helper set stays pure while `MainWindow` keeps the real execution-heavy entry points visible. Stage 21M is not required; if ever needed, it should be limited to optional docs-only release-note cleanup.
- Stage 22A happens after the Gemini review stream is closed and deliberately stays out of architecture cleanup. It retunes real P-HPR wheel slip and wheel lock feel by introducing independent per-effect texture cadence settings, tightening the default cadence to `70 ms` for throttle slip and `60 ms` for brake lock, and exposing those cadence controls in the normal Effects workflow alongside the existing per-effect enable toggles. The continuous runtime/coordinator, safety limiter, command-rate limiter, startup behavior, ASIO/BST-1 behavior, and P-HPR report/protocol bytes remain unchanged. Recommended Stage 22B is Ethan-local hardware validation plus fine-tune guidance, or extra cadence diagnostics only if local testing shows the current visibility is insufficient.
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
- Stage 23A adds a dedicated `Testing / Validation` page for manual BST-1 checks, manual P-HPR pulse checks, Synthetic Test Bench, Paddle Gear Bench, and the controlled validation harness so normal Devices/Effects flow stays product-oriented instead of validation-heavy.
- Stage 23A also adds safe app-settings persistence for normal-user P-HPR enable/mode preference only. It can restore `Disabled` / `Mock` / `Direct` workflow intent without restoring private HID paths, active pulses, pending stops, emergency-stop latch state, startup output, or haptics-running state.
- Stage 23A intentionally does not change ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, parser layouts, replay format, or physical-validation claims.
- Stage 23B adds product-polish copy and first-run clarity on top of the Stage 23A shell: Dashboard now owns a ready-checklist/next-step summary, normal pages use plainer operator-facing wording, and Testing / Validation is grouped as deliberate tools rather than a raw validation dump.
- Stage 23B also keeps raw HID/report/debug detail on `Advanced / Diagnostics` while normal workflow pages avoid obvious report/candidate internals, without moving runtime ownership or changing the safe P-HPR preferred-mode persistence added in Stage 23A.
- Stage 23B intentionally does not change ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, parser/replay format behavior, or physical-validation boundaries; Stage 22B remains the local hardware validation/fine-tune guide for Ethan's later physical testing.
- Stage 23C extracts the Dashboard XAML into `Views/DashboardView` and moves Dashboard-only status/checklist/next-step shaping into `DashboardStatusPresenter`, while keeping `MainWindow` as the composition root, runtime owner, and live snapshot gatherer.
- Stage 23C begins gradual shell component extraction only. It does not introduce a broad MVVM rewrite, move Start/Stop/Mute ownership, change ASIO/BST-1 runtime behavior, change P-HPR HID/report behavior, change parser/replay format behavior, or make any physical-validation claim.
- Stage 23D extracts the normal Devices page into `Views/DevicesView` and moves Devices-only setup/readiness presentation shaping into `DevicesStatusPresenter`, while keeping `MainWindow` as the composition root, runtime owner, event-handler owner, and live snapshot gatherer.
- Stage 23D keeps Devices as setup/readiness only, keeps Testing / Validation as deliberate manual tools, keeps Advanced / Diagnostics as raw internals/troubleshooting, and does not change ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, parser/replay format behavior, or physical-validation boundaries.
- Stage 23H extracts the normal Profiles page into `Views/ProfilesView` and moves only deterministic profile workflow status/path wording into `ProfilesStatusPresenter`, while keeping `MainWindow` as the composition root, runtime owner, profile owner, event-handler owner, and persistence executor.
- Stage 23H keeps Profiles as the normal audio/BST-1 plus P-HPR profile workflow only, keeps Telemetry / UDP as normal F1 25 UDP/recording/replay/forwarding workflow, keeps Advanced / Diagnostics as raw internals/troubleshooting, and does not change profile schema, profile save/load/reset behavior, persistence boundaries, ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, parser/replay/forwarding behavior, or physical-validation boundaries.
- Stage 23I extracts the Testing / Validation workflow into `Views/TestingValidationView` and moves only deterministic synthetic-bench display/page-summary wording into `TestingValidationStatusPresenter`, while keeping `MainWindow` as the composition root, runtime owner, manual-tool owner, event-handler owner, and validation/export executor.
- Stage 23I keeps Testing / Validation as deliberate manual tools only, keeps Devices/Effects/Routing/Telemetry/Profiles focused on their normal workflows, keeps Advanced / Diagnostics as raw internals/troubleshooting, and does not change manual test behavior, validation harness behavior, persistence boundaries, ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, or parser/replay/forwarding behavior.
- Stage 23J extracts the Advanced / Diagnostics workflow into `Views/AdvancedDiagnosticsView` and reuses the existing `DiagnosticsStatusPresenter`, `DiagnosticsStatusSnapshotBuilder`, `PhprWorkflowStatusPresenter`, and `PersistedSettingsStatusPresenter` seams instead of introducing a parallel diagnostics rewrite, while keeping `MainWindow` as the composition root, runtime owner, event-handler owner, diagnostics-report executor, and settings/runtime orchestration owner.
- Stage 23J keeps Advanced / Diagnostics as raw internals and troubleshooting only, leaves Dashboard/Devices/Effects/Routing/Telemetry/Profiles/Testing focused on their normal workflows, and does not change diagnostics report behavior, manual test behavior, validation harness behavior, profile/persistence boundaries, UDP listener/forwarding/recording/replay/parser behavior, ASIO/BST-1 runtime behavior, or P-HPR HID/report behavior.
- Stage 23K closes the Stage 23C-23J page-extraction stream as an audit/guardrail/documentation stage. `MainWindow.xaml` is now a small shell host, `MainWindow.xaml.cs` remains the deliberate composition/runtime/safety/event-forwarding owner, and Gemini REC-01 is considered materially addressed for the current phase through the lightweight UserControl plus presenter/builder pattern rather than a broad MVVM rewrite.
- Stage 23K does not change runtime behavior, diagnostics report behavior, manual test behavior, validation harness behavior, profile/persistence boundaries, UDP listener/forwarding/recording/replay/parser behavior, ASIO/BST-1 runtime behavior, or P-HPR HID/report behavior. Stage 22B hardware validation and any future REC-02 runtime-start audit remain separate work.
- Stage 24A audits Gemini REC-02 and keeps the same deliberate ownership split: extracted views remain event-forwarding/presentation seams only, `MainWindow` remains the composition root plus Start/Stop/Emergency/startup/shutdown executor, `PHprContinuousEffectsRuntimeCoordinator` keeps the continuous road/slip/lock loop bodies, `PaddleInputRoutingCoordinator` keeps the paddle-routing body, `PHprDirectRuntimeCoordinator` remains outside `App`/`MainWindow`, planners remain pure, and no runtime behavior changes.
- Stage 25A aligns the public documentation baseline with the live Stage 24A codebase. `README.md` now reflects the current stage, records the deliberate F1-25-only plus Null-default plus separate-P-HPR architecture baseline, and the issue/docs set now calls out the remaining future-scale limitations directly: Runtime still needs a game-adapter abstraction, the effect engine remains a fixed-list design, recording still uses an unbounded in-memory queue model, settings/profile writes are not yet atomic/versioned, and production packaging/release automation is still incomplete.
- Stage 26K completes the structured diagnostics/privacy baseline for production hardening. Support bundles now export machine-readable diagnostic events, carry app/telemetry/recording/output-session correlation IDs, default to safe redaction, and require explicit UI opt-in before private IPs are included. Raw USB payloads, serial-like values, hostnames, process IDs, and full local private paths remain redacted.
- Stage 26L completes the release packaging/dependency-governance baseline for production hardening. The repo now uses central package management, committed lock files, vulnerability auditing, Release-only CI/package validation, coverage gates, documentation-bearing release zips, and public manifests that no longer leak absolute workspace paths.
- Stage 26M completes the accessibility and documentation-consolidation baseline for production hardening. The app now keeps the global safety state visible in the shell chrome, exposes critical control automation metadata and shortcut hints, simplifies the README to a live-current description, replaces root architecture/issues docs with current-state versions, archives historical stage-detail docs, and adds ADRs for the main hardening decisions.
- Stage 26N completes the final production-readiness pass for this roadmap. The repo now has the full hardening chain in place across safety interlocks, telemetry freshness, bounded ingress, lifecycle coordination, canonical game/frame seams, descriptor-driven effects, real-time render guardrails, recording/replay v2, actuation/runtime separation, structured diagnostics/privacy, dependency governance, accessibility, documentation consolidation, and final release-readiness checks.
- Production-readiness work is now considered complete for software hardening. Remaining non-software-complete items stay intentionally outside the finished roadmap scope:
  - physical shaker feel/gain/latency/frequency validation on the real local chain,
  - owner-selected redistribution/license terms,
  - explicit user authorization before any real P-HPR USB write testing.
