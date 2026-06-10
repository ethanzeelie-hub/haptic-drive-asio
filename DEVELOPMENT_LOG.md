# Development Log

## Stage 00 - Repo Setup

Date: 2026-06-01

Status: Complete.

Goal: Create the repository foundation for Haptic Drive ASIO without implementing telemetry, audio behavior, or UI features beyond the default WPF shell.

Notes:

- Created .NET solution and initial project layout.
- Added WPF app, core, F1 25 telemetry, audio, recording, and xUnit test projects.
- Added repository documentation placeholders and durable agent rules.
- Physical haptic hardware is not required for this stage.
- Installed a local .NET 8 SDK under `.dotnet/` for this workspace because no SDK was available on PATH; this local toolchain is ignored by git.
- Added repo-local NuGet configuration and package cache settings for sandbox-friendly restore.

Verification:

- `dotnet restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `dotnet build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test HapticDrive.Asio.sln --no-build` passed with 4 tests.
- `dotnet format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 00 stayed limited to solution structure, placeholders, documentation, and smoke tests.
- No telemetry parsing, UDP listener, audio output, effect engine, or hardware behavior was implemented.

## Stage 01 - App Shell

Date: 2026-06-01

Status: Complete.

Goal: Create the clean WPF shell without implementing telemetry, audio, replay, parser, or effect behavior.

Notes:

- Replaced the default empty WPF window with a usable Haptic Drive ASIO shell.
- Added sidebar navigation for Dashboard, Effects, Mixer / Routing, Devices, Telemetry / UDP Router, Recordings, Test Bench, Profiles, Settings, and Diagnostics.
- Added dashboard status cards for output mode, haptics state, and safety state.
- Added global Start/Stop Haptics and Emergency Mute placeholders.
- Added dark theme default with a light theme scaffold toggle.
- Added close/minimize-to-tray setting placeholder in the footer.
- Kept all behavior local to shell UI state; no telemetry or audio pipeline work was added.

Verification:

- `dotnet build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test HapticDrive.Asio.sln --no-build` passed with 4 tests.
- `dotnet format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 01 stayed scoped to UI shell and placeholder behavior.
- Hardware absence is still safe because no audio output path is implemented.
- Stage 02 should introduce output abstractions and hardware-absent mode without depending on physical devices.

## Stage 02 - Output Abstractions and Hardware-Absent Mode

Date: 2026-06-01

Status: Complete.

Goal: Add safe output abstractions and device implementations that build and test without physical haptic hardware.

Notes:

- Added `IAudioOutputDevice`, output configuration, status, state, kind, and operation result contracts.
- Added `NullAudioOutputDevice` as the deterministic default output.
- Added `WasapiDebugOutputDevice` as a manual debug placeholder only.
- Added `AsioAudioOutputDevice` as a graceful ASIO abstraction/stub.
- Added `IAsioDriverCatalog` so ASIO discovery can be faked in tests and implemented later.
- Added `AudioOutputDeviceFactory` without automatic ASIO-to-WASAPI fallback.
- Wired the app shell output status card to `NullAudioOutputDevice`.
- Added skipped manual ASIO hardware test marker.
- Updated hardware-absent, manual hardware test, and ASIO output docs.

Verification:

- `dotnet build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test HapticDrive.Asio.sln --no-build` passed with 9 passing tests and 1 skipped manual hardware test.
- `dotnet format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 02 stayed limited to output contracts, safe device state, docs, and tests.
- No real WASAPI output, ASIO callback, mixer, generated audio, telemetry, or effects were added.
- ASIO unavailability returns a failure result instead of throwing or falling back to WASAPI.

## Stage 03 - F1 25 Spec Extraction

Date: 2026-06-01

Status: Complete.

Goal: Convert the official F1 25 v3 PDF into implementation notes and parser test checklists without implementing parser code.

Notes:

- Verified the local source PDF at `C:\Users\ethan\Downloads\Data Output from F1 25 v3.pdf`.
- Extracted the PDF text to an ignored local helper file under `.tools/` for inspection only.
- Replaced `docs/F1_25_PACKET_SPEC_IMPLEMENTATION.md` with packet header offsets, packet ID/size/version table, validation rules, V1 packet list, wheel order, surface IDs, restricted telemetry notes, haptic field mapping, and parser test checklist.
- Replaced `docs/F1_25_TELEMETRY.md` with setup, UDP behavior, packet ordering, player indexing, effect mapping, runtime safety, and diagnostics notes.
- Did not commit the PDF.
- Did not implement parser code.

Verification:

- `dotnet build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test HapticDrive.Asio.sln --no-build` passed with 9 passing tests and 1 skipped manual hardware test.
- `dotnet format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 03 stayed scoped to spec extraction and implementation planning.
- The notes are concise and do not copy large sections of the PDF.
- Stage 04 can proceed to UDP listener without depending on parser implementation.

## Stage 04 - UDP Listener

Date: 2026-06-01

Status: Complete.

Goal: Add a raw UDP telemetry listener that works without F1 25, parser code, audio output, or physical haptic hardware.

Notes:

- Added `IUdpTelemetryReceiver` and `UdpTelemetryReceiver` in Core.
- The listener binds to UDP port `20778` by default and supports ephemeral test ports.
- Received datagrams are preserved as raw byte arrays and emitted with sequence number, remote endpoint, and receive timestamp.
- Added snapshot diagnostics for running state, configured port, bound port, packet count, packet rate, last packet time, no-packet warning, error count, and last error.
- Wired the WPF dashboard and Telemetry / UDP Router page to start the listener and show live listener status.
- Added mock UDP sender tests.
- Kept packet parsing and UDP forwarding out of scope for this stage.

Verification:

- `dotnet build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test HapticDrive.Asio.sln --no-build` passed with 14 passing tests and 1 skipped manual hardware test.
- `dotnet format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 04 stayed limited to raw UDP receive and status visibility.
- The listener preserves raw bytes for future forwarding, recording, replay, and parser stages.
- No F1 25 parser, recording, replay, mixer, safety chain, generated audio, real WASAPI output, or real ASIO streaming was added.

## Stage 05 - UDP Forwarding

Date: 2026-06-01

Status: Complete.

Goal: Add byte-preserving UDP forwarding that works from raw listener packets without F1 25 parser code, audio output, or physical haptic hardware.

Notes:

- Added `IUdpTelemetryForwarder` and `UdpTelemetryForwarder` in Core.
- Added forwarding destinations with friendly name, endpoint, and enabled state.
- Forwarding sends exact received payload bytes to each enabled destination.
- Forwarding diagnostics track configured destinations, enabled destinations, input packet count, forwarded datagrams, forwarded bytes, errors, last error, and last successful forward time.
- Wired the WPF shell to offer each received raw packet to the forwarder and show forwarding status on the dashboard and Telemetry / UDP Router page.
- Added loopback UDP tests for no-destination mode, exact-payload forwarding, multiple destinations, and disabled destinations.
- Kept destination editing, persistence, packet parsing, and haptic behavior out of scope for this stage.

Verification:

- `dotnet build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test HapticDrive.Asio.sln --no-build` passed with 18 passing tests and 1 skipped manual hardware test.
- `dotnet format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 05 stayed limited to byte-preserving forwarding and diagnostics.
- Forwarding remains independent of parser success and output device state.
- No F1 25 parser, recording, replay, mixer, safety chain, generated audio, real WASAPI output, real ASIO streaming, or haptic effects were added.

## Stage 06 - F1 25 Packet Header Parser

Date: 2026-06-01

Status: Complete.

Goal: Add the first official F1 25 parser layer by reading and validating packet headers from the v3 PDF notes without parsing packet bodies.

Notes:

- Added `F125PacketHeader`, `F125PacketKind`, packet definitions, parse status, parse result, and `F125PacketHeaderParser`.
- Implemented little-endian reads for the 29-byte `PacketHeader`.
- Added validation for minimum header length, packet format `2025`, game year `25`, known packet ID, packet version `1`, and exact documented packet length.
- Unknown packet IDs return ignored results instead of throwing.
- Malformed packets return failure results instead of throwing.
- Successful parse results preserve a copy of raw datagram bytes for later recording/replay handoff.
- Wired the WPF shell to parse incoming UDP packet headers for diagnostics while preserving Stage 05 forwarding behavior.
- Kept packet body parsing, event unions, and VehicleState mapping out of scope for this stage.

Verification:

- `dotnet build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test HapticDrive.Asio.sln --no-build` passed with 45 passing tests and 1 skipped manual hardware test.
- `dotnet format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 06 stayed limited to packet header parsing and diagnostics.
- The parser uses the extracted F1 25 v3 PDF notes and does not reuse older F1 specs.
- No packet body parser, recording, replay, mixer, safety chain, generated audio, real WASAPI output, real ASIO streaming, or haptic effects were added.

## Stage 07 - F1 25 Core Packet Parser

Date: 2026-06-02

Status: Complete.

Goal: Implement the F1 25 core packet body parser layer using the official F1 25 v3 PDF data output specification as the source of truth.

Notes:

- Added Stage 07 packet body implementation notes for the core parser slice.
- Added typed packet body models for Motion, Session, Lap Data, Event, Participants, Car Telemetry, Car Status, Car Damage, and Motion Ex.
- Added a full packet parser that reuses Stage 06 header validation before any body reads.
- Known non-Stage-07 packet IDs are validated by header, version, and exact length, then safely ignored.
- Unknown packet IDs remain safely ignored.
- Event union parsing now interprets official event string codes, including `COLL` collision vehicle indices.
- Wheel arrays are exposed as explicit RL, RR, FL, FR typed data.
- Raw datagram bytes remain preserved on successful packet parses.
- Updated the WPF diagnostics wording and counters to report Stage 07 packet parser status while keeping forwarding parser-independent.
- Used an ignored local copy of the official PDF for reference and did not commit the PDF.

Verification:

