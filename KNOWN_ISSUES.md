# Known Issues

## Stage 00

- No product functionality exists yet; this stage only creates the repository foundation.
- The F1 25 PDF has not yet been extracted into implementation notes.
- No audio output abstractions, telemetry listener, parser, recording, replay, or haptic effects are implemented yet.
- The physical Dayton BST-1 hardware is not available, so no physical tuning claims can be made.

## Stage 01

- The app shell is static; navigation pages contain placeholders only.
- Start/Stop Haptics and Emergency Mute are UI state placeholders and are not connected to an audio pipeline.
- The light theme button demonstrates theme scaffolding, but theme persistence is not implemented.
- Close/minimize-to-tray is represented as a disabled setting placeholder.
- No telemetry, parser, recording, replay, output device, mixer, safety processor, or haptic effect behavior exists yet.

## Stage 02

- `NullAudioOutputDevice` changes state but does not consume or render audio samples yet.
- `WasapiDebugOutputDevice` is a manual debug placeholder; it does not output sound yet.
- `AsioAudioOutputDevice` can select a driver through a catalog seam, but real ASIO streaming is not implemented.
- The default ASIO driver catalog reports no drivers until a real discovery implementation is added.
- Manual hardware tests are present only as skipped markers.
- The app still has no telemetry, parser, recording, replay, mixer, safety processor, or haptic effect behavior.

## Stage 03

- The F1 25 PDF has been summarized into implementation notes, but no parser code exists yet.
- Packet field offsets beyond `PacketHeader` have not been encoded in code yet.
- Parser tests listed in `docs/F1_25_PACKET_SPEC_IMPLEMENTATION.md` are not implemented yet.
- The PDF remains outside the repository; future work depends on the extracted notes unless the source PDF is supplied again.
- The app still has no UDP listener, packet parser, recording, replay, mixer, safety processor, or haptic effect behavior.

## Stage 04

- The UDP listener receives and counts raw datagrams only; no F1 25 packet parser is attached yet.
- UDP forwarding is not implemented yet, so the listener does not currently relay packets to other tools.
- Packet counters, packet rate, timestamps, and no-packet warning are in memory only and reset when the listener restarts.
- The app defaults to port `20778`; startup reports an unavailable listener if the port is already in use.
- Listen port, bind address, and no-packet warning threshold are not configurable through the UI yet.
- The app still has no recording, replay, mixer, safety processor, generated audio, real WASAPI output, real ASIO streaming, or haptic effect behavior.

## Stage 05

- The UDP forwarder is implemented in Core, but the shell does not yet provide UI controls for adding or editing destinations.
- The shell currently starts with zero forwarding destinations configured, so forwarding status is visible but disabled by default.
- Forwarding counters are in memory only and reset when the app restarts.
- Forwarding is raw-byte only and does not validate whether packets are real F1 25 packets yet.
- Parser work, packet ID diagnostics, recording, replay, mixer, safety processor, generated audio, real WASAPI output, real ASIO streaming, and haptic effects remain unimplemented.

## Stage 06

- The F1 25 parser validates packet headers only; packet bodies are not parsed yet.
- Header parser diagnostics count valid, ignored, and failed datagrams in memory only.
- Unknown packet IDs are ignored safely, but there is no per-packet-ID dashboard breakdown yet.
- Known packet IDs with valid headers are not converted into vehicle state yet.
- Recording, replay, mixer, safety processor, generated audio, real WASAPI output, real ASIO streaming, and haptic effects remain unimplemented.

## Stage 07

- The F1 25 parser now parses the Stage 07 core packet bodies, but it does not yet aggregate last-known packet state across packet types.
- Parsed packet bodies are not mapped into shared `VehicleState` yet; that is Stage 08.
- Known packet IDs outside the Stage 07 parser slice are validated at the header/length layer and then safely ignored.
- Parser diagnostics count valid, ignored, and failed datagrams in memory only.
- There is still no per-packet-ID dashboard breakdown or detailed body-field UI.
- Recording, replay, mixer, safety processor, generated audio, real WASAPI output, real ASIO streaming, and haptic effects remain unimplemented.

## Stage 08

- `VehicleState` is populated from parsed F1 25 packets, but it is still in memory only and resets when the app restarts.
- Missing packet slices are represented by null samples and received packet slices include packet stamps; timeout-based stale/mute policy is not implemented yet.
- The shell shows high-level VehicleState update count, player index, speed, and gear only; detailed per-field diagnostics are still planned.
- Recording, replay, mixer, safety processor, generated audio, real WASAPI output, real ASIO streaming, and haptic effects remain unimplemented.

