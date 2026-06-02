# F1 25 Packet Spec Implementation

Stage 03 source: official EA F1 25 UDP Data Output v3 PDF.

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

## Stage 07 Core Body Parser Slice

Stage 07 implements strongly typed body parsing for the V1 required packet IDs listed above. It does not map packets to shared `VehicleState`, recording/replay, audio, haptic effects, WASAPI, ASIO, or hardware output.

Fixed counts and body sizes extracted from the official v3 PDF:

| Structure | Size | Count / Use |
| --- | ---: | --- |
| `CarMotionData` | 60 | 22 entries in Motion |
| `MarshalZone` | 5 | 21 entries in Session |
| `WeatherForecastSample` | 8 | 64 entries in Session |
| `LapData` | 57 | 22 entries in Lap Data |
| `EventDataDetails` | 12 | Union storage in Event |
| `LiveryColour` | 3 | 4 entries in each Participant |
| `ParticipantData` | 57 | 22 entries in Participants |
| `CarTelemetryData` | 60 | 22 entries in Car Telemetry |
| `CarStatusData` | 55 | 22 entries in Car Status |
| `CarDamageData` | 46 | 22 entries in Car Damage |
| `PacketMotionExData` body | 244 | Player-car-only Motion Ex data |

Field order for Stage 07 parser models:

- `CarMotionData`: `float` `m_worldPositionX`, `m_worldPositionY`, `m_worldPositionZ`, `m_worldVelocityX`, `m_worldVelocityY`, `m_worldVelocityZ`; `int16` `m_worldForwardDirX`, `m_worldForwardDirY`, `m_worldForwardDirZ`, `m_worldRightDirX`, `m_worldRightDirY`, `m_worldRightDirZ`; `float` `m_gForceLateral`, `m_gForceLongitudinal`, `m_gForceVertical`, `m_yaw`, `m_pitch`, `m_roll`.
- `PacketSessionData` body: scalar fields from `m_weather` through `m_numMarshalZones`; `MarshalZone[21]`; `m_safetyCarStatus`, `m_networkGame`, `m_numWeatherForecastSamples`; `WeatherForecastSample[64]`; scalar fields from `m_forecastAccuracy` through `m_numSessionsInWeekend`; `uint8 m_weekendStructure[12]`; `float m_sector2LapDistanceStart`, `m_sector3LapDistanceStart`.
- `LapData`: scalar fields from `m_lastLapTimeInMS` through `m_speedTrapFastestLap` in the official struct order. `PacketLapData` appends `uint8 m_timeTrialPBCarIdx` and `uint8 m_timeTrialRivalCarIdx`.
- `PacketEventData`: `uint8 m_eventStringCode[4]`, followed by a 12-byte `EventDataDetails` union. Stage 07 interprets official event codes `SSTA`, `SEND`, `FTLP`, `RTMT`, `DRSE`, `DRSD`, `TMPT`, `CHQF`, `RCWN`, `PENA`, `SPTP`, `STLG`, `LGOT`, `DTSV`, `SGSV`, `FLBK`, `BUTN`, `RDFL`, `OVTK`, `SCAR`, and `COLL`; unknown codes preserve raw detail bytes.
- `ParticipantData`: `uint8` fields `m_aiControlled` through `m_nationality`; `char m_name[32]` as null-terminated UTF-8 bytes; `uint8 m_yourTelemetry`, `m_showOnlineNames`; `uint16 m_techLevel`; `uint8 m_platform`, `m_numColours`; `LiveryColour[4]`. The PDF text extraction misses the visible closing brace, but these fields reconcile exactly to the documented 57-byte struct and 1284-byte packet.
- `CarTelemetryData`: `uint16 m_speed`; `float m_throttle`, `m_steer`, `m_brake`; `uint8 m_clutch`; `int8 m_gear`; `uint16 m_engineRPM`; `uint8 m_drs`, `m_revLightsPercent`; `uint16 m_revLightsBitValue`; wheel arrays `m_brakesTemperature[4]`, `m_tyresSurfaceTemperature[4]`, `m_tyresInnerTemperature[4]`; `uint16 m_engineTemperature`; wheel arrays `m_tyresPressure[4]`, `m_surfaceType[4]`. `PacketCarTelemetryData` appends `m_mfdPanelIndex`, `m_mfdPanelIndexSecondaryPlayer`, and `int8 m_suggestedGear`.
- `CarStatusData`: scalar fields from `m_tractionControl` through `m_networkPaused` in the official struct order.
- `CarDamageData`: wheel arrays `m_tyresWear[4]`, `m_tyresDamage[4]`, `m_brakesDamage[4]`, `m_tyreBlisters[4]`, followed by scalar damage and fault fields from `m_frontLeftWingDamage` through `m_engineSeized`.
- `PacketMotionExData` body: player-car-only wheel arrays `m_suspensionPosition[4]`, `m_suspensionVelocity[4]`, `m_suspensionAcceleration[4]`, `m_wheelSpeed[4]`, `m_wheelSlipRatio[4]`, `m_wheelSlipAngle[4]`, `m_wheelLatForce[4]`, `m_wheelLongForce[4]`; scalar motion fields from `m_heightOfCOGAboveGround` through `m_chassisPitch`; wheel arrays `m_wheelCamber[4]`, `m_wheelCamberGain[4]`.

