# Coding Agent Rules

These rules are durable project guidance for Haptic Drive ASIO.

## Staged Work

- Work in small, controlled stages.
- Implement only the current stage.
- Build and test before moving to the next stage.
- Update `DEVELOPMENT_LOG.md` every stage.
- Update `ROADMAP.md` and `KNOWN_ISSUES.md` when the stage changes them.
- Commit every passing stage with a clear stage commit message.
- Do not mix unrelated features into one commit.

## Hardware-Absent Development

- Do not require physical shaker hardware until the user explicitly confirms it has arrived.
- Automated tests must use `NullAudioOutputDevice` by default once output devices exist.
- WASAPI is a manual debug fallback only.
- ASIO is the final intended low-latency output path.
- ASIO device absence must not fail normal builds, tests, or CI.
- Hardware-dependent tests must be manual and skipped by default.
- Do not claim final shaker feel, safe gain, physical latency, or frequency tuning before the real hardware chain is tested locally.

## F1 25 Telemetry

- Use the official F1 25 v3 PDF/spec as the parser source of truth.
- Do not guess packet layouts, offsets, lengths, enum values, or versions.
- Do not use older F1 23/F1 24 specs as substitutes.
- Do not rely on unofficial GitHub structs as authoritative.
- Preserve raw UDP packet bytes for recording, replay, and forwarding.
- UDP forwarding must preserve packets byte-for-byte.
- Unknown or malformed packets must be ignored safely without crashing.

## Architecture

- Keep game-specific telemetry parsing separate from shared `VehicleState` and haptic effects.
- Keep UI, telemetry receive, audio callback, disk IO, logging, and graphing separated.
- Never block the audio path with UI, disk, logging, networking, or graphing work.
- Build future-game support architecturally, but implement F1 25 first.
- Do not implement Simagic P-HPR output in V1 / Phase 1.
- Do not let future hardware research delay the ASIO bass shaker engine.

## Phase 2 / 3 Simagic P-HPR and GT Neo

- P-HPR modules are non-audio actuators; do not route them through ASIO or `IAudioOutputDevice`.
- Keep any future P-HPR actuator architecture separate from the ASIO/BST-1 audio path.
- Automated tests must never execute real P-HPR USB writes.
- Automated tests must never instantiate `WindowsHidReportWriter`.
- Non-stop real P-HPR writes require session-only authorization at the physical write boundary.
- The authorization phrase must never be persisted, logged, exported, or included in diagnostics/support bundles.
- Stop, stop-all, and emergency-stop remain permitted for fail-safe cleanup even when non-stop writes are unauthorized or the interlock is latched.
- Allowed work without hardware validation remains documentation, read-only device/input discovery, capture guides, protocol hypotheses, mock output, mock routing, diagnostics, and tests.
- Read-only GT Neo paddle input observation is allowed.
- Paddle input is the default future P-HPR gear-pulse source.
- Default future gear-pulse mode is `InstantPaddleOnly`: fire from paddle press through cached `DrivingArmed`, with no telemetry wait and no default second confirmation pulse.
- Menu-safe cached `DrivingArmed` gating is required before paddle input may route to a gear pulse.
- Mock P-HPR output and safety limiting must exist before any real output adapter.
- Detecting SimPro Manager / SimHub process status is allowed later, but do not kill, hook, inject into, or modify either application.
- Raw USB captures, private device inventories, serial numbers, and unsanitized hardware data must not be committed.

## Legal

- SimHub may be used for behavioral inspiration only.
- Do not copy SimHub code or UI assets unless license compatibility is checked and documented.
- Do not imply affiliation with SimHub, Simagic, EA, Track Impulse, or hardware vendors.
- Do not commit the EA PDF to a public repository unless licensing permits.
- Prefer extracted implementation notes over committing the original PDF.
