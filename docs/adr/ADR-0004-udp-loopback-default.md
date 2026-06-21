# ADR-0004: UDP loopback default

## Status

Accepted

## Context

Telemetry can drive physical haptic output. Listening on LAN by default increases the chance of unintended remote input.

## Decision

Use loopback as the default telemetry bind mode.

- LAN telemetry is explicit opt-in.
- Allowlists are supported for remote senders.
- The UI surfaces warnings when LAN telemetry is enabled without an allowlist.

## Consequences

Positive:

- Safer same-PC default behavior.
- Better operator clarity around network scope.

Tradeoff:

- Console / cross-device telemetry requires deliberate configuration.

## Related files

- `UdpTelemetryReceiverOptions`
- `TelemetryIngressWorker`
- `TelemetryUdpStatusPresenter`