## Stage 09

- Recording captures raw UDP payload bytes and relative timing, but the app has only a minimal Start/Stop Recording control and status card.
- Replay is implemented as a deterministic service and covered by tests, but the app does not yet provide a recordings browser or replay controls.
- Recording files do not yet include profile snapshots, route configuration, or effect settings.
- Recording uses a background writer queue; advanced backpressure/drop diagnostics are deferred.
- Mixer, safety processor, generated audio, real WASAPI output, real ASIO streaming, haptic effects, and physical shaker validation remain unimplemented.

## Stage 10

- The mixer combines explicit source buffers only; no haptic effect generators are implemented yet.
- The app shell submits safe silence through the Stage 10 pipeline to `NullAudioOutputDevice`, but it does not run a continuous audio callback or timing-sensitive render loop yet.
- `NullAudioOutputDevice` consumes sample buffers deterministically for tests and diagnostics, but still produces no sound.
- `WasapiDebugOutputDevice` and `AsioAudioOutputDevice` do not stream sample buffers yet; real output remains deferred.
- Safety defaults limit normalized floating-point samples, but they are not physical shaker gain calibration and must not be treated as final hardware safety limits.
- Stage 11 test bench signals, generated haptic effects, real WASAPI output, real ASIO streaming, and physical shaker validation remain unimplemented.

## Stage 11

- The test bench renders deterministic validation buffers, but it does not run a continuous real-time audio callback or timing-sensitive render loop.
- Test signals are synthetic validation tools only; generated driving haptic effects remain unimplemented.
- The WPF Test Bench page is minimal and does not include graphs, routing controls, calibration, profile editing, or hardware setup workflows.
- `NullAudioOutputDevice` remains the automated-test output; `WasapiDebugOutputDevice` and `AsioAudioOutputDevice` still do not stream sample buffers to real hardware.
- Physical shaker feel, safe gain, latency, and frequency tuning remain unvalidated until the real hardware chain is tested locally.
- Real WASAPI output, real ASIO streaming, Stage 13 driving effects, and physical hardware readiness remain future stages.

## Stage 12

- Gear shift and engine vibration effects are implemented with conservative defaults, but they render deterministic validation buffers only; there is still no continuous real-time audio callback loop.
- Engine vibration is synthesized from F1 25 RPM, throttle, gear, speed, idle RPM, max RPM, and status gates. F1 25 does not output a direct engine-vibration telemetry signal.
- Gear shift is synthesized from valid forward gear changes. F1 25 does not output a dedicated gear-shift haptic event.
- The app shows minimal Stage 12 diagnostics, but there is no full SimHub-style tuning UI, profile editor, live graphing, routing editor, or per-channel assignment UI yet.
- `NullAudioOutputDevice` remains the automated-test output; `WasapiDebugOutputDevice` and `AsioAudioOutputDevice` still do not stream sample buffers to real hardware.
- Physical shaker feel, safe gain, latency, effect priority, and final frequency tuning remain unvalidated until the real hardware chain is tested locally.

## Stage 13

- Kerb, impact, road texture, and slip / brake-lock effects are implemented with conservative deterministic defaults, but they still render validation buffers only; there is no continuous real-time audio callback loop yet.
- Road texture is synthesized from surface IDs, speed, and optional suspension / vertical-G motion. F1 25 does not output a dedicated road-texture haptic signal.
- Kerb vibration is synthesized from rumble strip and ridged surface IDs, speed, and optional suspension/contact data.
- Impact pulses are synthesized from player collision events and abrupt vertical-G, wheel-vertical-force, or suspension-acceleration spikes. They are not crash physics or damage modelling.
- Slip and minimal brake-lock vibration are synthesized from wheel slip ratio, wheel slip angle, wheel speed, throttle, brake, speed, TC, and ABS fields. A full ABS/lock-up tuning model is deferred.
- Stage 13 uses packet frame stamps to reject clearly stale sample slices where possible, but a runtime wall-clock telemetry timeout/mute policy is still deferred.
- The app shows read-only Stage 13 diagnostics, but there is no full SimHub-style tuning UI, profile editor, live graphing, routing editor, per-channel assignment UI, calibration UI, or persistence yet.
- `NullAudioOutputDevice` remains the automated-test output; `WasapiDebugOutputDevice` and `AsioAudioOutputDevice` still do not stream sample buffers to real hardware.
- Physical shaker feel, safe gain, latency, effect priority, and final frequency tuning remain unvalidated until the real hardware chain is tested locally.
