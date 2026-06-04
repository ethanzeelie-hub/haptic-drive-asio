# Simagic Shift Intent Design

Stage 2A captures the design for low-latency gear-pulse intent. Stage 2B defines the input abstraction models and interfaces only; it does not implement a listener, router, or output path.

## Default Event Flow

```text
GT Neo paddle press
-> read-only input event
-> ShiftIntentEvent
-> cached DrivingArmed gate
-> immediate mock/P-HPR gear pulse
```

The paddle event path must not block waiting for a fresh F1 25 telemetry packet.

## Default Mode

`InstantPaddleOnly` is the default future mode.

Behavior:

- Left or right paddle press triggers a gear pulse immediately when `DrivingArmed` is true.
- The event uses cached driving state only.
- The event does not wait for telemetry confirmation.
- The event does not fire a second normal telemetry-confirmed pulse by default.
- Left and right paddles use the same pulse by default.

## Other Planned Modes

`TelemetryConfirmedOnly`:

- Triggers only when F1 25 telemetry confirms gear changed.
- Matches the existing Phase 1 ASIO gear-effect style for comparison/debugging.

`InstantWithRejectedShiftFeedback`:

- Fires the immediate paddle pulse first.
- Later inspects telemetry to determine whether gear changed within a configurable window.
- May optionally fire a subtle rejected-shift pulse if the gear did not change.
- Must never delay the initial pulse.
- Must never fire a second normal confirmation pulse by default.

Rejected shifts are expected to be rare, mostly during aggressive downshifts such as 4->3 or 3->2 when speed/revs are too high. Rejected-shift feedback should start disabled or subtle.

## DrivingArmed Gate

Future paddle event handling should be:

```text
if paddlePressed && DrivingArmed:
    fire shift pulse immediately
else:
    suppress pulse and record diagnostic reason
```

`DrivingArmed` must be cached continuously from the latest telemetry-derived state. It should default false until recent valid telemetry proves active driving.

Recommended future inputs:

- Recent valid telemetry.
- Telemetry age below configurable threshold.
- Not paused.
- Network pause not active.
- Not garage/menu/session results.
- Player status active enough for driving.
- Speed, RPM, gear, pit/driver status, session state, and result state consistent with on-track or pit-lane driving.
- Low-speed start-line or pit-lane active driving allowed when telemetry indicates active driving.

Recommended options:

- Menu Safe Mode enabled by default.
- Require recent telemetry for paddle haptics enabled by default.
- Telemetry freshness threshold.
- Allow zero-speed active driving.
- Diagnostics-only override for testing, clearly labelled unsafe for menus.

## Event Data

A future `ShiftIntentEvent` should include:

- Paddle side.
- Press timestamp.
- Source device identity where available.
- Event sequence.
- `DrivingArmed` at event time.
- Suppression reason when not accepted.
- Last telemetry gear for diagnostics.
- Correlation ID for optional telemetry confirmation/rejection.

## Routing

A future accepted `ShiftIntentEvent` should be able to route to:

- Brake P-HPR.
- Throttle P-HPR.
- Both P-HPR modules.
- BST-1 / ASIO gear effect later if suitable.

P-HPR output must not block ASIO output, and ASIO output must not block P-HPR output.