Stage 07 unsupported but known packet IDs:

- ID 5: Car Setups
- ID 8: Final Classification
- ID 9: Lobby Info
- ID 11: Session History
- ID 12: Tyre Sets
- ID 14: Time Trial
- ID 15: Lap Positions

The Stage 07 parser validates their headers and exact packet length through the existing Stage 06 definitions, then returns an ignored result without reading unsupported bodies.

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

## Stage 06 Implementation Status

Implemented:

- `PacketHeader` model with all fields at documented offsets.
- Packet ID, size, version, and V1-required metadata for IDs 0 through 15.
- Header parser validation for minimum header length, packet format `2025`, game year `25`, known packet ID, packet version `1`, and exact documented packet length.
- Unknown packet IDs return an ignored result.
- Malformed known packets return failure results.
- Raw datagram bytes are copied on parse results for diagnostics/replay handoff.

Not implemented yet:

- Packet body structs.
- Event union parsing.
- Per-packet body validation beyond exact datagram size.
- VehicleState mapping.

## Stage 07 Implementation Status

Implemented:

- Packet body models for Motion, Session, Lap Data, Event, Participants, Car Telemetry, Car Status, Car Damage, and Motion Ex.
- A full packet parser that reuses Stage 06 header validation before body reads.
- Safe ignored results for unknown packet IDs and known packets outside the Stage 07 body slice.
- Event union interpretation for official event string codes, including collision vehicle indices.
- Explicit RL, RR, FL, FR wheel ordering in typed wheel data.
- Raw datagram preservation on successful packet parses.

Not implemented yet:

- Mapping packet bodies to shared `VehicleState`.
- Last-known-packet state aggregation across packet types.
- Recording, replay, audio, haptic effects, WASAPI output, ASIO streaming, or physical hardware behavior.

## Stage 08 Implementation Status

Implemented:

- Shared Core `VehicleState` records for motion, session, lap, participant, car telemetry, car status, damage, Motion Ex, and last event samples.
- `VehicleStateSample<T>` packet stamps that preserve source packet name, session UID, session time, frame identifiers, and player car index.
- F1 25 adapter mapping from parsed Stage 07 packet bodies into shared `VehicleState`.
- Last-known sample aggregation that tolerates packet arrival in any order.
- Player-car selection from `m_header.m_playerCarIndex` for 22-car packet arrays.
- Direct player-only mapping for Motion Ex.
- Raw surface type ID preservation in `VehicleState`.
- Wheel-order preservation as RL, RR, FL, FR.
- Safe ignores for failed parser results and invalid player indices.

Not implemented yet:

- Timeout-based stale sample policy or safety mute behavior.
- Haptic effects, mixer, generated audio, WASAPI output, ASIO streaming, or physical hardware behavior.

## Stage 09 Implementation Status

Implemented:

