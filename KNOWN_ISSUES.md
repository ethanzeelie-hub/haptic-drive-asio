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
- The Dayton BST-1 chain has been proven through SimHub, but Haptic Drive ASIO app-driven shaker feel, safe gain, latency, and frequency tuning are still not final.

## Stage 16

- ASIO driver-name discovery is implemented through Windows ASIO registry locations, but driver visibility still depends on how the vendor driver registers itself locally.
- The app exposes explicit ASIO selection, driver selection, channel selection, arming, routing diagnostics, and fake-backend tests, but a native ASIO callback streaming backend is not installed in this Stage 16 build.
- Selecting ASIO with the default unavailable backend can discover a driver but fail safely at backend open/start; this is readiness behavior, not physical output validation.
- Output channel count is known only when a backend can open the selected ASIO driver. Until then, channel choices are manual readiness selections and must be confirmed locally.
- The WPF shell still uses simple code-behind wiring and rebuilds the runtime pipeline when output mode/settings change; polished device profiles and persisted output selection are deferred.
- The deterministic synthetic benchmark remains bound to Null output by default for CI and hardware-absent development. Stage 18 follow-up adds a separate manual ASIO hardware test for short 40/50 Hz pulses through the selected M-Audio / M-Track ASIO output; local app-driven BST-1 validation is still pending.
- The Fosi amplifier is available, but no gain safety, physical shaker feel, physical latency, or frequency tuning is claimed.
- Haptic Drive ASIO app-driven Dayton BST-1 physical shaker testing is still pending deliberate local validation even though the chain has been proven through SimHub.
- Windows sound output visibility is explicitly treated as separate from ASIO driver visibility and does not prove ASIO usage.

## Stage 17

- Native ASIO streaming is implemented behind `IAsioOutputBackend`, but physical Dayton BST-1 output, physical gain safety, shaker feel, physical latency, and final frequency tuning remain unvalidated.
- The render path is output-owned and no longer driven by WPF `DispatcherTimer`, but final hardware callback behavior still depends on the selected local ASIO driver and must be checked manually with the full chain.
- The ASIO backend uses a small preallocated queue between app rendering and the driver callback; underrun and dropped-buffer diagnostics are surfaced, but they are not a final latency measurement.
- Stale telemetry is muted by wall-clock timeout so old live samples cannot drive effects indefinitely; effect behavior under network loss or game pause may need future refinement after real sessions.
- ASIO driver selection, output channel selection, and arming remain explicit. The app still must not auto-switch to ASIO or WASAPI.
- `NullAudioOutputDevice` remains the automated-test default. Hardware-dependent tests now run as hardware-safe readiness/pending checks by default rather than xUnit skips.
- Recording library polish, forwarding destination UI, advanced routing matrices, live graphing, real WASAPI output, physical calibration UI, and Simagic P-HPR output remain deferred.

## Stage 18

- The final pre-shaker software package is implemented: launch/runtime prerequisite handling, app settings persistence, UDP forwarding destination UI, recordings library UI, selected replay, packet-ID diagnostics, and copyable diagnostics reports are now available.
- App settings are separate from haptic profiles and persist theme, forwarding destinations, safe output selection, and normal device preferences. They still must not restore haptic running state, emergency mute, direct-output runtime state, or physical calibration.
- Stage 18 follow-up adds `Manual ASIO Bass Shaker Test` for deliberate short 40/50 Hz ASIO pulses through the selected real ASIO output. It requires ASIO Output mode, M-Audio / M-Track driver selection, arming, haptics running, emergency mute clear, normal mute off, and a valid selected output channel. The existing Null synthetic benchmark remains unchanged.
- Stage 18 follow-up adds `Paddle Gear Bench Test` for local GT Neo paddle validation without recent telemetry. Enable/arm are runtime-only, mapped paddles are still required, mock output sends no HID reports, and direct output is blocked unless all strict P-HPR direct gates are green.
- UDP forwarding now supports IP addresses, `localhost`, and DNS hostnames. Obvious enabled loopback to the local listener port `20778` is blocked in the UI.
- Recordings are listed from local app data with metadata summaries, but recording trimming, profile snapshots, and route snapshots are not implemented.
- Advanced routing matrices, live graphing, real WASAPI output, Simagic P-HPR output, and physical calibration UI remain outside the pre-BT-1 scope.
- The M-Audio -> Fosi -> Dayton BST-1 chain has been locally proven through SimHub, but Haptic Drive ASIO app-driven BST-1 output, safe physical gain, shaker feel, physical latency, and final frequency tuning remain pending deliberate local validation with the manual ASIO hardware test and later effect checks.

## Stage 18b

- Paddle Gear Bench Direct mode now schedules matching SimHub/F1 EC stop reports after `DurationMs`; fake-writer tests cover brake stop, throttle stop, both-target starts/stops, emergency-stop cancellation, and active-pulse cleanup.
- Startup may auto-refresh P-HPR candidates, auto-select the known `VID_3670/PID_0905` HID device-interface candidate by FeatureReport `0xF1` / 64-byte capability, and run no-output open-check/dry-run readiness work in the background. Startup still sends no P-HPR vibration command.
- The bench is enabled, auto-armed, and Direct-mode by default for local validation. It uses the normal Devices brake/throttle P-HPR gear-pulse strength/frequency/duration values; duplicate bench strength/frequency/duration controls are no longer part of the normal workflow.
- Direct bench output remains blocked unless the selected candidate is openable, uses FeatureReport transport with report ID `0xF1` and 64-byte shape, passes open-check/report-shape readiness, has clear coexistence, has clear emergency stop, and road/slip/lock direct routes are disabled.
- Defaults now prefer GT Neo paddle buttons left `14` and right `13`, but physical paddle mapping, safe gain, sustained behavior, emergency-stop physical behavior, road/slip/lock feel, and physical latency still require Ethan's supervised local validation.
- The ASIO/BST-1 audio path is unchanged by Stage 18b.

## Stage 2A

- Stage 2A is documentation and readiness only; no P-HPR abstractions, input listener, mock output, protocol code, or real P-HPR output exists yet.
- Required SimPro, SimHub, Windows Device Manager, USBView, game-controller mapping, and later USBPcap/Wireshark data is still outstanding.
- No GT Neo paddle input discovery has been implemented yet.
- No cached `DrivingArmed` service has been implemented yet.
- No shift-intent event router has been implemented yet.
- No P700/P-HPR USB inventory or protocol hypothesis has been implemented yet.
- Before Phase 3J approval, no real P-HPR USB writes were allowed unless the user said exactly: `I approve Phase 2 controlled P-HPR write testing`.
- Raw captures and private device inventories must stay uncommitted.

## Stage 2B

- Stage 2B adds abstractions and a mock-only P-HPR output skeleton, but no Windows Raw Input, DirectInput, HID listener, or real device discovery implementation exists yet.
- `ShiftIntentEvent` and source interfaces exist, but no shift intent router or paddle input event pipeline exists yet.
- `MockPhprOutputDevice` records clamped mock commands only; it is not a real protocol adapter and does not send USB writes.
- P-HPR safety defaults exist, and Stage 2L now adds the full mock-only `PHprSafetyLimiter`; no routing uses it yet.
- Later stages add protocol hypotheses, SimPro/SimHub coexistence detection, and a controlled write test plan, but no real P-HPR output exists.

## Stage 2C

- `DrivingArmedStateService` evaluates cached snapshots only; it is not yet wired into the WPF app or paddle input pipeline.
- No Raw Input, DirectInput, HID reader, or real wheel/paddle device discovery exists yet.
- No `ShiftIntentRouter` exists yet, so accepted driving state does not route any gear pulse.
- Menu-safe gating uses the current `VehicleState` fields and may need refinement after live F1 25 menu, pause, garage, pit-lane, and start-line observations.
- Real P-HPR output remains forbidden before the exact approval phrase and is not implemented.

## Stage 2D

- Input discovery is read-only and manual. The app enumerates Raw Input metadata and Windows game-controller capabilities only when Refresh Input Devices is pressed.
- No live paddle listener exists yet, so left/right paddle press events are not observed, debounced, or raised as `ShiftIntentEvent` values.
- No left/right paddle mapping exists yet because user-visible button IDs from Windows controller tools, Device Manager, USBView, SimPro Manager, and optional SimHub screenshots are still required.
- DirectInput-specific enumeration and HID input-report reading are deferred unless Raw Input and the built-in Windows game-controller capability path are insufficient.
- Candidate scoring is non-authoritative and uses names, HID usage metadata, and broad device class hints until exact Alpha Evo / GT Neo / P700 VID/PID and button data are supplied.
- No haptic routing, P-HPR output, USB output report, write-capable feature report, controlled write testing, SimPro control, or SimHub integration is implemented.

## Stage 2E

- The live paddle listener is read-only and currently uses the Windows game-controller button-state API. Raw Input live button decoding and HID input-report parsing remain deferred unless the game-controller path is insufficient.
- Manual mapping may still require user-provided left/right paddle button IDs from Windows controller tools, SimPro Manager, SimHub, or Haptic Drive ASIO's last-changed-button diagnostics.
- Mapped paddle presses now feed the Stage 2F shift-intent diagnostics layer, but the Stage 2E listener itself remains read-only and does not route haptics.
- `DrivingArmed`-gated paddle evaluation exists in Stage 2F, but no accepted intent is routed to any actuator yet.
- No P-HPR output, P-HPR USB write, output report, feature report, gear pulse, or controlled write testing is implemented.
- Device selection is persisted only as a safe input setting. If Windows changes the joystick index or device identity, the user may need to refresh devices and reselect the wheel input path.

