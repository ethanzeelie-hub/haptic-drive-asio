# ADR-0005: Recording format v2

## Status

Accepted

## Context

The project needs replayable telemetry captures that preserve raw packets, survive partial writes, and remain useful for diagnostics and regression testing.

## Decision

Use the v2 recording format with:

- raw UDP payload preservation,
- per-record CRC,
- footer CRC,
- recoverable incomplete recordings,
- metadata describing drop/incomplete state.

## Consequences

Positive:

- Safer recovery from interrupted recordings.
- Better trust in replay/regression artifacts.
- Lower coupling between parser behavior and recording fidelity.

Tradeoff:

- Slightly more format complexity than a naive append-only dump.

## Related files

- `TelemetryRecordingService`
- `TelemetryRecordingReader`
- `TelemetryReplayService`