- `dotnet restore HapticDrive.Asio.sln --configfile NuGet.Config` passed with the repo-local SDK after allowing NuGet access.
- `dotnet build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test HapticDrive.Asio.sln --no-build` passed with 60 passing tests and 1 skipped manual ASIO hardware test.
- `dotnet format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 07 stayed within parser-body scope.
- No VehicleState mapping was implemented.
- No recording or replay was implemented.
- No audio, haptic, WASAPI, ASIO streaming, or physical hardware output was implemented.
- No Simagic P-HPR work was added.
- No guessed packet fields or offsets were introduced; parser layouts came from the official F1 25 v3 PDF.
- Tests cover valid body parsing, exact length validation, truncated and malformed datagrams, ignored packet IDs, player index handling, wheel order, raw byte preservation, Stage 06 header behavior, and parser-independent forwarding.

## Stage 08 - VehicleState Model

Date: 2026-06-02

Status: Complete.

Goal: Add the shared `VehicleState` model and F1 25 adapter layer without implementing recording, replay, audio, haptic effects, WASAPI output, ASIO streaming, or hardware behavior.

Notes:

- Added Core `VehicleState` records for motion, session, lap, participant, car telemetry, car status, damage, Motion Ex, and last event state.
- Added per-sample packet stamps so later stages can distinguish missing packet slices from real telemetry zeros and evaluate stale data.
- Added `F125VehicleStateAdapter` to map parsed Stage 07 F1 25 packets into shared last-known VehicleState samples.
- Player car selection uses `m_header.m_playerCarIndex` for 22-car packet arrays.
- Motion Ex is mapped as player-car-only data.
- Wheel arrays preserve official RL, RR, FL, FR order.
- Surface type IDs are preserved raw in VehicleState.
- Failed parser results and invalid player indices are ignored safely without crashing or mutating VehicleState.
- Wired the WPF diagnostics to show VehicleState update count, player index, speed, and gear while keeping forwarding parser-independent.
- Updated README, architecture notes, F1 25 telemetry notes, packet implementation notes, roadmap, and known issues for Stage 08.

Verification:

- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 65 passing tests and 1 skipped manual ASIO hardware test.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 08 stayed within shared model and F1 25 adapter scope.
- No recording or replay was implemented.
- No audio, haptic, WASAPI, ASIO streaming, or physical hardware output was implemented.
- No Simagic P-HPR work was added.
- No new packet offsets or layouts were guessed; Stage 08 maps fields already parsed from the official F1 25 v3 PDF-derived Stage 07 parser models.

## Stage 09 - Recording and Replay

Date: 2026-06-02

Status: Complete.

Goal: Add raw telemetry recording and deterministic replay without requiring F1 25, live UDP traffic, ASIO hardware, shaker hardware, physical output devices, audio generation, mixer behavior, safety processing, or haptic effects.

Notes:

- Added a versioned `.hdrec` binary recording format with magic, format version, created UTC timestamp, source game/profile metadata, app version, finalized packet count, packet sequence numbers, relative timestamps, payload lengths, and raw payload bytes.
- Added `TelemetryRecordingService` to enqueue copied `UdpTelemetryPacket` payloads into a background writer queue so disk IO is kept out of the receive callback.
- Recording remains parser-independent, so unknown, unsupported, malformed, or truncated F1-looking UDP packets can still be captured as raw payloads.
- Added safe load failures for invalid magic, unsupported versions, negative or excessive counts, invalid string lengths, negative relative timestamps, truncated records, trailing bytes, and unreasonable payload lengths.
- Added `TelemetryReplayService` for deterministic fast replay and optional time-preserving replay with a speed multiplier.
- Replay emits `UdpTelemetryPacket` values in recorded order without UDP sockets, allowing recorded packets to reuse the existing `F125PacketParser.Parse(packet.Payload)` and `F125VehicleStateAdapter.Apply(parseResult)` path.
- Added a minimal WPF Start/Stop Recording button and recording status card; replay UI and recording library management are deferred.
- Updated README, architecture notes, F1 25 telemetry notes, packet implementation notes, recording/replay docs, roadmap, and known issues for Stage 09.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed. NuGet emitted `NU1900` because restricted network access prevented vulnerability-feed metadata from loading.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 errors. The same `NU1900` warning was reported from the restored package assets.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 77 passing tests and 1 skipped manual ASIO hardware test.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 09 stayed within recording/replay scope plus minimal app status wiring.
- Raw UDP packet bytes are copied and preserved exactly in recording and replay tests.
- Parser success is not required for recording.
- Replay feeds the existing parser and VehicleState adapter path through `UdpTelemetryPacket` events.
- UDP forwarding behavior was not changed.
- No audio mixer, safety chain, generated audio, haptic effects, real WASAPI output, ASIO streaming, physical shaker tuning, or Simagic P-HPR work was added.
- No packet layouts, offsets, enum values, versions, or PDF-derived parser fields were changed.
- File IO errors and corrupt recording files fail through result objects instead of crashing normal callers.
- Tests cover recording order, byte preservation, relative timing, zero-packet finalization, invalid paths, excessive payloads, replay order, replay byte preservation, fast replay, replay stop/cancellation, corrupt headers, unsupported versions, truncated records, invalid lengths, parser/VehicleState integration, and malformed replay packets.

## Stage 10 - Audio Mixer and Safety Chain

Date: 2026-06-02

Status: Complete.

Goal: Add the deterministic internal audio sample pipeline, mixer, safety processor, and null-output sample consumption without implementing haptic effects, the Stage 11 test bench, real WASAPI output, real ASIO streaming, or physical hardware behavior.

Notes:

- Added `AudioSampleFormat` and `AudioSampleBuffer` for interleaved floating-point sample buffers with explicit sample rate, channel count, and frame count.
- Extended `IAudioOutputDevice` with `SubmitBufferAsync` so final sample buffers can be handed to the existing output abstraction.
- Added deterministic mixer support for empty output as silence, single/multiple source buffer summing, per-source gain, master gain, normal mute, emergency mute, and invalid sample/gain sanitisation.
- Added `AudioSafetyProcessor` with conservative default output gain `0.25`, normalized output ceiling `0.75`, NaN/infinity sanitisation, peak limiting, hard clipping protection, peak diagnostics, and emergency silence.
- Added `AudioRenderPipeline` to run mixer output through the safety processor and submit the final buffer to an output device.
- Updated `NullAudioOutputDevice` so it consumes matching sample buffers after start, counts submitted buffers/frames/samples, records the last peak level, and still produces no sound.
- Wired the WPF shell minimally so Start Haptics submits safe silence through the Stage 10 pipeline to `NullAudioOutputDevice`, and Emergency Mute toggles the mixer/safety emergency mute flags.
- Updated audio safety, ASIO, hardware-absent, architecture, README, roadmap, and known-issues documentation for Stage 10.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed after approved network access. The first sandboxed restore attempt failed with `NU1301` because the restricted sandbox could not reach NuGet.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors after using the restored workspace package cache.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 92 passing tests and 1 skipped manual ASIO hardware test.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 10 stayed within audio sample buffers, mixer, safety chain, null-output consumption, minimal shell status, docs, and tests.
- No haptic effect generation was implemented.
- No Stage 11 test bench was implemented.
- No real WASAPI output or ASIO callback streaming was implemented.
- Default output remains `NullAudioOutputDevice` and automated tests do not require hardware, F1 25, live telemetry, WASAPI, ASIO, or shaker hardware.
- Emergency mute is available in both mixer and safety processing and is test-covered.
- Unsafe sample values are sanitised, limited, or clipped before final output.
- UDP forwarding and recording/replay raw byte guarantees were not changed.
- Parser and VehicleState behavior were not changed.
- No Simagic P-HPR work was added.
- Tests cover mixer silence, pass-through, summing, gain, mute, emergency mute, invalid samples, safety limiting/clipping, peak diagnostics, null-output consumption, and hardware-absent pipeline operation.

## Stage 11 - Test Bench

Date: 2026-06-02

Status: Complete.

Goal: Add a deterministic test bench for validating the internal audio path, mixer, safety chain, mute behavior, and output abstraction without requiring F1 25, live telemetry, ASIO hardware, WASAPI hardware, shaker hardware, or physical output devices.

Notes:

- Reviewed the Stage 11 brief and the attached F1 25 v3 PDF reference before coding; Stage 11 does not change packet layouts, parser offsets, enum values, packet lengths, or versions.
- Added deterministic synthetic test signal generators for silence, fixed-frequency sine tone, linear frequency sweep, pulse/transient, and constant-value/DC validation.
- Added `AudioTestBench` to select signals, start/stop the bench, render explicit validation buffers, and feed the existing Stage 10 mixer/safety pipeline into `NullAudioOutputDevice` by default.
- Test bench diagnostics include active state, selected signal, sample format, rendered buffers/frames, mixer peak, output peak, sanitized samples, limited samples, clipped samples, and output mode.
- Normal mute and emergency mute are applied through the same mixer/safety path used by the Stage 10 audio pipeline.
- Wired the WPF Test Bench page with minimal signal selection, start/stop, peak, limiter, output mode, and hardware warning status.
- Added `docs/TEST_BENCH.md` and updated audio safety, hardware-absent, ASIO output, architecture, README, roadmap, and known-issues documentation.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed. NuGet emitted `NU1900` warnings because restricted sandbox network access prevented vulnerability-feed metadata from loading.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 errors. The same 4 `NU1900` warnings were reported from the restored package assets.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 105 passing tests and 1 skipped manual ASIO hardware test.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 11 stayed within test-bench scope.
- Test signals are synthetic validation tools, not driving haptic effects.
- No Stage 12 gear shift or engine effects were implemented.
- No Stage 13 kerb, impact, road texture, slip, traction loss, ABS, or other driving effects were implemented.
- No real WASAPI output, ASIO callback streaming, or physical shaker calibration was implemented.
- Default output remains hardware-safe through `NullAudioOutputDevice`.
- Emergency mute is simple, reliable, and test-covered through the test bench path.
- Unsafe over-range generated samples are limited by the safety chain before output.
- UDP forwarding and recording/replay raw byte guarantees were not changed.
- Parser and VehicleState behavior were not changed.
- No Simagic P-HPR work was added.
- Tests cover test signal generation, deterministic reset/repeat behavior, test bench lifecycle, render-before-start failure, mixer/safety/null-output integration, normal mute, emergency mute, over-range limiting, and hardware-absent default output.

## Stage 12 - Gear Shift and Engine Effects

Date: 2026-06-02

Status: Complete.

Goal: Add the first real haptic effect generators for gear shift and engine vibration without requiring F1 25, live UDP traffic, ASIO hardware, WASAPI hardware, shaker hardware, or physical output devices.

Notes:

- Checked the local official F1 25 v3 PDF before implementation.
- Confirmed F1 25 directly outputs gear, engine RPM, throttle, speed, suggested gear, idle RPM, max RPM, game pause, network pause, pit status, driver status, and result status fields.
- Confirmed F1 25 does not output a direct engine-vibration signal or dedicated gear-shift haptic event, so Stage 12 synthesizes both effects from shared `VehicleState`.
- Added `HapticDrive.Asio.Audio.Effects` with small deterministic effect sources, conservative option records, snapshots, and `HapticEffectEngine`.
- Added engine vibration generation from RPM-derived frequency, throttle-scaled intensity, optional high-frequency shaping, optional deterministic frequency jitter, pit reduction, and pause/garage/inactive gating.
- Added gear shift transient generation from valid forward gear transitions with initial-state protection, neutral/reverse safety, debounce, optional RPM modulation, and a decaying pulse envelope.
- Effect buffers are wrapped as `AudioMixerInput` values and pass through the existing Stage 10 mixer, safety processor, emergency mute, limiter, clipping protection, and `NullAudioOutputDevice` test path.
- Wired the WPF shell minimally so VehicleState updates feed the effect engine and the Effects page shows engine/gear diagnostics and conservative defaults.
- Added `docs/HAPTIC_EFFECTS.md` for direct telemetry fields, synthesized effect assumptions, defaults, and boundaries.
- Updated README, architecture, F1 25 telemetry/spec notes, audio safety, ASIO output, hardware-absent mode, roadmap, and known issues.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 125 passing tests and 1 skipped manual ASIO hardware test.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 12 stayed within gear shift and engine vibration scope.
- No Stage 13 kerb, impact, road texture, slip, traction loss, ABS, suspension, or surface-specific effects were implemented.
- No real WASAPI output, ASIO callback streaming, Simagic P-HPR output, profile editor, or physical shaker calibration was implemented.
- Default output remains hardware-safe through `NullAudioOutputDevice`.
- Effects consume shared `VehicleState`, not F1 25 parser packet bodies directly.
- Emergency mute remains controlled by the existing mixer/safety path and is test-covered.
- Invalid and unsafe values are sanitized, gated, bounded, or silenced before final output.
- UDP forwarding and recording/replay raw byte guarantees were not changed.
- Parser packet layouts, offsets, enum values, versions, lengths, and VehicleState adapter behavior were not changed unnecessarily.
- Physical shaker feel, safe gain, latency, and final frequency tuning remain unvalidated until real hardware testing.
- Tests cover engine silence/invalid data, determinism, throttle amplitude, RPM frequency mapping, invalid values, pause/inactive gating, gear initial state, valid gear changes, unchanged gear, neutral/reverse/missing gear, transient decay, rapid changes, mixer/safety integration, emergency mute, null-output consumption, and deterministic VehicleState sequences.

## Stage 13 - Kerb, Impact, Road Texture, and Slip Effects

Date: 2026-06-03

Status: Complete.

Goal: Add conservative VehicleState-driven kerb, impact, road texture, and slip / brake-lock effect generators without requiring live telemetry, F1 25, WASAPI hardware, ASIO hardware, shaker hardware, physical calibration, or parser changes.

Notes:

- Added Stage 13 effect sources under `HapticDrive.Asio.Audio.Effects` for kerb, impact, road texture, and slip.
- Kerb vibration is synthesized from raw F1 25 surface type IDs for rumble strip and ridged surfaces, speed, active wheel count, and optional Motion Ex contact / suspension data.
- Impact pulses are synthesized from player collision events and abrupt vertical-G, wheel-vertical-force, or suspension-acceleration changes with initial-state protection, cooldown, bounded envelopes, and invalid-value guards.
- Road texture is synthesized from documented surface IDs, speed, and optional suspension / vertical-G motion using conservative per-surface defaults and deterministic roughness.
- Slip and minimal brake-lock vibration are synthesized from wheel slip ratio, wheel slip angle, wheel speed, throttle, brake, speed, traction control, and ABS state.
- Added frame-stamp freshness checks so clearly stale telemetry slices are treated safely without adding a wall-clock timeout policy.
- Extended `HapticEffectEngine` so Stage 13 source buffers feed the existing Stage 10 mixer, safety processor, emergency mute, limiter, clipping protection, and `NullAudioOutputDevice` path.
- Wired minimal read-only WPF diagnostics for kerb, impact, road texture, and slip. No tuning UI, profile editor, routing UI, live graphs, calibration UI, persistence, real WASAPI output, or ASIO streaming was added.
- Updated effect, architecture, F1 25 implementation, F1 25 telemetry, README, roadmap, and known-issues documentation.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 144 passing tests and 1 skipped manual ASIO hardware test.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 13 stayed within kerb, impact, road texture, slip, and minimal brake-lock scope.
- No Stage 14 tuning/profile work was implemented.
- No real WASAPI output, ASIO streaming, Simagic P-HPR output, profile editor, live graphing, routing editor, physical calibration, or hardware readiness work was added.
- Effects consume shared `VehicleState`, not F1 25 packet internals.
- No F1 25 packet layouts, parser offsets, enum values, packet lengths, or packet versions were changed or guessed.
- UDP forwarding and recording/replay raw byte guarantees were not changed.
- Default output remains hardware-safe through `NullAudioOutputDevice`.
- Emergency mute remains controlled by the existing mixer/safety path and is test-covered.
- Invalid and unsafe sample values are sanitized, gated, bounded, or silenced before final output.
- Physical shaker feel, safe gain, latency, and final frequency tuning remain unvalidated until real hardware testing.

## Stage 14 - UI Tuning, Profiles, and Diagnostics

Date: 2026-06-03

Status: Complete.

Goal: Add practical UI tuning, basic profile management, and useful diagnostics for the existing haptic engine without adding new haptic effects, real hardware output, ASIO streaming, or physical shaker calibration.

Notes:

- Verified Stage 13 was complete, tested, and committed as `2bf87c8 stage-13-kerb-impact-road-slip-effects` before beginning Stage 14.
- Added a versioned JSON profile model for existing effect, mixer, and safety settings.
- Profile save/load/reset now supports a conservative default profile, safe validation, unsupported-version rejection, corrupt/missing file failures, and repair/clamping of partially invalid values.
- Emergency mute remains runtime-only and is not saved in profiles.
- Added effect-engine retuning by replacing immutable effect option records under the existing engine lock.
- Wired WPF tuning controls for per-effect enabled/gain state plus selected existing parameters for engine frequency bounds, gear/impact pulse duration, kerb frequency, road texture speed gate, and slip threshold.
- Wired WPF mixer/safety controls for master gain, normal mute, safety output gain, conservative output ceiling, and limiter enabled state.
- Added device, recording/replay, profiles, settings, and diagnostics panels while keeping the default output as `NullAudioOutputDevice`.
- Added read-only diagnostics for UDP listener, forwarding, parser counts, VehicleState, recording, replay, effects, mixer/safety, test bench, and output status.
- Added replay snapshots for inactive/active/completed replay status without changing raw packet replay order or byte preservation.
- Added `docs/PROFILES_AND_DIAGNOSTICS.md` and updated existing docs for Stage 14 status and limitations.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed. NuGet emitted `NU1900` warnings because restricted network access prevented vulnerability-feed metadata from loading.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 errors and the same 4 `NU1900` warnings.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 156 passing tests and 1 skipped manual ASIO hardware test.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed. The formatter reported workspace-load warnings but no required formatting changes.

Self-review:

- Stage 14 stayed within UI tuning, profiles, and diagnostics scope.
- No Stage 15 playable milestone work was implemented.
- No Stage 16 ASIO hardware readiness work was implemented.
- No new haptic effect categories were implemented.
- No real ASIO or WASAPI hardware streaming was implemented.
- No physical shaker tuning or calibration was implemented.
- Default output remains hardware-safe through `NullAudioOutputDevice`.
- Profile values are validated/clamped safely, and emergency mute remains simple, runtime-only, and reliable.
- Unsafe sample values still pass through the Stage 10 mixer/safety chain before output.
- UDP forwarding and recording/replay raw byte guarantees were not changed.
- Parser and VehicleState behavior were not changed unnecessarily.
- No guessed parser fields, packet offsets, packet layouts, packet lengths, enum values, or versions were introduced.
- No Simagic P-HPR work was added.
- Tests cover profile defaults, save/load, missing/corrupt/unsupported files, invalid-value repair, effect/mixer/safety mapping, emergency mute preservation, diagnostics snapshots without hardware or telemetry, and replay inactive/completed status.

## Stage 15 - First Playable Mock Output

Date: 2026-06-03

Status: Complete.

Goal: Create the first end-to-end playable mock haptic milestone while keeping `NullAudioOutputDevice` as the default output and avoiding Stage 16 real ASIO hardware readiness.

Notes:

- Verified Stage 14 was complete, tested, and committed as `cc4ee32 stage-14-ui-tuning-profiles-diagnostics` before beginning Stage 15.
- Added `HapticDrive.Asio.Runtime` with `HapticPipelineCoordinator` as the integration boundary for live UDP packets, replay packets, the F1 25 parser, `VehicleState` adapter, existing haptic effects, mixer, safety chain, recording, forwarding, replay, and output abstraction.
- Live UDP and replay packets now share the same parser, `VehicleState`, effect, mixer, safety, and output path.
- Start/Stop Haptics now starts and stops the mock software pipeline and selected output device cleanly.
- Added a simple WPF mock render timer for runtime Null-output rendering while keeping tests deterministic through explicit render steps.
- Emergency mute immediately updates the coordinator mixer/safety state and submits a muted buffer when the pipeline is running.
- Stage 14 profile settings now apply to the active coordinator effect, mixer, and safety configuration.
- Added minimal `Replay Latest` shell control that replays the newest local `.hdrec` file through the same mock pipeline without UDP sockets.
- Added optional ASIO driver visibility diagnostics through the existing `IAsioDriverCatalog` seam. The default catalog remains hardware-absent safe and reports no drivers unless a real discovery implementation is added later.
- Added `docs/STAGE_15_MOCK_PIPELINE.md` with the hardware-safe mock validation checklist and M-Audio / ASIO visibility caveats.
- Updated README, roadmap, telemetry, ASIO, hardware-absent, and known-issues documentation for Stage 15.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 168 passing tests and 1 skipped manual ASIO hardware test.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 15 stayed within the first playable mock output milestone scope.
- No Stage 16 real ASIO hardware readiness, ASIO callback streaming, WASAPI streaming, physical shaker calibration, physical gain tuning, latency measurement, or Simagic P-HPR work was implemented.
- Default output remains `NullAudioOutputDevice`, and automated tests do not require hardware, F1 25, live telemetry, M-Audio, Fosi, Dayton BST-1, ASIO, or WASAPI.
- UDP forwarding and recording/replay raw byte guarantees were preserved and remain parser-independent.
- Parser packet layouts, offsets, enum values, versions, lengths, and `VehicleState` mappings were not changed.
- Tests cover lifecycle, restart, stopped rendering, live-like packet flow, replay flow, malformed packets, recording before parser failure, normal mute, emergency mute, disabled effects, Null output diagnostics, replay stop safety, and fake ASIO/M-Audio visibility diagnostics.

## Stage 16 - Manual ASIO Hardware Readiness

Date: 2026-06-03

Status: Complete.

Goal: Prepare the app for controlled manual ASIO readiness checks with the connected M-Audio M-Track Solo while preserving Null output defaults, hardware-safe behavior, and CI-safe automated tests.

Notes:

- Verified Stage 15 was complete, tested, and committed as `624534a stage-15-first-playable-mock-output` before beginning Stage 16.
- Added `WindowsRegistryAsioDriverCatalog` to list ASIO driver names from standard Windows ASIO registry locations when available.
- Kept ASIO discovery behind `IAsioDriverCatalog` so tests can use fake catalogs and missing ASIO drivers remain non-fatal.
- Added `AsioReadinessDiagnostics` for ASIO availability, M-Audio / M-Track visibility, selected output mode, selected driver, sample rate, buffer size, channel state, arming state, running state, buffer counters, drops, and last error.
- Extended `AudioOutputConfiguration` and `AudioOutputStatus` with optional hardware arming, selected output channel, output channel count, buffer counters, and last-error diagnostics.
- Reworked `AsioAudioOutputDevice` so ASIO requires explicit driver selection, explicit output-channel selection, explicit arming, and explicit Start Haptics before it can run.
- Added `IAsioOutputBackend` and a default unavailable backend so native streaming remains isolated and failure-safe while fake backends cover lifecycle/routing tests.
- Implemented Stage 16 mono-to-selected-channel routing after the existing effect, mixer, and safety processor path. Other routed ASIO channels are cleared.
- Added WPF Devices controls for output mode, ASIO refresh, ASIO driver selection, channel selection, and ASIO arming. Changing output mode/settings stops haptics and rebuilds the runtime pipeline in a stopped state.
- Updated diagnostics and documentation to state that Windows sound output visibility is not proof of ASIO usage.
- Added `docs/STAGE_16_ASIO_READINESS.md` with the manual M-Audio/Fosi/BST-1 readiness checklist.
- Updated README, ASIO output docs, manual hardware docs, hardware-absent mode docs, roadmap, and known issues for Stage 16.
- Did not add a new external dependency. Native ASIO callback streaming remains future local Windows validation work behind the backend seam.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 188 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 16 stayed within manual ASIO hardware readiness scope.
- Updated M-Audio/Fosi/BST-1 hardware state is reflected accurately.
- No Simagic P-HPR, USB haptic output, reverse engineering, or new haptic effect categories were implemented.
- Default output remains `NullAudioOutputDevice`.
- ASIO is explicit, selectable, channel-routed, and arming-gated by default.
- ASIO absence, M-Audio absence, Fosi absence, and Dayton BST-1 absence do not break startup, build, tests, or CI.
- Windows sound output visibility is not treated as proof of ASIO usage.
- Emergency mute remains applied through the existing mixer/safety path before output.
- Stop Haptics stops output, and switching away from ASIO stops the previous output path first in the shell workflow.
- Safety chain remains mandatory before hardware-capable output submission.
- UDP forwarding and recording/replay raw byte guarantees were not changed.
- Parser and `VehicleState` behavior were not changed.
- No guessed parser fields, packet offsets, packet layouts, packet lengths, enum values, or versions were introduced.
- Hardware tests are skipped/manual by default.
- Tests cover fake ASIO discovery, fake M-Audio visibility, unavailable ASIO, invalid driver, invalid channel, arming, lifecycle, stop/dispose safety, routing, emergency mute, safety-processed output, default Null output, and hardware-absent operation.

## Stage 17 - Native ASIO Streaming and Low-Latency Pre-Shaker Hardening

Date: 2026-06-03

Status: Complete.

Goal: Add native ASIO streaming and move live haptic rendering into an output-owned low-latency path while preserving Null output defaults, explicit ASIO arming/selection, and hardware-absent automated tests.

Notes:

- Verified Stage 16 was complete before beginning Stage 17.
- Added `NAudio.Asio` 2.3.0 and documented it in third-party notices.
- Added output-owned render callback contracts to `IAudioOutputDevice`.
- Added render/backend diagnostics for render callbacks, backend callbacks, submitted buffers, dropped buffers, underruns, render duration, callback jitter, and telemetry age.
- Added `NativeAsioOutputBackend` behind `IAsioOutputBackend` using NAudio `AsioOut`.
- Preserved explicit ASIO output mode, driver selection, output-channel selection, arming, and Start Haptics requirements.
- Added a small preallocated native-ASIO queue so callback underruns and dropped buffers are counted.
- Removed the WPF haptic render `DispatcherTimer`; the shell now starts/stops the output-owned pipeline and polls diagnostics only.
- Kept manual deterministic render submission available for tests and the test bench.
- Added wall-clock stale telemetry mute so old live telemetry cannot continue driving effects indefinitely.
- Kept the render callback limited to in-memory effect rendering, mixer, safety processing, and buffer filling; UI, disk IO, logging, networking, blocking waits, and async continuations remain outside the callback.
- Added fake-backend and Null-output tests for callback cadence, stale telemetry mute, emergency mute, channel routing, stop/dispose, and dropped-buffer diagnostics.
- Added `docs/STAGE_17_NATIVE_ASIO_STREAMING.md` and updated README, architecture, ASIO, audio safety, hardware-absent, haptic effects, manual hardware, telemetry, recording/replay, roadmap, known issues, and Stage 16 readiness docs.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 189 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 17 stayed within native ASIO streaming, low-latency render-path hardening, telemetry timeout mute, diagnostics, and focused tests.
- Null output remains the startup and automated-test default.
- ASIO selection, driver selection, channel selection, arming, and Start Haptics remain explicit.
- ASIO absence, M-Audio absence, Fosi absence, Dayton BST-1 absence, F1 25 absence, and live telemetry absence do not break startup, build, tests, or CI.
- Emergency mute remains applied before output and is covered in the output-owned render path.
- UDP forwarding and recording/replay raw byte guarantees were not changed.
- Parser packet layouts, offsets, enum values, versions, lengths, and `VehicleState` mappings were not changed.
- No Simagic P-HPR, forwarding UI, recordings library polish, broad UI rewrite, real WASAPI output, advanced routing matrix, or physical calibration work was added.
- No final shaker feel, safe physical gain, physical latency, or final frequency tuning is claimed.

## Stage 18 - Final Pre-Shaker Readiness Package

Date: 2026-06-04

Status: Complete.

Goal: Finish the maximum safe software package before the Dayton shaker arrives by adding launch/runtime prerequisite handling, forwarding and recording UI polish, app settings persistence, diagnostics reporting, and final pre-shaker documentation cleanup without requiring physical shaker output.

Notes:

- Verified Stage 17 was complete before beginning Stage 18.
- Added `Run-HapticDrive.ps1` to set `DOTNET_ROOT` to the repo-local .NET 8 runtime, check `Microsoft.WindowsDesktop.App 8.x`, build the solution, and launch the WPF executable.
- Added `Run-HapticDrive.cmd` as the recommended wrapper so normal PowerShell execution policy does not block launch.
- Added app settings persistence separate from haptic profiles. Settings persist theme, UDP forwarding destinations, and last ASIO driver/channel selection only.
- Kept `NullAudioOutputDevice` as the startup and automated-test default. ASIO armed state, haptic running state, emergency mute, and physical calibration are not persisted.
- Added UDP forwarding destination editing in the Telemetry / UDP Router page with name, host/IP, port, enabled state, persistence, removal, and obvious loopback protection for the local listener port.
- Extended UDP forwarding destinations to support DNS hostnames as well as IP endpoints while preserving byte-for-byte payload forwarding.
- Added a metadata-only recording summary reader and a Recordings page library that lists local `.hdrec` files, shows metadata summaries, refreshes the library, and replays the selected recording.
- Added packet-ID observation diagnostics to the runtime pipeline snapshot for known F1 25 packet IDs.
- Added copyable diagnostics reports that include pipeline, UDP, forwarding, packet-ID, recording/replay, effects, mixer/safety, test bench, output, ASIO readiness, runtime prerequisite, and app-settings state.
- Updated Stage 18 UI text and documentation to remove stale Stage 15 mock/Stage 17-only wording where it affected current behavior.
- Added `docs/STAGE_18_FINAL_PRE_SHAKER.md` and updated README, roadmap, known issues, ASIO, hardware-absent, manual hardware, telemetry, forwarding, recording/replay, profiles/diagnostics, and test bench docs.

Verification:

- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 192 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 18 stayed within final pre-shaker readiness scope.
- No physical shaker output, physical gain calibration, physical latency measurement, final frequency tuning, live graphing, advanced routing matrix, real WASAPI output, Simagic P-HPR output, or speculative F1 packet layout work was implemented.
- The app is now complete for the available pre-BT-1 hardware state: M-Audio and Fosi can be checked through explicit readiness diagnostics, but the Dayton shaker remains absent.
- Automated tests still do not require F1 25, live UDP, M-Audio, Fosi, Dayton shaker hardware, ASIO, or WASAPI.
- UDP forwarding and recording/replay raw byte guarantees remain parser-independent.
- Parser packet layouts, offsets, enum values, versions, lengths, and `VehicleState` mappings were not changed.
- Physical shaker feel, safe physical gain, physical latency, and final frequency tuning remain explicitly unclaimed until the full chain is tested locally.

## Stage 2A - Phase 2 Readiness, Research Intake, and Data Request

Date: 2026-06-04

Status: Complete.

Goal: Start Phase 2 safely by documenting the Simagic P-HPR / GT Neo scope, confirming the Stage 18 baseline, requesting required user data, and recording the safety gate before any implementation or hardware writes.

Notes:

- Verified Stage 18 was complete from README, roadmap, Stage 18 documentation, and the previous development-log entry.
- Confirmed searches of `src/` and `tests/` found no Simagic, P-HPR, P700, GT Neo, `ShiftIntent`, or `DrivingArmed` implementation code.
- Expanded `docs/SIMAGIC_P_HPR_PHASE_2_RESEARCH.md` from a placeholder into the Phase 2 baseline and research intake document.
- Added `docs/SIMAGIC_USER_DATA_REQUEST.md` for SimPro Manager, SimHub, Windows Device Manager, USBView, game-controller mapping, and later USBPcap/Wireshark data requests.
- Added `docs/SIMAGIC_CAPTURE_GUIDE.md` for future capture scenarios, naming, metadata, and raw-capture handling.
- Added `docs/SIMAGIC_WHEEL_INPUT_RESEARCH.md` for the read-only GT Neo paddle input discovery plan.
- Added `docs/SIMAGIC_SHIFT_INTENT_DESIGN.md` for the default `InstantPaddleOnly` gear-pulse design, cached `DrivingArmed` gate, and no-default-double-pulse rule.
- Added `docs/SIMAGIC_P_HPR_SAFETY_PLAN.md` with the exact approval phrase required before any real P-HPR write testing.
- Updated `AGENTS.md` with Phase 2 / 3 Simagic P-HPR and GT Neo rules, including the no-write gate, read-only input allowance, default future paddle gear-pulse source, and raw-capture commit prohibition.
- Updated `.gitignore` for raw USB captures, private captures, local device inventories, and local Simagic inventory JSON files.
- Updated README, roadmap, known issues, and architecture docs to reflect Stage 2A without adding runtime code.

Verification:

- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 192 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 2A stayed within research, documentation, and safety-intake scope.
- No input listener, DirectInput, Raw Input, HID reader, P-HPR abstraction, mock output, protocol encoder/decoder, protocol hypothesis code, or capture analysis code was implemented.
- No real USB writes, output reports, write-capable feature reports, real P-HPR vibration commands, SimPro control, firmware work, driver replacement, or hardware loops were implemented or executed.
- P-HPR remains documented as a separate non-audio actuator path, not an ASIO or `IAudioOutputDevice` path.
- The default future P-HPR gear-pulse mode is documented as `InstantPaddleOnly`, gated by cached `DrivingArmed`, with no telemetry wait and no default second confirmation pulse.
- Required user data is now explicitly requested and documented.
- Raw capture and private hardware inventory files are ignored by default.

## Stage 2B - Input and P-HPR Abstractions

Date: 2026-06-04

Status: Complete.

Goal: Create safe input and P-HPR abstraction projects, model the future shift-intent and actuator command surfaces, and add a mock-only P-HPR output skeleton without implementing real input discovery or hardware writes.

Notes:

- Added `HapticDrive.Input.Abstractions`.
- Added `HapticDrive.Input.Windows` as the future Windows read-only input project placeholder.
- Added `HapticDrive.Simagic.PHPR.Abstractions`.
- Added input device, paddle input, shift intent, and cached driving-state contracts: `IInputDeviceDiscovery`, `InputDeviceDescriptor`, `IShiftIntentSource`, `IWheelPaddleInputSource`, `ShiftIntentEvent`, `PaddleSide`, `DrivingArmedState`, and `IDrivingArmedStateProvider`.
- Added P-HPR command/output contracts: `IPHprOutputDevice`, `PHprCommand`, `PHprModuleId`, `PHprCommandSource`, `PHprSafetyFlags`, `PHprSafetyLimits`, `PHprOutputSnapshot`, command results, and command status.
- Added `MockPhprOutputDevice` skeleton that records clamped commands in memory, marks accepted commands as mock-only, supports emergency-stop suppression, and performs no hardware writes.
- Added `HapticDrive.Input.Tests` for cached `DrivingArmed`, shift-intent, and read-only descriptor defaults.
- Added `HapticDrive.Simagic.PHPR.Tests` for conservative P-HPR safety defaults, command clamping, mock command recording, and emergency-stop suppression.
- Updated README, roadmap, known issues, architecture, and Simagic docs for Stage 2B.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Initial `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` run showed two timing-sensitive pre-existing streaming/runtime test failures while both new Stage 2B test projects passed.
- Immediate rerun of `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 200 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 2B stayed within abstraction and mock-only scope.
- No Raw Input implementation, DirectInput implementation, HID reader, P700/P-HPR discovery, shift-intent router, telemetry-backed `DrivingArmed` service, protocol encoder/decoder, real P-HPR output adapter, or USB write path was implemented.
- `HapticDrive.Input.Windows` contains no listener and performs no device access.
- `MockPhprOutputDevice` is explicitly mock-only and does not send output reports, feature reports, or vibration commands.
- `PHprSafetyLimits.Default` keeps real device writes disabled and uses conservative first-write caps for future gated testing.
- Existing ASIO/BST-1 architecture and `NullAudioOutputDevice` automated-test default were not changed.