## Stage 2F

- The Shift Intent Event Layer exists and can accept or suppress mapped paddle intent through cached `DrivingArmed` state, but no P-HPR routing exists yet.
- P-HPR protocol hypotheses now exist in Stage 2J, but no production protocol or output route exists.
- No mock P-HPR gear-pulse routing exists yet; `MockPhprOutputDevice` is not called by Stage 2F.
- No real P-HPR output, USB output report, feature report, vibration command, controlled write testing, SimPro control, or SimHub integration is implemented.
- No rejected-shift feedback output exists yet. `InstantWithRejectedShiftFeedback` records pending confirmation diagnostics only.
- No haptic output is triggered from paddles. Stage 2F does not call `GearShiftEffect`, ASIO output, the audio mixer, P-HPR output, or `PHprCommand`.
- `DrivingArmed` menu-safe gating may need refinement after live F1 25 menu, pause, garage, pit-lane, and start-line observations.

## Stage 2G

- Read-only P700 / P-HPR inventory tooling exists, but real P700/P-HPR hardware identity is still awaiting user-provided Device Manager, USBView, or tool output.
- The local Stage 2G inventory run found no Simagic-specific P700, P-HPR, Alpha Evo, or GT Neo candidates; no validated VID/PID, endpoint, report length, or P-HPR visibility claim is made.
- P-HPR modules may not appear as separate USB/HID devices and may be visible only through the P700 pedal controller.
- Registry, Raw Input, and Windows game-controller metadata can be incomplete, stale, or non-authoritative; candidate scoring is a research hint only.
- USB capture workflow and metadata tooling exists in Stage 2H, and read-only capture analysis tooling exists in Stage 2I.
- Protocol hypotheses now exist in Stage 2J, but no production protocol implementation exists.
- No mock P-HPR gear-pulse routing exists yet.
- No real P-HPR output, USB output report, feature report, vibration command, controlled write testing, SimPro control, or SimHub integration is implemented.

## Stage 2H

- Capture workflow documentation, scenario definitions, metadata template generation, filename building, metadata validation, sanitization, and sanitized manifest export are implemented. Stage 2I adds read-only capture analysis, but Stage 2H itself remains metadata-only.
- Real raw USB captures remain private and uncommitted. Stage 2I can analyze actual local captures or sanitized transfer summaries before any evidence-backed conclusions are made.
- The Stage 2H manifest intentionally contains sanitized metadata only and excludes raw capture bytes/content.
- Protocol hypotheses exist in Stage 2J, but no production protocol implementation exists.
- Mock P-HPR protocol and output diagnostics now exist in Stage 2K, but no mock routing exists yet.
- No mock P-HPR gear-pulse routing exists yet.
- No real P-HPR output, USB output report, feature report, vibration command, HID write, controlled write testing, SimPro control, or SimHub integration is implemented.

## Stage 2I

- Capture analysis tooling can read Wireshark CSV/text summaries and summarize pcap/pcapng containers, but it does not infer protocol fields or classify commands.
- pcap/pcapng support is container-level summary only; USBPcap protocol semantics remain future work if needed.
- Generated analysis reports under `capture-metadata/generated/` are ignored and should remain private unless deliberately reviewed as sanitized artifacts.
- The local `Complete Files Required` P-HPR evidence bundle was used as private/sanitized input context, but the bundle and generated analysis output are not committed.
- Protocol hypotheses now exist in Stage 2J, but no production protocol implementation exists.
- Mock P-HPR protocol and output diagnostics now exist in Stage 2K, but no mock routing exists yet.
- No mock P-HPR gear-pulse routing exists yet.
- No real P-HPR output, USB output report, feature report, vibration command, HID write, controlled write testing, SimPro control, or SimHub integration is implemented.

## Stage 2J

- Protocol hypotheses exist, but they are not a production implementation.
- SimHub `F1 EC` is a high-confidence observation and is marked ready for Stage 2K mock protocol only; it is not approved for real writes.
- SimPro `80 1E 89` is represented as a separate family, but field meanings remain conservative and are not promoted to a mock-ready command surface in Stage 2J.
- No production encoder or decoder exists.
- Mock P-HPR protocol and output diagnostics now exist in Stage 2K, but they remain mock-only and are not a production implementation.
- No real P-HPR output exists.
- No USB writes, HID output reports, HID feature reports, or vibration commands are implemented.
- Real write blockers remain: approval phrase, controlled write plan, stop validation, device/report/interface identity, coexistence behavior, emergency stop path, and `PHprSafetyLimiter`.

## Stage 2K

- Mock protocol exists, but no real P-HPR output exists.
- SimHub `F1 EC` mock frames are based on the Stage 2J `ReadyForMockProtocol` hypothesis and are not approved for hardware writes.
- SimPro `80 1E 89` remains `SimProUnknownMock` / `NeedsMoreCaptures`; detailed SimPro mock encoding is unsupported.
- `MockPhprOutputDevice` records commands and generated mock frames in memory only.
- The full P-HPR safety limiter is implemented in Stage 2L, but Stage 2K itself remains mock protocol/output only.
- Mock gear-pulse routing is added later in Stage 2M, not inside Stage 2K.
- Mock road vibration, wheel slip, and wheel lock routing is added later in Stage 2N, not inside Stage 2K.
- No production encoder or production decoder exists.
- No real P-HPR output, USB write, HID output report, HID feature report, vibration command, controlled write testing, SimPro control, or SimHub control is implemented.
- The ASIO/BST-1 audio path is unchanged by Stage 2K.

## Stage 2L

- The P-HPR safety layer exists and later stages connect accepted shift intents plus road/slip/lock mock effects to the safety-limited mock output; Stage 2L itself remains safety infrastructure only.
- `SafetyLimitedPhprOutputDevice` wraps `MockPhprOutputDevice` only; no real output adapter exists.
- Mock road vibration, wheel slip, and wheel lock routing is added later in Stage 2N, not inside Stage 2L.
- SimPro / SimHub coexistence detection is not implemented yet; Stage 2L has a synthetic conflict context placeholder only.
- No controlled write test plan exists yet.
- No production encoder or production decoder exists.
- No real P-HPR output exists.
- No USB writes, HID output reports, HID feature reports, vibration commands, device-handle writes, controlled write testing, SimPro control, or SimHub control is implemented.
- Raw/private captures, serial numbers, unsanitized hardware data, and generated local analysis exports remain uncommitted.
- The ASIO/BST-1 audio path is unchanged by Stage 2L.

## Stage 2M

- Mock gear pulse routing exists from accepted `ShiftIntentEvent` values through `PHprGearPulseRouter`, `SafetyLimitedPhprOutputDevice`, and `MockPhprOutputDevice`.
- The route is mock-only and records in-memory commands/frames; no real P-HPR output exists.
- No USB writes, HID output reports, HID feature reports, vibration commands, device-handle writes, controlled write testing, SimPro control, or SimHub control are implemented.
- Road vibration, wheel slip, and wheel lock mock routing exists in Stage 2N through a separate `PHprPedalEffectsRouter`, not inside `PHprGearPulseRouter`.
- SimPro / SimHub coexistence detection is not implemented yet.
- No controlled write plan existed in Stage 2L; Stage 2P later adds the plan without executing hardware writes.
- Emergency-stop state, safety latch state, mock command history, and mock frame history are runtime-only and not persisted.
- The ASIO/BST-1 audio path is unchanged by Stage 2M.

## Stage 2N

- Mock road vibration, wheel slip, and wheel lock routing exists through `PHprPedalEffectsRouter`, `SafetyLimitedPhprOutputDevice`, and `MockPhprOutputDevice`.
- The route is mock-only and records in-memory commands/frames; no real P-HPR output exists.
- Road/slip/lock heuristics use existing `VehicleState` fields and do not add new F1 25 parser fields. Live tuning may need refinement after real telemetry sessions and local hardware validation.
- Pedal effects are evaluated from the WPF telemetry/status update path, not the audio callback. This is intentional for Stage 2N mock diagnostics and not a final real-output timing claim.
- Gear routing and pedal effects share one WPF mock output stack, so clearing either mock diagnostics surface clears shared mock output history.
- No USB writes, HID output reports, HID feature reports, vibration commands, device-handle writes, controlled write testing, SimPro control, or SimHub control are implemented.
- SimPro / SimHub coexistence detection is not implemented yet.
- No controlled write plan existed in Stage 2N; Stage 2P later adds the plan without executing hardware writes.
- Emergency-stop state, safety latch state, mock command history, mock frame history, real-write enabled state, and real-write armed state are runtime-only and not persisted.
- The ASIO/BST-1 audio path is unchanged by Stage 2N.

## Stage 2O

