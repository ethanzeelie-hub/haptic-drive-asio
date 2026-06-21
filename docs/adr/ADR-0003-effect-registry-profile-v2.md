# ADR-0003: Effect registry and profile schema v2

## Status

Accepted

## Context

Adding a new effect previously required central wiring changes across UI, profile persistence, and runtime construction.

## Decision

Use an effect descriptor registry and profile schema v2 keyed by stable effect keys.

- Descriptors define parameters, defaults, validation, and runtime factories.
- Profiles persist `EffectSettingsDocument` values by effect key.
- Unknown keys are preserved in metadata and ignored at runtime.

## Consequences

Positive:

- New effects become much more mechanical to add.
- Validation is centralized.
- Profile migrations become more explicit.

Tradeoff:

- The UI still needs a gradual migration away from legacy fixed controls.

## Related files

- `IHapticEffectRegistry`
- `BuiltInHapticEffectRegistry`
- `HapticDriveProfile`
