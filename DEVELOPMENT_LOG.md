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
