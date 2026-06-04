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

## Stage 14

- The WPF shell now exposes practical tuning controls, mixer/safety controls, versioned JSON profile save/load/reset, and runtime diagnostics, but it still uses simple code-behind shell wiring rather than a full MVVM architecture.
- Profiles cover existing effect, mixer, and safety settings only. They do not save emergency mute, output-device selection, forwarding destinations, recording files, hardware calibration, or physical gain data.
- Profile files are user-generated under local app data and are not committed. Missing, corrupt, unsupported-version, and partially invalid profiles fail or repair safely through result objects.
- Diagnostics are read-only snapshots and manual/periodic UI refreshes. There is no heavy graphing, charting, long-term diagnostics log, or copy-report workflow yet.
- The Recordings page still has only minimal recording status/control and replay status diagnostics; a file picker and polished recordings library remain deferred.
- Mixer/routing controls expose master mute/gain and safety output settings only. Advanced routing matrices, per-channel assignments, effect priority, and ducking remain deferred.
- `NullAudioOutputDevice` remains the default automated-test output. Real WASAPI output, real ASIO streaming, hardware readiness checks, and physical shaker calibration are still not implemented.
- Physical shaker feel, safe physical gain, latency, effect priority, and final frequency tuning remain unvalidated until real hardware testing.

## Stage 15

- The first playable mock software pipeline is wired for live UDP and replayed packets, but it still renders through `NullAudioOutputDevice` by default and produces no physical shaker output.
- The WPF shell uses a simple mock render `DispatcherTimer`; a real low-latency audio callback loop is still Stage 16/future work.
- Replay controls intentionally use a minimal latest-recording flow. A polished recordings library, file picker, trimming, and recording metadata browser remain deferred.
- Optional ASIO visibility diagnostics use the existing `IAsioDriverCatalog` seam. The default catalog still reports no drivers unless a real discovery implementation is added later.
- The M-Audio M-Track Solo and Fosi amplifier may be present locally, but automated tests and normal startup do not require them.
- Seeing the M-Audio device in the Windows sound output selector is not proof that the app is using ASIO.
- Real ASIO streaming, M-Audio hardware readiness, physical latency measurement, physical gain calibration, and Dayton BST-1 shaker tuning remain deferred to Stage 16/manual testing.
- The Dayton BST-1 has not been physically validated in this project yet, so no final shaker feel, safe gain, latency, or frequency tuning claims can be made.

## Stage 16

- ASIO driver-name discovery is implemented through Windows ASIO registry locations, but driver visibility still depends on how the vendor driver registers itself locally.
- The app exposes explicit ASIO selection, driver selection, channel selection, arming, routing diagnostics, and fake-backend tests, but a native ASIO callback streaming backend is not installed in this Stage 16 build.
- Selecting ASIO with the default unavailable backend can discover a driver but fail safely at backend open/start; this is readiness behavior, not physical output validation.
- Output channel count is known only when a backend can open the selected ASIO driver. Until then, channel choices are manual readiness selections and must be confirmed locally.
- The WPF shell still uses simple code-behind wiring and rebuilds the runtime pipeline when output mode/settings change; polished device profiles and persisted output selection are deferred.
- Test bench remains bound to Null output by default. Manual ASIO test-bench streaming awaits the native backend and local validation.
- The Fosi amplifier is available, but no gain safety, physical shaker feel, physical latency, or frequency tuning is claimed.
- Dayton BST-1 physical shaker testing is deferred until the shaker arrives.
- Windows sound output visibility is explicitly treated as separate from ASIO driver visibility and does not prove ASIO usage.

## Stage 17