- Read-only SimPro Manager / SimHub process detection exists and reports coexistence status into WPF diagnostics and P-HPR safety contexts.
- Detection is process-name based and conservative. It does not prove which application currently owns a P-HPR device, endpoint, interface, or report.
- Process access errors and unsupported platforms report `Unknown`; direct control remains blocked/warned until status is clear.
- `ActiveConflict` blocks P-HPR starts through `PHprSafetyLimiter`, but no real direct-control mode exists yet.
- No process kill, hook, injection, patching, memory inspection, IPC, settings modification, or external software control is implemented.
- No controlled write plan existed in Stage 2O; Stage 2P later adds the plan without executing hardware writes.
- No production encoder or production decoder exists.
- No real P-HPR output exists.
- No USB writes, HID output reports, HID feature reports, vibration commands, device-handle writes, controlled write testing, SimPro control, or SimHub control are implemented.
- The ASIO/BST-1 audio path is unchanged by Stage 2O.

## Stage 2P

- The controlled write test plan and manual validation runbook exist, but they have not been executed on real hardware.
- `PHprControlledWriteReadiness` intentionally blocks direct output during Stage 2P, even if future manual checklist fields are all true.
- WPF direct-write readiness diagnostics are disabled/read-only; no pulse buttons, real adapter, HID writer, or write-capable UI exists yet.
- Exact P700 VID/PID, report ID, interface, endpoint, descriptor, and device-open behavior still require local confirmation before real validation.
- SimHub `F1 EC` remains the preferred later minimal direct-control hypothesis; SimPro `80 1E 89` remains separate and not the first direct path.
- No production encoder or production decoder exists.
- No real P-HPR output exists.
- No USB writes, HID output reports, HID feature reports, vibration commands, device-handle writes, controlled write testing, SimPro control, or SimHub control are implemented or executed.
- Physical P-HPR validation is pending a local user-run procedure after gated implementation exists.
- The ASIO/BST-1 audio path is unchanged by Stage 2P.

## Stage 2Q

- A gated write-capable Windows HID P-HPR adapter now exists, but no real P-HPR hardware pulse has been executed or validated by Codex.
- Direct control remains disabled and unarmed by default. Enable state, armed state, selected device path, emergency-stop latch, command history, and write history are runtime-only and not persisted.
- Device path, interface, report ID, report length, report descriptor, endpoint, exclusive-open behavior, and brake/throttle module mapping still require local manual confirmation.
- The Stage 2Q encoder implements only the SimHub `F1 EC` hypothesis. SimPro Manager `80 1E 89` detailed writes remain unsupported.
- Real direct starts are blocked unless coexistence status is `Clear`; `Unknown`, `SimProRunning`, `SimHubRunning`, and `ActiveConflict` all block real starts.
- Stop and emergency-stop behavior is implemented in code with fake-writer tests, but real stop/off behavior on hardware remains unvalidated.
- Automated tests use fake HID writers only; no CI or automated verification writes to hardware.
- The ASIO/BST-1 audio path is unchanged by Stage 2Q.

## Stage 2R

- The controlled validation harness exists. User-run local direct pulse validation has confirmed brake and throttle pulses plus parameter response, but not every harness/safety-envelope check is complete.
- Exported validation results are private local files and must not be committed if they contain raw captures, private device paths, serial numbers, or unsanitized hardware data.
- A `pass` decision is blocked in the result model until required manual fields and hardware confirmations are present, but the app cannot independently verify physical truth.
- The harness does not trigger hardware output; brake, throttle, emergency stop, and paddle tests remain manual user actions.
- Remaining P-HPR physical validation includes wrong-pedal checks beyond the one-pulse cases, sustained-vibration checks, safe-gain confirmation, emergency-stop physical behavior, real coexistence behavior, road/slip/lock feel, and direct-pulse latency.
- The ASIO/BST-1 audio path is unchanged by Stage 2R.

## Phase 3A

- The real P-HPR output adapter now has explicit lifecycle, timeout, disconnect, report-validation, and close-on-dispose handling, but no real P-HPR hardware pulse has been executed or validated by Codex.
- Fake-writer tests cover adapter failure behavior, but they do not prove Windows HID exclusive-open behavior, real report descriptor details, P700/P-HPR endpoint ownership, or physical module response.
- A disconnected or faulted adapter state blocks later starts through the safety context until the selected output is reconfigured, but the physical reconnect workflow still needs local validation.
- Write timeout defaults are software safeguards, not physical latency measurements.
- Stop and emergency-stop report behavior is code-tested with fakes only; real stop/off behavior remains pending supervised local validation.
- Direct control remains disabled and unarmed by default, and enable/arm/device selection remain runtime-only and not persisted.
- The ASIO/BST-1 audio path is unchanged by Phase 3A.

## Phase 3B

- Instant paddle gear-pulse production integration is implemented, but no real P-HPR hardware pulse has been executed or validated by Codex.
- Software latency diagnostics show paddle, accepted-intent, command-created, and fake-writer write-completion timestamps; they are not physical latency measurements.
- Brake/throttle gear-pulse settings are persisted safely, but direct-control enablement, arming, selected private HID path, emergency-stop latch, command history, and write history remain runtime-only.
- The route still depends on cached `DrivingArmed` / Menu Safe state for suppression. Live menu, pause, garage, pit-lane, start-line, and stale-telemetry behavior may need local refinement after real F1 25 sessions.
- Real road vibration, wheel slip, and wheel lock routing are not implemented in Phase 3B; they remain later Phase 3 production-integration stages.
- Physical pedal mapping, safe strength, stop/off behavior, sustained-vibration behavior, SimPro/SimHub real-device coexistence, and physical latency remain pending supervised local validation.
- The ASIO/BST-1 audio path is unchanged by Phase 3B.

## Phase 3C

- Real road-vibration production routing is implemented through the same gated P-HPR output backend, but no real P-HPR hardware road vibration has been executed or validated by Codex.
- Road-vibration settings are persisted safely for brake and throttle, but direct-control enablement, arming, selected private HID path, emergency-stop latch, command history, and write history remain runtime-only.
- Road intensity is synthesized from existing `VehicleState` / telemetry state and conservative defaults; live surface feel, safe strength, frequency tuning, and physical sustained-vibration behavior remain pending supervised local validation.
- The route depends on telemetry freshness, cached `DrivingArmed` / Menu Safe state, haptics running state, emergency mute, SimPro/SimHub coexistence status, selected output readiness, and the safety limiter. Live menu, pause, garage, pit-lane, start-line, and stale-telemetry behavior may need local refinement after real F1 25 sessions.
- Wheel slip and wheel lock production routing were not implemented in Phase 3C itself; Phase 3D adds that route separately.
- Real road priority is below gear pulse, wheel slip, and wheel lock, but physical priority feel and interaction with real pedal modules remain unvalidated.
- The ASIO/BST-1 road texture effect remains separate and unchanged by Phase 3C.

## Phase 3D

- Real wheel-slip and wheel-lock production routing is implemented through the same gated P-HPR output backend, but no real P-HPR hardware slip or lock vibration has been executed or validated by Codex.
- Slip/lock settings are persisted safely for target, strength, frequency, duration, and per-effect enabled state, but direct-control enablement, arming, selected private HID path, emergency-stop latch, command history, and write history remain runtime-only.
- Slip and lock intensity are synthesized from existing `VehicleState` / telemetry state and conservative defaults; live slip feel, lock feel, safe strength, frequency tuning, and sustained-vibration behavior remain pending supervised local validation.
- The route depends on telemetry freshness, cached `DrivingArmed` / Menu Safe state, haptics running state, emergency mute, SimPro/SimHub coexistence status, selected output readiness, and the safety limiter. Live menu, pause, garage, pit-lane, start-line, and stale-telemetry behavior may need local refinement after real F1 25 sessions.
- Gear pulse remains the highest-priority P-HPR route, wheel lock is above wheel slip, and both are above road vibration, but physical priority feel and interaction with real pedal modules remain unvalidated.
- The ASIO/BST-1 slip and brake-lock audio effect remains separate and unchanged by Phase 3D.

## Phase 3E

- P-HPR workflow UI, P-HPR effect profile save/load, and diagnostics report coverage are implemented, but no physical P-HPR validation has been executed or recorded by Codex.
- P-HPR profiles save safe effect preferences only. Direct-control enablement, arming, selected private HID path, emergency-stop latch, command history, write history, and validation result data remain runtime-only or private local data.
- Loading a P-HPR profile can change active safe effect preferences while preserving the current runtime direct-control arm/device state; users should keep real direct control disabled/unarmed while editing profiles until manual validation is complete.
- Diagnostics intentionally summarize selected-output status without printing private raw HID paths, serial numbers, raw captures, or unsanitized hardware inventories.
- The UI is still WPF code-behind rather than a full MVVM workflow. Further polish may be useful after live replay and live F1 validation.
- Physical P-HPR workflow usability, wrong-pedal checks, sustained-vibration behavior, safe gain, and physical latency remain pending supervised local validation.

## Phase 3F

