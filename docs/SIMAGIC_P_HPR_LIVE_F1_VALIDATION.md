# Simagic P-HPR Live F1 25 Validation Workflow

Phase 3G adds the manual live F1 25 validation workflow for P-HPR features. It is a checklist and diagnostics workflow only. It does not claim that live F1 25 sessions, real P-HPR hardware, pedal mapping, physical latency, safe gain, stop behavior, road feel, slip feel, or lock feel have been validated.

Automated tests remain fake-only and mock-only. Do not run unattended real P-HPR output.

## App Workflow

The Devices page now includes `P-HPR Live F1 Validation`.

The live checklist is generated from existing runtime diagnostics:

- live F1 25 telemetry input source,
- UDP receiver state and packet count,
- parser success count,
- telemetry age and stale mute state,
- cached `DrivingArmed` state and reason,
- paddle listener state,
- shift-intent enabled state and accepted/suppressed counts,
- P-HPR output mode,
- selected output readiness,
- SimPro/SimHub coexistence state,
- emergency-stop state,
- real road-vibration enabled state,
- real slip/lock enabled state.

The Diagnostics page includes the same checklist summary in copyable diagnostics. It prints selected-output readiness only, not raw private HID device paths or serial numbers.

## Manual Live Test Order

Run these steps locally only when physically present:

1. Open the app and confirm real direct control starts disabled and unarmed.
2. Start F1 25 UDP telemetry and confirm live packet/parser counts increase.
3. Enter a driving session and confirm `DrivingArmed` is true.
4. Press GT Neo paddles and confirm shift intent accepts the mapped presses.
5. Use mock mode first and confirm gear-pulse diagnostics update.
6. Enable and arm real direct mode manually only after mock validation is sane.
7. Run one low-strength brake gear-pulse test and one low-strength throttle gear-pulse test.
8. Test road vibration only after gear pulse and stop behavior are safe.
9. Test slip/lock only if safe and controllable.
10. Pause, tab out, enter menus/garage/results, and confirm paddle pulses are suppressed.
11. Press emergency stop and confirm both modules stop before clearing the latch.
12. Start SimPro Manager or SimHub only for conflict testing and confirm warnings/blocking appear.

## Required Non-Claims

Until Ethan performs and records a supervised local run:

- physical validation is pending,
- physical latency is unknown,
- safe strength/gain is unknown,
- real brake/throttle module mapping is unconfirmed,
- wrong-pedal behavior is unconfirmed,
- sustained-vibration behavior is unconfirmed,
- real SimPro/SimHub coexistence behavior is unconfirmed,
- live menu/pause/garage/result suppression may need refinement.

## Automation Boundary

Phase 3G tests exercise the passive checklist builder and diagnostics text. They do not open HID devices, send output reports, send feature reports, vibrate P-HPR modules, control SimPro/SimHub, or require F1 25 to be running.

