# Simagic P-HPR Mock Protocol

## Stage 2K Purpose

Stage 2K converts the Stage 2J protocol hypotheses into a safe, testable, mock-only P-HPR protocol and output layer.

This stage exists so later routing and safety stages can exercise deterministic P-HPR command shapes without touching real Simagic hardware.

## Safety Boundary

- Mock only.
- No USB writes.
- No HID output reports.
- No HID feature reports.
- No real P-HPR vibration.
- No hardware access.
- No production protocol adapter.
- Stage 2M live routing is limited to accepted `ShiftIntentEvent` values into safety-limited mock output diagnostics. Stage 2N adds `VehicleState` road/slip/lock mock routing through a separate safety-limited router.

Nothing in this mock protocol may be sent to real hardware.

## Relationship To Stage 2J

Stage 2J marked the SimHub `F1 EC` family as `ReadyForMockProtocol` and still `BlockedForRealWrite`.

Stage 2K implements that readiness as mock records, mock frame encoding/decoding, deterministic duration scheduling, mock output history, diagnostics, and CLI examples. The implementation remains grounded in the Stage 2J evidence notes and does not promote any bytes to real write status.

## SimHub F1 EC Mock Frame Format

Stage 2K models supported SimHub F1 EC mock frames as 64-byte payloads:

```text
F1 EC [MODULE] [STATE] [FREQ] [STRENGTH] 00 ...
```

Active/start mock payload:

```text
F1 EC [MODULE] 01 [FREQ] [STRENGTH] 00 ...
```

Stop/idle mock payload:

```text
F1 EC [MODULE] 00 0A 00 00 00 ...
```

The 64-byte length comes from Stage 2I/2J sanitized observations. It is used for mock fixtures only and is not a hardware report guarantee.

## Module Mapping

| Target | Mock byte | Notes |
| --- | ---: | --- |
| Brake | `01` | High-confidence Stage 2J observation. |
| Throttle | `02` | High-confidence Stage 2J observation. |
| Both | expands to brake + throttle frames | Stage 2K deliberately avoids module `00` for both-module semantics. |

Module `00` remains a low-confidence all/neutral/init/baseline candidate and is not used for Stage 2K both-target mock output.

## State Mapping

| State | Mock byte | Notes |
| --- | ---: | --- |
| Start | `01` | Active/on mock frame. |
| Stop | `00` | Stop/off/idle mock frame. |
| EmergencyStop | stop frames for brake and throttle | Immediate mock stop frames only. |

## Frequency And Strength

Frequency is encoded directly as a mock byte after clamping to the mock range.

Examples:

| Frequency | Mock byte |
| ---: | ---: |
| 10 Hz | `0A` |
| 20 Hz | `14` |
| 30 Hz | `1E` |
| 40 Hz | `28` |
| 50 Hz | `32` |

Strength is encoded directly as percent.

Examples:

| Strength | Mock byte |
| ---: | ---: |
| 10% | `0A` |
| 20% | `14` |
| 40% | `28` |
| 60% | `3C` |
| 80% | `50` |
| 100% | `64` |

These fields are mock modelling fields, not permission to send bytes to a device.

## Duration Model

Stage 2J evidence indicates SimHub duration is software-timed for tested cases.

Stage 2K models duration as:

```text
t0:                 start frame
t0 + DurationMs:    stop frame
```

The `PHprMockDurationScheduler` returns an ordered frame list. It does not sleep, loop, start background work, open devices, or send frames.

For duration `0`, Stage 2K emits stop-only mock frames. This avoids representing sustained active output for a zero-duration request.

## Emergency Stop

Mock emergency stop behavior:

- emits immediate stop frames for brake and throttle,
- removes pending scheduled stop frames from `MockPhprOutputDevice`,
- increments emergency-stop diagnostics,
- leaves the mock device in an emergency-stop-active state that suppresses later mock commands.

This is mock behavior only. Real emergency-stop validation remains blocked until later gated stages.

## SimPro 80 1E 89 Status

Stage 2K keeps SimPro Manager payloads as `SimProUnknownMock`.

Supported:

- classify payloads beginning with `80 1E 89`,
- record that the family remains `NeedsMoreCaptures`,
- return a safe unsupported result when asked to encode detailed SimPro commands.

Not supported:

- module inference,
- strength inference,
- frequency inference,
- duration inference,
- checksum/counter inference,
- SimPro-compatible mock encoding,
- SimPro-compatible real output.

## MockPhprOutputDevice Behavior

`MockPhprOutputDevice` remains memory-only.

It now records:

- accepted clamped `PHprCommand` values,
- generated Stage 2K mock protocol frames,
- connection simulation,
- brake/throttle availability simulation,
- rejected-command simulation,
- emergency-stop count,
- generated frame count,
- last generated frame,
- pending scheduled stop count,
- last command/status/message.

It does not open device handles, write HID reports, send feature reports, control SimPro Manager, control SimHub, or route live haptics.

## Research CLI

Safe mock-only commands:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-export --output capture-metadata\generated\simagic-mock-protocol-examples.json
```

The commands print a no-write safety banner and only display or export sanitized mock examples.

## Stage 2L Safety Layer

Stage 2L adds the full mock-only P-HPR safety layer documented in `docs/SIMAGIC_P_HPR_SAFETY_LAYER.md`:

- command rate limits,
- continuous duration limits,
- stronger validation and rejection diagnostics,
- telemetry stale / haptics stopped / emergency mute / `DrivingArmed` context gates for later routing,
- module availability and disconnected-device start rejection,
- emergency-stop latching and clear behavior,
- real-write blocking diagnostics,
- safety-limited mock output wrapping,
- and explicit safety-limiter tests.

Stage 2L itself does not route `ShiftIntentEvent` values, `VehicleState`, road/slip/lock effects, ASIO output, audio effects, or mixer output to P-HPR. Stage 2M and Stage 2N add separate mock routers on top of the Stage 2L safety wrapper.

## Stage 2M Follow-Up

Stage 2M adds mock gear-pulse routing from accepted shift intent to mock P-HPR output through the Stage 2L safety layer.

Stage 2K itself does not route `ShiftIntentEvent` values; Stage 2M owns that router.

## Stage 2N Follow-Up

Stage 2N adds mock road vibration, wheel slip, and wheel lock routing from existing `VehicleState` / `HapticPipelineSnapshot` data to mock P-HPR output through the Stage 2L safety layer.

Stage 2K itself does not route `VehicleState` values; Stage 2N owns that router.

## Final Statement

Nothing in this mock protocol may be sent to real hardware.