- Integrated replay validation uses deterministic synthetic replay packets and mock P-HPR output only; it does not validate live F1 25 sessions or physical P-HPR behavior.
- Replay can drive road, slip, and lock software routing from `VehicleState`, but real direct-control output still requires explicit manual enablement, arming, selected device/interface/report, clear coexistence, and local supervision.
- Replay does not synthesize GT Neo paddle events. Instant gear-pulse validation still requires read-only paddle input or a later explicit synthetic-input test path.
- Replay-source diagnostics show the source file name or in-memory status only. Raw captures, full private paths, serial numbers, and unsanitized device inventories remain excluded.
- Live menu suppression, pause behavior, SimPro/SimHub coexistence on the real device, and physical pedal response remain pending manual live validation.

## Phase 3G

- The manual live F1 25 P-HPR validation workflow is implemented as a passive checklist and diagnostics line, but no live F1 25 session or physical P-HPR hardware validation has been completed or recorded by Codex.
- The checklist reports current telemetry, `DrivingArmed`, paddle listener, shift-intent, output mode, coexistence, selected-output, and emergency-stop state, but it cannot prove physical truth.
- Automated tests cover checklist generation only and do not run F1 25, open HID devices, send P-HPR reports, or vibrate hardware.
- Actual brake/throttle mapping, emergency-stop physical behavior, wrong-pedal checks, sustained vibration, safe strength, physical latency, road feel, slip feel, lock feel, and real SimPro/SimHub coexistence remain pending Ethan's supervised local run.
- The ASIO/BST-1 audio path is unchanged by Phase 3G.

## Phase 3H

- The final P-HPR acceptance package is documentation-only and does not complete physical validation.
- `docs/QUICK_START.md`, `docs/TROUBLESHOOTING.md`, and `docs/FINAL_P_HPR_ACCEPTANCE.md` summarize current workflows, but local hardware evidence is still required before marking P-HPR physically accepted.
- Remaining physical risks are unchanged: pedal mapping, emergency-stop physical behavior, wrong-pedal behavior, sustained vibration, safe gain, physical latency, road feel, slip feel, lock feel, and real SimPro/SimHub coexistence.
- The ASIO/BST-1 audio path is unchanged by Phase 3H.

## Phase 3I

- The normal Devices UI is simplified, but physical P-HPR behavior is still unvalidated.
- Advanced diagnostics can be shown from a persisted preference, but real direct control remains disabled/unarmed by default and still requires explicit enable, arm, selected device/interface/report, clear coexistence, and clear emergency stop.
- The user-facing P-HPR range is now 0-100% strength, 1-50 Hz, and 10-1000 ms; these are software limits and still are not validated safe physical gain or final tuning.
- Mock test pulses are software/mock only unless Direct mode is explicitly ready; they do not prove physical brake/throttle mapping, stop behavior, or sustained-vibration safety.
- The ASIO/BST-1 audio path is unchanged by Phase 3I.

## Phase 3J

- Controlled P-HPR write testing is approved and the `controlled-write-test` CLI exists, but this commit did not execute a real hardware pulse.
- The direct-output picker now treats `VID_3670` HID entries as Simagic-family candidates and can apply an openable HID device-interface private path internally, but physical validation still requires Ethan's local supervised run.
- Raw Input metadata-only candidates can help identify Simagic-family hardware, but they are not openable HID output targets and cannot pass real direct-output gates.
- The `controlled-write-test --execute` CLI path still requires the exact approval phrase and local physical presence before real writes. Normal app direct paths now use runtime Direct mode enablement plus selected openable HID device-interface candidate, successful no-report open-check, known HID output-report capability or successful no-command report-shape validation, clear SimPro/SimHub coexistence, and clear emergency stop.
- A local `VID_3670/PID_0905` candidate can be openable while exposing feature reports but no output-report length. The picker now surfaces feature report ID `0xF1` and can validate the FeatureReport shape without writing, but real pulses remain blocked unless the selected output/feature transport, report ID, report length, no-command shape validation, open-check, coexistence, and emergency-stop gates all pass. The correct physical behavior still requires Ethan's supervised hardware validation.
- Console output, copied diagnostics, docs, and sanitized exports intentionally hide private HID paths; command history or local validation notes may still contain private local data if Ethan types a path manually; do not commit those artifacts.
- The full automated suite now reports zero skipped tests, but zero skipped tests are readiness coverage, not physical validation.
- User-run local validation has confirmed brake and throttle direct pulses plus parameter response. Emergency-stop physical behavior, sustained vibration, safe gain, road/slip/lock feel, physical latency, and real SimPro/SimHub coexistence remain pending Ethan's local run.
- The ASIO/BST-1 audio path is unchanged by Phase 3J.

## Stage 18c

- Paddle input auto-selection now prefers a saved usable controller, then the 32-button `VID_3670/PID_0905` Windows game-controller, and blocks automatic selection/listener start for 0-button-only devices. If Windows exposes only 0-button candidates, the listener remains blocked until a usable button-capable device appears.
- Direct Paddle Gear Bench pulses now use the shared direct P-HPR output path and expose active-pulse, pending-stop, last-start, last-stop, stop-result, stop-target, and scheduled-duration diagnostics. These software diagnostics do not prove physical stop feel or sustained-vibration safety.
- Direct bench routing is limited to mapped paddle events from the visible listener path and still requires direct readiness, FeatureReport `0xF1`, 64-byte report shape, successful open-check, clear coexistence, clear emergency stop, and disabled road/slip/lock routes.
- Automated coverage uses fake HID writers, fake stop clocks, and read-only/fake input paths only. Physical GT Neo input behavior, real P-HPR stop behavior, emergency-stop physical behavior, sustained vibration, safe gain, road/slip/lock feel, physical latency, and real SimPro/SimHub coexistence remain pending Ethan's local validation.
- The ASIO/BST-1 audio path is unchanged by Stage 18c.

## Stage 18d

- Direct Paddle Gear Bench now routes through the same Devices-tab direct pulse service used by the blue Test Brake/Throttle buttons and no longer uses a bench-only pulse planner. Physical stop behavior still must be revalidated locally after the runaway-output report.
- Bench target defaults to Both; both mapped paddles intentionally trigger the selected output target. Release events and retriggers while a direct pulse is active or awaiting stop are blocked in software.
- Direct timed pulses now have a `DurationMs + 100 ms` watchdog that forces stop-all if the target remains active, and emergency stop attempts brake and throttle stop reports independently with retries. These safeguards improve software safety but do not prove physical emergency-stop behavior until Ethan validates the actual hardware chain.
- Sanitized local crash-state logs are written on unhandled app/task failures under local app data; they avoid private HID paths and are not intended for repository commits.
- The ASIO/BST-1 audio path is unchanged by Stage 18d.

## Stage 18e

- Direct Paddle Gear Bench output is now owned by a runtime state machine with stop-only startup cleanup, a local unclean-shutdown marker, serialized direct commands, and a local JSONL flight recorder. These software contracts reduce crash/runaway risk but still do not prove physical stop feel, sustained-vibration safety, safe gain, physical latency, or real coexistence behavior.
- If the unclean marker exists, Direct Bench starts are blocked until `P-HPR Stop All / Clear Device State` succeeds. This recovery path sends stop-only reports and must not be treated as proof that the physical modules responded unless Ethan confirms it locally.
- The flight recorder and marker are local validation artifacts under `local-validation-results/`; they should not be committed, and private HID paths remain redacted from recorder entries.
- Startup cleanup may send stop-only brake/throttle reports when a selected direct device is already configured, but it never sends active/start/vibration reports.
- The F1 25 parser, UDP forwarding, recording/replay raw-byte preservation, confirmed P-HPR report bytes, normal telemetry `DrivingArmed` routing, and ASIO/BST-1 audio path are unchanged by Stage 18e.

## Stage 18f

- The Direct Paddle Gear Bench WPF cross-thread crash is hotfixed in software by marshaling paddle-path status updates to the UI dispatcher and recording/recovering paddle callback exceptions. This does not prove physical P-HPR duration, stop feel, safe gain, physical latency, or real coexistence behavior.
- If a paddle-path exception occurs after a direct bench pulse may have started, the runtime records it to the local flight recorder and attempts stop-all recovery. The local flight recorder and marker remain local validation artifacts and must not be committed.
- Blue Test Brake/Throttle Pulse and Direct Paddle Gear Bench still use the same shared direct pulse service and confirmed FeatureReport `0xF1` / 64-byte command format; ASIO/BST-1, F1 25 parsing, UDP forwarding, and recording/replay paths are unchanged.

## Stage 18g

- Direct Paddle Gear Bench now uses latest-press-wins retriggering with per-module generation-guarded scheduled stops, but physical rapid downshift feel and real latency remain unvalidated until Ethan tests the P-HPR hardware locally.
- Older scheduled stops are ignored in software when their generation no longer matches; this is covered with fake HID writers and fake clocks only.
- Stale paddle work older than the 80 ms software threshold is dropped and recorded rather than played late. This does not prove the real Windows HID stack or module firmware latency.
- Paddle debounce defaults to 5 ms and remains per mapped button; Ethan may still need to tune debounce locally if the physical paddles bounce or double-fire.
- Emergency Stop and Stop All override generations in software, but physical emergency-stop behavior still requires supervised local validation.
- ASIO/BST-1, F1 25 parser, UDP forwarding, recording/replay raw-byte preservation, SimPro/SimHub coexistence, and confirmed P-HPR report bytes are unchanged by Stage 18g.