## Stage 2C - DrivingArmed State Service

Date: 2026-06-04

Status: Complete.

Goal: Add a cached menu-safe `DrivingArmed` service from existing `VehicleState` and runtime pipeline snapshots so future paddle input can be accepted or suppressed without waiting for telemetry at paddle-event time.

Notes:

- Added `HapticDrive.Actuation` for non-audio actuator gating logic.
- Added `DrivingArmedStateService` implementing `IDrivingArmedStateProvider`.
- Added `DrivingArmedStateServiceOptions` with menu-safe mode enabled by default, recent-telemetry requirement enabled by default, telemetry freshness threshold, zero-speed active-driving allowance, and diagnostics-only unsafe override.
- Added `DrivingArmedEvaluationContext`, `DrivingArmedSuppressionReason`, and `DrivingArmedStateServiceSnapshot`.
- Added update paths from existing `VehicleState` and `HapticPipelineSnapshot`.
- Driving is unarmed by default until recent valid telemetry proves active driving.
- Suppression reasons now cover no telemetry, stale telemetry, paused, network paused, garage/menu/result state, invalid vehicle state, not moving/inactive, emergency mute, and haptics stopped.
- Zero-speed active driving remains allowed by default for pit-lane/start-line cases when telemetry state indicates active driving.
- Added `HapticDrive.Actuation.Tests` covering active fresh telemetry, stale telemetry, pause, network pause, garage/result states, haptics stopped, emergency mute, zero-speed behavior, menu-safe override behavior, pipeline snapshot updates, and change events.
- Updated README, roadmap, known issues, architecture, and Simagic docs for Stage 2C.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- One final parallel `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` run showed two pre-existing timing-sensitive output-owned rendering failures while the new Stage 2C tests passed.
- Immediate rerun of `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 215 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Stage 2C stayed within cached driving-state gating scope.
- No paddle input listener, Raw Input, DirectInput, HID reader, shift-intent router, P-HPR routing, protocol encoder/decoder, real output adapter, USB write path, or UI wiring was implemented.
- The service evaluates cached state and does not block waiting for telemetry at paddle-event time.
- Existing ASIO/BST-1 audio path and `NullAudioOutputDevice` default were not changed.
- P-HPR remains a separate non-audio actuator path with no real writes before the exact approval phrase.

## Stage 2D - Read-Only Wheel / Paddle Input Discovery

Date: 2026-06-04

Status: Complete.

Goal: Implement read-only wheel / paddle input discovery for the Simagic Alpha Evo / GT Neo / P700 setup without adding live paddle listening, haptic routing, or any P-HPR write path.

Notes:

- Extended `HapticDrive.Input.Abstractions` with richer read-only discovery models: `InputDeviceInfo`, `InputDeviceKind`, `InputDiscoveryMethod`, `InputControlInfo`, `InputDeviceDiscoverySnapshot`, and `IWheelInputCandidateProvider`.
- Updated `IInputDeviceDiscovery` to return a full `InputDeviceDiscoverySnapshot`.
- Added `WheelInputCandidateProvider` scoring for likely Simagic wheelbase, likely GT Neo / wheel input path, likely P700 pedals, and unknown HID/game-controller candidates.
- Implemented `WindowsInputDeviceDiscovery` in `HapticDrive.Input.Windows`.
- Added `RawInputDeviceEnumerator` for read-only Raw Input metadata, including broad device class, redacted device path / instance text, HID VID/PID where available, and HID usage page / usage.
- Added `WindowsGameControllerDeviceEnumerator` for dependency-free Windows game-controller capability discovery, including display name, button count, axis count, and read-only control slots.
- Added normal-display redaction for Windows device paths so serial-like path segments are not shown directly in the UI.
- Added a WPF Devices-page Input Discovery section with a manual Refresh Input Devices button, status summary, candidate groups, discovery errors, and a safety note.
- Added input discovery to the copyable runtime diagnostics report.
- Added hardware-free tests for model construction, zero devices, discovery exceptions, candidate scoring from synthetic names, Simagic / P700 / Alpha / GT Neo detection, deterministic fake discovery, empty snapshot consumption, and no write-like discovery interface methods.
- Updated README, architecture, roadmap, known issues, and Simagic docs for Stage 2D.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 225 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 2D stayed within read-only discovery scope.
- No live paddle listener, rising-edge detection, left/right paddle mapping, `ShiftIntentEvent` routing, haptic routing, P-HPR output, protocol encoder/decoder, USB output report, write-capable feature report, or controlled write testing was implemented.
- Raw Input and Windows game-controller discovery are manual diagnostics only and do not take control from SimPro Manager or SimHub.
- DirectInput-specific enumeration and HID input-report reading remain deferred until Stage 2E or later if the implemented discovery paths are insufficient.
- Candidate scoring is deliberately non-authoritative until the user supplies Device Manager, USBView, controller tester, SimPro Manager, and optional SimHub data.
- Existing ASIO/BST-1 audio behavior and `NullAudioOutputDevice` automated-test default were not changed.
- P-HPR remains a separate non-audio actuator path with no real writes before the exact approval phrase.

## Stage 2E - Raw Paddle Input Listener and Mapping

Date: 2026-06-04

Status: Complete.

Goal: Implement a safe, read-only raw paddle input listener and manual left/right paddle mapping layer for the Simagic Alpha Evo / GT Neo wheel setup without triggering haptic output.

Notes:

- Parsed the Stage 2E brief and verified Stage 2D was complete before implementation.
- Added read-only paddle listener models in `HapticDrive.Input.Abstractions`: `InputDeviceSelection`, `WheelPaddleMapping`, `InputButtonState`, `InputEventTimestamp`, `InputListenerStatus`, raw button events, mapped paddle input events, snapshots, a mockable `IInputButtonStateReader`, `WheelPaddleInputProcessor`, and `PollingWheelPaddleInputSource`.
- Added rising-edge detection so a press fires once, holding does not repeat, release rearms the button, and repeated press after release fires again.
- Added conservative configurable debounce with a 20 ms default.
- Added UTC and stopwatch-tick timestamps for mapped paddle press diagnostics.
- Added `WindowsGameControllerButtonStateReader` in `HapticDrive.Input.Windows` using read-only Windows game-controller button polling by native joystick index from Stage 2D discovery.
- Preserved Raw Input as discovery metadata. Live Raw Input/HID report decoding is deferred until the user's exact report-descriptor/button data proves it is needed.
- Added Devices-page controls for Refresh Input Devices, selected Windows game-controller device, Start/Stop Listener, left/right button entry, Set Left From Last Button, Set Right From Last Button, listener status, current left/right state, last raw changed button, last mapped paddle event, event count, debounce, and errors.
- Persisted only safe input mapping settings: selected input device ID, selected method, left/right button IDs, and debounce duration. Haptics running state, emergency mute, ASIO arming, and P-HPR enablement remain non-persisted.
- Added hardware-free tests for mock listener start/stop, selected device tracking, left/right mapping, unmapped buttons, rising-edge behavior, hold suppression, release/repress behavior, timestamps, debounce, listener errors, disconnect state, no Stage 2E `ShiftIntentEvent` emission, and no USB write-capable listener API surface.
- Updated README, architecture, roadmap, known issues, Simagic research, user-data request, wheel-input research, shift-intent design, and safety-plan docs.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 236 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 2E stayed within read-only paddle input diagnostics and manual mapping scope.
- No real P-HPR USB writes, output reports, write-capable feature reports, real vibration commands, Simagic-specific control messages, controlled write testing, SimPro/SimHub hooking, or driver changes were implemented or executed.
- No hardware-derived `ShiftIntentEvent` routing, cached `DrivingArmed` paddle gate connection, P-HPR routing, ASIO gear-pulse integration, audio haptic routing from paddles, or `EffectEngine` gear-triggering from paddle presses was implemented.
- The existing ASIO/BST-1 audio path and `NullAudioOutputDevice` automated-test default were not changed.
- Mapped paddle events remain diagnostics only; Stage 2F is next for the Shift Intent Event Layer.

## Stage 2F - Shift Intent Event Layer

Date: 2026-06-05

Status: Complete.

Goal: Convert mapped GT Neo paddle input events from Stage 2E into accepted or suppressed ShiftIntent evaluations using cached `DrivingArmed` state from Stage 2C, without producing any haptic output.

Notes:

- Parsed the Stage 2F brief and kept scope to mapped paddle input -> shift intent evaluation -> diagnostics / in-memory accepted-event sink only.
- Added `ShiftIntentMode`, `ShiftIntentDirection`, and `ShiftIntentSource`.
- Extended `ShiftIntentEvent` with direction, source, mode, stopwatch ticks, source button ID, last known telemetry gear/speed/RPM/session/frame diagnostics, and a correlation ID while preserving the existing factory usage.
- Added `HapticDrive.Actuation.Shift` with `ShiftIntentProcessor`, `ShiftIntentProcessorOptions`, `ShiftIntentEvaluationResult`, `ShiftIntentDiagnosticsSnapshot`, `ShiftIntentTelemetrySnapshot`, `IShiftIntentSink`, and `InMemoryShiftIntentSink`.
- Default mode is `InstantPaddleOnly`: mapped left/right paddle presses are accepted immediately when cached `DrivingArmed` is true, with no telemetry wait and no confirmation event.
- Left paddle maps to `Downshift`; right paddle maps to `Upshift`.
- `TelemetryConfirmedOnly` observes mapped paddle presses diagnostically but suppresses immediate accepted paddle intent.
- `InstantWithRejectedShiftFeedback` accepts immediate intent when `DrivingArmed` is true and records a pending-confirmation diagnostic count only.
- Suppressed evaluations preserve the cached `DrivingArmed` reason when the gate is false.
- Wired the WPF Devices and Diagnostics pages to show shift intent enabled state, mode, `DrivingArmed` state/reason, telemetry age, menu-safe/recent-telemetry state, last paddle side, last direction, accepted/suppressed counters, last accepted event, last suppression reason, last known telemetry gear/speed/RPM/frame, pending confirmations, and errors.
- Persisted only safe shift-intent preferences: enabled state and selected mode.
- Added hardware-free tests for default mode, direction mapping, accepted/suppressed behavior, diagnostics counters, last accepted/suppressed state, disabled layer suppression, telemetry-confirmed-only suppression, future rejected-feedback diagnostics, cached-state-only evaluation, error capture, and no output-facing processor constructor surface.
- Updated README, architecture, roadmap, known issues, and Simagic Phase 2 docs for Stage 2F.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 250 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 2F stayed within the Shift Intent Event Layer only.
- No P700/P-HPR USB discovery, capture workflow, capture analysis, protocol hypothesis, mock gear pulse routing, real P-HPR output, or real hardware control was implemented.
- No real USB writes, output reports, write-capable feature reports, vibration commands, SimPro/SimHub hooking, driver changes, firmware work, or controlled write testing were implemented or executed.
- No haptic routing was added from paddle input.
- `MockPhprOutputDevice` is not called.
- `IPHprOutputDevice` is not called.
- `PHprCommand` is not created.
- `GearShiftEffect`, `AudioRenderPipeline`, `AudioMixer`, ASIO output, and the ASIO/BST-1 audio path are not called from paddle input.
- The paddle hot path reads cached `DrivingArmed` state only and does not wait for telemetry, perform disk IO, perform network IO, or touch audio rendering.
- `InstantPaddleOnly` is confirmed as the default mode, with no default second confirmation event.
- Stage 2G is next for read-only P700 / P-HPR device inventory.

## Stage 2G - Read-Only P700 / P-HPR Device Inventory

Date: 2026-06-05

Status: Complete.

Goal: Implement read-only Simagic P700 / P-HPR device inventory tooling and documentation without adding USB captures, protocol hypotheses, output routing, or any P-HPR writes.

Notes:

- Added `HapticDrive.Simagic.PHPR.Research` as a console/reusable research utility.
- Added inventory models and services: `SimagicDeviceInventorySnapshot`, `SimagicDeviceInventoryItem`, `SimagicDeviceCandidateKind`, `SimagicDeviceInventoryMethod`, `SimagicDeviceInventoryError`, `SimagicDeviceInventoryExport`, `SimagicDeviceInventorySanitizer`, `ISimagicDeviceInventoryProvider`, and `ISimagicDeviceInventoryExporter`.
- The default provider reuses Stage 2D read-only input discovery and adds read-only Windows HID/USB registry metadata inventory.
- Inventory captures safe metadata where available: display/manufacturer/product strings, service/driver/class data, VID/PID, MI/interface number, collection number, HID usage metadata from existing input discovery, report length placeholders, endpoint-summary placeholders, safe instance/path text, candidate kind/score/reason, method, status, error message, and timestamp.
- Added candidate classification for P700 pedal controller, P-HPR module/controller, Alpha Evo wheelbase, GT Neo wheel input, Simagic unknown, generic HID, generic USB input, and unknown.
- Added redaction for serial-like path segments and Windows usernames while preserving VID/PID and non-sensitive matching details.
- Added sanitized JSON and Markdown export support under ignored `local-device-inventory/`.
- Added a CLI safety banner, help command, inventory command, console summary, and safe no-hardware behavior.
- Fixed the existing read-only winmm game-controller P/Invoke entry-point casing so Stage 2D/2G Windows game-controller discovery degrades normally instead of reporting a missing entry point.
- Added `HapticDrive.Simagic.PHPR.Research.Tests` with hardware-free tests for model construction, empty snapshots, synthetic P700/Simagic/Alpha/GT Neo/generic HID classification, redaction, sanitized export, provider failure capture, inventory interface no-write surface, no P-HPR output/audio project references, JSON round-trip, and summary formatting.
- Created `docs/SIMAGIC_USB_DEVICE_INVENTORY.md`.
- Updated README, architecture, roadmap, known issues, Simagic research, user-data request, safety plan, and wheel-input research docs for Stage 2G.
- Local read-only inventory observed 168 total inventory items, 166 generic HID/USB candidates, 0 Simagic-specific P700/P-HPR/Alpha/GT Neo candidates, 0 P700 candidates, 0 P-HPR module/controller candidates, and 0 discovery errors.
- Real P700/P-HPR hardware identity, VID/PID, report lengths, endpoints, and P-HPR visibility remain awaiting user-provided Device Manager / USBView / sanitized tool output.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed. NuGet emitted `NU1900` because restricted network access prevented vulnerability-feed metadata from loading.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 errors. The same `NU1900` warning was reported from restored package assets.
- First `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` run had one timing-sensitive pre-existing audio streaming assertion failure while the new Stage 2G tests passed.
- Immediate rerun of `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 246 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed. The formatter reported generic workspace-load warnings only.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- inventory` passed and wrote sanitized exports to ignored `local-device-inventory/`.

Self-review:

- Stage 2G stayed within read-only inventory scope.
- No USB capture workflow, capture analysis, protocol hypothesis, mock gear-pulse routing, real P-HPR output, or controlled write testing was implemented.
- No real USB writes, output reports, write-capable feature reports, vibration commands, device-handle write paths, SimPro/SimHub hooks, driver changes, firmware work, or unsafe libusb/WinUSB takeover were implemented or executed.
- No haptic routing was added from `ShiftIntentEvent` values.
- `MockPhprOutputDevice` is not called.
- `IPHprOutputDevice` is not called.
- `PHprCommand` is not created.
- The new research project does not reference the P-HPR output abstraction project, `HapticDrive.Asio.Audio`, `GearShiftEffect`, `AudioRenderPipeline`, `AudioMixer`, ASIO output, or the ASIO/BST-1 audio path.
- Raw/private inventory files are ignored, and no raw device paths, serial numbers, USB captures, screenshots, or private hardware inventories were committed.
- P-HPR module visibility is documented as unknown and may be exposed only through the P700 controller.
- Stage 2H is next for capture workflow and metadata tooling; Stage 2G stops here.

## Stage 2H - Capture Workflow and Metadata Tooling

Date: 2026-06-05

Status: Complete.

Goal: Implement capture workflow and metadata tooling for future Simagic P700 / P-HPR USB protocol research without analyzing captures, generating protocol hypotheses, routing haptics, or sending any USB writes.

Notes:

- Parsed the Stage 2H brief and kept scope to workflow documentation, metadata models, validation, sanitization, templates, and manifests only.
- Extended `HapticDrive.Simagic.PHPR.Research` with `SimagicCaptureScenario`, `SimagicCaptureScenarioId`, required scenario definitions, target module enum, metadata records, software/device/action contexts, and setting snapshots.
- Added `SimagicCaptureFilenameBuilder` for the `YYYY-MM-DD_HHMMSS_<software>_<device>_<scenario>_<target>_<settings>.pcapng` convention with unsafe-character and serial-like text sanitization.
- Added `SimagicCaptureTemplateFactory` for synthetic metadata templates.
- Added `SimagicCaptureMetadataValidator` for required-field errors, scenario-specific strength/frequency/duration warnings, private/gitignored raw-capture path warnings, and redaction warnings.
- Added `SimagicCaptureSanitizer` to redact serial-like strings, Windows user paths, raw capture paths, and pasted raw-transfer byte snippets while preserving useful scenario/settings metadata.
- Added `SimagicCaptureManifest` and `SimagicCaptureManifestExporter` for sanitized metadata-only manifests that exclude raw capture bytes/content.
- Refactored the research console entry point into `SimagicResearchCli` and preserved the Stage 2G `inventory` command.
- Added safe Stage 2H commands: `capture-scenarios`, `capture-template`, `validate-capture-metadata`, and `capture-manifest`.
- Added `capture-metadata/` to `.gitignore`.
- Added hardware-free tests for the required scenarios, template creation, filename sanitization, validator acceptance/warnings, private path warnings, sanitization, manifest export, CLI help, and assembly reference boundaries.
- Updated `docs/SIMAGIC_CAPTURE_GUIDE.md` with the full capture workflow, scenario table, metadata fields, private storage rules, command examples, troubleshooting, and Stage 2I handoff.
- Updated README, architecture, roadmap, known issues, Simagic research, user-data request, USB inventory, safety plan, wheel-input research, and shift-intent design docs.
- Real USB captures are not present in the repository and remain pending user collection before Stage 2I analysis.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed. NuGet emitted `NU1900` because restricted network access prevented vulnerability-feed metadata from loading.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 errors. The same `NU1900` warning was reported from restored package assets.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 306 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed. The formatter reported generic workspace-load warnings only.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- capture-scenarios` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- capture-template --scenario BrakeTestVibration --target Brake` passed and wrote a generated template under ignored `capture-metadata/generated/`.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- validate-capture-metadata capture-metadata\generated\braketestvibration-brake-metadata-template.json` passed with 0 errors and expected template-completion warnings.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- capture-manifest capture-metadata\generated` passed and wrote a sanitized manifest under ignored `capture-metadata/generated/`.

Self-review:

- Stage 2H stayed within capture workflow and metadata tooling only.
- No `.pcap` or `.pcapng` parser, USB transfer analysis, protocol byte inference, report ID inference, checksum inference, command classification, protocol hypothesis, decoder, encoder, or mock protocol was implemented.
- No real P-HPR USB writes, HID writes, output reports, feature reports, vibration commands, SimPro/SimHub control, driver changes, firmware work, or controlled write testing were implemented or executed.
- No haptic routing was added from paddle input or `ShiftIntentEvent` values.
- `MockPhprOutputDevice` is not called.
- `IPHprOutputDevice` is not called.
- `PHprCommand` is not created.
- The research project still does not reference the P-HPR output abstraction project, `HapticDrive.Asio.Audio`, `GearShiftEffect`, `AudioRenderPipeline`, `AudioMixer`, ASIO output, or the ASIO/BST-1 audio path.
- No raw/private captures, USB captures, serial numbers, screenshots, or unsanitized hardware data were committed.
- Capture workflow and metadata tooling are complete. Real USB captures are pending user collection before Stage 2I analysis.
- Stage 2I Capture Analysis Framework is next; Stage 2H stops here.

## Stage 2I - Capture Analysis Framework

Date: 2026-06-07

Status: Complete.

Goal: Implement read-only Simagic P700 / P-HPR capture analysis tooling without generating protocol hypotheses, routing haptics, or sending any USB writes.

Notes:

- Parsed the attached Stage 2H brief as safety/background context, verified the repo already had Stage 2H complete, and proceeded with the local roadmap's Stage 2I capture analysis framework.
- Added `HapticDrive.Simagic.PHPR.Research.CaptureAnalysis` models for analysis reports, source kinds, file summaries, payload observations, payload summaries, byte-diff observations, pcap summaries, and warnings.
- Added Wireshark CSV import for payload columns such as `payload_spaced`, `usb.data_fragment`, and `usbhid.data`.
- Added Wireshark text-summary import for payload counts and `payload=` records.
- Added compare-summary import for byte-diff observations.
- Added `SimagicPayloadDiffAnalyzer` for closest-pair byte comparisons between two capture/export sources.
- Added pcap/pcapng container-summary parsing for sections, interfaces, packets, link types, and captured-byte totals without decoding protocol semantics.
- Added sanitized JSON export for capture analysis and capture diff reports under ignored `capture-metadata/generated/`.
- Added safe Stage 2I CLI commands: `capture-analysis` and `capture-diff`.
- Added `docs/SIMAGIC_CAPTURE_ANALYSIS.md` and updated README, architecture, roadmap, known issues, capture guide, Phase 2 research notes, user-data request, USB inventory notes, wheel-input research notes, and safety plan.
- Added hardware-free tests for synthetic Wireshark CSV, text summary, compare summary, capture diffing, pcapng container summary, sanitized export, and CLI help.
- Ran `capture-analysis` against the local sanitized P-HPR evidence bundle at `C:\Users\ethan\Downloads\Complete Files Required\P-HPR Haptics\phpr_codex_upload_bundle`. The tool observed 7 source files, 3,854 payload observations, 63 unique payload fingerprints, and 1 expected warning for a timing-only SimHub duration CSV with no payload column.
- Ran `capture-diff` against two sanitized SimPro compare-summary files and produced sanitized byte-diff observations under ignored `capture-metadata/generated/`.
- Raw/private captures, external evidence bundles, and generated analysis reports remain uncommitted.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed. NuGet emitted `NU1900` because restricted network access prevented vulnerability-feed metadata from loading.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 errors. The same `NU1900` warning was reported from restored package assets.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 283 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed. The formatter reported generic workspace-load warnings only.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- capture-analysis "C:\Users\ethan\Downloads\Complete Files Required\P-HPR Haptics\phpr_codex_upload_bundle"` passed and wrote a sanitized analysis report under ignored `capture-metadata/generated/`.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- capture-diff <two sanitized SimPro compare summaries>` passed and wrote a sanitized diff report under ignored `capture-metadata/generated/`.

Self-review:

- Stage 2I stayed within read-only capture analysis and sanitized summary export only.
- No protocol hypotheses, protocol field names, report ID inference, checksum inference, endpoint semantics, command classification, decoder, encoder, mock protocol, or mock output were implemented.
- No real P-HPR USB writes, HID writes, output reports, feature reports, vibration commands, SimPro/SimHub control, driver changes, firmware work, or controlled write testing were implemented or executed.
- No haptic routing was added from paddle input or `ShiftIntentEvent` values.
- `MockPhprOutputDevice` is not called.
- `IPHprOutputDevice` is not called.
- `PHprCommand` is not created.
- The research project still does not reference the P-HPR output abstraction project, `HapticDrive.Asio.Audio`, `GearShiftEffect`, `AudioRenderPipeline`, `AudioMixer`, ASIO output, or the ASIO/BST-1 audio path.
- No raw/private captures, USB captures, screenshots, serial numbers, unsanitized hardware data, external evidence bundles, or generated analysis reports were committed.
- Stage 2J P-HPR protocol hypotheses is next; Stage 2I stops here.

## Stage 2J - P-HPR Protocol Hypotheses

Date: 2026-06-07

Status: Complete.

Goal: Convert Stage 2I P-HPR evidence and related sanitized input-analysis outputs into formal, clearly labelled P-HPR protocol hypotheses without implementing real output, mock output integration, or any write path.

Notes:

- Created `docs/SIMAGIC_PROTOCOL_HYPOTHESES.md` with the Stage 2J purpose, no-write safety boundary, evidence reviewed, confidence scale, confirmed non-output input mappings, SimHub F1 EC hypotheses, SimPro 80 1E 89 family hypothesis, unknowns, Stage 2K mock-only surface, real-write blockers, optional user data, and the explicit real-write non-authorization statement.
- Added sanitized evidence notes under `docs/research/simagic/` for P700 pedal input, GT Neo paddle input, and P-HPR output capture observations.
- Added analysis-only hypothesis models under `HapticDrive.Simagic.PHPR.Research.Hypotheses`.
- Added built-in hypothesis records for:
  - SimHub F1 EC active/start packet: `ReadyForMockProtocol`, high confidence, blocked for real writes.
  - SimHub F1 EC stop/idle packet: `ReadyForMockProtocol`, high confidence, blocked for real writes.
  - SimHub duration timing: app-side start plus scheduled stop, `ReadyForMockProtocol`, high confidence, blocked for real writes.
  - SimPro 80 1E 89 family: separate family, `NeedsMoreCaptures`, conservative Low/Unknown field meanings, blocked for real writes.
  - P700/GT Neo input-output separation: `EvidenceOnly`, confirmed observation, not an output command.
  - Runtime identity: `EvidenceOnly`; capture USB addresses are session-only and runtime must use stable Windows identity/configured selection.
- Added sanitized JSON/Markdown hypothesis export support and safe CLI commands:
  - `hypotheses-list`
  - `hypotheses-export --output <path>`
- Updated README, architecture, roadmap, known issues, Simagic safety/research/user-data/capture/inventory/shift/wheel docs for Stage 2J completion and Stage 2K next.
- Added hardware-free tests for hypothesis construction, SimHub field map, stop/duration timing, conservative SimPro status, input/output separation, no-write notes, real-write blocking, sanitized export, and CLI commands.

Confidence levels:

- Confirmed input mappings: `ConfirmedObservation`.
- SimHub F1 EC active/stop/duration: `High` with specific byte observations marked `ConfirmedObservation` where appropriate.
- SimHub module `00`: Low/uncertain exact meaning.
- SimPro 80 1E 89 family prefix: `ConfirmedObservation`; SimPro field meanings remain Low/Unknown for Stage 2J.
- Runtime identity rule: High.

Stage 2K mock-only boundary:

- Stage 2K may create mock protocol objects, mock SimHub F1 EC packet representations, mock-only `PHprCommand` mapping, and mock start plus scheduled stop timing.
- Stage 2K may feed `MockPhprOutputDevice` in mock tests only.
- Stage 2K must not write hardware, open Simagic write handles, send HID output reports, send feature reports, or trigger real P-HPR vibration.

Real write blockers:

- Exact approval phrase has not been provided.
- No controlled write test plan has been executed.
- No real hardware write safety validation exists.
- Stop command behavior is not validated on real hardware.
- SimPro/SimHub coexistence and device ownership are not validated.
- Report ID, endpoint, interface, checksum/sequence/keepalive behavior, emergency stop path, and `PHprSafetyLimiter` remain required before any real write.
- First real test must be manual, low strength, short duration, one pedal, and no loop.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed. NuGet emitted `NU1900` because restricted network access prevented vulnerability-feed metadata from loading.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 errors. The same `NU1900` warning was reported from restored package assets.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 290 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed. The formatter reported generic workspace-load warnings only.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- hypotheses-list` passed and reported 6 hypotheses, 3 unknowns, and 12 real-write blockers.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- hypotheses-export --output capture-metadata\generated\simagic-protocol-hypotheses.json` passed and wrote a sanitized ignored export.

Self-review:

- Stage 2J stayed within protocol hypotheses, documentation, sanitized export, and tests.
- No production encoder, production decoder, live protocol adapter, or command-sending path was implemented.
- No real P-HPR USB writes, HID output reports, feature reports, vibration commands, SimPro/SimHub control, driver changes, firmware work, or controlled write testing were implemented or executed.
- No haptic routing was added from paddle input or `ShiftIntentEvent` values.
- `MockPhprOutputDevice` is not called by Stage 2J code.
- `IPHprOutputDevice` is not called by Stage 2J code.
- `PHprCommand` is not created by Stage 2J code.
- The ASIO/BST-1 audio path was not changed.
- No raw/private captures, USB captures, screenshots, serial numbers, unsanitized hardware data, external evidence bundles, or generated hypothesis exports were committed.
- Stage 2K Mock P-HPR Protocol and Output is next; Stage 2J stops here.

## Stage 2K - Mock P-HPR Protocol and Output

Date: 2026-06-07

Status: Complete.

Goal: Implement a mock-only P-HPR protocol and mock output layer based on the Stage 2J protocol hypotheses without creating real hardware writes, production protocol adapters, haptic routing, or controlled write testing.

Notes:

- Added mock-only protocol models under `HapticDrive.Simagic.PHPR.Abstractions.MockProtocol` for command intent, frames, protocol family, state, support status, encoding results, and decode results.
- Added `SimHubF1EcMockEncoder` and `SimHubF1EcMockDecoder` for Stage 2K fixtures only.
- SimHub F1 EC mock active/start frames use `F1 EC [module] 01 [frequency_hz] [strength_percent] 00 ...` with 64-byte mock payloads.
- SimHub F1 EC mock stop frames use `F1 EC [module] 00 0A 00 00 00 ...`.
- Brake maps to module byte `01`, throttle maps to module byte `02`, and `Both` expands to explicit brake plus throttle frames instead of using low-confidence module `00`.
- Added deterministic mock duration planning as start frames at offset 0 ms plus stop frames at `DurationMs`; zero-duration start requests produce stop-only mock frames.
- Emergency stop produces immediate stop frames for brake and throttle.
- Added `SimProUnknownMockFrame` and `SimProUnknownMockEncoder`; SimPro `80 1E 89` is classified separately but remains `NeedsMoreCaptures` and unsupported for detailed mock encoding.
- Extended `MockPhprOutputDevice` to record generated mock frames, connection/module availability simulation, rejected-command simulation, emergency-stop count, generated frame count, last frame, and pending scheduled stop count.
- Added safe research CLI commands:
  - `mock-protocol-examples`
  - `mock-protocol-export --output <path>`
- Added `docs/SIMAGIC_P_HPR_MOCK_PROTOCOL.md`.
- Updated README, architecture, roadmap, known issues, Simagic protocol/safety/research/capture/inventory/shift/wheel docs, and P-HPR evidence notes.
- Added hardware-free tests for SimHub mock payload examples, stop frames, both-target expansion, emergency stop, duration scheduling, clamping, invalid module/payload failures, decoder round-trip, SimProUnknownMock, mock output diagnostics, CLI examples/export, and no write-capable mock protocol API names.
- During full-suite verification, fixed a pre-existing `PollingWheelPaddleInputSource` start/stop race where the polling task captured `_listenerCancellation` through the field after `StopAsync` could null it.

SimHub F1 EC mock status:

- `ReadyForMockProtocol` only.
- Mock encoding/decoding is test/diagnostic-only.
- Nothing in the SimHub F1 EC mock protocol may be sent to real hardware.

SimProUnknownMock status:

- `80 1E 89` prefix classification is supported.
- Detailed SimPro mock encoding remains unsupported.
- Status remains `NeedsMoreCaptures`.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build` passed with 22 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Research.Tests\HapticDrive.Simagic.PHPR.Research.Tests.csproj --no-build` passed with 44 passing tests.
- First full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` attempts exposed the pre-existing input polling race and one timing-sensitive output-cadence assertion. After the input race fix and rebuild, the full suite passed with 288 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- hypotheses-list` passed and reported 6 hypotheses, 3 unknowns, and 12 real-write blockers.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- hypotheses-export --output capture-metadata\generated\simagic-protocol-hypotheses.json` passed and wrote a sanitized ignored export.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-export --output capture-metadata\generated\simagic-mock-protocol-examples.json` passed and wrote a sanitized ignored export.

Self-review:

- Stage 2K stayed within mock protocol, mock output diagnostics, docs, tests, and safe CLI examples.
- No production P-HPR encoder, production decoder, live protocol adapter, hardware sender, HID writer, output-report writer, feature-report writer, or device-control path was implemented.
- No real P-HPR USB writes, HID output reports, feature reports, vibration commands, SimPro/SimHub control, driver changes, firmware work, or controlled write testing were implemented or executed.
- No haptic routing was added from paddle input, `ShiftIntentEvent`, `VehicleState`, audio effects, ASIO output, or the mixer.
- `MockPhprOutputDevice` is used only by tests and mock-only abstractions/diagnostics.
- The ASIO/BST-1 audio path was not changed.
- Raw/private captures, USB captures, screenshots, serial numbers, unsanitized hardware data, external evidence bundles, and generated mock/hypothesis exports were not committed.
- Stage 2L P-HPR Safety Layer is next; Stage 2K stops here.

## Stage 2L - P-HPR Safety Layer

Date: 2026-06-08

Status: Complete.

Goal: Implement the reusable P-HPR safety layer around the existing mock P-HPR command/protocol/output path without creating real hardware writes, haptic routing, production protocol adapters, or controlled write testing.

Notes:

- Added `PHprSafetyLimiter`, `IPHprSafetyLimiter`, `PHprSafetyContext`, `PHprSafetyDecision`, `PHprSafetySnapshot`, `PHprSafetyViolation`, `PHprSafetyClampDetails`, `PHprSoftwareConflictStatus`, and `IPHprSafetyClock` under `HapticDrive.Simagic.PHPR.Abstractions.Safety`.
- Kept `PHprSafetyLimits.Default` conservative: max strength 0.10, max duration 100 ms, min frequency 5 Hz, max frequency 250 Hz, max command rate 10 commands/s, max continuous duration 500 ms, and `AllowRealDeviceWrites` false.
- Implemented safety clamping for strength, duration, and frequency, with deterministic decision and clamp diagnostics.
- Implemented deterministic command-rate limiting with an injected fake clock for tests.
- Implemented per-module continuous-duration estimation and rejection for sustained mock starts.
- Implemented synthetic safety context gates for disconnected device, unavailable brake/throttle modules, telemetry stale, haptics stopped, emergency mute active, `DrivingArmed` false, SimPro/SimHub conflict placeholder, and real-write blocking.
- Added emergency-stop latching to the safety limiter. Emergency stop clears command-rate and continuous-duration tracking, blocks future start commands until cleared, and records safety diagnostics.
- Added `SafetyLimitedPhprOutputDevice` to wrap `MockPhprOutputDevice`; accepted and clamped commands are forwarded to the mock output, rejected commands are not forwarded, and emergency stop is forwarded to produce immediate mock stop frames.
- Extended `MockPhprOutputDevice` with `ClearEmergencyStop` and safe stop handling so stop/emergency-stop commands can be recorded safely in restrictive mock states.
- Added safe research CLI command `safety-examples` with a no-write safety banner.
- Added `docs/SIMAGIC_P_HPR_SAFETY_LAYER.md` and updated README, architecture, roadmap, known issues, Simagic research/safety/mock-protocol/hypotheses/capture/inventory/shift/wheel/user-data docs for Stage 2L completion and Stage 2M next.
- Added hardware-free tests for defaults, strength/duration/frequency clamps, command-rate limit and recovery, continuous-duration rejection, emergency stop and clear, disconnected-device behavior, module availability, restrictive context gates, safe stop allowance, real-write blocking diagnostics, deterministic fake clock behavior, safety wrapper forwarding, rejected-command suppression, and no HID/USB write API surface.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build` passed with 40 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Research.Tests\HapticDrive.Simagic.PHPR.Research.Tests.csproj --no-build` passed with 46 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 332 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- hypotheses-list` passed and reported 6 hypotheses, 3 unknowns, and 12 real-write blockers.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Stage 2L stayed within safety infrastructure, mock-output wrapping, docs, tests, and safe CLI examples.
- No mock gear-pulse routing, mock road/slip/lock routing, SimPro/SimHub coexistence detection, controlled write test plan, production encoder, production decoder, real output adapter, or real P-HPR control was implemented.
- No real P-HPR USB writes, HID output reports, HID feature reports, vibration commands, device-handle writes, SimPro/SimHub control, driver changes, firmware work, or controlled write testing were implemented or executed.
- No haptic routing was added from paddle input, `ShiftIntentEvent`, `VehicleState`, audio effects, ASIO output, or the mixer.
- `MockPhprOutputDevice` remains memory-only and is used only by tests, mock-only abstractions, diagnostics, and the safety-limited mock wrapper.
- The ASIO/BST-1 audio path was not changed.
- Raw/private captures, USB captures, screenshots, serial numbers, unsanitized hardware data, external evidence bundles, and generated local analysis exports were not committed.
- Stage 2M Mock Gear Pulse Routing is next; Stage 2L stops here.

