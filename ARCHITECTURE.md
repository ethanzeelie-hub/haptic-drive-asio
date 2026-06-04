# Architecture

Haptic Drive ASIO is organized around clear boundaries between telemetry input, shared vehicle state, haptic effects, audio output, recording, and UI.

## Initial Project Boundaries

- `HapticDrive.Asio.App`: WPF app shell and presentation layer.
- `HapticDrive.Asio.Core`: shared models, audio/output interfaces, and domain rules.
- `HapticDrive.Asio.Telemetry.F1_25`: official F1 25 UDP packet parsing and mapping into shared state.
- `HapticDrive.Asio.Audio`: output device abstractions, mixer, safety processors, test bench, haptic effect generation, and debug output paths.
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

## Phase 2 Planned Actuator Boundary

Phase 2 adds planned Simagic P-HPR pedal support as a separate non-audio actuator path. Stage 2A documents the boundary only; no P-HPR implementation exists yet.

P-HPR modules must not be routed through ASIO and must not implement `IAudioOutputDevice`.

Planned separation:

```text
F1 25 UDP packets
-> VehicleState
-> audio haptic effects
-> mixer and safety chain
-> ASIO/BST-1 output

GT Neo paddle input and VehicleState
-> shift intent / pedal effect routing
-> actuator safety limiter
-> P-HPR pedal output
```

The future default P-HPR gear-pulse path is `InstantPaddleOnly`: read-only GT Neo paddle press, cached `DrivingArmed` gate, then immediate pedal gear pulse. It must not wait for a fresh telemetry packet at paddle-press time and must not fire a default second telemetry-confirmed pulse.

Real P-HPR USB writes are gated behind the exact approval phrase in `docs/SIMAGIC_P_HPR_SAFETY_PLAN.md`.

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

## Stage 11 Test Bench

Audio owns the Stage 11 synthetic test bench under `HapticDrive.Asio.Audio.TestBench`.

The test bench:

- Generates deterministic silence, sine tone, frequency sweep, pulse transient, and constant-value buffers.
- Keeps test signals separate from F1 25 telemetry, `VehicleState`, and future driving haptic effects.
- Wraps generated buffers as `AudioMixerInput` values and feeds the existing `AudioRenderPipeline`.
- Applies the Stage 10 mixer, normal mute, emergency mute, safety processor, limiter, and clipping protection before output handoff.
- Defaults to `NullAudioOutputDevice` so automated tests do not require ASIO, WASAPI, live telemetry, F1 25, or shaker hardware.
- Exposes diagnostics for selected signal, active state, sample format, output peak, limiter/clipping counts, rendered buffers, and output mode.

The WPF Test Bench page adds minimal controls for selecting a synthetic signal and rendering deterministic validation buffers. It does not implement a real-time audio callback, hardware calibration, frequency response graphs, profile editing, real WASAPI output, real ASIO streaming, or driving haptic effects.

## Stage 12 Gear Shift and Engine Effects

Audio owns the Stage 12 effect layer under `HapticDrive.Asio.Audio.Effects`.

The effect layer:

- Defines small renderable effect sources that consume shared `VehicleState` snapshots.
- Keeps F1 25 packet bodies out of the audio/effect layer.
- Synthesizes engine vibration from RPM, throttle, idle RPM, max RPM, and available pause/driver/pit/result status gates.
- Synthesizes gear shift pulses from valid forward gear changes.
- Renders deterministic `AudioSampleBuffer` sources that are wrapped as `AudioMixerInput` values.
- Feeds the existing Stage 10 mixer, safety processor, emergency mute, limiter, clipping protection, and output handoff.
- Defaults to conservative software gains and `NullAudioOutputDevice` validation.

The WPF Effects page adds minimal diagnostics for engine active state, RPM-derived frequency, gear pulse state, last observed gear, last shift frame, and default settings. It does not implement a full tuning UI, profile editor, live graphs, per-channel routing, physical calibration, real WASAPI output, or real ASIO streaming.

## Stage 13 Kerb, Impact, Road Texture, and Slip Effects

Audio extends the Stage 12 effect layer with four additional `VehicleState`-driven effect sources:

- `KerbEffect` synthesizes rumble from documented rumble strip / ridged surface IDs, speed, active wheel count, and optional Motion Ex contact / suspension data.
- `ImpactEffect` synthesizes short bounded pulses from player collision events and abrupt vertical-G, wheel-vertical-force, or suspension-acceleration changes.
- `RoadTextureEffect` synthesizes low-level deterministic texture from documented surface IDs, speed, and optional suspension / vertical-G motion.
- `SlipEffect` synthesizes slip, traction-loss, and minimal brake-lock vibration from wheel slip ratio, wheel slip angle, wheel speed, throttle, brake, speed, TC, and ABS state.

The effect layer still consumes shared `VehicleState` only and does not read F1 25 packet bodies directly. The new sources render deterministic `AudioSampleBuffer` values and feed the same Stage 10 mixer, safety processor, emergency mute, limiter, clipping protection, and output handoff used by Stage 12.

The WPF Effects page adds read-only diagnostics for kerb, impact, road texture, and slip state. It does not implement Stage 14 tuning controls, profiles, persistence, live graphs, per-channel routing, calibration, real WASAPI output, real ASIO streaming, Simagic P-HPR output, or physical hardware tuning.

## Stage 17 Native ASIO Streaming

Core extends `IAudioOutputDevice` with a synchronous output render callback and diagnostics fields for render callbacks, backend callbacks, dropped buffers, underruns, render duration, callback jitter, and telemetry age.

Audio owns the output cadence and backend implementation:

- `AudioOutputDeviceBase` can run an output-owned render loop for hardware-absent and fake-backend tests.
- `NullAudioOutputDevice` consumes callback-rendered buffers deterministically without physical hardware.
- `AsioAudioOutputDevice` preserves explicit driver selection, channel selection, and arming, then routes mono safety-processed buffers to the selected ASIO output channel.
- `NativeAsioOutputBackend` uses `NAudio.Asio`/`AsioOut` and a small preallocated queue to bridge app rendering to the driver callback.

Runtime owns stale telemetry policy:

- `HapticPipelineCoordinator` no longer depends on WPF `DispatcherTimer` for live rendering.
- The render callback reads current in-memory effect state, runs the mixer and safety chain, and fills the provided buffer.
- UI, disk IO, logging, networking, blocking waits, and async continuations stay outside the render callback.
- If no fresh parsed `VehicleState` arrives within the wall-clock timeout, the callback renders safety silence until telemetry updates again.

Automated tests still use Null output and fake ASIO backends. Stage 17 does not validate physical shaker feel, safe physical gain, physical latency, or final frequency tuning.

## Stage 06 F1 25 Packet Header Parser

`HapticDrive.Asio.Telemetry.F1_25` owns the first parser implementation:

- `F125PacketHeader` models the 29-byte official header.
- `F125PacketDefinitions` records packet IDs, packet names, exact packet sizes, packet version, and V1-required packet flags from the v3 PDF notes.
- `F125PacketHeaderParser` reads little-endian fields and validates packet format `2025`, game year `25`, known packet ID, packet version `1`, and exact datagram length.
- Unknown packet IDs return an ignored result instead of throwing.
- Malformed datagrams return failure results instead of throwing.
- Successful results preserve a copy of the raw datagram bytes.

The WPF shell parses headers from incoming UDP packets for diagnostics while forwarding still uses the original raw UDP payload. Packet body parsing and `VehicleState` mapping remain scheduled for later stages.
