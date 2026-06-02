# Architecture

Haptic Drive ASIO is organized around clear boundaries between telemetry input, shared vehicle state, haptic effects, audio output, recording, and UI.

## Initial Project Boundaries

- `HapticDrive.Asio.App`: WPF app shell and presentation layer.
- `HapticDrive.Asio.Core`: shared models, interfaces, effect contracts, and domain rules.
- `HapticDrive.Asio.Telemetry.F1_25`: official F1 25 UDP packet parsing and mapping into shared state.
- `HapticDrive.Asio.Audio`: output device abstractions, mixer, safety processors, and debug output paths.
- `HapticDrive.Asio.Recording`: raw packet recording and deterministic replay.

## Target Flow

```text
F1 25 UDP packets
-> game-specific parser
-> game-specific adapter
-> shared VehicleState
-> haptic effect engine
-> mixer and safety chain
-> audio output device
```

## Early Development Rule

The app must build and test without ASIO hardware, M-Audio hardware, the Fosi amplifier, the Dayton BST-1, F1 25, or any live telemetry stream.

## Stage 01 App Shell

The WPF app shell provides navigation placeholders for Dashboard, Effects, Mixer / Routing, Devices, Telemetry / UDP Router, Recordings, Test Bench, Profiles, Settings, and Diagnostics.

Global haptics start/stop, emergency mute, theme selection, and close/minimize-to-tray setting placeholders exist in the shell. They are intentionally not connected to telemetry or audio behavior yet.

## Stage 02 Output Abstractions

Core owns the shared `IAudioOutputDevice` contract and related status/configuration records.

Audio owns concrete output device implementations:

- `NullAudioOutputDevice`: deterministic, hardware-free default for tests and startup.
- `WasapiDebugOutputDevice`: manual debug placeholder only.
- `AsioAudioOutputDevice`: ASIO abstraction/stub with graceful unavailable-driver handling.

ASIO driver discovery is isolated behind `IAsioDriverCatalog` so automated tests can use a fake catalog and later stages can add real driver enumeration without changing the output contract.

## Stage 03 F1 25 Spec Extraction

The official F1 25 v3 PDF has been converted into concise implementation notes in `docs/F1_25_PACKET_SPEC_IMPLEMENTATION.md`.

The parser boundary remains unchanged:

- F1-specific binary parsing belongs in `HapticDrive.Asio.Telemetry.F1_25`.
- Shared effect logic must consume a later `VehicleState` model, not raw F1 packet classes.
- Raw UDP bytes must be preserved for forwarding, recording, and replay.
- Packet parsing must validate format, year, ID, version, and exact byte length before body reads.

## Stage 04 UDP Listener

Core owns `IUdpTelemetryReceiver` and `UdpTelemetryReceiver`, a raw datagram listener that binds to port `20778` by default.

The listener:

- Preserves packet payload bytes exactly as received.
- Emits packet events with sequence number, remote endpoint, and receive timestamp.
- Tracks listener state, bound port, packet count, packet rate, last packet time, no-packet warning, and receive errors.
- Allows tests to bind an ephemeral port with `Port = 0`.

The WPF shell starts the listener on app load and surfaces high-level status on the dashboard and Telemetry / UDP Router page.

Parsing remains outside Stage 04. F1-specific binary parsing still belongs in `HapticDrive.Asio.Telemetry.F1_25`, and UDP forwarding is scheduled for Stage 05.

## Stage 05 UDP Forwarding

Core owns `IUdpTelemetryForwarder` and `UdpTelemetryForwarder`, a byte-preserving relay path that accepts `UdpTelemetryPacket` values from the raw listener.

The forwarder:

- Sends the exact received packet payload to each enabled destination.
- Keeps forwarding independent of F1 25 parser success, haptic output state, and audio hardware state.
- Tracks configured destinations, enabled destinations, input packet count, forwarded datagram count, forwarded bytes, forwarding errors, and last successful forward time.
- Skips disabled destinations and continues to later destinations if one send fails.

The WPF shell offers every received raw packet to the forwarder and surfaces forwarding status on the dashboard. Destination editing is intentionally not implemented in the shell yet.