## Stage 2M - Mock Gear Pulse Routing

Date: 2026-06-08

Status: Complete.

Goal: Route accepted `ShiftIntentEvent` values to the Stage 2L safety-limited mock P-HPR output path without creating any real P-HPR output, USB write, HID report, road/slip/lock routing, or ASIO/BST-1 audio-path change.

Notes:

- Added `HapticDrive.Actuation.PHpr` with `PHprGearPulseRouter`, router options, conservative profile defaults, routing result/status/snapshot models, and mock target mapping.
- Default mock gear pulse routing is enabled, targets both brake and throttle, uses strength `0.05`, frequency `50 Hz`, duration `50 ms`, priority `100`, and source `PaddleShiftIntent`.
- The router ignores disabled routing, missing events, events not accepted by `DrivingArmed`, and unknown-direction events.
- Accepted events create a mock-only `PHprCommand` and pass it through `SafetyLimitedPhprOutputDevice` before `MockPhprOutputDevice` can record commands or frames.
- Stage 2L safety remains authoritative: clamps are preserved, restrictive safety contexts reject starts, telemetry stale / haptics stopped / emergency mute / driving-not-armed gates still block, and emergency stop latches until cleared.
- Integrated a private WPF mock stack: `MockPhprOutputDevice`, `SafetyLimitedPhprOutputDevice`, and `PHprGearPulseRouter`.
- Added Devices-page mock gear routing diagnostics and controls for enabled state, target, strength, frequency, duration, clear diagnostics, mock emergency stop, and clear mock emergency stop.
- Persisted only mock routing preferences. Emergency-stop state, safety latch state, mock command history, mock frame history, real write approval, and real output state are not persisted.
- Added `docs/SIMAGIC_P_HPR_MOCK_GEAR_ROUTING.md` and updated README, architecture, roadmap, known issues, shift-intent, safety, mock protocol, research, and protocol-hypothesis docs.
- Added hardware-free router tests for accepted/suppressed/disabled routing, default both-target mock frames, default pulse settings, upshift/downshift equivalence, command source, safety clamps, blocked contexts, emergency stop, mock command/frame diagnostics, and no USB/HID/audio/road/slip/lock route surface.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Actuation.Tests\HapticDrive.Actuation.Tests.csproj --no-build` passed with 43 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build` passed with 40 passing tests.
- First full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` hit the known timing-sensitive `NullOutput_OutputOwnedStreamingReportsCallbackCadence` assertion; no Stage 2M tests failed.
- Rerun full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 347 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed and printed 6 safety examples.
- The first attempt to run all three research CLI commands in parallel failed with `.deps.json` file locks from concurrent project builds; rerunning the same commands sequentially passed.

Self-review:

- Stage 2M stayed within mock gear pulse routing from accepted `ShiftIntentEvent` values only.
- Suppressed shift intents do not route.
- `DrivingArmed` is not bypassed, no telemetry wait is added, and no second confirmation pulse is created by default.
- No road vibration, wheel slip, or wheel lock routing was implemented.
- No SimPro / SimHub coexistence detection was implemented.
- No controlled write testing was implemented.
- No production encoder, production decoder, real output adapter, or real P-HPR control was implemented.
- No real P-HPR USB writes, HID output reports, HID feature reports, vibration commands, device-handle writes, SimPro/SimHub control, driver changes, firmware work, or controlled write testing were implemented or executed.
- The ASIO/BST-1 audio path was not changed.
- Raw/private captures, USB captures, screenshots, serial numbers, unsanitized hardware data, external evidence bundles, and generated local analysis exports were not committed.
- Stage 2N Mock Road Vibration, Wheel Slip, and Wheel Lock Routing is next; Stage 2M stops here.

## Stage 2N - Mock Road Vibration, Wheel Slip, and Wheel Lock Routing

Date: 2026-06-08

Status: Complete.

Goal: Route road vibration, wheel slip, and wheel lock from existing `VehicleState` / `HapticPipelineSnapshot` data to the Stage 2L safety-limited mock P-HPR output path without creating real P-HPR output, USB writes, HID reports, SimPro/SimHub coexistence detection, new F1 25 parser fields, or ASIO/BST-1 audio-path changes.

Notes:

- Added `PHprPedalEffectsRouter`, `PHprPedalEffectsRouterOptions`, `PHprPedalEffectKind`, `PHprPedalEffectState`, `PHprPedalEffectProfile`, route result/status/snapshot models, and per-effect diagnostics under `HapticDrive.Actuation.PHpr`.
- Implemented mock road vibration routing from existing speed and surface-ID `VehicleState` data. Default target is both brake and throttle, strength range `0.01` to `0.04`, frequency range `25` to `45 Hz`, duration `50 ms`, and source `RoadTexture`.
- Implemented mock wheel slip routing from existing wheel slip ratio/angle, speed, throttle, brake, and traction-control `VehicleState` data. Default target is throttle, strength range `0.03` to `0.08`, frequency range `45` to `75 Hz`, duration `50 ms`, and source `WheelSlip`.
- Implemented mock wheel lock routing from existing brake input, wheel slip ratio, wheel speed, speed, and ABS `VehicleState` data. Default target is brake, strength range `0.04` to `0.10`, frequency range `60` to `90 Hz`, duration `50 ms`, and source `WheelLock`.
- Added per-target-module priority: wheel lock, then wheel slip, then road vibration.
- Added deterministic minimum interval suppression per effect/module to avoid command storms before commands reach the Stage 2L safety limiter.
- Routed all commands only through `SafetyLimitedPhprOutputDevice` wrapping `MockPhprOutputDevice`.
- Updated the WPF app to share one mock P-HPR output stack between Stage 2M gear routing and Stage 2N pedal effects, keeping mock command/frame counts, pending scheduled stops, safety state, and emergency stop global for the mock P-HPR path.
- Added Devices-page mock pedal-effect controls and diagnostics for global enabled state, road/slip/lock enabled state, target, strength, frequency, duration, route counts, safety rejections, interval suppression, last active effect, last target, command summary, safety decision, mock output counts, pending stops, and emergency stop.
- Persisted only safe mock pedal-effect preferences. Emergency-stop state, safety latch state, mock histories, real-write approval, real-write enabled state, and real-write armed state are not persisted.
- Added `docs/SIMAGIC_P_HPR_MOCK_PEDAL_EFFECTS_ROUTING.md` and updated README, architecture, roadmap, known issues, safety layer, safety plan, mock protocol, mock gear routing, and Phase 2 research docs.
- Added hardware-free router tests for disabled routing, default targets, per-effect enable flags, priority, safety context gates, safety clamping, mock command/frame/pending-stop diagnostics, interval suppression, emergency stop, and no USB/HID/ASIO/write API surface.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Actuation.Tests\HapticDrive.Actuation.Tests.csproj --no-restore` passed with 58 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 362 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Stage 2N stayed within mock-only road vibration, wheel slip, and wheel lock routing from existing `VehicleState` / `HapticPipelineSnapshot` data.
- No new F1 25 packet layouts, offsets, lengths, enum values, or versions were guessed or parsed.
- Stage 2M gear pulse routing still works and remains separate from the new pedal-effects router.
- `DrivingArmed`, telemetry stale, haptics stopped, emergency mute, module availability, disconnected output, emergency stop, command-rate, continuous-duration, and real-write gates remain enforced by the Stage 2L safety layer.
- No SimPro / SimHub coexistence detection was implemented.
- No controlled write testing was implemented.
- No production encoder, production decoder, real output adapter, or real P-HPR control was implemented.
- No real P-HPR USB writes, HID output reports, HID feature reports, vibration commands, device-handle writes, SimPro/SimHub control, driver changes, firmware work, or controlled write testing were implemented or executed.
- The ASIO/BST-1 audio path was not changed.
- Raw/private captures, USB captures, screenshots, serial numbers, unsanitized hardware data, external evidence bundles, and generated local analysis exports were not committed.
- Stage 2O SimPro / SimHub Coexistence Detection is next; Stage 2N stops here.