## Stage 18i

- Channel 1 is the locally validated BST-1 ASIO output channel, but safe gain, final physical feel, and physical latency remain Ethan-local validation items.
- Manual BST-1 pulse can run as a short ASIO-only local test without Start Haptics, but it still requires ASIO Output, selected M-Audio/M-Track driver, ASIO arm, valid selected channel, clear emergency mute, and normal mute off.
- Windows Sound Settings visibility does not prove ASIO usage; use the in-app ASIO status and ASIO driver/callback diagnostics.
- BST-1 Paddle Gear Bench output is disabled by default and only runs from accepted mapped `Pressed` bench events when explicitly enabled.
- BST-1 gear-pulse duration can sync to P-HPR gear pulse duration or use a custom BST-1 duration because the Dayton shaker may feel different from the P-HPR modules.
- `local-validation-results/bst1-asio-gear-flight-recorder.jsonl` is local/ignored validation output and must not be committed.
- Automated coverage uses fake ASIO output and does not require M-Audio, Fosi, Dayton BST-1, ASIO driver installation, Simagic hardware, F1 25, or live telemetry.
- P-HPR command format, HID report shape, paddle mappings, F1 25 parser, UDP forwarding, and recording/replay raw-byte preservation are unchanged by Stage 18i.

## Stage 18j

- Manual BST-1 pulse and BST-1 local gear-test pulse readiness is now separated from ASIO stream-running status, so an armed selected ASIO path can be ready even while the continuous stream is stopped.
- Last manual and last gear ASIO proof fields are software diagnostics only. They show that the app accepted and submitted the last bounded pulse through the ASIO path; they do not prove physical shaker feel, safe gain, or latency.
- Local Gear Test mode can start the mapped paddle listener and route local bench pulses without Start Haptics, UDP telemetry, live F1 25, or cached `DrivingArmed`, but it still requires valid paddle mapping plus the relevant P-HPR direct and/or BST-1 ASIO gates.
- The shared gear-pulse duration now drives brake P-HPR, throttle P-HPR, Direct Paddle Gear Bench, and BST-1 sync mode. BST-1 custom duration remains available only when sync is unchecked.
- Automated coverage still uses fake ASIO/P-HPR/input paths and does not require M-Audio, Fosi, Dayton BST-1, Simagic hardware, F1 25, live telemetry, or physical vibration.

## Stage 18k

- When the M-Audio M-Track Solo and Duo ASIO driver is discoverable, startup now selects ASIO Output, that driver, channel `1`, and Arm ASIO, but it does not start the ASIO stream or emit output on launch. If the driver is not discoverable, startup stays on Null output.
- Manual `Test BST-1 Pulse` and enabled BST-1 local paddle gear pulses use a standalone bounded ASIO pulse session when Start Haptics is stopped. They still require ASIO Output, selected M-Audio/M-Track driver, valid channel, Arm ASIO, clear emergency mute, normal mute off, and the mixer/safety/limiter path.
- `ASIO READY` means the selected/armed/channel/error gates are ready while the stream may be stopped. `ASIO ACTIVE` is reserved for actual running stream plus callback-active output.
- BST-1 output trim defaults to `200%` and scales only the ASIO bass-shaker pulse before the existing safety chain/limiter. It does not affect P-HPR strength scaling.
- `Select channel 1` is now pure channel selection and must not vibrate; `Test BST-1 Pulse` is the normal manual output button.
- Final safe physical gain, final shaker feel, and physical latency remain Ethan-local validation items; automated coverage uses fake ASIO/P-HPR/input paths only.

## Stage 18l

- The software queue-full/drop failure in the standalone BST-1 pulse path is fixed in fake-backed tests by waiting for ASIO callback activity and queue room before submitting pulse buffers.
- `local-validation-results/bst1-asio-pulse-flight-recorder.jsonl` is local/ignored validation output and must not be committed.
- Normal close now disposes ASIO/listener/timer resources and writes shutdown diagnostics unless a future tray-minimize implementation intentionally intercepts close.
- Physical shaker feel, safe gain, physical latency, and final frequency tuning still require Ethan-local validation on the real M-Audio/Fosi/Dayton chain.

## Stage 18m

- Stage 18m fixes software issues found from local `bst1-asio-pulse-flight-recorder` evidence: early `pulse-completed` records are no longer allowed when expected frames have not actually rendered.
- Manual and local paddle BST-1 pulses use the same request settings, waveform generator, mixer/safety/limiter path, output trim, and channel routing whether Start Haptics is off or on. When haptics are already running, the pulse completes through the running callback instead of a competing submit loop.
- ASIO driver/channel capability is hydrated by opening the selected ASIO output for readiness without starting output, so fresh startup can cache the M-Audio output-channel count before `Test BST-1 Pulse`.
- A pre-open channel-count `0` snapshot no longer blocks channel `1` as "outside 0 channels"; actual capability/open failures surface their real error.
- `ASIO ACTIVE` and recorder `AsioCallbackActive` now require a currently started ASIO stream, not only historical callback counts.
- If `Minimize to tray on close` remains unchecked, the window close path must not be cancelled for tray behavior. Close performs bounded cleanup, writes shutdown diagnostics, and then lets WPF close normally.
- `local-validation-results/bst1-asio-pulse-flight-recorder.jsonl` and rotated `.jsonl.1` files are local validation evidence only and must not be committed.
- Physical shaker feel, safe gain, physical latency, and final frequency tuning still require Ethan-local validation on the real M-Audio/Fosi/Dayton chain.

## Stage 18n-B

- Local/manual BST-1 pulses now use a persistent output-owned callback path when Start Haptics is stopped, but this is still software/fake-backed proof until Ethan validates the real M-Audio/Fosi/Dayton chain.
- Pulse-owned generated/consumed frame counts plus post-limiter peak/RMS energy are required before a non-zero BST-1 pulse can be recorded as `completed-full`; global callback movement alone is not a physical-output guarantee.
- Haptics-on and haptics-off local BST-1 pulse equivalence is covered by fake ASIO callback tests. Physical latency, final safe gain, and shaker feel remain unvalidated.
- Direct Paddle Gear Bench has enough direct-control limiter headroom for ten 5 ms-spaced left/downshift fake pulses targeting both P-HPR modules. Real P-HPR rapid retrigger feel, stop behavior, and emergency-stop behavior still require supervised local validation.
- Ordinary Direct Bench start rejections should not trigger Stop All unless a partial unsafe write may have occurred. If a real partial write is suspected, fail-closed Stop All remains the expected recovery path.
- The disabled tray checkbox placeholder was removed; there is still no implemented minimize-to-tray mode.

## Stage 18o-B

- BST-1 and P-HPR road texture now share one software `RoadTextureSignal`, but final physical road feel, safe gain, physical latency, and frequency tuning still require Ethan-local validation on the real M-Audio/Fosi/Dayton and P-HPR chains.
- Gear-pulse road ducking is covered by fake-backed tests only. The exact mixed-output feel and priority balance may need local tuning once both hardware paths are validated together.
- P-HPR road routing still uses the existing per-pedal min/max settings and safety limiter. Stage 18o-B does not add a new direct road scalar or automatic physical calibration workflow.
- Direct Paddle Gear Bench can now coexist with enabled road vibration in software, but emergency stop, Stop All, slip, lock, coexistence, selected output, and safety-limiter gates remain the expected higher-priority controls.
- Automated coverage does not require M-Audio, Fosi, Dayton BST-1, Simagic hardware, F1 25, live telemetry, or physical vibration.

## Stage 18p-A

- The product UI architecture report is complete, but the modern dark/sidebar/card rewrite is not implemented yet.
- `MainWindow.xaml` and `MainWindow.xaml.cs` remain large monolithic files. Future visual work should first extract shared `Theme.xaml` / `Styles.xaml` resources, then split stable page areas into smaller controls.
- Normal P-HPR road/slip/lock tuning is still partly buried in Advanced / Diagnostics. Later 18p stages should move normal tuning into Effects while keeping raw HID, validation, and low-level diagnostics advanced-only.

## Stage 18p-B

- Normal WPF replay is now time-preserving by default, but physical haptic feel and latency still require Ethan-local hardware validation on the real output chain.
- Fast replay remains available as explicit `Fast debug` mode for parser/diagnostic work and is not suitable for feel or latency testing.
- Delete Selected is guarded to `.hdrec` files inside the recordings folder and blocks the active recording output, but it is still a direct deliberate delete action rather than a two-step confirmation dialog.
- Recording files still do not include route snapshots, profile snapshots, trimming metadata, or effect-setting snapshots.
- The broader dark/sidebar/card product UI redesign remains staged for 18p-C onward.

## Stage 18p-C

- The dark/sidebar/card shell is now implemented as a shared WPF theme/style layer, but the Effects, Devices, Routing / Mixer, and Advanced pages are still structurally the same large in-place XAML panels.
- The new visual system is dark-first with red accents. Light theme remains supported by the existing toggle, but final visual polish and contrast review remain staged for 18p-F.
- Stage 18p-C intentionally does not move normal P-HPR road/slip/lock controls out of Advanced or restructure Effects into the final hybrid hardware/effect layout; those remain 18p-D/18p-E work.
- Physical shaker feel, safe gain, physical latency, and P-HPR physical behavior remain Ethan-local validation items; this stage changed WPF presentation resources only.

