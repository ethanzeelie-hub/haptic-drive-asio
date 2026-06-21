# ADR-0001: Global output interlock

## Status

Accepted

## Context

The application can drive multiple output paths:

- ASIO / Null audio output,
- manual validation tones,
- mock or future P-HPR actuation routes.

An audio-only mute was not strong enough for a production-ready safety story because it allowed non-audio paths to diverge.

## Decision

Use one global `IOutputInterlock` as the top-level safety authority.

- It starts latched.
- It must be able to trip from UI, runtime, stale telemetry, device faults, invalid configuration, manual-test blocking, and shutdown.
- All output paths must check it.

## Consequences

Positive:

- One visible safety truth for operators.
- Consistent emergency-stop semantics across audio and actuation.
- Safer shutdown behavior.

Tradeoff:

- More subsystems must respect one shared state boundary.

## Related files

- `HapticDrive.Asio.Core.Safety`
- `HapticPipelineCoordinator`
- `MainWindow`