Stage 06 should add the F1 25 packet header parser without changing the raw forwarding behavior.

## Stage 08 VehicleState Model

Core owns the shared `VehicleState` records under `HapticDrive.Asio.Core.Vehicle`.

`VehicleState` is a last-known snapshot with nullable samples for packet slices that have not arrived yet. Each populated sample carries a packet stamp with source packet name, session UID, session time, frame identifiers, and player car index. This lets later stages distinguish a real telemetry zero from data that is missing or stale.

`HapticDrive.Asio.Telemetry.F1_25` owns `F125VehicleStateAdapter`, which maps parsed Stage 07 packet bodies into shared state:

- Array-based packets select the player car through `PacketHeader.PlayerCarIndex`.
- Motion Ex remains player-car-only.
- Wheel arrays preserve official RL, RR, FL, FR order.
- Car Telemetry preserves raw surface type IDs instead of collapsing unknown future values.
- Failed or ignored parser results do not update `VehicleState`.

The WPF shell surfaces only high-level VehicleState diagnostics for now. Recording, replay, haptic effects, mixer, safety processors, real WASAPI output, and real ASIO streaming remain later stages.

## Stage 09 Recording and Replay

`HapticDrive.Asio.Recording` owns the raw telemetry capture and replay layer.

The recorder accepts `UdpTelemetryPacket` values, copies their payload bytes, stores packet sequence and relative receive timing, and writes a versioned `.hdrec` file through a background writer queue. Recording is intentionally parser-independent, so malformed or unsupported packets can still be captured exactly.

The replay service loads `.hdrec` files and emits `UdpTelemetryPacket` values in recorded order. Tests and later runtime paths can feed those packets through the same `F125PacketParser.Parse(packet.Payload)` and `F125VehicleStateAdapter.Apply(parseResult)` sequence used for live UDP packets.

The WPF shell adds only a minimal Start/Stop Recording control and status card. Replay controls, recording library management, profile snapshots, graphing, mixer work, safety processors, audio generation, and hardware output remain outside Stage 09.

## Stage 10 Audio Mixer and Safety Chain

Core owns the shared Stage 10 audio sample contracts:

- `AudioSampleFormat` records sample rate, channel count, frame count, and interleaved sample count.
- `AudioSampleBuffer` stores interleaved `float` samples and validates buffer shape.
- `IAudioOutputDevice.SubmitBufferAsync` is the narrow output handoff for final sample buffers.

Audio owns the deterministic processing implementation:

- `AudioMixer` combines explicit source buffers with per-source gain, master gain, normal mute, emergency mute, and invalid sample/gain sanitisation.
- `AudioSafetyProcessor` sanitises NaN/infinity values, applies conservative output gain, peak-limits buffers to the configured normalized ceiling, hard-clips any remaining overflow, and forces silence on emergency mute.
- `AudioRenderPipeline` keeps a reusable mix buffer, applies mixer and safety processing, and hands the final buffer to an `IAudioOutputDevice`.
- `NullAudioOutputDevice` consumes matching sample buffers after start and discards them deterministically for hardware-absent tests.

The WPF shell connects Start Haptics and Emergency Mute to the Stage 10 pipeline only by submitting safe silence to `NullAudioOutputDevice`. There is no continuous audio callback, generated haptic effect, Stage 11 test signal, real WASAPI output, or real ASIO streaming in Stage 10.

## Stage 06 F1 25 Packet Header Parser

`HapticDrive.Asio.Telemetry.F1_25` owns the first parser implementation:

- `F125PacketHeader` models the 29-byte official header.
- `F125PacketDefinitions` records packet IDs, packet names, exact packet sizes, packet version, and V1-required packet flags from the v3 PDF notes.
- `F125PacketHeaderParser` reads little-endian fields and validates packet format `2025`, game year `25`, known packet ID, packet version `1`, and exact datagram length.
- Unknown packet IDs return an ignored result instead of throwing.
- Malformed datagrams return failure results instead of throwing.
- Successful results preserve a copy of the raw datagram bytes.

The WPF shell parses headers from incoming UDP packets for diagnostics while forwarding still uses the original raw UDP payload. Packet body parsing and `VehicleState` mapping remain scheduled for later stages.
