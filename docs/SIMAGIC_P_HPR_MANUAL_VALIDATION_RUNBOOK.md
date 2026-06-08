# Simagic P-HPR Manual Validation Runbook

## Purpose

This runbook defines how Ethan should manually validate P-HPR direct control after the gated Stage 2Q implementation.

Stage 2Q does not execute this runbook automatically. A gated real adapter and manual pulse UI exist, but no real P-HPR hardware output has been validated by Codex.

Stage 2R adds a Devices-page validation harness for checklist status and private local result export. The harness does not trigger hardware output.

## Before Starting

Confirm:

- the user is physically present,
- the P700 is connected,
- brake and throttle P-HPR modules are installed,
- SimPro Manager is closed unless testing coexistence,
- SimHub is closed unless testing coexistence,
- Haptic Drive ASIO is running,
- emergency stop is visible,
- real writes are disabled by default,
- direct control is not armed on startup,
- selected device/interface/report are known,
- safety limits are visible,
- and the latest commit hash is recorded.

## First Manual Brake Pulse

1. Open Haptic Drive ASIO.
2. Confirm P-HPR direct control is disabled.
3. Confirm coexistence status is `Clear`.
4. Select the P700/P-HPR device path, interface label, optional report ID, and report length.
5. Enable direct-control mode.
6. Arm direct control.
7. Trigger one brake pulse only.
8. Use strength `<= 10%`.
9. Use duration `<= 100 ms`.
10. Use conservative frequency, initially `50 Hz` unless later evidence changes it.
11. Verify the brake module vibrates.
12. Verify the throttle module does not vibrate.
13. Verify vibration stops.
14. Record the result.
15. Export private local notes from the P-HPR Controlled Validation Harness.

## First Manual Throttle Pulse

Repeat the brake procedure for throttle only:

1. Confirm direct control is still explicitly enabled and armed.
2. Trigger one throttle pulse only.
3. Verify the throttle module vibrates.
4. Verify the brake module does not vibrate.
5. Verify vibration stops.
6. Record the result.

## Emergency Stop Test

1. Trigger one low-strength pulse.
2. Press emergency stop while the pulse is active.
3. Confirm stop frames are requested for brake and throttle.
4. Confirm vibration stops.
5. Confirm the safety latch prevents further starts until cleared.
6. Record the result.

## Gate Tests

Run these after one-pulse brake/throttle behavior is correct:

- telemetry stale gate blocks starts,
- emergency mute gate blocks starts,
- `DrivingArmed` false blocks starts,
- haptics stopped blocks starts,
- SimPro/SimHub `ActiveConflict` blocks starts,
- SimPro/SimHub `Unknown`, `SimProRunning`, or `SimHubRunning` also block real starts,
- no real write occurs when direct control is disabled,
- no real write occurs when direct control is unarmed,
- and no real write occurs on app startup.

## Result Template

Save private local results under an ignored path in a later validation stage, such as `manual-validation/private/`.

Stage 2R exports private local Markdown results under `local-validation-results/` when the repo root is available.

```text
Date/time:
App branch/commit:
P700 connected: yes/no
P-HPR brake module installed: yes/no
P-HPR throttle module installed: yes/no
SimPro Manager status:
SimHub status:
Selected device/interface/report:
Direct control enabled manually: yes/no
Direct control armed manually: yes/no
Brake pulse settings:
Brake pulse result:
Throttle pulse settings:
Throttle pulse result:
Stop result:
Emergency stop result:
Telemetry stale gate result:
Emergency mute gate result:
DrivingArmed gate result:
SimPro conflict gate result:
Wrong pedal behavior:
Sustained vibration behavior:
Unexpected behavior:
Notes:
Pass/fail decision:
```

Do not include raw captures, serial numbers, unsanitized device paths, or private USB inventories in committed results.

## Stop Conditions

Abort immediately if:

- wrong pedal vibrates,
- both pedals vibrate unexpectedly,
- vibration continues after stop,
- output is stronger than expected,
- the app becomes unresponsive,
- the selected device disconnects,
- SimPro/SimHub conflict appears,
- the selected report/interface is uncertain,
- or emergency stop does not stop output.

## Validation Status

As of Stage 2Q:

- validation runbook implemented,
- readiness diagnostics implemented,
- gated real adapter implemented,
- manual Devices-page direct-control UI implemented,
- physical validation pending local user run,
- no real hardware validation has been recorded,
- no real hardware vibration has been executed by Codex.