## Stage 18p-D

- Effects is now grouped by Shared / Global Effect Settings, BST-1 Seat Shaker, Brake P-HPR, and Throttle P-HPR, but the page still lives in the large `MainWindow.xaml` file until later component splitting is justified.
- Devices still contains broader hardware readiness, manual tests, paddle listener/mapping, and Direct Paddle Gear Bench controls. Full Devices cleanup remains staged for 18p-E.
- Advanced still contains direct candidate/report internals, validation harnesses, mock routers, low-level P-HPR min/max ranges, command-duration fields, target overrides, and raw diagnostics. Full Advanced cleanup remains staged for 18p-E.
- Road texture is shown as continuous/synthetic and has no normal pulse duration in Effects, but final mixed BST-1/P-HPR road feel, priority balance, safe gain, physical latency, and frequency tuning still require Ethan-local hardware validation.
- Stage 18p-D changed UI structure and settings-control placement only; it does not prove physical P-HPR slip/lock/road behavior or BST-1 shaker feel.

## Stage 18p-E

- Devices now keeps the hardware setup/readiness/manual-test surface, while Local Gear Test and Paddle Gear Bench internals are behind the Advanced diagnostics gate.
- Advanced / Diagnostics still lives inside the large `MainWindow.xaml` file; later component extraction may be useful if the page grows further.
- The moved bench controls still use the existing runtime-only safety gates and handlers. This stage does not persist direct arming, add unattended hardware writes, or validate physical P-HPR behavior.
- Physical shaker feel, safe gain, physical latency, final BST-1 tuning, and final P-HPR feel remain Ethan-local validation items.

## Stage 18p-F

- Routing / Mixer now summarizes output routing, software gain/mute/limiter state, priority, ducking, and active effects, but it does not add new routing logic or physical calibration.
- The Stage 18p UI restructure is visually consistent enough to move on from UI reshuffling; future work should focus on Ethan-local physical road-texture validation/tuning and final hardware feel.
- `MainWindow.xaml` and `MainWindow.xaml.cs` remain large monolithic files. Future component extraction can be considered after hardware validation pressure is lower.
- Physical shaker feel, safe gain, physical latency, final BST-1 tuning, road texture balance, and final P-HPR feel remain Ethan-local validation items.

## Stage 18q-B

- Road texture now has richer live/replay diagnostics and an optional local JSONL flight recorder, but this stage intentionally does not tune road feel, change P-HPR road cadence, or alter gear priority.
- The BST-1 road proof fields estimate road contribution from the shared road signal, mixer gain, safety output gain, and limiter state. The existing output peak remains total output, not road-only physical proof.
- P-HPR road diagnostics now distinguish route attempts, routed commands, stale telemetry, gear ducking, interval/safety/rate suppressions, higher-priority effects, and in-flight drops, but they do not prove physical P-HPR cadence or feel.
- `local-validation-results/road-texture-flight-recorder.jsonl` is local/ignored validation evidence and must not be committed.
- Earlier Ethan-local physical findings remain open: P-HPR road enabled produced sparse 3-5 second gaps and occasional thumps, while the exported diagnostics were captured with P-HPR road off.

## Stage 18q-C/D/E/F

- BST-1 / ASIO road output gain can now be raised to 100% for local tuning, but the previous 25% setting remains the conservative starting point. The new maximum is not a universal safe physical gain.
- Shared road signal enablement is now separate from BST-1, brake P-HPR, and throttle P-HPR output toggles. If the shared signal is off, road output should be suppressed everywhere even when output-specific road toggles are enabled.
- P-HPR road now uses a bounded continuous cadence model with overlapping duration, explicit stops, and hold-timeout watchdog diagnostics, but physical cadence feel, safe strength, stop behavior, and mixed road/gear priority still require Ethan-local validation.
- If P-HPR road still feels like sparse thumps, sticks on, blocks gear pulses, triggers command-rate suppression, or fails to stop on Emergency Stop / Stop Haptics / app close, stop the run and keep the diagnostics export plus `local-validation-results/road-texture-flight-recorder.jsonl`.
- Stage 18q-F documentation gives the local validation order, but the project still must not claim final road feel, physical latency, safe gain, or frequency tuning from software/fake-backed tests alone.

## Stage 18r-B

- Normal audio tuning now auto-saves to `default.hdprofile.json`, so deliberate temporary experimentation can persist unless the user loads/resets another profile. This is intentional for sim-style workflow but still means the default profile is the live working profile.
- Safe output selection, replay mode, BST-1 local paddle gear settings, and paddle mapping persist across launches. Haptics running, emergency mute, direct P-HPR enable/arm/private-device state, active pulses, pending stops, and bench-active state remain runtime-only and must be re-established deliberately.
- Routing / Mixer no longer exposes the conservative ceiling or limiter toggle as normal-user controls. Advanced diagnostics and runtime summaries still need to be used if deeper safety-chain inspection is required.
- Rename Selected blocks overwrite and active-recording output, but it remains a single deliberate action rather than a confirmation dialog or in-place list editing workflow.
- Physical shaker feel, safe physical gain, physical latency, and final road/slip/lock tuning remain Ethan-local validation items. Stage 18r-B changes persistence/defaults/UI language only and does not claim physical validation.

## Stage 18r-C

- Arm ASIO can now persist as a safe readiness preference alongside saved ASIO driver/channel selection, but restoring that preference still must not start haptics, start the ASIO stream, or emit output on launch.
- Paddle debounce now persists through the normal Devices-tab save path, but stale Windows controller identity changes can still require manual refresh/reselection of the mapped wheel input device.
- BST-1 road speed/frequency/grain controls now expose more tuning headroom and carry the road speed reference through roughly F1 top-speed range, but final asphalt feel, safe gain, physical latency, and exact high-speed balance remain Ethan-local validation items on the real M-Audio/Fosi/Dayton chain.
- Stage 18r-C intentionally does not change gear runtime timing, P-HPR HID/protocol/runtime, slip/lock tuning, or claim physical validation.

## Stage 18r-D

- BST-1 wheel slip and wheel lock now have separate normal tuning controls and profile fields, but they still share one software evaluator/render path. Final physical separation of feel, safe gain, and real-world balance remains Ethan-local validation on the real M-Audio/Fosi/Dayton chain.
- Older combined slip profiles are migrated conservatively into the new split slip/lock settings, but Ethan may still want to revisit wheel-lock gain/frequency/roughness after loading a much older profile because only the legacy combined enabled/gain values can be inherited directly.
- The new BST-1 slip/lock diagnostics explain current source, thresholds, and raw telemetry indicators, but they are software-side estimates and do not prove physical shaker output, latency, or tyre/ABS realism.
- Stage 18r-D intentionally does not change P-HPR slip/lock routing, road tuning, gear timing, parser layouts, or claim physical validation.

## Stage 18r-E/F

- Real P-HPR slip/lock now uses a bounded continuous cadence runtime with explicit stop commands and a hold-timeout watchdog, but physical tyre-scrub feel, safe gain, real pedal stop feel, and physical latency still require Ethan-local validation on the real brake/throttle modules.
- The new real P-HPR slip/lock defaults are software-safe starting points only. They are clamped to the existing direct-control safety bounds and should not be treated as final physical tuning.
- P-HPR road now yields while real P-HPR slip/lock is actively holding a module, and accepted gear pulses suppress both road and slip/lock briefly for protection, but the physical interaction between continuous road, slip/lock, and real pedal hardware still remains unvalidated.
- Slip/lock diagnostics now report cadence, hold timeout, raw telemetry inputs, active/inactive reason, stop counts, stale suppression, command-rate suppression, and last start/update/stop age, but those are software-side observations and do not prove physical module behavior.
- Stage 18r-E/F intentionally does not change BST-1 road/slip/lock tuning, gear timing, P-HPR HID/report bytes, direct writer transport, parser layouts, or claim physical validation.

## Stage 19A

- `MainWindow.xaml.cs` still owns real P-HPR continuous runtime startup and loop orchestration through `StartRealSlipLockRuntime` / `RunRealSlipLockRuntimeAsync` and `StartRealRoadVibrationRuntime` / `RunRealRoadVibrationRuntimeAsync`.
- `MainWindow.xaml.cs` still owns GT Neo paddle event routing through `PaddleInputSource_PaddleInputReceived`.
- `PHprDirectRuntime.cs` and `PhprDeviceCardPulseService.cs` still live in `HapticDrive.Asio.App`. Stage 19A intentionally does not move them directly into `HapticDrive.Asio.Runtime` because that would add `HapticDrive.Asio.Runtime -> HapticDrive.Actuation` while `HapticDrive.Actuation -> HapticDrive.Asio.Runtime` already exists.
- `SlipEffect` and `PHprSlipLockRouter` still duplicate core slip/lock threshold and attenuation logic. A shared evaluator remains later work.
- Stage 19A adds project-graph guardrails and shared direct-pulse-path tests only. Continuous road/slip/lock loop extraction, paddle-routing extraction, and broader `MainWindow` decomposition remain future stages.
- Physical P-HPR behavior, safe gain, physical latency, and mixed-output feel remain Ethan-local validation items; Stage 19A changes tests and architecture docs only.

