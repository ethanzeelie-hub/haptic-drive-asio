# Simagic P-HPR Instant Shift Guide

## Phase 3B Status

Phase 3B completes the production integration for instant GT Neo paddle gear pulses through the existing P-HPR direct-output backend.

The feature path is:

```text
mapped GT Neo paddle press
-> cached DrivingArmed/Menu Safe gate
-> accepted ShiftIntentEvent
-> PHprDirectGearPulseRouter
-> PHprSafetyLimiter
-> mock or gated real P-HPR output
```

The hot path does not wait for F1 25 telemetry gear-change confirmation and does not fire a default second confirmation pulse. F1 25 telemetry is still used to maintain cached `DrivingArmed` state for menu suppression, stale-telemetry suppression, diagnostics, and future optional rejected-shift behavior.

## Pedal Settings

Brake and throttle gear pulses are configured independently:

- enabled or disabled,
- strength,
- frequency,
- duration.

Upshift and downshift use the same default pulse. Direction is retained in diagnostics, but there is no separate default pulse shape for left/right paddles.

The conservative real direct defaults remain:

- strength `0.10` / `10%`,
- frequency `50 Hz`,
- duration `50 ms`,
- both brake and throttle enabled.

All real direct settings are normalized through the direct-control safety limits before use or persistence.

## Persistence

The app persists safe gear-pulse preferences:

- brake enabled/strength/frequency/duration,
- throttle enabled/strength/frequency/duration.

The app does not persist:

- real direct-control enabled state,
- real direct-control armed state,
- selected private HID device path,
- emergency-stop latch,
- command/write history,
- validation result data.

Real direct control still starts disabled and unarmed every launch.

## Runtime Gates

A real gear pulse can write only when all direct-control gates pass:

- direct control enabled for the current session,
- direct control armed for the current session,
- selected device/interface/report configured,
- SimPro/SimHub coexistence status is `Clear`,
- emergency stop is clear,
- cached `DrivingArmed` accepted the paddle event,
- safety limiter accepts the command.

Mock mode remains available for diagnostics without hardware output. Real mode is default-off and requires explicit manual enable and arm.

## Latency Diagnostics

Phase 3B records timestamps for the instant gear-pulse route:

- paddle event time,
- accepted shift-intent time,
- command creation time,
- first write completion time,
- per-command trace times.

These diagnostics are software timestamps for routing visibility and fake-writer tests. They are not physical P-HPR latency measurements.

## Out Of Scope

Phase 3B itself does not route road vibration, wheel slip, or wheel lock to real P-HPR output. Phase 3C and Phase 3D add those telemetry-driven routes separately.

Phase 3B does not prove:

- physical brake/throttle mapping,
- safe real strength,
- physical stop behavior,
- physical latency,
- SimPro/SimHub real-device coexistence,
- sustained-vibration behavior.

Automated verification uses fake HID writers only and does not send hardware output.
