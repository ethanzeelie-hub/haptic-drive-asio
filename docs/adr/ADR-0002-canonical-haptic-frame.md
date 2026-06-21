# ADR-0002: Canonical haptic frame

## Status

Accepted

## Context

Game-specific packet layouts and enums should not leak into effect DSP or actuator routing. That would make future game support expensive and brittle.

## Decision

Normalize adapter output into a canonical `HapticFrame`.

- `VehicleState` remains the parser/adaptor boundary.
- `HapticFrame` becomes the cross-game effect and actuation boundary.
- Freshness and driving context travel with the normalized frame.

## Consequences

Positive:

- Effects can remain game-agnostic.
- Future game additions can focus on parser + normalizer work.
- Freshness rules are easier to keep consistent.

Tradeoff:

- There is one more transformation step between parsing and rendering.

## Related files

- `IVehicleStateNormalizer`
- `F125VehicleStateNormalizer`
- `HapticFrame`
