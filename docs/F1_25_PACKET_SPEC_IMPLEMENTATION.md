# F1 25 Packet Spec Implementation

Stage 03 source: `C:\Users\ethan\Downloads\Data Output from F1 25 v3.pdf`.

The PDF is the parser source of truth. Do not guess layouts from memory, older F1 specs, or unofficial structs. The PDF itself is not committed in this repo.

## Global Parser Rules

- All numeric values are little endian.
- All structures are packed with no padding.
- Every packet starts with `PacketHeader`.
- Preserve the original UDP datagram bytes for recording, replay, and forwarding.
- UDP forwarding must forward the original datagram byte-for-byte.
- Unknown packet IDs must be ignored safely.
- Malformed packets must return a parse failure, not crash the app.
- Parser tests must validate exact packet byte lengths before reading packet bodies.

## PacketHeader

The header is 29 bytes.

| Offset | Field | Type | Notes |
| ---: | --- | --- | --- |
| 0 | `m_packetFormat` | `uint16` | Must be `2025`. |
| 2 | `m_gameYear` | `uint8` | Expected `25` for F1 25. |
| 3 | `m_gameMajorVersion` | `uint8` | Game major version. |
| 4 | `m_gameMinorVersion` | `uint8` | Game minor version. |
| 5 | `m_packetVersion` | `uint8` | Version for the packet type. Stage 03 table uses version `1`. |
| 6 | `m_packetId` | `uint8` | Packet ID table below. |
| 7 | `m_sessionUID` | `uint64` | Unique session identifier. |
| 15 | `m_sessionTime` | `float` | Session timestamp. |
| 19 | `m_frameIdentifier` | `uint32` | Frame identifier. |
| 23 | `m_overallFrameIdentifier` | `uint32` | Does not rewind after flashbacks. |
| 27 | `m_playerCarIndex` | `uint8` | Use this to select the player in per-car arrays. |
| 28 | `m_secondaryPlayerCarIndex` | `uint8` | `255` when no second player. |

## Packet IDs, Sizes, Versions

All packet versions in the F1 25 v3 PDF are `1`.

| ID | Packet | Size | Frequency note |
| ---: | --- | ---: | --- |
| 0 | Motion | 1349 | Menu send rate; only while player is in control. |
| 1 | Session | 753 | 2 per second. |
| 2 | Lap Data | 1285 | Menu send rate. |
| 3 | Event | 45 | When the event occurs. |
| 4 | Participants | 1284 | Every 5 seconds. |
| 5 | Car Setups | 1133 | 2 per second. |
| 6 | Car Telemetry | 1352 | Menu send rate. |
| 7 | Car Status | 1239 | Menu send rate. |
| 8 | Final Classification | 1042 | Once at end of race. |
| 9 | Lobby Info | 954 | 2 per second in lobby. |
| 10 | Car Damage | 1041 | 10 per second. |
| 11 | Session History | 1460 | 20 per second, cycling cars. |
| 12 | Tyre Sets | 231 | 20 per second, cycling cars. |
| 13 | Motion Ex | 273 | Menu send rate. |
| 14 | Time Trial | 101 | 1 per second. |
| 15 | Lap Positions | 1131 | 1 per second. |

## V1 Required Packets

Implement these first:

- `PacketHeader`
- ID 0: Motion
- ID 1: Session
- ID 2: Lap Data
- ID 3: Event
- ID 4: Participants
- ID 6: Car Telemetry
- ID 7: Car Status
- ID 10: Car Damage
- ID 13: Motion Ex

Other packet IDs should still be recognized for metadata validation and safe ignoring.

## Validation Rules

For every incoming datagram:

1. Require at least 29 bytes for the header.
2. Read the header using little-endian primitive reads.
3. Require `m_packetFormat == 2025`.
4. Require `m_gameYear == 25` for the F1 25 parser.
5. Require `m_packetId` to be known before body parsing.
6. Require `m_packetVersion == 1` for all packets listed in this document.
7. Require datagram length to equal the documented packet size.
8. Return an ignored/unknown result for unsupported packet IDs.
9. Preserve raw bytes on successful parse and on forwarding paths.

## Vehicle Indexing

Do not assume the player car is index `0`.

- For packets containing arrays for all cars, use `m_header.m_playerCarIndex`.
- Vehicle indices remain stable during a session.
- The Participants packet array is indexed by vehicle index.
- Motion Ex contains extra player-car-only data, not a 22-car array.

## Wheel Arrays

All wheel arrays use:

| Index | Wheel |
| ---: | --- |
| 0 | Rear Left |
| 1 | Rear Right |
| 2 | Front Left |
| 3 | Front Right |