## Stage 19B

- `MainWindow.xaml.cs` still owns real P-HPR continuous runtime startup and loop orchestration through `StartRealSlipLockRuntime` / `RunRealSlipLockRuntimeAsync` and `StartRealRoadVibrationRuntime` / `RunRealRoadVibrationRuntimeAsync`. Stage 19C should extract those loops.
- `MainWindow.xaml.cs` still owns GT Neo paddle event routing through `PaddleInputSource_PaddleInputReceived`. Stage 19D should extract that route.
- `PHprGearPulseTarget`, `PHprGearPulseProfile`, `PaddleGearBenchTestOutputMode`, `PaddleGearBenchTestOptions`, and `PaddleGearBenchTestResult` now live in `HapticDrive.Simagic.PHPR.Abstractions.Routing` instead of `HapticDrive.Actuation.PHpr`.
- `PHprDirectRuntime.cs`, `PhprDeviceCardPulseService.cs`, and `PaddleGearBenchDirectGate.cs` now live in `HapticDrive.Simagic.PHPR.Output.Windows` instead of `HapticDrive.Asio.App`. This reduces App-owned non-UI runtime code, but it does not yet remove `MainWindow` as the owner of the continuous road/slip/lock and paddle-entry workflows.
- `SlipEffect` and `PHprSlipLockRouter` still duplicate core slip/lock threshold and attenuation logic. Stage 20 should introduce the shared evaluator.
- Physical P-HPR behavior, safe gain, physical latency, and mixed-output feel remain Ethan-local validation items. Stage 19B changes dependency direction, runtime ownership boundaries, tests, and docs only.

## Stage 19C

- `PHprContinuousEffectsRuntimeCoordinator` now owns the real P-HPR continuous road and slip/lock background loops in `HapticDrive.Actuation.PHpr`, so `MainWindow.xaml.cs` no longer declares those loop body methods.
- `MainWindow.xaml.cs` still owns GT Neo paddle event routing through `PaddleInputSource_PaddleInputReceived`. Stage 19D should extract that route.
- `MainWindow.xaml.cs` still owns UI-only settings reads/writes, diagnostics/status formatting, and startup/shutdown orchestration around the coordinator and output device. Stage 19C intentionally does not attempt a broader MVVM rewrite.
- `SlipEffect` and `PHprSlipLockRouter` still duplicate core slip/lock threshold and attenuation logic. Stage 20 should introduce the shared evaluator.
- Physical P-HPR behavior, safe gain, physical latency, and mixed-output feel remain Ethan-local validation items. Stage 19C changes continuous loop ownership, tests, and docs only.

## Stage 19D

- `PaddleInputRoutingCoordinator` now owns the non-UI runtime response to mapped paddle input events, so `MainWindow.xaml.cs` no longer declares the old bench-routing helper methods or the substantive `PaddleInputSource_PaddleInputReceived` body.
- The coordinator intentionally remains in `HapticDrive.Asio.App` for now because the live route still spans internal `IPHprDirectRuntime` APIs plus App-owned BST-1 manual ASIO test injection. Stage 19D avoids a worse dependency move by using a non-WPF App service boundary instead of forcing that route into `HapticDrive.Actuation` or `HapticDrive.Asio.Runtime`.
- `MainWindow.xaml.cs` still owns UI-only status/control updates, footer/status formatting, safety-context builders, and settings parsing around the coordinator. Stage 19D intentionally does not attempt a broader MVVM rewrite.
- `SlipEffect` and `PHprSlipLockRouter` still duplicate core slip/lock threshold and attenuation logic. Stage 20 should introduce the shared evaluator.
- Physical P-HPR behavior, safe gain, physical latency, and mixed-output feel remain Ethan-local validation items. Stage 19D changes paddle-route ownership, tests, and docs only.

## Stage 20

- `SlipLockEvaluator` now centralizes the shared BST-1, mock P-HPR, and real direct P-HPR slip/lock freshness, sanitization, threshold, speed-scale, and TC/ABS attenuation logic in `HapticDrive.Asio.Core.Haptics`, so `SlipEffect`, `PHprPedalEffectsRouter`, and `PHprSlipLockRouter` no longer maintain separate copies of that math.
- `MainWindow.xaml.cs` still owns broader UI-only settings/status formatting and shell orchestration around the non-UI coordinators. Stage 20 intentionally does not attempt MVVM or general WPF decomposition.
- Stage 20 intentionally does not change UI/XAML, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, or parser layouts.
- Physical BST-1 shaker feel, physical P-HPR slip/lock feel, safe physical gain, and mixed-output latency still require Ethan-local validation and later tuning.

## Stage 21A

- `MainWindow.xaml.cs` still remains the dominant App-shell file and still owns startup/load wiring, shutdown/dispose ordering, settings/profile hydration, control parsing, safety-context construction, recording/replay UI workflow, diagnostics-panel assembly, and general WPF status/footer/dashboard updates.
- Stage 21A extracts only the lowest-risk P-HPR workflow/status presentation logic into `PhprWorkflowStatusSnapshotBuilder` and `PhprWorkflowStatusPresenter`. That removes one substantive report-assembly slice from `MainWindow`, but it intentionally does not claim a broad MVVM decomposition.
- The next safest extraction target is still the larger diagnostics/status assembly path around `UpdateDiagnosticsStatus`, followed by settings snapshot/hydration builders. Startup/shutdown lifecycle ownership and safety-context builders remain higher-risk follow-up work.
- Stage 21A intentionally does not change UI/XAML, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, recording format, or replay timing.
- Physical BST-1 shaker feel, physical P-HPR feel, safe physical gain, and mixed-output latency remain Ethan-local validation items.

## Stage 21B

- `MainWindow.xaml.cs` still remains the dominant App-shell file and still owns startup/load wiring, shutdown/dispose ordering, settings/profile hydration, control parsing, safety-context construction, recording/replay UI workflow, general WPF status/footer/dashboard updates, and the live snapshot gathering that feeds diagnostics.
- Stage 21B extracts the broader diagnostics/status report assembly into `DiagnosticsStatusSnapshotBuilder` and `DiagnosticsStatusPresenter`, and it routes the diagnostics workflow/profile/live-validation lines through `PhprWorkflowStatusPresenter` instead of rebuilding those strings inline. That reduces `MainWindow` report ownership, but it intentionally does not claim a broad MVVM decomposition.
- Helper subsection formatting for individual diagnostics areas still lives in `MainWindow.xaml.cs` when that wording depends directly on current shell/runtime fields. Stage 21B keeps those helpers in place rather than forcing a larger runtime/service move.
- The next safest extraction target is app/settings snapshot and hydration shaping, including the long persisted-settings diagnostics/status line. Startup/shutdown lifecycle ownership and safety-context builders remain higher-risk follow-up work after that.
- Stage 21B intentionally does not change UI/XAML, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, recording format, replay timing, or diagnostics redaction/privacy boundaries.
- Physical BST-1 shaker feel, physical P-HPR feel, safe physical gain, and mixed-output latency remain Ethan-local validation items.

## Stage 21C

- `MainWindow.xaml.cs` still remains the dominant App-shell file and still owns WPF control assignment, startup/load wiring, shutdown/dispose ordering, profile lifecycle, replay-control reads, safety-context construction, general status/dashboard updates, and live snapshot gathering from current shell/runtime state.
- Stage 21C extracts the safe app-settings hydration/save mapping and the persisted-settings status/diagnostics wording into `AppSettingsSnapshotBuilder` and `PersistedSettingsStatusPresenter`. That reduces `MainWindow` ownership of persistence-shaping logic, but it intentionally does not claim a broad MVVM decomposition.
- The new builder/presenter keep direct-control enable/arm state, selected private HID path, emergency-stop state, output-active state, command histories, validation paths, and other startup-energising runtime state out of persisted settings and status restoration.
- Replay timing still has some direct WPF control reads in `MainWindow.xaml.cs`, and other settings-application helpers still live there. Stage 21D should target those remaining pure parsing/hydration helpers before any startup/shutdown or safety-context extraction.
- Stage 21C intentionally does not change UI/XAML, app-settings/profile schemas, `.hdrec` format, replay timing behavior, startup/shutdown ordering, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, or privacy/redaction boundaries.
- Physical BST-1 shaker feel, physical P-HPR feel, safe physical gain, and mixed-output latency remain Ethan-local validation items.

## Stage 21D

- `MainWindow.xaml.cs` still remains the dominant App-shell file and still owns direct WPF reads/writes, candidate/item-list binding, startup/load wiring, shutdown/dispose ordering, profile lifecycle, local gear test readiness, safety-context construction, general status/dashboard updates, and live snapshot gathering.
- Stage 21D extracts the remaining pure primitive control parsing, clamp/default shaping, and plain control-value hydration plans into `ControlSettingsSnapshotBuilder`. That reduces `MainWindow` ownership of non-WPF control mapping logic, but it intentionally does not claim a broad MVVM decomposition.
- Replay timing, paddle mapping, forwarding editor validation, mock routing, real P-HPR safe option parsing, and BST-1 manual/local pulse parsing now flow through the App-layer builder without introducing hardware access or startup output.
- The larger remaining control-heavy seam is still the audio-profile control parsing/application path in `BuildProfileFromControls()` / `ApplyProfileToControls()`. Stage 21E should target that before any startup/shutdown or safety-context extraction if more `MainWindow` reduction is still worth the risk.
- Stage 21D intentionally does not change UI/XAML, app-settings/profile schemas, `.hdrec` format, replay timing behavior, startup/shutdown ordering, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, or privacy/redaction boundaries.
- Physical BST-1 shaker feel, physical P-HPR feel, safe physical gain, and mixed-output latency remain Ethan-local validation items.

