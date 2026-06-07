# Simagic P-HPR Safety Layer

## Stage 2L Purpose

Stage 2L adds the reusable P-HPR safety layer that future mock routing and gated real-output work must pass through.

The implementation lives in `HapticDrive.Simagic.PHPR.Abstractions`:

- `PHprSafetyLimiter`
- `IPHprSafetyLimiter`
- `PHprSafetyContext`
- `PHprSafetyDecision`
- `PHprSafetySnapshot`
- `SafetyLimitedPhprOutputDevice`

Stage 2L may evaluate commands and forward accepted or clamped commands to `MockPhprOutputDevice` only. Stage 2M now routes accepted `ShiftIntentEvent` values through this safety-limited mock path. Stage 2L/2M do not route `VehicleState`, telemetry road/slip/lock effects, ASIO effects, or mixer output to P-HPR.

## Safety Boundary

- Mock/safety only.
- No USB writes.
- No HID output reports.
- No HID feature reports.
- No real P-HPR vibration.
- No real device control.
- No production encoder or decoder.
- No SimPro Manager or SimHub control.

Nothing in this safety layer may be used to send real USB commands before the gated write approval.

## Relationship To Stage 2K

Stage 2K created the mock protocol model and memory-only `MockPhprOutputDevice`.

Stage 2L wraps that mock output with `SafetyLimitedPhprOutputDevice` and evaluates every command through `PHprSafetyLimiter` before it can produce mock frames. Rejected commands do not reach the mock output. Accepted-with-clamp commands reach the mock output with conservative values and safety flags.

## Default Limits

`PHprSafetyLimits.Default` remains conservative:

| Limit | Default |
| --- | ---: |
| `MaxStrength01` | `0.10` |
| `MaxDurationMs` | `100` |
| `MinFrequencyHz` | `5` |
| `MaxFrequencyHz` | `250` |
| `MaxCommandsPerSecond` | `10` |
| `MaxContinuousDurationMs` | `500` |
| `AllowRealDeviceWrites` | `false` |

## Strength Limit

Strength above `MaxStrength01` is clamped. The safety decision is `AcceptedWithClamp`, the effective command carries `ClampedStrength`, and diagnostics record `StrengthExceeded`.

## Duration Limit

Duration above `MaxDurationMs` is clamped. The safety decision is `AcceptedWithClamp`, the effective command carries `ClampedDuration`, and diagnostics record `DurationExceeded`.

## Frequency Limit

Frequency below `MinFrequencyHz` or above `MaxFrequencyHz` is clamped. The safety decision is `AcceptedWithClamp`, the effective command carries `ClampedFrequency`, and diagnostics record `FrequencyTooLow` or `FrequencyTooHigh`.

## Command Rate Limit

The limiter tracks accepted start-command timestamps in a deterministic one-second window. If the window already contains `MaxCommandsPerSecond` accepted starts, later start commands are rejected with `CommandRateExceeded`.

Tests use an injected `IPHprSafetyClock` fake clock so rate-limit behavior does not depend on wall-clock timing.

## Continuous Duration Limit

The limiter estimates continuous active duration per brake/throttle module. For overlapping or back-to-back starts, a command is rejected with `ContinuousDurationExceeded` if accepting it would push a module beyond `MaxContinuousDurationMs`.

Stop commands clear the estimate for their target module. Emergency stop clears both modules.

## Module Availability

`PHprSafetyContext` carries brake and throttle module availability. Brake, throttle, and both-target start commands are rejected with `ModuleUnavailable` when their required module is unavailable. Safe stop and emergency stop paths remain allowed for diagnostics and cleanup.

## Device Disconnect

Start commands are rejected with `DeviceDisconnected` when the context or mock output snapshot says the device is disconnected. Stop commands and emergency stop remain safe to record in mock mode.

## Emergency Stop

Emergency stop:

- records a safety `EmergencyStopped` decision,
- forwards to `MockPhprOutputDevice.EmergencyStopAsync`,
- creates immediate mock stop frames for brake and throttle,
- clears pending scheduled mock stop frames,
- clears continuous-duration tracking,
- clears the command-rate window,
- latches safety emergency state,
- blocks later start commands until `ClearEmergencyStop` is called.

`SafetyLimitedPhprOutputDevice.ClearEmergencyStop` clears both the limiter latch and the mock output latch.

## Runtime Context Gates

`PHprSafetyContext` includes fields used by Stage 2M gear routing and later Stage 2N road/slip/lock routing:

- `TelemetryStale`
- `HapticsStopped`
- `EmergencyMuteActive`
- `DrivingArmed`

When any of these gates disallow starts, the limiter rejects start commands with the matching violation and still allows safe stop/emergency stop behavior.

## SimPro Conflict Placeholder

`PHprSoftwareConflictStatus` is a synthetic Stage 2L context placeholder. `ActiveConflict` rejects start commands with `SimProConflict`.

Stage 2L does not detect processes, inspect SimPro Manager, inspect SimHub, kill processes, hook processes, inject into processes, or modify either application's settings. Stage 2O owns coexistence detection.

## Real Write Gate

`AllowRealDeviceWrites` remains false by default. If a context requests real device writes while the active limits disallow them, start commands are rejected with `RealWritesNotAllowed`.

Mock mode can proceed without enabling real writes. This does not approve direct hardware control.

## Diagnostics Snapshot

`PHprSafetySnapshot` reports:

- limits and context in effect,
- total evaluated commands,
- accepted count,
- accepted-with-clamp count,
- rejected count,
- emergency-stop count,
- last decision,
- last violation,
- last accepted command,
- last rejected command,
- last clamp details,
- current command-rate window count,
- current continuous-duration estimate,
- emergency-stop active state,
- real-write allowed/blocked state,
- and last error.

`SafetyLimitedPhprOutputDevice` exposes safety diagnostics and inner mock-output diagnostics separately.

## Research CLI

Stage 2L adds a safe console-only command:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples
```

The command prints mock safety decisions only. It does not export hardware packets, send USB writes, issue output reports, issue feature reports, vibrate hardware, or access devices.

## Stage 2M Follow-Up

Stage 2M routes accepted mock gear-pulse commands through this safety layer before they reach `MockPhprOutputDevice`.

## Stage 2N Follow-Up

Stage 2N should route mock road vibration, wheel slip, and wheel lock commands through this safety layer before they reach `MockPhprOutputDevice`.

## Final Statement

Nothing in this safety layer may be used to send real USB commands before the gated write approval.
