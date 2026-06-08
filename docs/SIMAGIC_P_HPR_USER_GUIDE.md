# Simagic P-HPR User Guide

## Status

Stage 2Q adds a gated direct-control UI and write-capable adapter for later manual testing. Phase 3A hardens that adapter with explicit writer lifecycle, timeout handling, disconnect diagnostics, report validation, and close-on-dispose behavior. Phase 3B completes instant paddle gear-pulse production integration through that same gated backend. Phase 3C adds road-vibration production routing through the same gated backend.

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
- Brake and throttle road-vibration settings: enabled, minimum strength, maximum strength, minimum frequency, maximum frequency, and duration.
- `Test Brake Pulse` and `Test Throttle Pulse`: one manual pulse only, no loop.
- `Emergency Stop`: attempts brake and throttle stop reports when a device is selected, then latches.
- `Clear Emergency Stop`: clears the latch, but does not enable or arm direct control.

The pulse buttons remain disabled until direct control is enabled, armed, a device is selected, coexistence status is `Clear`, and emergency stop is clear.

Phase 3A diagnostics include connection state, writer-open state, open/close counts, last open/write/stop/close status, disconnect count, timeout count, invalid-report count, and write timeout. Phase 3B adds last gear-pulse latency diagnostics: paddle event time, accepted shift-intent time, command creation time, write completion time, and per-command traces. Phase 3C adds real road-vibration enabled state, per-pedal road settings, and last road route result diagnostics. These diagnostics do not auto-run output and do not prove physical latency or road feel.

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

Brake and throttle gear-pulse settings are independent. Each pedal can be enabled or disabled and can use its own strength, frequency, and duration. Upshift and downshift use the same default pulse; the direction is still visible in diagnostics.

Safe gear-pulse preferences are persisted. Direct-control enablement, arming, selected HID path, emergency-stop latch, command history, write history, and validation result data are not persisted.

## Road Vibration Routing

Real road vibration is disabled by default.

When enabled, the app can route road vibration to brake, throttle, or both pedals. Each pedal has independent minimum/maximum strength, minimum/maximum frequency, and duration settings. The route scales between those values from the current road intensity.

Road vibration requires direct control to be enabled and armed for the current session. It is also blocked by stale telemetry, stopped haptics, emergency mute, cached `DrivingArmed` false, SimPro/SimHub conflict, missing selected output, emergency stop, safety-limiter rejection, and the deterministic route interval.

The ASIO/BST-1 road texture effect remains separate and unchanged.

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

Stage 2Q through Phase 3C do not prove physical pedal mapping, safe output strength, real stop behavior, sustained-vibration behavior, SimPro/SimHub coexistence on the device, report descriptor details, road feel, or latency.
