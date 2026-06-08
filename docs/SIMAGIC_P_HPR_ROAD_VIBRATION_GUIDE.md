# Simagic P-HPR Road Vibration Guide

## Phase 3C Status

Phase 3C adds production road-vibration routing for the P-HPR path while preserving the existing ASIO/BST-1 road texture effect.

The production road path is:

```text
F1 25 telemetry / VehicleState
-> cached DrivingArmed/Menu Safe state
-> PHprRoadVibrationRouter
-> PHprSafetyLimiter
-> mock or gated real P-HPR output
```

The ASIO/BST-1 road texture path remains separate:

```text
VehicleState
-> RoadTextureEffect
-> mixer and audio safety chain
-> ASIO/BST-1 or Null output
```

Neither path blocks the other, and the P-HPR router does not use `IAudioOutputDevice`.

## Real Road Settings

Real road vibration starts disabled by default.

When enabled, brake and throttle road vibration are configured independently:

- enabled or disabled,
- minimum strength,
- maximum strength,
- minimum frequency,
- maximum frequency,
- duration.

The router scales strength and frequency from road intensity between each pedal's minimum and maximum values. Defaults are conservative:

- strength `0.01` to `0.04`,
- frequency `25 Hz` to `45 Hz`,
- duration `50 ms`,
- priority `10`.

Road priority stays below gear pulse, wheel slip, and wheel lock.

## Runtime Gates

Real road vibration can write only when:

- real road vibration is enabled,
- real direct control is enabled and armed for the current session,
- a P-HPR HID device/interface/report is selected,
- SimPro/SimHub coexistence status is `Clear`,
- emergency stop is clear,
- haptics are running,
- telemetry is fresh,
- cached `DrivingArmed` is true,
- the safety limiter accepts the command,
- the deterministic route interval allows another command.

The route is evaluated from the existing telemetry/status update path, not the audio callback.

## Persistence

The app persists safe real road-vibration preferences:

- global real road-vibration enabled state,
- brake road settings,
- throttle road settings.

The app does not persist:

- direct-control enablement,
- direct-control arming,
- selected private HID device path,
- emergency-stop latch,
- command/write history,
- validation result data.

## Out Of Scope

Phase 3C itself did not route wheel slip or wheel lock to real P-HPR output. Phase 3D now adds that route separately through `PHprSlipLockRouter`.

When Phase 3D slip/lock routing is enabled, road remains lower priority and yields in the same WPF routing tick after a slip/lock command routes.

Phase 3C does not prove:

- physical brake/throttle mapping,
- safe real strength,
- physical stop behavior,
- physical latency,
- sustained-vibration behavior,
- SimPro/SimHub real-device coexistence.

Automated verification uses mock output and fake HID writers only. No real hardware output is executed.
