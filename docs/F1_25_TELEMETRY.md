# F1 25 Telemetry

This document captures setup and behavior notes for F1 25 telemetry integration. Packet layout details live in `docs/F1_25_PACKET_SPEC_IMPLEMENTATION.md`.

## Source Of Truth

- Official local PDF: `C:\Users\ethan\Downloads\Data Output from F1 25 v3.pdf`.
- UDP format: `2025`.
- Do not use F1 23/F1 24 specs as substitutes.
- Do not commit the PDF unless licensing is explicitly cleared.

## UDP Setup

F1 25 can enable UDP telemetry through the in-game telemetry settings. PC users can also edit `hardware_settings_config.xml`, but the app must not depend on XML editing.

The PDF examples use port `20777`. Haptic Drive ASIO defaults to listening on `20778` because another tool, router, or Simagic software may already consume `20777`.

Stage 06 implements raw listening, byte-preserving forwarding, and F1 25 packet header validation. The listener counts datagrams, tracks packet rate and last packet time, and preserves packet bytes for forwarding, recording, replay, and parsing. Forwarding sends exact raw payload bytes to enabled destinations and does not depend on parser success. Packet bodies are not parsed yet.

Supported input modes planned:

- Direct F1 25 UDP input.
- Forwarded UDP input from another telemetry consumer.
- Multiple forwarding destinations independent of haptic output.

## Initial Packet Expectations

When connected as the game begins sending telemetry, the first frame includes enough data to initialize consumers. The PDF lists Session, Participants, Car Setups, Lap Data, Motion, Car Telemetry, Car Status, Car Damage, and Motion Ex on the first frame.

The app must tolerate packets arriving in any order and must maintain last-known packet state per packet type.

## Player Car Selection

Use `m_header.m_playerCarIndex` for packets that contain 22-car arrays.

Do not assume player index `0`. Do not use secondary player data unless a later stage explicitly implements split-screen behavior.

## Effect Mapping

Gear shift:

- Primary: Car Telemetry packet.
- Trigger from player `m_gear` changes.
- Use `m_engineRPM`, `m_throttle`, `m_speed`, and `m_suggestedGear` for shaping later.

Engine vibration:

- Primary: Car Telemetry packet.
- Secondary: Car Status packet.
- Use RPM, throttle, gear, speed, max RPM, idle RPM, and available engine power fields.

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

## Diagnostics To Add Later

- Packet rate by packet ID.
- Last packet timestamp by packet ID.
- Parser error counts.
- Dropped, malformed, unknown, and duplicate packet counters.
- Forwarded packet count and forwarding errors.
- Current player car index.
- Current surface IDs and wheel order display.
- Current output mode and safety state.