## Stage 21E

- `MainWindow.xaml.cs` still remains the dominant App-shell file and still owns direct WPF reads/writes, profile file lifecycle, startup/load wiring, shutdown/dispose ordering, runtime apply/configure calls, local gear test readiness, safety-context construction, general status/dashboard updates, and live snapshot gathering.
- Stage 21E extracts the remaining deterministic audio-profile control parsing, validated profile-to-control hydration planning, and profile display-text formatting into `AudioProfileControlSnapshotBuilder`. That reduces `MainWindow` ownership of non-WPF audio-profile mapping logic, but it intentionally does not claim a broad MVVM decomposition.
- Audio profile controls for engine, gear shift, kerb, impact, road texture, slip/wheel lock, mixer, and safety output gain now flow through the App-layer builder without introducing hardware access, startup output, or schema changes.
- The larger remaining seams are now lifecycle-heavy rather than mapping-heavy: profile load/save/reset orchestration, local gear readiness/status flow, startup/shutdown sequencing, and safety-context construction. Stage 21F should re-audit those seams before attempting another extraction.
- Stage 21E intentionally does not change UI/XAML, app-settings/profile schemas, `.hdrec` format, replay timing behavior, startup/shutdown ordering, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, or privacy/redaction boundaries.
- Physical BST-1 shaker feel, physical P-HPR feel, safe physical gain, and mixed-output latency remain Ethan-local validation items.

## Stage 21F

- `MainWindow.xaml.cs` still remains the dominant App-shell file and still owns direct WPF reads/writes, profile file lifecycle, startup/load wiring, shutdown/dispose ordering, runtime apply/configure calls, local gear readiness evaluation, safety-context construction, general status/dashboard updates, live snapshot gathering, and direct hardware/runtime orchestration.
- Stage 21F re-audits the residual ownership after Stages 21A-21E and confirms that the remaining large seams are mostly lifecycle-heavy or hardware-adjacent. The only additional low-risk extraction is `LocalGearReadinessPresenter`, which now owns the local gear readiness status/button/tooltip shaping without touching readiness evaluation or listener lifecycle.
- Startup/shutdown ordering, Stop All / Emergency Stop handlers, safety-context builders, ASIO start/stop ownership, P-HPR runtime coordination, direct-output hardware ownership, and runtime `Configure(...)` calls remain intentionally in `MainWindow`.
- The next worthwhile step is no longer another tiny mapping extraction. Stage 21G should be framed as an explicitly higher-risk orchestration stage with dedicated fake-backed tests and manual validation planning.
- Stage 21F intentionally does not change UI/XAML, app-settings/profile schemas, `.hdrec` format, replay timing behavior, startup/shutdown ordering, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, or privacy/redaction boundaries.
- Physical BST-1 shaker feel, physical P-HPR feel, safe physical gain, and mixed-output latency remain Ethan-local validation items.

## Stage 21G

- `MainWindow.xaml.cs` still remains the dominant App-shell file and still owns direct WPF reads/writes, profile file lifecycle, startup/load wiring, shutdown/dispose ordering, pipeline rebuild/readiness hydration, input/candidate discovery, startup cleanup, telemetry/timer/runtime start, safety-context construction, general status/dashboard updates, and direct hardware/runtime orchestration.
- Stage 21G extracts only `StartupReadinessPlanner`, which now owns deterministic no-output startup planning for ASIO selection/default fallback and startup preferred P-HPR candidate auto-selection for readiness checks only.
- Startup no longer reuses the generic preferred-candidate auto-enable path for the load-time refresh. Preferred P-HPR candidate selection at startup now stays disabled/unarmed until the user enables it manually, while still allowing the existing no-output readiness/open-check flow to run explicitly in `MainWindow`.
- Startup/shutdown ordering, `InitializeStartupCleanupAsync()`, Stop All / Emergency Stop handlers, safety-context builders, ASIO start/stop ownership, P-HPR runtime coordination, direct-output hardware ownership, and runtime `Configure(...)` calls remain intentionally in `MainWindow`.
- The next worthwhile step is a dedicated Stage 21H shutdown/cleanup ordering audit rather than mixing shutdown, safety-context, and Stop All work into the startup-readiness stage.
- Stage 21G intentionally does not change UI/XAML, app-settings/profile schemas, `.hdrec` format, replay timing behavior, shutdown ordering, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, or privacy/redaction boundaries.
- Physical BST-1 shaker feel, physical P-HPR feel, safe physical gain, and mixed-output latency remain Ethan-local validation items.

## Stage 21H

- `MainWindow.xaml.cs` still remains the dominant App-shell file and still owns `OnClosing` / `OnClosed`, actual event/timer detachment, actual stop/dispose execution, shutdown exception aggregation, shutdown diagnostic writes, startup cleanup invocation, safety-context construction, and direct hardware/runtime orchestration.
- Stage 21H extracts only `ShutdownCleanupPlanner`, which now owns deterministic stop-only shutdown ordering metadata and bounded timeout metadata for the app-owned cleanup sequence.
- `InitializeStartupCleanupAsync()` remains in `PHprDirectRuntime` because it is an output-capable stop-only startup path and must stay explicit rather than being hidden inside an App planner.
- Stop All / Emergency Stop handlers, safety-context builders, ASIO start/stop ownership, P-HPR runtime coordination, and direct-output hardware ownership remain intentionally in `MainWindow`.
- The next worthwhile step is a separate Stage 21I safety-context-builder audit or Stop All / Emergency Stop audit, not both together.
- Stage 21H intentionally does not change UI/XAML, app-settings/profile schemas, `.hdrec` format, replay timing behavior, startup behavior, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, or privacy/redaction boundaries.
- Physical BST-1 shaker feel, physical P-HPR feel, safe physical gain, and mixed-output latency remain Ethan-local validation items.

## Stage 21I

- `MainWindow.xaml.cs` still remains the dominant App-shell file and still owns runtime snapshot gathering, direct WPF reads/writes, actual Stop All / Emergency Stop execution, startup cleanup invocation, direct-control enable/arm mutation, safety-limiter/output call sites, and direct hardware/runtime orchestration.
- Stage 21I extracts only `SafetyContextSnapshotBuilder`, which now owns the deterministic mapping from already-gathered mock/real output snapshots and runtime booleans into immutable `PHprSafetyContext`-equivalent snapshots.
- Stop All / Emergency Stop execution did not move, and `InitializeStartupCleanupAsync()` remains explicit and unchanged in `PHprDirectRuntime`.
- The next worthwhile step is a dedicated Stage 21J Stop All / Emergency Stop ownership audit now that the pure safety-context mapping has been separated.
- Stage 21I intentionally does not change UI/XAML, app-settings/profile schemas, `.hdrec` format, replay timing behavior, startup behavior, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, safety-limit numeric defaults, parser layouts, or privacy/redaction boundaries.
- Physical BST-1 shaker feel, physical P-HPR feel, safe physical gain, and mixed-output latency remain Ethan-local validation items.

## Stage 21J

- `MainWindow.xaml.cs` still remains the dominant Stop All / Emergency Stop owner because the current handlers coordinate real execution across mock routers, the direct runtime, startup cleanup assumptions, shutdown cleanup ordering, bench runtime block reset, footer/status updates, and diagnostics refreshes.
- Stage 21J intentionally does not extract a new coordinator because the remaining seam is execution-heavy rather than a pure planner or presenter. Moving it now would mostly hide safety-critical call order behind another wrapper without reducing risk.
- Added `StopEmergencyOwnershipGuardrailTests` so future refactors must keep mock emergency stop, real emergency stop, direct-runtime Stop All, startup cleanup invocation, and shutdown cleanup planning visibly anchored in `MainWindow`, while the previously extracted planners/builders remain free of stop/emergency execution.
- Stop All / Emergency Stop execution still did not move, `InitializeStartupCleanupAsync()` remains explicit and unchanged in `PHprDirectRuntime`, and `ShutdownCleanupPlanner` still describes only deterministic shutdown order metadata rather than performing stop work.
- The next worthwhile step is a dedicated Stage 21K Start Haptics / Emergency Mute ownership audit instead of mixing those adjacent controls into Stop All / Emergency Stop work.
- Stage 21J intentionally does not change UI/XAML, app-settings/profile schemas, `.hdrec` format, replay timing behavior, startup behavior, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, safety-limit numeric defaults, parser layouts, or privacy/redaction boundaries.
- Physical BST-1 shaker feel, physical P-HPR feel, safe physical gain, emergency-stop physical response, and mixed-output latency remain Ethan-local validation items.