## Stage 2O - SimPro / SimHub Coexistence Detection

Date: 2026-06-08

Status: Complete.

Goal: Implement safe, read-only SimPro Manager and SimHub coexistence detection and warnings, wire the status into P-HPR safety contexts, and keep Stage 2O free of real P-HPR output, USB writes, process control, or controlled write testing.

Notes:

- Added `HapticDrive.Simagic.PHPR.Abstractions.Coexistence` with `IPHprSoftwareCoexistenceDetector`, `PHprSoftwareCoexistenceDetector`, `IPHprSoftwareProcessProvider`, `WindowsProcessSnapshotProvider`, `PHprSoftwareCoexistenceSnapshot`, `PHprSoftwareProcessSnapshot`, `PHprDetectedSoftwareProcess`, and `PHprCoexistenceOptions`.
- Implemented conservative read-only process-name matching for SimPro Manager and SimHub.
- Reported `Unknown`, `Clear`, `SimProRunning`, `SimHubRunning`, and `ActiveConflict` coexistence states.
- Added safe provider failure and non-Windows fallback handling. Snapshot-level access failures report `Unknown`; per-process metadata failures are skipped without controlling or modifying processes.
- Wired the latest WPF coexistence snapshot into `PHprSafetyContext.SoftwareConflictStatus`.
- Kept the Stage 2L `ActiveConflict` rejection path authoritative through `PHprSafetyViolationCode.SimProConflict`.
- Added WPF Devices-page coexistence diagnostics for SimPro Manager running state, SimHub running state, status, last scan time, direct-control block status, detected process matches, read-only detection statement, and errors.
- Added the same coexistence status to the copyable diagnostics report.
- Added `docs/SIMAGIC_SIMPRO_SIMHUB_COEXISTENCE.md` and updated README, architecture, roadmap, known issues, safety layer, safety plan, and Phase 2 research docs.
- Documented that the extended Phase 2 / Phase 3 master prompt authorizes implementing the later gated Stage 2Q real-write code path, while still forbidding unattended hardware vibration, automated real writes, automatic startup pulses, persisted arming, and claims of physical validation.
- Added hardware-free tests for status mapping, safe provider errors, non-Windows fallback, safety limiter conflict rejection, and absence of control/hook/kill/write API surface in coexistence types.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build` passed with 49 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 371 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Stage 2O stayed within read-only SimPro Manager / SimHub process-name detection and safety-context warning integration.
- No process kill, hook, injection, patching, memory inspection, IPC control, file modification, settings modification, or external software control was implemented.
- No controlled write test plan was implemented.
- No production encoder, production decoder, real output adapter, or real P-HPR control was implemented.
- No real P-HPR USB writes, HID output reports, HID feature reports, vibration commands, device-handle writes, SimPro/SimHub control, driver changes, firmware work, or controlled write testing were implemented or executed.
- The ASIO/BST-1 audio path was not changed.
- Raw/private captures, USB captures, screenshots, serial numbers, unsanitized hardware data, external evidence bundles, and generated local analysis exports were not committed.
- Stage 2P Controlled Write Test Plan is next; Stage 2O stops here.

## Stage 2P - Controlled Write Test Plan

Date: 2026-06-08

Status: Complete.

Goal: Create the no-write controlled write test plan, UI readiness checklist, evidence mapping, and manual runbook before any direct real-write code is enabled.

Notes:

- Added `HapticDrive.Simagic.PHPR.Abstractions.Readiness` with `PHprControlledWriteChecklist`, `PHprControlledWriteReadiness`, `PHprControlledWriteReadinessIssue`, `PHprControlledWriteReadinessIssueCode`, `PHprControlledWriteTestPlan`, and `PHprManualTestResultTemplate`.
- The readiness model intentionally reports Stage 2P as blocked for real P-HPR output, even when future manual checklist inputs are all true.
- Added disabled WPF Devices-page direct-write readiness diagnostics. The section has no pulse buttons, no write-capable controls, no real adapter, and no HID writer.
- Added direct-write readiness state to the copyable diagnostics report.
- Reviewed sanitized local evidence summaries from `C:\Users\ethan\Downloads\Complete Files Required` for P-HPR output, GT Neo shift paddles, and P700 brake/throttle input. Raw captures, zips, serials, and private paths were not committed.
- Added `docs/SIMAGIC_P_HPR_CONTROLLED_WRITE_TEST_PLAN.md` with preconditions, first-write limits, manual sequence, pass/fail criteria, abort criteria, evidence map, and Stage 2Q boundary.
- Added `docs/SIMAGIC_P_HPR_MANUAL_VALIDATION_RUNBOOK.md` with manual brake/throttle pulse steps, emergency stop test, gate tests, and a result template.
- Updated README, architecture, roadmap, known issues, safety plan, protocol hypotheses, Phase 2 research notes, and SimPro/SimHub coexistence docs.
- Added hardware-free tests for readiness blockers, future checklist behavior, test-plan coverage, and manual-result privacy boundaries.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build` passed with 58 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 380 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Stage 2P stayed within planning, documentation, readiness diagnostics, and hardware-free tests.
- The readiness model remains no-write and cannot enable, arm, or send manual pulses.
- No real output adapter, HID writer, write-capable UI, direct pulse button, production encoder, production decoder, or real P-HPR control was implemented.
- No real P-HPR USB writes, HID output reports, HID feature reports, vibration commands, device-handle writes, SimPro/SimHub control, driver changes, firmware work, controlled write execution, or physical validation were implemented or executed.
- The ASIO/BST-1 audio path was not changed.
- Raw/private captures, USB captures, screenshots, serial numbers, unsanitized hardware data, external evidence bundles, generated local analysis exports, and private manual validation results were not committed.
- Stage 2Q Gated Minimal Real P-HPR Write Implementation is next; Stage 2P stops here.

