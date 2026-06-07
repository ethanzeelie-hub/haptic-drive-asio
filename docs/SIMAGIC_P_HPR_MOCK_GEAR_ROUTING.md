# Simagic P-HPR Mock Gear Routing

## Stage 2M Purpose

Stage 2M adds mock-only gear pulse routing from accepted `ShiftIntentEvent` values to the existing Stage 2L safety-limited mock P-HPR output path.

The routing flow is:

```text
ShiftIntentEvent
-> PHprGearPulseRouter
-> SafetyLimitedPhprOutputDevice
-> MockPhprOutputDevice
-> in-memory mock command/frame diagnostics
```

Stage 2M does not send real USB commands and does not vibrate real hardware.

## Mock-Only Safety Boundary

- Mock only.
- No real P-HPR output.
- No USB writes.
- No HID output reports.
- No HID feature reports.
- No production protocol adapter.
- No Simagic/P700/P-HPR device handle write access.
- No SimPro Manager or SimHub control.
- No ASIO/BST-1 audio routing.
- No road, wheel-slip, or wheel-lock P-HPR routing.

Accepted shift intents route only through `SafetyLimitedPhprOutputDevice` wrapping `MockPhprOutputDevice`.

## Routing Flow

`PHprGearPulseRouter.RouteAsync` accepts an already accepted `ShiftIntentEvent`.

Behavior:

- Disabled router: records an ignored route and sends no command.
- Missing event: records an ignored route and sends no command.
- Event not accepted by `DrivingArmed`: records an ignored route and sends no command.
- Accepted event: creates a conservative `PHprCommand`, applies the supplied/current safety context, and sends it through `SafetyLimitedPhprOutputDevice`.
- Safety rejection: no mock active frames are generated, and diagnostics record the safety violation.

The WPF app calls the router only when `ShiftIntentProcessor.HandlePaddleInput` returns `WasAccepted` with a non-null `ShiftIntentEvent`.

## Default Pulse Settings

The default mock gear pulse profile is intentionally conservative:

| Setting | Default |
| --- | ---: |
| Enabled | true |
| Target module | Both |
| Strength | 0.05 |
| Frequency | 50 Hz |
| Duration | 50 ms |
| Priority | 100 |
| Source | `PaddleShiftIntent` |

Upshift and downshift use the same default pulse. Direction remains available in diagnostics.

These values are mock defaults only. They are not validated real-hardware safety settings.

## Safety Context Behaviour

The app builds a Stage 2M safety context from:

- mock output connection state,
- brake/throttle mock module availability,
- current telemetry stale mute state,
- haptics running/stopped state,
- emergency mute state,
- the event's cached `DrivingArmed` state,
- mock emergency-stop state,
- `SoftwareConflictStatus.Clear`,
- and `RequiresRealDeviceWrites = false`.

`PHprSafetyLimiter` still clamps or rejects commands according to Stage 2L limits. Telemetry stale, haptics stopped, emergency mute active, driving not armed, unavailable modules, disconnected mock output, active emergency stop, and real-write requests block start commands.

## Emergency Stop Behaviour

`PHprGearPulseRouter.EmergencyStopAsync` passes through `SafetyLimitedPhprOutputDevice`.

Emergency stop:

- records a safety emergency-stop decision,
- forwards immediate mock stop frames for brake and throttle,
- clears pending scheduled mock stop frames,
- latches emergency-stop state,
- blocks later mock gear pulses until cleared,
- and performs no hardware write.

`ClearEmergencyStop` clears the mock/safety latch without persisting emergency state.

## Diagnostics

Stage 2M diagnostics include:

- routing enabled/disabled,
- target module,
- strength, frequency, and duration,
- accepted route count,
- ignored route count,
- safety rejected count,
- last shift direction,
- last target,
- last `PHprCommand` summary,
- last routing result,
- last safety decision and violation,
- mock output command count,
- mock frame count,
- pending scheduled stop count,
- emergency stop state,
- and explicit mock-only/no-hardware-output text.

The WPF Devices page exposes minimal controls for mock routing preferences, clearing diagnostics, mock emergency stop, and clearing mock emergency stop.

Persisted settings are limited to enabled, target module, strength, frequency, and duration. Emergency-stop state, safety latch state, mock command history, and mock frame history are not persisted.

## Stage 2N Follow-Up

Stage 2N will add mock routing for:

- road vibration,
- wheel slip,
- wheel lock.

Those effects remain out of Stage 2M.