- Native ASIO streaming is implemented behind `IAsioOutputBackend`, but physical Dayton BST-1 output, physical gain safety, shaker feel, physical latency, and final frequency tuning remain unvalidated.
- The render path is output-owned and no longer driven by WPF `DispatcherTimer`, but final hardware callback behavior still depends on the selected local ASIO driver and must be checked manually with the full chain.
- The ASIO backend uses a small preallocated queue between app rendering and the driver callback; underrun and dropped-buffer diagnostics are surfaced, but they are not a final latency measurement.
- Stale telemetry is muted by wall-clock timeout so old live samples cannot drive effects indefinitely; effect behavior under network loss or game pause may need future refinement after real sessions.
- ASIO driver selection, output channel selection, and arming remain explicit. The app still must not auto-switch to ASIO or WASAPI.
- `NullAudioOutputDevice` remains the automated-test default. Hardware-dependent tests remain manual and skipped by default.
- Recording library polish, forwarding destination UI, advanced routing matrices, live graphing, real WASAPI output, physical calibration UI, and Simagic P-HPR output remain deferred.

## Stage 18

- The final pre-shaker software package is implemented: launch/runtime prerequisite handling, app settings persistence, UDP forwarding destination UI, recordings library UI, selected replay, packet-ID diagnostics, and copyable diagnostics reports are now available.
- App settings are separate from haptic profiles and persist theme, forwarding destinations, and last ASIO driver/channel only. ASIO armed state, haptic running state, emergency mute, and physical calibration are not persisted.
- UDP forwarding now supports IP addresses, `localhost`, and DNS hostnames. Obvious enabled loopback to the local listener port `20778` is blocked in the UI.
- Recordings are listed from local app data with metadata summaries, but recording trimming, profile snapshots, and route snapshots are not implemented.
- Advanced routing matrices, live graphing, real WASAPI output, Simagic P-HPR output, and physical calibration UI remain outside the pre-BT-1 scope.
- Physical Dayton BST-1 output, safe physical gain, shaker feel, physical latency, and final frequency tuning remain unvalidated until the BT-1 arrives and the full M-Audio -> Fosi -> Dayton BST-1 chain is tested locally.

## Stage 2A

- Stage 2A is documentation and readiness only; no P-HPR abstractions, input listener, mock output, protocol code, or real P-HPR output exists yet.
- Required SimPro, SimHub, Windows Device Manager, USBView, game-controller mapping, and later USBPcap/Wireshark data is still outstanding.
- No GT Neo paddle input discovery has been implemented yet.
- No cached `DrivingArmed` service has been implemented yet.
- No shift-intent event router has been implemented yet.
- No P700/P-HPR USB inventory or protocol hypothesis has been implemented yet.
- No real P-HPR USB writes are allowed unless the user says exactly: `I approve Phase 2 controlled P-HPR write testing`.
- Raw captures and private device inventories must stay uncommitted.

## Stage 2B

- Stage 2B adds abstractions and a mock-only P-HPR output skeleton, but no Windows Raw Input, DirectInput, HID listener, or real device discovery implementation exists yet.
- `ShiftIntentEvent` and source interfaces exist, but no shift intent router or paddle input event pipeline exists yet.
- `MockPhprOutputDevice` records clamped mock commands only; it is not a real protocol adapter and does not send USB writes.
- P-HPR safety defaults exist, but the full `PHprSafetyLimiter` is still deferred to Stage 2L.
- No P700/P-HPR read-only inventory, USB capture analysis, protocol hypothesis, SimPro/SimHub coexistence detection, or controlled write plan exists yet.

## Stage 2C

- `DrivingArmedStateService` evaluates cached snapshots only; it is not yet wired into the WPF app or paddle input pipeline.
- No Raw Input, DirectInput, HID reader, or real wheel/paddle device discovery exists yet.
- No `ShiftIntentRouter` exists yet, so accepted driving state does not route any gear pulse.
- Menu-safe gating uses the current `VehicleState` fields and may need refinement after live F1 25 menu, pause, garage, pit-lane, and start-line observations.
- Real P-HPR output remains forbidden before the exact approval phrase and is not implemented.