## Stage 2Q - Gated Minimal Real P-HPR Write Implementation

Date: 2026-06-08

Status: Complete.

Goal: Implement the minimal gated real P-HPR direct-output path for later manual local testing while keeping real output default-off, unarmed, non-persisted, fake-writer tested, and physically unvalidated.

Notes:

- Added `HapticDrive.Simagic.PHPR.Output.Windows` with `PHprHidDeviceSelector`, `IPhprHidReportWriter`, `WindowsHidReportWriter`, `SimHubF1EcRealReportEncoder`, `SimagicPhprOutputDevice`, `PHprRealOutputOptions`, direct-output diagnostics, and `PHprDirectGearPulseRouter`.
- Implemented the SimHub F1 EC start/stop family only: brake module `01`, throttle module `02`, start state `01`, stop state `00`, direct Hz/percent bytes, and software-timed delayed stop.
- Kept SimPro Manager `80 1E 89` detailed writes unsupported.
- Gated real start reports behind direct-control enable, direct-control arm, selected device/interface/report, non-latched emergency stop, clear SimPro/SimHub coexistence, and `PHprSafetyLimiter`.
- Made stop/emergency-stop capable of sending brake/throttle stop reports when a selected device is available; dispose attempts stop only when selected and armed or when a stop is already pending.
- Added WPF Devices-page real direct-control controls for runtime-only enable/arm, manual device/interface/report selection, per-pedal brake/throttle settings, one-pulse brake/throttle test buttons, emergency stop, clear emergency stop, coexistence/safety status, and last write diagnostics.
- Wired accepted `ShiftIntentEvent` values to `PHprDirectGearPulseRouter`; the route stays inert unless real direct control is enabled and armed for the current session.
- Added the real direct-control snapshot to Diagnostics and clarified that real direct-control enable/arm/device selection is not persisted.
- Added `docs/SIMAGIC_P_HPR_REAL_WRITE_IMPLEMENTATION.md` and `docs/SIMAGIC_P_HPR_USER_GUIDE.md`.
- Updated README, architecture, roadmap, known issues, safety plan, protocol hypotheses, controlled write plan, manual validation runbook, Phase 2 research notes, and SimPro/SimHub coexistence docs.
- Added fake-writer tests for default-off startup, no selected device, non-clear coexistence blocking, safety rejection, brake/throttle report bytes, duration stop scheduling, emergency stop, dispose stop behavior, accepted/suppressed paddle routing, per-pedal settings, ASIO isolation, and mock-path preservation.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build` passed with 77 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 399 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Stage 2Q implemented gated write-capable infrastructure only; no real P-HPR hardware pulse was executed.
- Real direct control remains disabled and unarmed by default, and enable/arm/device selection are runtime-only.
- Automated tests use fake HID writers only and do not open real devices.
- No physical P-HPR safety, pedal mapping, stop behavior, safe gain, physical latency, or feel claim is made.
- No SimPro Manager `80 1E 89` write path was implemented.
- No raw captures, serial numbers, private device paths, unsanitized inventories, generated local analysis exports, or private manual validation results were committed.
- The ASIO/BST-1 audio path was not changed.
- Stage 2R Controlled Real P-HPR Validation Harness is next; Stage 2Q stops here.

## Stage 2R - Controlled Real P-HPR Validation Harness

Date: 2026-06-08

Status: Complete.

Goal: Add a controlled real P-HPR validation harness, checklist, private result export, app workflow, and manual validation guide without executing hardware vibration from automated verification.

Notes:

- Added `HapticDrive.Simagic.PHPR.Abstractions.Validation` with `PHprManualValidationChecklist`, readiness issues, manual result models, result evaluation, and a local Markdown exporter.
- Added a WPF Devices-page controlled validation harness with user-confirmed checklist inputs, readiness diagnostics, manual result fields, copyable validation diagnostics, and local private result export.
- The harness derives direct-control readiness from the existing real P-HPR runtime state, selected device/interface/report, coexistence status, safety visibility, emergency-stop state, and user confirmations.
- Exported validation files are written under `local-validation-results/`, which is ignored by git together with `manual-validation/`.
- Added `docs/SIMAGIC_P_HPR_CONTROLLED_REAL_VALIDATION.md` and `docs/USER_GUIDE.md`.
- Updated README, architecture, roadmap, known issues, safety plan, user guide, Phase 2 research notes, and manual validation runbook to describe the controlled validation harness and private evidence boundary.
- Added hardware-free tests for validation readiness blockers, ready-state behavior, pass-result gating, hardware-confirmation gating, Markdown warnings, and local export behavior.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build` passed with 84 passing tests.
- Initial full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` reported two timing-sensitive failures in `OutputStreamingTests.NullOutput_OutputOwnedStreamingReportsCallbackCadence` and `HapticPipelineCoordinatorTests.OutputOwnedRendering_StaleTelemetryMutesEffectsByWallClockTimeout`; both tests passed when rerun individually.
- Rerun full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 406 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Stage 2R added the validation harness and private result workflow only; no automated real P-HPR pulse or HID write was executed.
- The harness does not send hardware output and does not bypass the Stage 2Q runtime direct-control enable, arm, selected device/interface/report, coexistence, safety, or emergency-stop gates.
- Manual result export blocks an attempted pass decision until required observations and hardware-confirmation fields are complete.
- No physical P-HPR safety, pedal mapping, stop behavior, safe gain, physical latency, or feel claim is made.
- Raw captures, serial numbers, private device paths, unsanitized inventories, generated local analysis exports, and private manual validation results were not committed.
- The ASIO/BST-1 audio path was not changed.
- Phase 3A Production P-HPR Output Adapter Hardening is next; Stage 2R stops here.

## Phase 3A - Production P-HPR Output Adapter Hardening

Date: 2026-06-08

Status: Complete.

Goal: Harden the Stage 2Q real direct-output adapter into a production-quality P-HPR backend while preserving all safety gates and avoiding unattended hardware output.

Notes:

- Added explicit HID writer lifecycle to `IPhprHidReportWriter`: `OpenAsync`, `WriteReportAsync`, `CloseAsync`, and `IsOpen`.
- Hardened `WindowsHidReportWriter` with selected-report validation, persistent open/close ownership, write failure classification, disconnect classification, and close behavior.
- Added `PHprHidConnectionState`, `PHprHidWriteStatus`, and `PHprRealOutputConnectionDiagnostics`.
- Added normalized write timeout settings to `PHprRealOutputOptions` with a default of `250 ms`.
- Reworked `SimagicPhprOutputDevice` to lazily open only on explicit start/stop/emergency-stop operations, validate selected interface/report state, timeout-wrap writer operations, classify timeout/disconnect/invalid-report failures, track lifecycle counters, close on dispose, and preserve stop/emergency-stop behavior where safe.
- Kept start commands gated behind direct-control enable, direct-control arm, selected interface/report, clear SimPro/SimHub coexistence, clear emergency stop, and `PHprSafetyLimiter` acceptance.
- Updated WPF real direct-control diagnostics with connection state, writer-open state, open/close counts, timeout, last open/write/stop/close status, disconnect count, timeout count, invalid-report count, and stop-report count.
- Added `docs/SIMAGIC_P_HPR_OUTPUT_ADAPTER.md`.
- Updated user guide, Simagic P-HPR guide, real-write implementation notes, safety plan, Phase 2 research notes, README, architecture, roadmap, and known issues.
- Added fake-writer tests for explicit open/close, open gating, write failure, stop failure, disconnect, write timeout, invalid report length, and dispose stop/close behavior.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build` passed with 91 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 413 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Phase 3A hardens the existing direct-output adapter only; it does not add a second output path.
- App startup and configuration still do not open the writer, start vibration, or send reports.
- Direct starts remain default-off, unarmed, runtime-only, coexistence-gated, emergency-stop-gated, and safety-limited.
- Stop and emergency-stop paths can attempt stop reports only with a selected valid interface; dispose closes the writer where possible.
- Automated tests use fake HID writers only and do not open real hardware.
- No physical P-HPR safety, pedal mapping, stop behavior, safe gain, physical latency, or feel claim is made.
- No raw captures, serial numbers, private device paths, unsanitized inventories, generated local analysis exports, or private manual validation results were committed.
- The ASIO/BST-1 audio path was not changed.
- Phase 3B Instant Paddle Gear Pulse Production Integration is next; Phase 3A stops here.

## Phase 3B - Instant Paddle Gear Pulse Production Integration

Date: 2026-06-08

Status: Complete.

Goal: Complete the instant GT Neo paddle to P-HPR gear-pulse production path without waiting for F1 25 telemetry gear confirmation, while preserving direct-control gates and hardware-free automated verification.

Notes:

- Added accepted-time diagnostics to `ShiftIntentEvent` and `ShiftIntentProcessor` so paddle event time and accepted shift-intent time can be tracked separately.
- Extended `PHprDirectGearPulseRouter` with command-created timestamps, per-command trace records, first write-completion timestamp, and route result access to the accepted shift intent.
- Kept upshift/downshift on the same default pulse while preserving direction in diagnostics.
- Preserved independent brake/throttle real gear-pulse settings for enabled state, strength, frequency, and duration.
- Persisted safe real gear-pulse settings in app settings while keeping direct-control enablement, arming, selected device path, emergency-stop latch, command history, and write history runtime-only.
- Added WPF diagnostics for last real gear-pulse latency and safe-settings persistence status.
- Added `docs/SIMAGIC_P_HPR_INSTANT_SHIFT_GUIDE.md`.
- Updated user guide, Simagic P-HPR guide, output adapter notes, real-write implementation notes, shift-intent design, safety plan, Phase 2 research notes, README, architecture, roadmap, and known issues.
- Added app-settings tests for safe real gear-pulse persistence and clamping.
- Added fake-writer/router tests for accepted upshift/downshift routing, latency traces, default same up/down pulse, default-off real mode, SimPro conflict rejection, brake-only, throttle-only, both-pedal writes, and expected start/stop reports.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build` passed with 97 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.App.Tests\HapticDrive.Asio.App.Tests.csproj --no-build` passed with 2 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Actuation.Tests\HapticDrive.Actuation.Tests.csproj --no-build` passed with 58 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 421 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Phase 3B completes the instant paddle gear-pulse production route only; it does not route real road vibration, wheel slip, or wheel lock.
- The hot path uses accepted shift intent and does not wait for F1 25 telemetry gear-change confirmation.
- Cached `DrivingArmed` / Menu Safe gating remains the protection against menu, stale telemetry, stopped haptics, and emergency-mute pulses.
- Direct real output remains disabled and unarmed by default, and enable/arm/device path state is not persisted.
- Automated tests use fake HID writers only and do not open real hardware.
- Software latency diagnostics are not physical latency measurements.
- No physical P-HPR safety, pedal mapping, stop behavior, safe gain, physical latency, or feel claim is made.
- No raw captures, serial numbers, private device paths, unsanitized inventories, generated local analysis exports, or private manual validation results were committed.
- The ASIO/BST-1 audio path was not changed.
- Phase 3C P-HPR Road Vibration Production Integration is next; Phase 3B stops here.

## Phase 3C - P-HPR Road Vibration Production Integration

Date: 2026-06-08

Status: Complete.

Goal: Route road vibration to the P-HPR output path in production mode through the same safe backend, while keeping ASIO/BST-1 road texture independent and keeping real output disabled by default.

Notes:

- Added `PHprRoadVibrationRouter` and related options, settings, result, status, and snapshot models under `HapticDrive.Actuation.PHpr`.
- Routed production road vibration from existing `VehicleState` / `HapticPipelineSnapshot` data through mock or gated real `IPHprOutputDevice` backends without adding new F1 25 parser fields.
- Added independent brake/throttle road-vibration settings for enabled state, minimum strength, maximum strength, minimum frequency, maximum frequency, and duration.
- Kept road-vibration priority below gear pulse, wheel slip, and wheel lock.
- Added deterministic per-pedal route-interval suppression to avoid command storms before commands reach the safety limiter.
- Preserved gates for telemetry freshness, haptics running, emergency mute, cached `DrivingArmed`, selected real output readiness, SimPro/SimHub coexistence, emergency stop, and `PHprSafetyLimiter` acceptance.
- Persisted safe real road-vibration settings while keeping direct-control enablement, arming, private HID device path, emergency-stop latch, command history, and write history runtime-only.
- Added WPF real direct-control controls and diagnostics for road vibration enabled state, brake/throttle road settings, and last road route result.
- Added `docs/SIMAGIC_P_HPR_ROAD_VIBRATION_GUIDE.md`.
- Updated user guides, output adapter notes, safety plan, Phase 2 research notes, README, architecture, roadmap, and known issues.
- Added mock/fake-real tests for road routing, stale telemetry blocking, `DrivingArmed` blocking, SimPro conflict rejection, command-interval suppression, priority ordering, per-pedal settings persistence, and ASIO independence.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Actuation.Tests\HapticDrive.Actuation.Tests.csproj --no-build` passed with 65 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build` passed with 99 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.App.Tests\HapticDrive.Asio.App.Tests.csproj --no-build` passed with 4 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 432 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Phase 3C routes real road vibration only through the existing gated real P-HPR backend; no second output path was added.
- The ASIO/BST-1 road texture effect remains independent and unchanged.
- Real road vibration is disabled by default and still requires explicit direct-control enablement and arming at runtime.
- Direct-control enablement, arming, selected device path, emergency-stop latch, command history, and write history are not persisted.
- Automated tests use mock output and fake HID writers only and do not open real hardware.
- No physical P-HPR road feel, safety, pedal mapping, stop behavior, safe gain, physical latency, sustained-vibration behavior, or SimPro/SimHub real-device coexistence claim is made.
- No raw captures, serial numbers, private device paths, unsanitized inventories, generated local analysis exports, or private manual validation results were committed.
- Phase 3D P-HPR Wheel Slip And Wheel Lock Production Integration is next; Phase 3C stops here.

