# Simagic P-HPR Safety Plan

This plan governs all Simagic P-HPR work. Stage 2A is documentation and readiness only. Stage 2B adds P-HPR command/safety/output abstractions and a mock-only output skeleton. Stage 2C adds cached driving-state gating. Stage 2D adds read-only wheel / paddle input discovery. Stage 2E adds read-only Windows game-controller paddle listening and manual mapping diagnostics. Stage 2F adds shift-intent accepted/suppressed diagnostics from mapped paddle input and cached `DrivingArmed` state. Stage 2G adds read-only P700 / P-HPR inventory tooling and sanitized exports. Stage 2H adds capture workflow and metadata tooling only. None of these stages implements USB writes, P-HPR routing, or real P-HPR output.

## Required Approval Phrase

No real P-HPR USB writes, output reports, write-capable feature reports, or real vibration commands may be implemented or executed until the user says exactly:

```text
I approve Phase 2 controlled P-HPR write testing
```

That phrase has not been provided as of Stage 2H.

Stage 2B keeps `PHprSafetyLimits.AllowRealDeviceWrites` false by default, and `MockPhprOutputDevice` only records mock commands in memory.

Stage 2F does not call `MockPhprOutputDevice`, `IPHprOutputDevice`, or `PHprCommand`; accepted shift-intent events are diagnostics only.

Stage 2G does not reference the P-HPR output abstraction project, call `MockPhprOutputDevice`, call `IPHprOutputDevice`, create `PHprCommand`, send output reports, send feature writes, or open P700/P-HPR device handles for control.

Stage 2H does not parse USB captures, analyze USB transfers, hypothesize protocol bytes, call `MockPhprOutputDevice`, call `IPHprOutputDevice`, create `PHprCommand`, send output reports, send feature reports, send HID writes, or open P700/P-HPR device handles for control. Its CLI commands create templates, validate metadata, and export sanitized manifests only.

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
- Capture analysis tooling using synthetic fixtures in later stages.
- Protocol hypotheses.
- Mock P-HPR output.
- Mock protocol.
- Mock routing.
- UI placeholders.
- Diagnostics.
- Tests.

## Forbidden Before Approval

- Real P-HPR USB writes.
- Real P-HPR vibration commands.
- Real Simagic device output reports.
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
- Hardware tests manual and skipped by default.

No telemetry-driven real P-HPR output may occur until manual low-amplitude pulse testing has passed.

## First Controlled Write Limits After Approval

If and only if the exact approval phrase is provided and protocol confidence is sufficient, the first controlled write test must use:

- Strength <= 10%.
- Duration <= 100 ms.
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

- Detect if SimPro Manager V3 is running.
- Detect if SimHub is running.
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
