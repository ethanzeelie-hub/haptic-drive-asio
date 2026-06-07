# Simagic Shift Intent Design

Stage 2A captures the design for low-latency gear-pulse intent. Stage 2B defines the input abstraction models and interfaces only. Stage 2C adds the cached `DrivingArmedStateService`. Stage 2D adds read-only wheel / paddle input discovery and candidate scoring. Stage 2E adds a read-only Windows game-controller paddle listener with manual left/right mapping diagnostics. Stage 2F implements the Shift Intent Event Layer for accepted/suppressed diagnostics. Stage 2H adds capture workflow and metadata tooling for later protocol research. Stage 2I adds read-only capture analysis. Stage 2J adds protocol hypotheses. Stage 2K adds mock-only P-HPR protocol/output modelling. Stage 2L adds mock-only P-HPR safety limiting. Stage 2M adds mock-only gear pulse routing from accepted shift intents through the Stage 2L safety-limited mock output path. These stages do not implement real P-HPR output or any live hardware write path.

## Default Event Flow

```text
GT Neo paddle press
-> read-only input event
-> ShiftIntentEvent
-> cached DrivingArmed gate
-> accepted/suppressed diagnostics
-> Stage 2M mock-only gear pulse routing
```

The paddle event path must not block waiting for a fresh F1 25 telemetry packet.

## Default Mode

`InstantPaddleOnly` is the default mode.

Behavior:

- Left or right paddle press creates an accepted `ShiftIntentEvent` immediately when `DrivingArmed` is true.
- The event uses cached driving state only.
- The event does not wait for telemetry confirmation.
- The event does not fire a second normal telemetry-confirmed pulse by default.
- Stage 2F records the accepted event for diagnostics, and Stage 2M may route that accepted event to mock-only P-HPR gear pulse diagnostics.
- Future routing should use the same pulse for left and right by default while retaining direction in diagnostics.

## Other Planned Modes

`TelemetryConfirmedOnly`:

- Stage 2F observes mapped paddle presses diagnostically and suppresses immediate accepted intent.
- The suppression reason states that telemetry-confirmed-only mode is active.
- The existing Phase 1 ASIO gear-effect style remains separate for comparison/debugging.

`InstantWithRejectedShiftFeedback`:

- Emits immediate accepted intent first when `DrivingArmed` is true.
- Later inspects telemetry to determine whether gear changed within a configurable window.
- May optionally fire a subtle rejected-shift pulse if the gear did not change.
- Must never delay the initial pulse.
- Must never fire a second normal confirmation pulse by default.
- Stage 2F records a pending-confirmation diagnostic count only; no rejected-shift output is implemented yet.

Rejected shifts are expected to be rare, mostly during aggressive downshifts such as 4->3 or 3->2 when speed/revs are too high. Rejected-shift feedback should start disabled or subtle.

## DrivingArmed Gate

Future paddle event handling should be:

```text
if paddlePressed && DrivingArmed && mode allows immediate intent:
    accept ShiftIntentEvent immediately
else:
    suppress intent and record diagnostic reason
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

Stage 2C implements these options in `DrivingArmedStateServiceOptions`.

## Event Data

Stage 2F `ShiftIntentEvent` includes:

- Paddle side.
- Press timestamp.
- Source device identity where available.
- Event sequence.
- `DrivingArmed` at event time.
- Shift direction (`Left` = `Downshift`, `Right` = `Upshift`).
- Source (`WheelPaddle`, `TelemetryGearChange`, or `Test`).
- Mode.
- Stopwatch ticks.
- Source button ID.
- Last telemetry gear for diagnostics where available.
- Last known speed, RPM, session time, and frame identifier where available.
- Correlation ID for optional telemetry confirmation/rejection.

Suppression state is kept in `ShiftIntentEvaluationResult` and `ShiftIntentDiagnosticsSnapshot`, not emitted as an accepted event.

## Stage 2E Paddle Diagnostics And Stage 2F Evaluation

Stage 2E uses Stage 2D `InputDeviceDiscoverySnapshot` values to let the user select a Windows game-controller device for the Alpha Evo / GT Neo path.

Implemented Stage 2E diagnostics:

- selected input device,
- selected input method,
- manual left/right button mapping,
- last changed raw button,
- left/right current state,
- rising-edge mapped paddle press events,
- conservative debounce,
- UTC and stopwatch timestamps,
- input event count,
- listener error and disconnect state,
- safe app-settings persistence for mapping only.

Stage 2E still needs user mapping data before reliable routing:

- left paddle button number,
- right paddle button number,
- device display name shown by Windows,
- whether the paddles appear through Raw Input, the Windows controller panel, both, or neither.

No hardware-derived `ShiftIntentEvent` is raised by Stage 2E. No haptic output is triggered by mapped paddle presses.

Stage 2F converts mapped paddle diagnostics into `ShiftIntentEvaluationResult` values and accepted `ShiftIntentEvent` values when allowed by cached `DrivingArmed` state and mode, still without real or mock P-HPR output.

Implemented Stage 2F diagnostics:

- shift intent enabled state,
- current `ShiftIntentMode`,
- cached `DrivingArmed` state and reason,
- telemetry age,
- menu-safe and require-recent-telemetry state,
- last paddle side,
- last direction,
- last paddle event time,
- last accepted event,
- accepted and suppressed counters,
- last suppression reason,
- last known telemetry gear, speed, RPM, and frame,
- pending confirmation count for the future rejected-feedback mode,
- and last evaluation error.

Stage 2F persists only shift-intent enabled state and mode. It does not persist haptics running state, emergency mute state, real P-HPR approval, or output state.

## Routing

Stage 2F does not directly route accepted `ShiftIntentEvent` values to haptics. Stage 2M adds a separate `PHprGearPulseRouter` that routes accepted events to mock P-HPR gear pulses through the Stage 2L safety layer.

Stages 2H through 2L do not change this routing boundary. Stage 2H creates capture metadata workflow tooling, Stage 2I analyzes captures read-only, Stage 2J documents hypotheses, Stage 2K creates mock protocol/output diagnostics, and Stage 2L creates mock safety limiting. They do not route accepted shift intents.

An accepted `ShiftIntentEvent` can now route in mock mode to:

- Brake P-HPR.
- Throttle P-HPR.
- Both P-HPR modules.
- BST-1 / ASIO gear effect later if suitable, but Stage 2M does not do this.

P-HPR output must not block ASIO output, and ASIO output must not block P-HPR output.

Stage 2F/2M safety confirmations:

- Stage 2F still has no `MockPhprOutputDevice` call, `IPHprOutputDevice` call, or `PHprCommand` creation.
- Stage 2M creates `PHprCommand` values only for safety-limited mock output through `PHprGearPulseRouter`.
- No real P-HPR output.
- No ASIO gear pulse from paddle input.
- No `GearShiftEffect` call from paddle input.
- No telemetry wait on the paddle event path.
- No disk IO, network IO, or audio rendering on the paddle event path.