## Phase 3D - P-HPR Wheel Slip And Wheel Lock Production Integration

Date: 2026-06-08

Status: Complete.

Goal: Route wheel slip and wheel lock to the production P-HPR output path through the same safe backend, while keeping ASIO/BST-1 slip effects independent and keeping real output disabled by default.

Notes:

- Added `PHprSlipLockRouter` and related options, settings, result, status, and snapshot models under `HapticDrive.Actuation.PHpr`.
- Routed production wheel slip and wheel lock from existing `VehicleState` / `HapticPipelineSnapshot` data through mock or gated real `IPHprOutputDevice` backends without adding new F1 25 parser fields.
- Added wheel-slip and wheel-lock settings for enabled state, target module, minimum strength, maximum strength, minimum frequency, maximum frequency, and duration.
- Kept gear pulse as highest priority, wheel lock above wheel slip, and both slip/lock effects above road vibration.
- Added deterministic per-module route-interval suppression to avoid command storms before commands reach the safety limiter.
- Preserved gates for telemetry freshness, haptics running, emergency mute, cached `DrivingArmed`, selected real output readiness, SimPro/SimHub coexistence, emergency stop, and `PHprSafetyLimiter` acceptance.
- Persisted safe real slip/lock settings while keeping direct-control enablement, arming, private HID device path, emergency-stop latch, command history, and write history runtime-only.
- Added WPF real direct-control controls and diagnostics for slip/lock enabled state, effect settings, and last slip/lock route result.
- Added same-tick coordination so real road vibration yields after a higher-priority slip/lock route.
- Added `docs/SIMAGIC_P_HPR_SLIP_LOCK_GUIDE.md`.
- Updated user guides, output adapter notes, real-write implementation notes, safety plan, Phase 2 research notes, README, architecture, roadmap, and known issues.
- Added mock/fake-real tests for slip routing, lock routing, priority, safety gates, SimPro conflict rejection, command-interval suppression, safe settings persistence, fake-writer command output, and ASIO independence.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Actuation.Tests\HapticDrive.Actuation.Tests.csproj --no-build` passed with 76 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build` passed with 101 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.App.Tests\HapticDrive.Asio.App.Tests.csproj --no-build` passed with 6 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 447 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Phase 3D routes real wheel slip and wheel lock only through the existing gated real P-HPR backend; no second output path was added.
- The ASIO/BST-1 slip and brake-lock audio effect remains independent and unchanged.
- Real slip/lock routing is disabled by default and still requires explicit direct-control enablement and arming at runtime.
- Direct-control enablement, arming, selected device path, emergency-stop latch, command history, and write history are not persisted.
- Automated tests use mock output and fake HID writers only and do not open real hardware.
- No physical P-HPR slip feel, lock feel, safety, pedal mapping, stop behavior, safe gain, physical latency, sustained-vibration behavior, or SimPro/SimHub real-device coexistence claim is made.
- No raw captures, serial numbers, private device paths, unsanitized inventories, generated local analysis exports, or private manual validation results were committed.
- Phase 3E P-HPR UI, Profiles, Diagnostics, And User Workflow is next; Phase 3D stops here.

## Phase 3E - P-HPR UI, Profiles, Diagnostics, And User Workflow

Date: 2026-06-08

Status: Complete.

Goal: Polish the P-HPR workflow around Devices, Profiles, Settings, Diagnostics, and user documentation without adding a new hardware output path or persisting runtime-only direct-control state.

Notes:

- Added a Devices-page `P-HPR Workflow Summary` covering mode, selected-output status, SimPro/SimHub coexistence, direct-control arm state, emergency stop, validation readiness, settings summaries, counters, and warnings.
- Added `PhprEffectProfileStore` for safe P-HPR effect-profile save/load beside the existing audio profile.
- P-HPR profiles save shift intent, mock gear routing, mock pedal effects, real gear pulse, road vibration, wheel slip, and wheel lock preferences only.
- Kept real direct-control enablement, arming, selected private device path, emergency-stop latch, command history, write history, and validation results runtime-only and out of profiles.
- Integrated P-HPR profile save/load/reset into the Profiles page while preserving runtime-only arm/device state.
- Added diagnostics report lines for profile boundaries, P-HPR workflow mode, mock/real routing status, coexistence, validation, and selected-output state without raw private device paths.
- Added `docs/SIMAGIC_P_HPR_UI_PROFILES_DIAGNOSTICS.md`.
- Expanded `docs/USER_GUIDE.md` to cover ASIO/BST-1, F1 telemetry, UDP forwarding, recording/replay, P-HPR mock mode, real direct mode, instant gear pulse, road vibration, slip, lock, emergency stop, SimPro/SimHub warnings, profiles, diagnostics, and validation workflow.
- Updated README, architecture, roadmap, known issues, safety plan, Phase 2 research notes, and Simagic P-HPR user guide for Phase 3E.
- Added app tests for P-HPR profile round-trip, safe clamping, corrupt/missing/unsupported load failures, profile privacy boundaries, and diagnostics report text.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.App.Tests\HapticDrive.Asio.App.Tests.csproj --no-build` passed with 12 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 453 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Phase 3E adds workflow polish, profiles, diagnostics, and user documentation only; no new HID write path, USB protocol path, ASIO/BST-1 route, or telemetry parser field was added.
- Real direct-control enablement, arming, selected device path, emergency-stop latch, command history, write history, and private validation data are not persisted in app settings or P-HPR profiles.
- Diagnostics report selected-output status is summarized without raw private device paths or serial numbers.
- Automated tests use app models, mock output, and fake/model diagnostics only and do not open real hardware.
- No physical P-HPR safety, pedal mapping, stop behavior, safe gain, physical latency, sustained-vibration behavior, road feel, slip feel, lock feel, or UI usability claim is made.
- No raw captures, serial numbers, private device paths, unsanitized inventories, generated local analysis exports, or private manual validation results were committed.
- Phase 3F Integrated Replay Validation is next; Phase 3E stops here.

## Phase 3F - P-HPR Integrated Replay Validation

Date: 2026-06-08

Status: Complete.

Goal: Validate P-HPR road, wheel-slip, and wheel-lock routing from recorded or synthetic replay telemetry without requiring live F1 25, live pedal input, or real P-HPR writes.

Notes:

- Added deterministic replay-validation tests that drive the existing `TelemetryReplayService` through `HapticPipelineCoordinator` and then route fake P-HPR effects through mock output only.
- Validated replay updates `DrivingArmed` from F1 25 session, lap, and car-status packets.
- Validated replay road-vibration routing from F1 25 car telemetry without creating gear-paddle events.
- Validated replay wheel-slip and wheel-lock routing from F1 25 motion and car-telemetry packets without creating synthetic gear-paddle events.
- Validated profile settings for replay-driven pedal effects, including road target selection and disabled slip/lock behavior.
- Validated stale telemetry after replay and emergency mute rejection before commands reach mock output.
- Added replay source, replay packet count, and pipeline input source to P-HPR workflow diagnostics.
- Added `docs/SIMAGIC_P_HPR_REPLAY_VALIDATION.md`.
- Updated user guide, Simagic P-HPR user guide, safety plan, Phase 2 research notes, README, architecture, roadmap, and known issues for Phase 3F.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.Runtime.Tests\HapticDrive.Asio.Runtime.Tests.csproj --no-build` passed with 22 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.App.Tests\HapticDrive.Asio.App.Tests.csproj --no-build` passed with 12 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 457 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Phase 3F validates integrated replay routing only; it does not validate live F1 25 telemetry, live GT Neo paddle input, real P-HPR hardware, physical pedal mapping, physical safety, or physical latency.
- Automated replay tests use mock output only and do not open real hardware or enable real direct-control writes.
- Replay-driven validation does not synthesize gear-paddle events unless a future explicit synthetic-input test is added.
- The F1 25 parser source-of-truth boundary was preserved; no packet layouts, offsets, lengths, enum values, or parser fields were guessed or expanded beyond the existing tested packet helpers.
- Direct-control enablement, arming, selected device path, emergency-stop latch, command history, write history, and private validation results remain runtime-only and are not persisted.
- No physical P-HPR road feel, slip feel, lock feel, safety, pedal mapping, stop behavior, safe gain, physical latency, or sustained-vibration behavior claim is made.
- No raw captures, serial numbers, private device paths, unsanitized inventories, generated local analysis exports, or private manual validation results were committed.
- The ASIO/BST-1 audio path was not changed.
- Phase 3G Live F1 P-HPR Validation Workflow is next; Phase 3F stops here.

## Phase 3G - Live F1 P-HPR Validation Workflow

Date: 2026-06-08

Status: Complete.

Goal: Create the manual live F1 25 validation workflow for P-HPR routing without claiming live or physical validation before Ethan performs a supervised local run.

Notes:

- Added `PhprLiveF1ValidationGuide` to build a passive twelve-step live F1 validation checklist from existing runtime snapshots.
- Added a Devices-page `P-HPR Live F1 Validation` section showing live telemetry, `DrivingArmed`, paddle listener, shift intent, P-HPR output mode, selected-output readiness, SimPro/SimHub coexistence, emergency stop, road vibration, and slip/lock status.
- Added a copied Diagnostics report line for the same live F1 validation workflow while excluding raw private HID paths, serial numbers, captures, and private validation data.
- Covered the required manual sequence: app open with direct control disabled, live telemetry, `DrivingArmed`, accepted paddle press, mock gear-pulse diagnostics, manual real arming, brake/throttle gear pulse, road vibration, slip/lock if safe, menu/tabbing suppression, emergency stop, and SimPro/SimHub conflict warnings.
- Added `docs/SIMAGIC_P_HPR_LIVE_F1_VALIDATION.md`.
- Updated user guide, Simagic P-HPR user guide, safety plan, Phase 2 research notes, README, architecture, roadmap, and known issues for Phase 3G.
- Added app-level tests for checklist coverage, ready mock-validation gates, real-mode manual/session-scoped status, physical-validation pending language, and private-data exclusion.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.App.Tests\HapticDrive.Asio.App.Tests.csproj --no-restore` passed with 15 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 460 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Phase 3G adds checklist and diagnostics workflow only; it does not add a new output route, HID write path, USB protocol path, synthetic paddle source, telemetry parser field, ASIO/BST-1 route, or live F1 execution.
- Automated tests use app models only and do not open hardware, send HID reports, send feature reports, control SimPro/SimHub, or vibrate P-HPR modules.
- Real direct-control enablement, arming, selected private device path, emergency-stop latch, command history, write history, and private validation results remain runtime-only or private local data.
- No physical P-HPR safety, pedal mapping, stop behavior, safe gain, physical latency, sustained-vibration behavior, road feel, slip feel, lock feel, SimPro/SimHub real-device coexistence, or live F1 behavior claim is made.
- No raw captures, serial numbers, private device paths, unsanitized inventories, generated local analysis exports, or private manual validation results were committed.
- The ASIO/BST-1 audio path was not changed.
- Phase 3H Final P-HPR Acceptance Package is next; Phase 3G stops here.

## Phase 3H - Final P-HPR Acceptance Package

Date: 2026-06-08

Status: Complete.

Goal: Package the final P-HPR implementation status, safety review, user guide, quick start, troubleshooting guide, acceptance checklist, and run commands without claiming physical validation.

Notes:

- Added `docs/QUICK_START.md` with run command, telemetry confirmation, paddle mapping, mock P-HPR, real direct mode, effect settings, replay validation, live validation, and final doc pointers.
- Added `docs/TROUBLESHOOTING.md` covering no vibration, wrong pedal, menu suppression, SimPro/SimHub conflicts, device/interface selection, telemetry, replay gear-pulse expectations, and emergency-stop latch behavior.
- Added `docs/FINAL_P_HPR_ACCEPTANCE.md` with final feature status, safety status, manual acceptance checklist, verification expectations, physical-validation status, run command, and doc links.
- Expanded `docs/USER_GUIDE.md` with wheel/paddle input, brake/throttle gear-pulse configuration, strength/frequency/duration guidance, troubleshooting summary, and final reference docs.
- Updated README, architecture, roadmap, known issues, Simagic safety plan, Simagic Phase 2 research notes, and Simagic P-HPR user guide for Phase 3H.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.App.Tests\HapticDrive.Asio.App.Tests.csproj --no-build` passed with 15 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 460 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- mock-protocol-examples` passed and printed 10 mock examples.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- safety-examples` passed and printed 6 safety examples.

Self-review:

- Phase 3H is documentation and acceptance packaging only; it does not add runtime output routes, HID write paths, USB protocol paths, parser fields, synthetic input sources, ASIO/BST-1 changes, or new automated hardware behavior.
- Real direct-control enablement, arming, selected private device path, emergency-stop latch, command history, write history, and private validation results remain runtime-only or private local data.
- Automated verification did not open hardware, send HID reports, send feature reports, control SimPro/SimHub, run F1 25, or vibrate P-HPR modules.
- No physical P-HPR safety, pedal mapping, stop behavior, safe gain, physical latency, sustained-vibration behavior, road feel, slip feel, lock feel, SimPro/SimHub real-device coexistence, live F1 behavior, or UI usability claim is made.
- No raw captures, serial numbers, private device paths, unsanitized inventories, generated local analysis exports, or private manual validation results were committed.
- The ASIO/BST-1 audio path was not changed.
- Phase 3H completes the final stage in the pasted master prompt.

## Phase 3I - P-HPR UI Simplification And Routing Cleanup

Date: 2026-06-08

Status: Complete.

Goal: Simplify P-HPR from a research/debug harness into normal app controls while preserving advanced diagnostics, mock safety coverage, direct-control gates, and hardware-absent development.

Notes:

- Reworked the Devices page around normal user-facing cards for Bass Shaker / ASIO, Simagic P-HPR Pedals, and Simagic Wheel / Shift Paddles.
- Moved raw P-HPR real direct control internals, controlled validation harness details, input-discovery internals, shift-intent diagnostics, and mock routing internals behind a persisted Advanced / Diagnostics gate that defaults off.
- Added normal P-HPR pedal controls using user-facing percentages, a 1-50 Hz frequency range, and safe 10%, 50 Hz, 50 ms default test pulses.
- Kept mock brake/throttle test pulses available in Mock mode, blocked pulses in Disabled mode, and left Direct mode behind the existing manual enablement, arming, device/interface/report, coexistence, emergency-stop, and module-ready gates.
- Preserved emergency-stop behavior across mock gear routing, mock pedal effects routing, and real direct output.
- Added `PhprUiValueConverter` and persistence coverage so P-HPR settings remain separate from the ASIO/BST-1 audio output settings.
- Updated safety/default normalization and documentation for the new 0-100%, 1-50 Hz, and 10-1000 ms user-facing model.
- Added and updated tests for percent conversion, frequency/duration caps, default real gear pulses, advanced diagnostics persistence, P-HPR settings persistence, mock/direct safety normalization, and hardware-absent behavior.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 449 passing tests and 3 skipped manual hardware tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Phase 3I changes UI, settings, safe mock/direct routing gates, tests, and docs only; it does not add a new real hardware write approval path or bypass any existing manual direct-control gate.
- Direct P-HPR writes remain unavailable unless the existing runtime readiness checks pass, and no controlled real P-HPR write testing is claimed.
- Automated tests do not require ASIO hardware, shaker hardware, F1 25, Simagic P-HPR modules, or wheel hardware.
- No physical shaker feel, physical P-HPR feel, safe gain, latency, sustained-vibration behavior, slip feel, lock feel, live F1 behavior, or real SimPro/SimHub coexistence claim is made.
- No raw captures, serial numbers, private device paths, unsanitized inventories, generated local analysis exports, or private manual validation results were committed.
- The F1 25 telemetry parser and ASIO/BST-1 audio path were not changed.
- Phase 3I completes the pasted P-HPR UI simplification prompt.

## Phase 3J - Final Controlled P-HPR Hardware Readiness And Zero-Skip Tests

Date: 2026-06-08

Status: Complete.

Goal: Convert the remaining manual skipped-test reporting into hardware-safe readiness checks, add a final controlled P-HPR smoke-test command after Ethan supplied the exact approval phrase, and preserve private hardware-data boundaries before Ethan's local physical validation.

Missing Items Addressed:

- Added an explicit `controlled-write-test` CLI path for final P-HPR brake/throttle smoke testing.
- Kept the CLI dry-run by default and required `--execute`, selected private HID path, clear SimPro/SimHub coexistence, and exact approval phrase before real writes.
- Converted the three previously skipped ASIO/BST-1 manual tests into zero-skip readiness/pending tests.
- Added fake-writer controlled-write coverage for brake, throttle, and emergency-stop reports without opening real hardware.
- Updated manual hardware, P-HPR validation, acceptance, safety, quick-start, roadmap, known-issues, README, and architecture docs for Phase 3J.