This applies to wheel speed, slip, suspension, force, damage, tyre, brake, and surface arrays.

## Surface Types

Surface IDs verified from the PDF appendix:

| ID | Surface |
| ---: | --- |
| 0 | Tarmac |
| 1 | Rumble strip |
| 2 | Concrete |
| 3 | Rock |
| 4 | Gravel |
| 5 | Mud |
| 6 | Sand |
| 7 | Grass |
| 8 | Water |
| 9 | Cobblestone |
| 10 | Metal |
| 11 | Ridged |

Preserve raw surface IDs in `VehicleState`; do not collapse unknown future values.

## Restricted Telemetry

The player can always see data for the car they are driving. In multiplayer, other players with restricted telemetry have selected fields zeroed.

Restricted fields noted by the PDF:

- Car Status: fuel values, fuel mix, front brake bias, ERS deploy/storage/harvest/deploy values, `m_enginePowerICE`, `m_enginePowerMGUK`.
- Car Damage: wing, floor, diffuser, sidepod, engine, gearbox, tyre wear, tyre damage, brake damage, DRS fault, engine wear/fault fields.

Parser behavior:

- Preserve zero values as received.
- Do not infer hidden values.
- VehicleState should be able to distinguish missing/stale packets from real zeros at the adapter layer where practical.

## Haptic-Relevant Fields

Gear shift:

- Packet 6 `m_carTelemetryData[player].m_gear`
- Packet 6 `m_carTelemetryData[player].m_engineRPM`
- Packet 6 `m_carTelemetryData[player].m_throttle`
- Packet 6 `m_carTelemetryData[player].m_speed`
- Packet 6 `m_suggestedGear`

Engine vibration:

- Packet 6 `m_engineRPM`, `m_throttle`, `m_gear`, `m_speed`
- Packet 7 `m_maxRPM`, `m_idleRPM`, `m_enginePowerICE`, `m_enginePowerMGUK`

Road texture:

- Packet 13 `m_suspensionPosition[4]`
- Packet 13 `m_suspensionVelocity[4]`
- Packet 13 `m_suspensionAcceleration[4]`
- Packet 13 `m_wheelVertForce[4]`
- Packet 6 `m_surfaceType[4]`
- Packet 6 `m_speed`
- Packet 0 `m_gForceVertical`
- Packet 13 `m_localVelocityX/Y/Z`
- Packet 13 `m_angularVelocityX/Y/Z`
- Packet 13 `m_angularAccelerationX/Y/Z`

Kerbs and impacts:

- Packet 6 `m_surfaceType[4]`
- Packet 13 suspension and vertical force fields
- Packet 0 `m_gForceVertical`
- Packet 3 collision event code `COLL`

Slip and traction loss:

- Packet 13 `m_wheelSlipRatio[4]`
- Packet 13 `m_wheelSlipAngle[4]`
- Packet 13 `m_wheelSpeed[4]`
- Packet 13 `m_wheelLatForce[4]`
- Packet 13 `m_wheelLongForce[4]`
- Packet 6 `m_throttle`, `m_brake`, `m_speed`
- Packet 7 `m_tractionControl`, `m_antiLockBrakes`

Pause, garage, and mute:

- Packet 2 `m_driverStatus`
- Packet 2 `m_resultStatus`
- Packet 2 `m_pitStatus`
- Packet 1 `m_gamePaused`
- Packet 1 `m_sessionType`
- Packet 1 `m_gameMode`
- Packet 7 `m_networkPaused`

## Parser Test Checklist

- Header parses all fields at the documented offsets.
- Header size is exactly 29 bytes.
- Packet ID map matches IDs 0 through 15.
- Packet size map matches every documented size.
- Packet version map requires version `1` for every documented packet.
- Little-endian primitive reads are verified with known byte patterns.
- Incorrect `m_packetFormat` is rejected.
- Incorrect F1 25 `m_gameYear` is rejected.
- Unknown packet IDs are ignored safely.
- Known packet IDs with wrong byte length are rejected.
- Known packet IDs with wrong version are rejected.
- Truncated datagrams do not throw unhandled exceptions.
- Parser never assumes player car index is `0`.
- Wheel-array order is tested as RL, RR, FL, FR.
- Surface type IDs 0 through 11 are mapped and raw values are preserved.
- Raw datagram bytes are preserved for recording/replay/forwarding.
- Event union parsing interprets details based on event code.
- Collision event `COLL` exposes both vehicle indices.
- Restricted telemetry zero values remain zero and are not synthesized.

## Stage 03 Scope Boundary

This stage creates implementation notes only. Parser code begins in later stages.
