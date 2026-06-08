# Simagic P-HPR Slip / Lock Guide

## Phase 3D Status

Phase 3D adds production wheel-slip and wheel-lock routing for the P-HPR path while preserving the existing ASIO/BST-1 slip and brake-lock audio effects.

The production slip/lock path is:

```text
F1 25 telemetry / VehicleState
-> cached DrivingArmed/Menu Safe state
-> PHprSlipLockRouter
-> PHprSafetyLimiter
-> mock or gated real P-HPR output
```

The ASIO/BST-1 path remains separate:

```text
VehicleState
-> SlipEffect
-> mixer and audio safety chain
-> ASIO/BST-1 or Null output
```

The P-HPR router does not use ASIO, `IAudioOutputDevice`, the audio mixer, or the audio render callback.

## Default Effects

Real slip/lock routing starts disabled by default.

When enabled, the defaults are:

- wheel slip: throttle pedal, strength `0.03` to `0.08`, frequency `45 Hz` to `75 Hz`, duration `50 ms`, priority `50`;
- wheel lock: brake pedal, strength `0.04` to `0.10`, frequency `60 Hz` to `90 Hz`, duration `50 ms`, priority `75`.

Wheel lock priority is above wheel slip. Both are above road vibration and below instant gear pulse.

Each effect can be enabled or disabled independently and can target brake, throttle, or both pedals. Settings are clamped to the current P-HPR safety limits before routing.

## Runtime Gates

Real wheel slip or wheel lock can write only when:

- real slip/lock routing is enabled,
- the individual effect is enabled,
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

The app persists safe real slip/lock preferences:

- global real slip/lock enabled state,
- wheel-slip target and strength/frequency/duration settings,
- wheel-lock target and strength/frequency/duration settings.

The app does not persist:

- direct-control enablement,
- direct-control arming,
- selected private HID device path,
- emergency-stop latch,
- command/write history,
- validation result data.

## Road Interaction

Real road vibration remains lower priority than wheel slip and wheel lock. In the WPF routing tick, road routing yields when a higher-priority slip/lock command has just routed, which avoids competing same-tick pedal commands.

## Out Of Scope

Phase 3D does not prove:

- physical brake/throttle mapping,
- safe real strength,
- physical stop behavior,
- physical latency,
- sustained-vibration behavior,
- physical slip or lock feel,
- SimPro/SimHub real-device coexistence.

Automated verification uses mock output and fake HID writers only. No real hardware output is executed.
