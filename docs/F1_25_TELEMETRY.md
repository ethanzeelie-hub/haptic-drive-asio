# F1 25 Telemetry

This document captures setup and behavior notes for F1 25 telemetry integration. Packet layout details live in `docs/F1_25_PACKET_SPEC_IMPLEMENTATION.md`.

## Source Of Truth

- Official EA F1 25 UDP Data Output v3 PDF.
- UDP format: `2025`.
- Do not use F1 23/F1 24 specs as substitutes.
- Do not commit the PDF unless licensing is explicitly cleared.

## UDP Setup

F1 25 can enable UDP telemetry through the in-game telemetry settings. PC users can also edit `hardware_settings_config.xml`, but the app must not depend on XML editing.

The PDF examples use port `20777`. Haptic Drive ASIO defaults to listening on `20778` because another tool, router, or Simagic software may already consume `20777`.

Stage 13 implements raw listening, byte-preserving forwarding, raw packet recording, deterministic replay, F1 25 packet header validation, core packet body parsing for Motion, Session, Lap Data, Event, Participants, Car Telemetry, Car Status, Car Damage, and Motion Ex, mapping into shared `VehicleState`, and conservative generated haptic effects for engine vibration, gear shift, kerb, impact, road texture, and slip / brake-lock. The listener counts datagrams, tracks packet rate and last packet time, and preserves packet bytes for forwarding, recording, replay, and parsing. Forwarding and recording use exact raw payload bytes and do not depend on parser or effect success.

Supported input modes planned:

- Direct F1 25 UDP input.
- Forwarded UDP input from another telemetry consumer.
- Recorded `.hdrec` replay through the same parser and VehicleState path.
- Multiple forwarding destinations independent of haptic output.

## Initial Packet Expectations

When connected as the game begins sending telemetry, the first frame includes enough data to initialize consumers. The PDF lists Session, Participants, Car Setups, Lap Data, Motion, Car Telemetry, Car Status, Car Damage, and Motion Ex on the first frame.

The app tolerates packets arriving in any order and maintains last-known `VehicleState` samples per mapped packet slice. Missing slices remain null until their source packet arrives, and populated slices carry packet stamps so later timeout and safety stages can distinguish missing/stale data from real zero values.

## Player Car Selection

Use `m_header.m_playerCarIndex` for packets that contain 22-car arrays.

Do not assume player index `0`. Do not use secondary player data unless a later stage explicitly implements split-screen behavior.

Motion Ex contains player-car-only data and is mapped directly.

## Effect Mapping

Gear shift:

- Primary: Car Telemetry packet.
- Trigger from player `m_gear` changes.
- Use `m_engineRPM`, `m_throttle`, `m_speed`, and `m_suggestedGear` for shaping later.
- F1 25 does not output a dedicated gear-shift haptic event; Stage 12 synthesizes a short pulse from valid forward-gear transitions.

Engine vibration:

- Primary: Car Telemetry packet.
- Secondary: Car Status packet.
- Stage 12 uses RPM, throttle, max RPM, idle RPM, pause/status gates, and safe fallbacks. Gear, speed, and available engine power fields remain available for later shaping if needed.
- F1 25 does not output a direct engine-vibration signal; Stage 12 synthesizes vibration frequency and intensity from RPM, throttle, idle RPM, max RPM, and status gates.

Road texture:

- Primary: Motion Ex packet.
- Secondary: Car Telemetry and Motion packets.
- Use suspension movement, wheel vertical force, surface type, speed, local velocity, vertical G, and angular motion.

Kerbs and impacts:

- Primary: Motion Ex packet.
- Secondary: Car Telemetry, Motion, and Event packets.
- Use surface type, suspension movement, vertical force, vertical G, speed, and collision events.

Slip and traction loss:

- Primary: Motion Ex packet.
- Secondary: Car Telemetry and Car Status packets.
- Use slip ratio, slip angle, wheel speed, lateral force, longitudinal force, throttle, brake, speed, traction control, and ABS state.

Pause, garage, and mute:

- Primary: Lap Data packet.
- Secondary: Session and Car Status packets.
- Use driver status, result status, pit status, game pause, network pause, session type, game mode, and packet timeout.

## Safe Runtime Behavior

- Mute or heavily reduce output when telemetry is paused, stale, invalid, in garage/menus, or not moving.
- Always mute on packet stream timeout.
- Preserve raw UDP bytes for recording, replay, and forwarding.
- Forwarding must not depend on parser success.
- Parser failures must be counted for diagnostics without crashing the app.
- VehicleState mapping must not synthesize restricted telemetry values; zero values are preserved as received.

## Diagnostics To Add Later

- Packet rate by packet ID.
- Last packet timestamp by packet ID.
- Parser error counts.
- Dropped, malformed, unknown, and duplicate packet counters.
- Forwarded packet count and forwarding errors.
- Current player car index.
- Current surface IDs and wheel order display.
- Current output mode and safety state.