- Versioned raw UDP recording files with metadata, packet order, relative timing, payload length, and raw payload bytes.
- Background recording writer queue so disk IO is kept out of the UDP receive callback.
- Deterministic fast replay and optional time-preserving replay that emits `UdpTelemetryPacket` values without UDP sockets.
- Replay tests that feed recorded packets through `F125PacketParser` and `F125VehicleStateAdapter`.
- Safe failures for corrupt headers, unsupported versions, truncated packet records, and unreasonable payload lengths.

Not implemented yet:

- Recording library UI, replay controls in the app, profile snapshots, generated audio, haptic effects, WASAPI output, ASIO streaming, or physical hardware behavior.

## Stage 10 Implementation Status

Implemented:

- Interleaved floating-point sample buffers and a deterministic audio mixer/safety pipeline outside the F1 25 parser.
- Null-output sample buffer consumption for hardware-absent tests.

Parser impact:

- No F1 25 packet layouts, offsets, enum values, packet lengths, packet versions, parser behavior, raw UDP forwarding, or recording/replay byte-preservation behavior changed in Stage 10.

Not implemented yet:

- Stage 11 test bench signals, generated haptic effects, real WASAPI output, ASIO streaming, or physical hardware behavior.

## Stage 11 Implementation Status

Implemented:

- Deterministic synthetic test-bench signals for validating the Stage 10 mixer, safety chain, and null-output path without F1 25, live telemetry, ASIO hardware, WASAPI hardware, or shaker hardware.

Parser impact:

- No F1 25 packet layouts, offsets, enum values, packet lengths, packet versions, parser behavior, raw UDP forwarding, or recording/replay byte-preservation behavior changed in Stage 11.

Not implemented yet:

- Generated driving haptic effects, real WASAPI output, ASIO streaming, or physical hardware behavior.

## Stage 12 Implementation Status

Implemented:

- `VehicleState`-driven engine vibration effect generation from direct F1 25 RPM, throttle, idle RPM, max RPM, and pause/status fields.
- `VehicleState`-driven gear shift transient generation from valid forward `m_gear` transitions.
- Conservative SimHub-inspired default effect options for gain, frequency, pulse duration, debounce, high-frequency engine component, and optional RPM modulation.
- Deterministic source buffers that feed the existing Stage 10 mixer, safety chain, and `NullAudioOutputDevice` test path.
- Minimal app diagnostics for engine active state, RPM-derived frequency, gear pulse state, last gear, and last shift frame.

Parser impact:

- No F1 25 packet layouts, offsets, enum values, packet lengths, packet versions, parser behavior, raw UDP forwarding, or recording/replay byte-preservation behavior changed in Stage 12.
- The effect layer consumes shared `VehicleState` only and does not read F1 25 parser packet bodies directly.

Not implemented yet:

- Real WASAPI output, ASIO streaming, Simagic P-HPR output, profile editing, live graphs, continuous real-time audio callback, or physical shaker calibration.

## Stage 13 Implementation Status

Implemented:

- `VehicleState`-driven kerb effect generation from raw surface type IDs, speed, and optional Motion Ex contact / suspension data.
- `VehicleState`-driven impact effect generation from player collision events, vertical-G deltas, wheel-vertical-force deltas, and suspension-acceleration deltas.
- `VehicleState`-driven road texture generation from raw surface type IDs, speed, and optional Motion Ex / vertical-G motion.
- `VehicleState`-driven slip / brake-lock generation from wheel slip ratio, wheel slip angle, wheel speed, throttle, brake, speed, traction control, and ABS state.
- Conservative default effect options, deterministic roughness/noise, frame-stamp stale-slice checks, bounded transient envelopes, and read-only WPF diagnostics.

Parser impact:

- No F1 25 packet layouts, offsets, enum values, packet lengths, packet versions, parser behavior, raw UDP forwarding, or recording/replay byte-preservation behavior changed in Stage 13.
- The effect layer consumes shared `VehicleState` only and does not read F1 25 parser packet bodies directly.

Not implemented yet:

- Stage 14 tuning UI, profiles, persistence, advanced diagnostics, live graphs, per-channel routing, calibration, advanced ABS/lock-up modelling, real WASAPI output, ASIO streaming, Simagic P-HPR output, continuous real-time audio callback, or physical shaker calibration.

## Stage 03 Scope Boundary

This stage creates implementation notes only. Parser code begins in later stages.
