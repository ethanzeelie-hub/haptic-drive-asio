# Simagic P-HPR User Guide

## Status

Stage 2Q adds a gated direct-control UI and write-capable adapter for later manual testing. Phase 3A hardens that adapter with explicit writer lifecycle, timeout handling, disconnect diagnostics, report validation, and close-on-dispose behavior.

No real P-HPR hardware validation has been performed by Codex. Do not treat any default as physically validated.

## Devices Page Controls

Open the Devices page and find `P-HPR Real Direct Control`.

The section starts disabled and unarmed every app launch. The selected device path is not saved.

Controls:

- `Enable real direct control`: allows the direct-control path to be considered.
- `Arm direct control`: required in addition to enablement; cleared when enablement is off and never persisted.
- `Device path`: manually selected Windows HID path for the P700/P-HPR interface.
- `Interface`: short manual label for the selected interface.
- `Report ID`: optional report ID if later descriptor evidence requires one.
- `Report bytes`: expected report length; Stage 2Q encoder emits 64-byte SimHub F1 EC payloads.
- Brake and throttle pulse settings: enabled, strength, frequency, and duration.
- `Test Brake Pulse` and `Test Throttle Pulse`: one manual pulse only, no loop.
- `Emergency Stop`: attempts brake and throttle stop reports when a device is selected, then latches.
- `Clear Emergency Stop`: clears the latch, but does not enable or arm direct control.

The pulse buttons remain disabled until direct control is enabled, armed, a device is selected, coexistence status is `Clear`, and emergency stop is clear.

Phase 3A diagnostics include connection state, writer-open state, open/close counts, last open/write/stop/close status, disconnect count, timeout count, invalid-report count, and write timeout. These diagnostics do not auto-run output.

## First Safe Manual Settings

For later supervised local validation:

- one pedal at a time,
- strength no higher than `0.10`,
- duration no higher than `100 ms`,
- conservative frequency such as `50 Hz`,
- no loop,
- emergency stop visible,
- SimPro Manager closed,
- SimHub closed,
- selected device/interface/report confirmed.

If the wrong pedal moves, both pedals move unexpectedly, output feels too strong, vibration does not stop, the app stalls, or the device disconnects, use emergency stop and stop testing.

## Gear Pulse Routing

Accepted GT Neo paddle shift intent can route to the direct P-HPR adapter only when direct control is explicitly enabled and armed for the current session.

Default future gear-pulse behavior remains `InstantPaddleOnly`:

```text
mapped paddle press
-> cached DrivingArmed/Menu Safe gate
-> accepted ShiftIntentEvent
-> PHprDirectGearPulseRouter
-> safety limiter
-> real direct adapter
```

There is no telemetry gear-confirmation wait and no default second confirmation pulse.

## Controlled Validation Harness

Stage 2R adds a `P-HPR Controlled Validation Harness` section on the Devices page.

It does not trigger hardware output. It evaluates readiness and exports private local notes.

Use it to record:

- user present,
- P700 connected,
- brake and throttle modules installed,
- selected device/interface/report,
- brake pulse result,
- throttle pulse result,
- emergency stop result,
- paddle upshift result,
- paddle downshift result,
- wrong-pedal behavior,
- sustained-vibration behavior,
- notes,
- pass/fail decision.

If `pass` is entered, export is blocked until the required fields and hardware confirmations are complete.

Private exports go under `local-validation-results/` when the repo root is available. Do not commit those results.

## What Is Not Saved

These runtime states are not persisted:

- direct control enabled,
- direct control armed,
- selected P-HPR HID device path,
- emergency stop latch,
- command history,
- write history,
- safety latch state.

Mock routing preferences and input mapping remain separate from real direct-control arming.

## What Stage 2Q Does Not Prove

Stage 2Q/Phase 3A do not prove physical pedal mapping, safe output strength, real stop behavior, SimPro/SimHub coexistence on the device, report descriptor details, or latency.