Notes:

- Ethan supplied the exact approval phrase `I approve Phase 2 controlled P-HPR write testing`, so controlled P-HPR write testing is now permitted only through explicit manual gates.
- The read-only local inventory command observed 168 inventory items, 0 specific Simagic candidates, 0 P700 candidates, and 0 P-HPR/module-controller candidates.
- Because no Simagic-specific P700/P-HPR candidate or selected private HID path was available from read-only tooling, no real P-HPR hardware pulse was executed by Codex in this stage.
- `controlled-write-test` hides the private HID path in console output and does not export validation artifacts.
- Zero skipped tests do not mean physical validation has passed; they mean normal automated tests now report readiness/pending states instead of xUnit skips.

Verification:

- Read-only `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj --no-build -- inventory --console-only` passed and observed no Simagic-specific P700/P-HPR candidates.
- Dry-run `controlled-write-test` passed with approval phrase recognized, coexistence `Clear`, sequence plan brake/throttle at 10%, 50 Hz, 50 ms, no HID writer opened, and no private path echoed.
- `rg -n "Skip\s*=" tests` found no xUnit skip markers.
- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 480 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.

Self-review:

- Phase 3J adds a controlled CLI route and readiness tests only; it does not add unattended hardware vibration, startup writes, loops, persisted direct arming, or physical validation claims.
- Automated tests use fake writers, fake ASIO backends/catalogs, readiness flags, and Null output; they do not vibrate P-HPR modules, ASIO hardware, or the Dayton BST-1 path.
- Real P-HPR execution remains local/manual and requires `controlled-write-test --execute` or explicitly armed app Direct mode with selected private HID path and clear coexistence.
- No raw captures, serial numbers, private HID paths, unsanitized inventories, generated local analysis exports, command-history private data, or private manual validation results were committed.
- Physical P-HPR pedal mapping, stop behavior, emergency-stop effectiveness, safe gain, sustained vibration, road/slip/lock feel, physical latency, and real SimPro/SimHub coexistence remain pending Ethan's local run.
- The F1 25 telemetry parser and ASIO/BST-1 audio path were not changed.
- Phase 3J is the final readiness stage before Ethan's local physical validation attempt.

## Phase 3J Follow-up - Direct Output Candidate Picker And Dry Run Gates

Date: 2026-06-09

Status: Complete.

Goal: Unblock real P-HPR validation selection by treating observed `VID_3670` HID devices as Simagic-family candidates, adding a local-only direct-output picker, and keeping real writes blocked unless every explicit gate is satisfied.

Missing Items Addressed:

- Updated inventory classification so `VID_3670` candidates, including observed `PID_0500`, `PID_0905`, `PID_B500`, and `PID_B905`, are specific Simagic-family candidates instead of generic-only HID/USB entries.
- Added a local direct-output candidate model and Raw Input candidate provider that keeps private HID paths in memory while exposing only safe labels: VID/PID, display name, class, interface, collection, report lengths when available, and confidence.
- Replaced the Advanced / Diagnostics raw path textbox with a refreshable direct-output candidate picker, safe candidate status, and a dry-run gate button.
- Added a `direct-output-dry-run` CLI command that enumerates safe local labels and validates selected candidate/report/gates without opening the HID writer.
- Added session-only approval confirmation to `PHprRealOutputOptions` and blocked real starts/opens unless direct control is enabled, armed, approval-confirmed, selected, coexistence-clear, and emergency-stop-clear.
- Kept direct output enable, arm, approval, and selected private path runtime-only and unpersisted.

Notes:

- The picker does not print or export the private HID path. Selection applies the private path internally to the runtime `PHprHidDeviceSelector`.
- Candidate confidence prefers Simagic-family VID/PID candidates and known report-length matches, but it does not claim a P-HPR role from VID/PID alone.
- `controlled-write-test` remains the only CLI route that can execute real writes, and still requires `--execute` plus the exact approval phrase.
- No real P-HPR hardware write, output report, feature report, vibration command, or physical validation was executed by Codex in this follow-up.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build --verbosity minimal` passed with 490 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- Dry-run `controlled-write-test` with the approval phrase passed without opening the HID writer or sending a report; it correctly reported the selected-path blocker because no private path was supplied.
- Dry-run `direct-output-dry-run --enable --arm --approval ...` found 11 safe-labeled HID candidates, including `VID_3670/PID_0500` and `VID_3670/PID_0905`, without printing private HID paths.
- Dry-run `direct-output-dry-run --candidate-index 0 --enable --arm --approval ...` validated selected candidate `[0]`, report length 64 bytes, coexistence `Clear`, emergency stop clear, and `can pulse True` without opening the HID writer.

Self-review:

- The private HID path remains absent from copied diagnostics, docs, tests, sanitized exports, and console output.
- Dry-run validation reports selected candidate, report length, coexistence, emergency-stop, approval, enable, arm, and can-pulse status without constructing or opening a writer.
- The F1 25 telemetry parser and ASIO/BST-1 audio path were not changed.

## Phase 3J Follow-up - HID Device Interface Open-Check Gates

Date: 2026-06-09

Status: Complete.

Goal: Fix the remaining direct P-HPR validation blocker by separating Raw Input metadata from openable HID device-interface candidates, preserving private HID paths without corruption, and requiring a successful no-report open-check before any real direct pulse can pass gates.

Missing Items Addressed:

- Added Windows HID device-interface discovery alongside Raw Input metadata discovery, with openable HID-interface candidates preferred ahead of Raw Input-only candidates.
- Added candidate source method, Raw Input-only status, openable HID path status, open-check attempted/succeeded/failed fields, and sanitized open-error categories to direct-output options, diagnostics, copied diagnostics, and CLI output.
- Blocked Raw Input metadata-only candidates from selector creation, dry-run `can pulse`, router paths, real output open, and direct pulse readiness.
- Added a no-report `direct-output-open-check` command and app Open Check button that opens and immediately closes the selected HID writer without sending an output report.
- Required successful open-check, in addition to selected/enabled/armed/approval/coexistence/emergency-stop gates, before real direct pulses can become eligible.
- Added path safety checks so corrupted or relative-looking HID paths are rejected before `FileStream` open and reported through sanitized error categories.
- Added tests for VID_3670 family classification, Raw Input-only gate blocking, private path preservation/redaction, corrupted path rejection, open-check no-write behavior, and controlled-write open-check failure blocking.
- Hardened the runtime stale-telemetry mute test timeout so full-solution parallel test runs do not race the output-owned render loop before its first callback.

Notes:

- Raw Input metadata can still identify `VID_3670` Simagic-family hardware, but it is not treated as an openable HID output path.
- The private HID path remains runtime-only in the selected candidate/selector and is not printed in safe labels, copied diagnostics, CLI output, docs, or sanitized exports.
- Protocol bytes were not changed.
- No P-HPR output report, feature report, vibration command, sustained write loop, or physical validation was executed by Codex in this follow-up.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build --verbosity minimal` passed with 498 passing tests and 0 skipped tests after the runtime timing test hardening.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- Read-only `inventory --console-only` passed and observed 170 inventory items, 12 specific Simagic-family candidates, 158 generic HID/USB candidates, and 0 P-HPR/module-controller candidates.
- `direct-output-dry-run --candidate-index 0 --enable --arm --approval ...` selected a `VID_3670/PID_0500` HID device-interface candidate, reported report length 64 bytes, and correctly kept `can pulse False` because open-check had not passed; no HID writer was opened.
- `direct-output-open-check --candidate-index 0 --enable --arm --approval ...` opened and closed the selected HID writer without sending any output report, then reported open-check succeeded and dry-run `can pulse True` with 0 issues.

Self-review:

- Real direct P-HPR writes remain blocked unless selected output, openable HID device-interface candidate, successful open-check, direct enabled, direct armed, exact approval phrase, coexistence `Clear`, emergency stop clear, and local manual action are all present.
- Automated tests use fake writers and do not open real hardware or send HID reports.
- The F1 25 telemetry parser, ASIO/BST-1 audio path, and protocol bytes were not changed.

## Phase 3J Follow-up - HID Report Capability and Shape Gate

Date: 2026-06-10

Status: Complete.

Goal: Fix the direct P-HPR validation blocker where a selected `VID_3670` HID path could pass open-check but reject the actual output report write with `IOException:0x80070057`.

Missing Items Addressed:

- Added read-only HID capability discovery for `VID_3670` HID device-interface candidates, surfacing usage page/usage, input/output/feature report byte lengths, and report IDs when Windows exposes them.
- Added candidate output/feature capability flags and report-ID-safe labels while continuing to redact private HID paths.
- Added no-command report-shape validation based on HID output-report capability metadata.
- Updated dry-run, app diagnostics, normal P-HPR controls, manual validation readiness, direct gear-pulse routing, road/slip/lock routing, and the final real-output command gate so `can pulse True` requires selected/openable/open-check/approval/coexistence/emergency-stop plus known output-report capability or successful report-shape validation.
- Kept the confirmed SimHub F1 EC protocol bytes unchanged.
- Reclassified HID write `IOException:0x80070057` as an invalid report-shape/write-format failure instead of a plain disconnect.
- Preferred output/feature-capable HID device-interface candidates ahead of input-only/game-controller-style candidates.
- Kept real pulses blocked when output-report length is unavailable.

Notes:

- Open-check still sends no report and proves only that the selected path can be opened and closed.
- The direct-output dry run now prints a separate `VID_3670` candidate section with safe labels only.
- No real P-HPR hardware write, output report, feature report, vibration command, or physical validation was executed by Codex in this follow-up.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build` passed with 118 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.App.Tests\HapticDrive.Asio.App.Tests.csproj --no-build` passed with 28 passing tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --no-restore` completed.
- Rebuilt after formatting with `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore`; passed with 0 warnings and 0 errors.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build --verbosity minimal` passed with 504 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- Read-only `inventory --console-only` passed and observed 168 inventory items, 10 specific Simagic-family candidates, 158 generic HID/USB candidates, and 0 P-HPR/module-controller candidates.
- `direct-output-dry-run --enable --arm --approval ...` passed without opening the HID writer, listed 27 safe-labeled candidates, surfaced the `VID_3670/PID_0905` HID device-interface candidate separately, and reported `can pulse False` with no selected candidate.
- `direct-output-dry-run --candidate-index 0 --enable --arm --approval ...` selected the `VID_3670/PID_0905` HID device-interface candidate and reported input 64 bytes, output unavailable, feature 64 bytes, input report IDs `0x01,0x02`, feature IDs `0x80,0xF1`, report-shape validation failed, and `can pulse False`; the same dry-run surfaced a separate sanitized `VID_3670` inventory section including `PID_0500`, `PID_0905`, `PID_B500`, and `PID_B905`.
- `direct-output-open-check --candidate-index 0 --enable --arm --approval ...` opened and closed the selected HID writer without sending any output report, reported open-check succeeded, and still kept dry-run `can pulse False` because output-report length was unavailable.

Self-review:

- The F1 EC report bytes were not modified.
- Private HID paths remain held in memory only and are absent from safe labels, CLI output, copied diagnostics, and docs.
- The ASIO/BST-1 audio path was not changed.

## Phase 3J Follow-up - Feature Report Transport and VID_3670 Surfacing

Date: 2026-06-10

Status: Complete.

Goal: Fix the remaining direct P-HPR validation blocker by supporting explicit HID FeatureReport shape validation, surfacing all locally known `VID_3670` family candidates in the picker, and keeping real writes blocked until the selected transport/report shape is valid.

Missing Items Addressed:

- Added explicit HID report transport selection: `OutputReport` vs `FeatureReport`.
- Added FeatureReport shape validation using read-only HID capability metadata, including selected feature report byte length, selected report ID, and expected F1 EC first bytes.
- Surfaced feature report ID `0xF1` for compatible `VID_3670/PID_0905` candidates and marked it as likely matching the known F1 EC command-family prefix.
- Added a gated FeatureReport writer path using `HidD_SetFeature`; it is only reachable through the existing direct-control gates and was not executed by Codex.
- Added safe HID registry metadata candidates to the direct picker so observed `VID_3670` family PIDs such as `PID_0500`, `PID_0905`, `PID_B500`, and `PID_B905` are not hidden just because they are not openable HID device-interface paths.
- Updated dry-run, open-check, app diagnostics, copied diagnostics, manual validation export text, and router/runtime gates to report selected transport, selected report ID, report byte length, feature/output capability, expected first bytes, and report-shape validation.
- Tightened `can pulse` so it requires selected/openable/non-Raw-Input candidate, successful open-check, matching output/feature capability for the selected transport, successful no-command report-shape validation, approval, coexistence `Clear`, and clear emergency stop.
- Kept the confirmed SimHub F1 EC report bytes unchanged.

Notes:

- `direct-output-dry-run` and app Dry Run Gates do not open the HID writer and do not send output or feature reports.
- `direct-output-open-check` opens and closes the selected HID writer path only; it sends no output report and no feature report.
- Raw Input-only and HID registry metadata-only candidates remain blocked from real direct-output gates because they do not expose an openable HID device-interface path.
- No real P-HPR hardware write, output report, feature report, vibration command, or physical validation was executed by Codex in this follow-up.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build --verbosity minimal` passed with 124 passing tests.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build --verbosity minimal` passed with 510 passing tests and 0 skipped tests after rerunning a transient audio cadence test that passed on project rerun.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --no-restore` completed.
- Rebuilt after formatting with `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore`; passed with 0 warnings and 0 errors.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build --verbosity minimal` passed with 510 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- Read-only `inventory --console-only` passed and observed 168 inventory items, 10 specific Simagic-family candidates, 158 generic HID/USB candidates, and local `VID_3670` entries for `PID_0500`, `PID_0905`, `PID_B500`, and `PID_B905`.
- `direct-output-dry-run --enable --arm --approval ...` passed without opening the HID writer, listed 33 safe-labeled candidates, and surfaced six direct `VID_3670` candidates: one openable `PID_0905` HID device-interface candidate, metadata rows for `PID_0500`, `PID_0905`, `PID_B500`, `PID_B905`, and one Raw Input metadata row.
- `direct-output-dry-run --candidate-index 0 --transport feature --enable --arm --approval ...` selected the openable `VID_3670/PID_0905` HID device-interface candidate, reported `FeatureReport`, report ID `0xF1`, 64-byte feature report capability, expected first bytes `F1 EC 01 01 32 0A 00`, successful no-command report-shape validation, and `can pulse False` because open-check had not yet passed. No HID writer was opened.
- `direct-output-open-check --candidate-index 0 --transport feature --enable --arm --approval ...` opened and closed the selected HID writer without sending any output report or feature report, reported open-check succeeded, report-shape validation succeeded, and `can pulse True` only after all gates were satisfied.

Self-review:

- The F1 EC report bytes were not modified.
- Private HID paths remain held in memory only and are absent from safe labels, CLI output, copied diagnostics, docs, and tests.
- The ASIO/BST-1 audio path was not changed.

## Stage 18 Follow-up - Manual ASIO and Paddle Bench Validation

Date: 2026-06-10

Status: Complete.

Goal: Add two focused manual validation surfaces without merging hardware paths: a controlled Paddle Gear Bench Test for mapped GT Neo paddles without live F1 telemetry, and a Manual ASIO Bass Shaker Test for short 40/50 Hz pulses through the selected real ASIO output.

Missing Items Addressed:

- Added a runtime-only `PaddleGearBenchTestController` that requires local enable and arm, accepts mapped paddle input without recent telemetry, records accepted/suppressed diagnostics, and leaves normal `ShiftIntentProcessor` / `DrivingArmed` behavior unchanged.
- Added mock paddle-bench routing through `PHprGearPulseRouter` with per-call bench options so mock gear routing can be validated without HID output or ASIO output.
- Added strict Paddle Gear Bench Direct gating requiring selected/openable HID device-interface output, FeatureReport transport, report ID `0xF1`, 64-byte report length, successful open-check, report-shape/capability acceptance, approval, coexistence `Clear`, emergency stop clear, and disabled road/slip/lock routes.
- Added Devices-page Paddle Gear Bench controls for enable, arm, output mode, target, strength, frequency, duration, counters, status, and diagnostics.
- Added a manual ASIO hardware test request/snapshot/result model and runtime injection path that creates short 40/50 Hz sine pulses as mixer inputs, then uses the existing Stage 10 mixer, safety chain, limiter, and selected ASIO output channel.
- Added Devices-page Manual ASIO Bass Shaker Test controls for 40 Hz, 50 Hz, 250/500 ms duration, channel 0, channel 1, mono/both diagnostic status, peak, and blocked reason.
- Kept the existing Null synthetic benchmark unchanged for deterministic automated tests.
- Updated README, architecture, roadmap, known issues, hardware-absent, ASIO, manual hardware, user guide, quick start, persistence, and P-HPR validation docs for the new runtime-only validation surfaces and current local hardware status.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Full `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build --verbosity minimal` passed with 529 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- The F1 25 parser, UDP forwarding, recording/replay raw-packet preservation, confirmed paddle mappings, and confirmed P-HPR protocol bytes were not changed.
- Simagic P-HPR remains a separate USB/HID FeatureReport actuator path and is not routed through ASIO or `IAudioOutputDevice`.
- ASIO/BST-1 remains an audio output path and is not routed through P-HPR.
- Null output remains default. Manual ASIO hardware tests require explicit ASIO selection, M-Audio / M-Track driver selection, arming, haptics running, clear mutes, and valid channel selection.
- Paddle bench enable/arm state, manual ASIO active state, ASIO armed state, direct-control armed state, emergency-stop state, selected private HID path, command history, and write history are not persisted.
- Automated tests use Null output, fake ASIO backends, mock P-HPR output, and fake HID writer/gate models only. No real ASIO hardware, M-Audio, Fosi, BST-1, Simagic hardware, SimPro, SimHub, F1 25, live telemetry, HID output report, HID feature report, or vibration command was required by automated verification.
- Current local status is documented as user-validated brake/throttle direct P-HPR pulses and SimHub-proven BST-1 chain, while Haptic Drive ASIO app-driven BST-1 validation, physical safe gain, physical latency, sustained-output behavior, road/slip/lock feel, and final tuning remain manual local work.
