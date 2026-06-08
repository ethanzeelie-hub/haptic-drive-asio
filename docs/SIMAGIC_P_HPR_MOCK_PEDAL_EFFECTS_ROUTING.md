# Simagic P-HPR Mock Pedal Effects Routing

## Stage 2N Purpose

Stage 2N adds mock-only P-HPR routing for road vibration, wheel slip, and wheel lock from the existing `VehicleState` / `HapticPipelineSnapshot` path.

The routing flow is:

```text
F1 25 telemetry / latest VehicleState
-> PHprPedalEffectsRouter
-> SafetyLimitedPhprOutputDevice
-> MockPhprOutputDevice
-> in-memory mock command/frame diagnostics
```

Stage 2N does not send real USB commands and does not vibrate real hardware.

## Mock-Only Safety Boundary

- Mock only.
- No real P-HPR output.
- No USB writes.
- No HID output reports.
- No HID feature reports.
- No production protocol adapter.
- No Simagic/P700/P-HPR device handle write access.
- No SimPro Manager or SimHub process detection or control.
- No ASIO/BST-1 audio routing.
- No change to the existing ASIO road/slip/lock audio effects.

Commands route only through `SafetyLimitedPhprOutputDevice` wrapping `MockPhprOutputDevice`.

## Effect Defaults

Road vibration:

- enabled by default,
- target `Both`,
- strength range `0.01` to `0.04` before safety clamp,
- frequency range `25 Hz` to `45 Hz`,
- duration `50 ms`,
- priority below slip and lock,
- source `RoadTexture`.

Wheel slip:

- enabled by default,
- target `Throttle`,
- strength range `0.03` to `0.08` before safety clamp,
- frequency range `45 Hz` to `75 Hz`,
- duration `50 ms`,
- priority above road and below lock,
- source `WheelSlip`.

Wheel lock:

- enabled by default,
- target `Brake`,
- strength range `0.04` to `0.10` before safety clamp,
- frequency range `60 Hz` to `90 Hz`,
- duration `50 ms`,
- priority above road and slip,
- source `WheelLock`.

## Routing Behaviour

The WPF app evaluates pedal effects from the existing telemetry/status timer using the latest pipeline snapshot. The router does not run on the audio callback, does not create a high-frequency background loop, and does not perform UI, disk, network, logging, or graphing work in the routing path.

The router uses existing `VehicleState` fields:

- surface IDs and speed for road vibration,
- wheel slip ratio/angle, speed, throttle, brake, and traction-control state for wheel slip,
- brake input, wheel slip ratio, wheel speed, speed, and ABS state for wheel lock.

It does not parse any new F1 25 packet fields.

Priority is per target module:

1. wheel lock,
2. wheel slip,
3. road vibration.

If multiple active effects target the same module, the higher-priority effect wins for that module. A lower-priority effect may still route to the other module if it is available. A deterministic minimum interval suppresses repeated commands for the same effect/module to avoid command storms.

## Coexistence With Gear Routing

Stage 2M gear pulse routing continues to work.

In the WPF app, gear routing and pedal effects share one mock P-HPR output stack:

- `MockPhprOutputDevice`,
- `SafetyLimitedPhprOutputDevice`,
- `PHprGearPulseRouter`,
- `PHprPedalEffectsRouter`.

This keeps mock command/frame counts, pending scheduled stops, safety state, and emergency stop global for the mock P-HPR path. Clearing diagnostics on either mock router clears the shared mock output history.

## Safety Context Behaviour

The app builds a mock safety context from:

- mock output connection state,
- brake/throttle mock module availability,
- telemetry stale mute state,
- haptics running/stopped state,
- emergency mute state,
- cached `DrivingArmed` state,
- mock emergency-stop state,
- `SoftwareConflictStatus.Clear`,
- and `RequiresRealDeviceWrites = false`.

Stage 2L safety remains authoritative. Telemetry stale, haptics stopped, emergency mute active, `DrivingArmed` false, unavailable modules, disconnected mock output, active emergency stop, and real-write requests block start commands.

## Diagnostics And Persistence

The WPF Devices and Diagnostics pages show:

- pedal effects routing enabled/disabled,
- road/slip/lock enabled state,
- road/slip/lock target, strength, frequency, and duration,
- route counts per effect,
- safety rejected counts per effect,
- interval suppression counts per effect,
- last active effect,
- last target module,
- last `PHprCommand` summary,
- last routing result,
- last safety decision and violation,
- mock output command/frame counts,
- pending scheduled stops,
- emergency stop state,
- and explicit mock-only/no-hardware-output text.

Persisted settings are limited to:

- global pedal effects enabled,
- road/slip/lock enabled,
- road/slip/lock target,
- road/slip/lock strength/frequency/duration.

Emergency-stop state, safety latch state, mock command history, mock frame history, real-write approval, real-write enabled state, and real-write armed state are not persisted.

## Test Coverage

Stage 2N adds hardware-free tests for:

- disabled router suppression,
- per-effect enable flags,
- default road/slip/lock targets,
- priority per target module,
- Stage 2M gear router guard compatibility,
- safety-limited output routing,
- stale telemetry, emergency mute, haptics stopped, and `DrivingArmed` blocking,
- safety clamping,
- mock output command/frame/pending-stop diagnostics,
- deterministic minimum interval suppression,
- emergency stop and clear behaviour,
- and absence of USB/HID/ASIO/write API surface.

## Final Statement

Stage 2N is mock-only. It does not validate physical P-HPR feel, safe hardware gain, physical latency, or final frequency tuning.
