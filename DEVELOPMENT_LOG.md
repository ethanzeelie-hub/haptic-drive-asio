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

## Stage 18b - Simplified P-HPR Direct Bench Startup and Stop Scheduling

Date: 2026-06-10

Status: Complete.

Goal: Fix Paddle Gear Bench Direct mode so P-HPR pulses stop after the configured duration, and simplify the local P-HPR bench workflow around automatic direct readiness without adding ASIO/BST-1 work.

Missing Items Addressed:

- Added deterministic direct stop scheduling through an injectable stop clock so Direct mode sends matching SimHub/F1 EC stop reports after `DurationMs`.
- Covered brake-only, throttle-only, both-target, emergency-stop cancellation, dispose-stop, and no-active-pulse-after-duration behavior with fake-writer tests.
- Kept the confirmed F1 EC start/stop bytes unchanged, including stop reports shaped as `F1 EC 01/02 00 0A 00 ...`.
- Added startup auto-refresh for input and P-HPR direct candidates, including automatic selection of the known `VID_3670/PID_0905` HID device-interface candidate by FeatureReport `0xF1` / 64-byte capability instead of candidate index.
- Ran startup open-check and dry-run readiness work in the background without sending output reports, feature reports, or vibration commands.
- Simplified normal P-HPR Direct mode by hiding the separate arm/approval workflow from the normal bench path while retaining selected-device, open-check, report-shape, coexistence, emergency-stop, and road/slip/lock gates.
- Made Paddle Gear Bench runtime options Direct-mode, enabled, and auto-armed by default; defaulted GT Neo paddle mapping to left `14` and right `13`.
- Removed duplicate normal bench strength/frequency/duration inputs from the workflow; bench direct pulses now use the normal Devices brake/throttle P-HPR gear-pulse values as the single source of truth.
- Updated diagnostics and status text so emergency stop, pending scheduled stops, direct readiness, selected output, and Devices-sourced brake/throttle settings are visible.

Notes:

- Hardware never vibrates on startup; automatic startup work is candidate selection, no-report open-check, and dry-run readiness only.
- Direct bench output is still blocked unless FeatureReport `0xF1`, 64-byte report shape, successful open-check, clear coexistence, clear emergency stop, and disabled road/slip/lock routes are all present.
- No ASIO/BST-1 audio path changes were made.
- No physical P-HPR safe gain, sustained-vibration behavior, emergency-stop physical behavior, road/slip/lock feel, or physical latency claim is made by this stage.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln` passed.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --no-restore` completed before final build/test verification.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build --verbosity minimal` passed with 129 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.Audio.Tests\HapticDrive.Asio.Audio.Tests.csproj --no-build --verbosity minimal` passed with 102 passing tests after a parallel full-solution fan-out hit a transient audio callback cadence timing failure.
- Full sequential `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build --verbosity minimal -m:1` passed across all projects: Core 12, Telemetry 47, Audio 102, Recording 16, Runtime 29, Input 26, PHPR 129, Actuation 83, Research 57, and App 37.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.ps1 -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- The F1 25 parser, UDP forwarding, recording/replay raw-packet preservation, ASIO/BST-1 audio output path, and confirmed P-HPR protocol bytes were not changed.
- Automated tests use fake writers, fake timing, fake ASIO backends, and Null output. No real ASIO hardware, M-Audio, Fosi, BST-1, Simagic hardware, SimPro, SimHub, F1 25, live telemetry, HID output report, HID feature report, or vibration command was required by automated verification.

## Stage 18c - Paddle Device Auto-selection and Direct Bench Stop Hardening

Date: 2026-06-10

Status: Complete.

Goal: Fix the remaining Paddle Gear Bench follow-up blockers without adding ASIO or bass-shaker work: select the usable GT Neo / wheelbase input device automatically, block 0-button listener starts, and make Direct Paddle Gear Bench stop behavior and diagnostics reuse the shared direct P-HPR output path.

Missing Items Addressed:

- Added metadata-based paddle input selection that prefers a saved usable controller, then the 32-button `VID_3670/PID_0905` Windows game-controller, then other usable button-capable controllers, and never auto-selects a 0-button controller when a usable controller exists.
- Treated generic Windows `Microsoft PC-joystick driver` entries with `VID_3670/PID_0905` as Simagic GT Neo / wheel input candidates even when the friendly name is generic.
- Blocked Windows game-controller listener start when the selected device explicitly reports 0 usable buttons, and surfaced a clear Devices-page blocked state instead of silently polling the wrong controller.
- Kept Paddle Gear Bench Direct routing tied to the same visible mapped paddle event path that updates listener diagnostics, including listener device ID, mapped side/button, event sequence number, and accepted/rejected reason.
- Extended the shared `SimagicPhprOutputDevice` diagnostics with active-pulse state, pending stop count, last start/stop sent timestamps, last start/stop targets, stop success/failure status, stop message, and scheduled duration.
- Hardened direct stop scheduling so emergency stop cancels pending stops without racing token-source disposal, app dispose clears active pulse state, and scheduled stop failures are visible diagnostics instead of silent background failures.
- Added direct bench fake-writer/fake-clock tests for brake, throttle, both targets, Devices-card brake/throttle settings, no startup output, emergency stop, and dispose-stop behavior.

Notes:

- Direct Paddle Gear Bench still requires P-HPR direct ready, FeatureReport `0xF1`, 64-byte report shape, successful open-check, clear coexistence, clear emergency stop, disabled road/slip/lock routes, a running usable listener, and a mapped paddle event from the visible listener path.
- Hardware still never vibrates on startup; startup work remains discovery, candidate selection, open-check, and dry-run readiness only.
- No physical stop feel, sustained vibration safety, safe gain, physical latency, or road/slip/lock feel is claimed by this stage.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused tests passed: Input 29, Actuation 86, Simagic P-HPR 130, and App 45.
- Full sequential `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build --verbosity minimal -m:1` passed with 553 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --no-restore` completed.
- Rebuilt after formatting with `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore`; passed with 0 warnings and 0 errors.
- Full sequential `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build --verbosity minimal -m:1` passed again with 553 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- The ASIO/BST-1 audio path, F1 25 parser, UDP forwarding, recording/replay raw-packet preservation, and confirmed P-HPR protocol bytes were not changed.
- Direct P-HPR remains a separate HID FeatureReport actuator path and is not routed through `IAudioOutputDevice`.
- Automated coverage uses fake HID writers, fake stop clocks, read-only/fake input paths, fake ASIO backends, and Null output. No real ASIO hardware, M-Audio, Fosi, BST-1, Simagic hardware, SimPro, SimHub, F1 25, live telemetry, HID output report, HID feature report, or vibration command was required by automated verification.

## Stage 18d - Direct Paddle Bench Runaway and Emergency Stop Hotfix

Date: 2026-06-10

Status: Complete.

Goal: Hotfix the Direct Paddle Gear Bench runaway-output report without adding ASIO or bass-shaker work by making direct bench output reuse the proven Devices-tab direct pulse path and hardening stop-all behavior around duration stops, watchdogs, emergency stop, and crash diagnostics.

Missing Items Addressed:

- Removed the bench-only direct pulse planner and added a shared `PhprDeviceCardPulseService` used by both the Devices-tab blue Test Brake/Throttle direct pulse buttons and Direct Paddle Gear Bench routing.
- Direct Paddle Gear Bench now uses Devices-tab brake/throttle card settings only, defaults target to Both, and maps both left and right paddles to the selected output target by design.
- Added explicit bench rejection for non-Pressed paddle events and direct-output suppression while a previous direct pulse is active or has a pending scheduled stop.
- Added real-output diagnostics for scheduled stop due time, emergency stop request/result, and watchdog stop-all result.
- Hardened `SimagicPhprOutputDevice` so Emergency Stop cancels pending stops, attempts brake and throttle stop reports independently, retries stop-all writes, and clears active pulse state only after a successful stop-all.
- Added a `DurationMs + 100 ms` watchdog for timed direct pulses; if the target module remains active after the grace window, the output device forces stop-all and latches emergency stop.
- Ensured dispose/shutdown requests stop-all when any direct pulse may still be active.
- Added sanitized local crash-state logging for unhandled app/task failures under local app data without writing private HID paths.
- Added/updated tests for release suppression, default Both target, shared Devices-tab pulse service routing, Devices-card values, brake/throttle/both start-stop behavior, emergency stop retry, watchdog stop-all, and no startup output.

Notes:

- This stage does not claim the physical runaway behavior is fixed until Ethan revalidates on the real P-HPR hardware chain.
- Direct bench output remains blocked unless the direct P-HPR gates pass: selected/openable HID device-interface, FeatureReport `0xF1`, 64-byte report shape, successful open-check, clear coexistence, clear emergency stop, disabled road/slip/lock routes, running usable listener, and mapped Pressed paddle event.
- No ASIO/BST-1 audio path files were changed.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --no-restore` completed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused tests passed: Input 29, Actuation 88, Simagic P-HPR 132, and App 45.
- First full sequential `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build --verbosity minimal -m:1` hit the unchanged audio callback cadence timing flake in `HapticDrive.Asio.Audio.Tests.OutputStreamingTests.NullOutput_OutputOwnedStreamingReportsCallbackCadence`.
- Focused audio rerun `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.Audio.Tests\HapticDrive.Asio.Audio.Tests.csproj --no-build --verbosity minimal -m:1` passed with 102 passing tests.
- Full sequential `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build --verbosity minimal -m:1` passed with 557 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- The ASIO/BST-1 audio path, F1 25 parser, UDP forwarding, recording/replay raw-packet preservation, and confirmed P-HPR report bytes were not changed.
- Direct P-HPR remains a separate HID FeatureReport actuator path and is not routed through `IAudioOutputDevice`.
- Automated coverage uses fake HID writers, fake stop clocks, fake/read-only input paths, fake ASIO backends, and Null output. No real ASIO hardware, M-Audio, Fosi, BST-1, Simagic hardware, SimPro, SimHub, F1 25, live telemetry, HID output report, HID feature report, or vibration command was required by automated verification.

## Stage 18e - P-HPR Direct Runtime Bench Crash Recovery

Date: 2026-06-11

Status: Complete.

Goal: Finish the Direct Paddle Gear Bench crash/runaway repair by extracting the direct route into a deterministic runtime owner, adding fail-closed recovery artifacts, and making stop-only cleanup and manual recovery visible without changing ASIO/BST-1, F1 25 telemetry parsing, UDP forwarding, recording/replay, or confirmed P-HPR report bytes.

Missing Items Addressed:

- Added `PHprDirectRuntimeCoordinator` with explicit runtime states, serialized command dispatch, startup stop-only cleanup, unhandled-exception stop-all recovery, and fail-closed bench start gates.
- Added `IPHprDirectPulseService`, `IPHprDirectCommandDispatcher`, `IPHprBenchFlightRecorder`, and `IPHprBenchUncleanShutdownStore` seams so the route can be unit-tested without real hardware.
- Converted `PhprDeviceCardPulseService` into the shared direct pulse service used by both the Devices-tab blue Test Brake/Throttle buttons and Direct Paddle Gear Bench, with instance IDs exposed for diagnostics.
- Added a local immediate-flush JSONL flight recorder and unclean-shutdown marker under `local-validation-results/`; the marker blocks Direct Bench starts until stop-only recovery succeeds.
- Added `SimagicPhprOutputDevice.StopAllAsync` and serialized HID report writes so stop-all recovery cannot interleave with start writes.
- Added `P-HPR Stop All / Clear Device State`, wired P-HPR emergency stop and clear paths through the runtime, and surfaced runtime state, shared-path proof, marker, recorder, stop-all, watchdog, and software latency diagnostics in the bench UI.
- Added tests covering startup stop-only cleanup, repeatable stop-only recovery, marker create/clear behavior, unclean-startup blocking, shared-path proof blocking, and recorder redaction/error-category output.

Notes:

- Startup cleanup sends stop-only reports only when a selected output is already configured; it never sends active/start/vibration reports.
- Direct Bench still requires the visible mapped paddle listener path, FeatureReport `0xF1`, 64-byte report shape, successful open-check, clear coexistence, clear emergency stop, disabled road/slip/lock routes, positive Devices-sourced pulse settings, and a proven shared pulse service instance.
- The new recovery artifacts are local validation files and must not be committed.
- No physical P-HPR stop feel, sustained-vibration safety, safe gain, physical latency, road/slip/lock feel, or real coexistence claim is made by this stage.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.App.Tests\HapticDrive.Asio.App.Tests.csproj --no-build --verbosity minimal` passed with 51 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Simagic.PHPR.Tests\HapticDrive.Simagic.PHPR.Tests.csproj --no-build --verbosity minimal` passed with 132 passing tests.
- Full sequential `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build --verbosity minimal -m:1` passed.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- The ASIO/BST-1 audio path, F1 25 parser, UDP forwarding, recording/replay raw-packet preservation, normal telemetry `DrivingArmed` route, and confirmed P-HPR report bytes were not changed.
- Direct P-HPR remains a separate HID FeatureReport actuator path and is not routed through `IAudioOutputDevice`.
- Automated coverage uses fake HID writers, fake runtime clocks, fake/read-only input paths, fake ASIO backends, and Null output. No real ASIO hardware, M-Audio, Fosi, BST-1, Simagic hardware, SimPro, SimHub, F1 25, live telemetry, HID output report, HID feature report, or vibration command was required by automated verification.

## Stage 18f - Direct Paddle Bench UI Thread Crash Hotfix

Date: 2026-06-11

Status: Complete.

Goal: Fix the Direct Paddle Gear Bench crash caused by WPF controls being updated from the paddle input background callback after a successful P-HPR direct start write, without changing ASIO/BST-1, telemetry parsing, or confirmed P-HPR report bytes.

Missing Items Addressed:

- Added a small `MainWindowUiDispatch` helper so status refreshes can post to WPF asynchronously when called off the dispatcher instead of touching dependency properties from the input thread.
- Made `UpdateRealPhprDirectControlStatus`, `UpdatePhprValidationStatus`, and `UpdateDiagnosticsStatus` self-marshal when invoked from a non-UI callback path.
- Wrapped `PaddleInputSource_PaddleInputReceived` in defensive exception handling, awaited the final UI update, and records recoverable paddle-path failures through the Stage 18e flight recorder.
- Added async paddle exception recovery to `PHprDirectRuntimeCoordinator`; when a direct bench pulse may have started, recovery attempts stop-all and does not rethrow into WPF/AppDomain.
- Kept Direct Paddle Gear Bench runtime ownership, stop-all/marker cleanup, flight recorder behavior, shared Devices pulse service proof, writer/encoder path, VID/PID/report ID/report length assumptions, and P-HPR command format unchanged.
- Added regression tests for off-dispatcher UI posting, awaited dispatcher exception flow, direct bench route completion after a fake successful start write, and paddle exception recording with stop-all attempts.

Notes:

- This hotfix addresses a software threading crash only. It does not prove physical stop feel, configured duration on real hardware, safe gain, physical latency, or real coexistence behavior.
- Blue Devices-tab Test Brake/Throttle pulse behavior and ASIO/BST-1 paths were not changed.
- No physical P-HPR validation was performed by Codex.

Verification:

- `.\.dotnet\dotnet.exe restore` passed.
- `.\.dotnet\dotnet.exe build --no-restore` passed with 0 warnings and 0 errors.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.App.Tests\HapticDrive.Asio.App.Tests.csproj --no-restore` passed with 56 passing tests.
- Full sequential `.\.dotnet\dotnet.exe test --no-build --verbosity minimal -m:1` passed with 568 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format --verify-no-changes --no-restore` passed.

## Stage 18i - BST-1 ASIO Gear Pulse Controls

Date: 2026-06-11

Status: Complete.

Goal: Replace the fixed manual ASIO bass-shaker test with Dayton BST-1 strength/frequency/duration controls, allow short manual ASIO pulses without global Start Haptics, and optionally synchronize BST-1 ASIO gear pulses with accepted Direct Paddle Gear Bench P-HPR paddle pulses.

Missing Items Addressed:

- Replaced the primary manual ASIO workflow in Devices with `BST-1 ASIO Pulse Control`: strength percent, frequency Hz, duration ms, `Test BST-1 Pulse`, selected ASIO channel, and channel 1 selection.
- Expanded manual pulse validation to the Dayton BST-1 normal 10-80 Hz control range, 0-100% strength, and bounded 10-1000 ms duration.
- Added a bounded manual ASIO pulse session that can open/start ASIO, render safety-processed pulse buffers through the existing Stage 10 mixer/safety/limiter path, submit them to the selected ASIO channel, then stop again without setting global haptics running.
- Added an internal True ASIO status line and detailed diagnostics for output mode, selected driver/channel, ASIO armed/running/callback state, callback counts, submitted/dropped frames, last error, manual pulse peak, limiter activity, and whether the last pulse used ASIO or was blocked.
- Added an off-by-default `BST-1 Paddle Gear Pulse` subsection with strength, frequency, sync/custom duration, and selected-channel targeting.
- Routed enabled BST-1 paddle gear pulses from the same accepted Paddle Gear Bench `Pressed` event used by P-HPR, in parallel with the existing P-HPR bench route, without waiting for telemetry gear confirmation or global Start Haptics.
- Added generation bookkeeping for BST-1 manual/paddle pulses so a completed older run cannot clear a newer active pulse.
- Added `local-validation-results/bst1-asio-gear-flight-recorder.jsonl` for accepted, blocked, completed, and failed BST-1 manual/bench pulse records.
- Added fake-ASIO runtime tests for Null blocking, unarmed blocking, invalid channel blocking, no global Start Haptics requirement, mixer/safety/limiter output, requested/clamped strength/frequency/duration, channel 1 routing, and flight-recorder records.

Notes:

- Channel 1 is documented as the locally validated BST-1 output channel.
- Manual BST-1 pulse and enabled Paddle Gear Bench BST-1 pulse use ASIO only; Windows Sound Settings visibility is not proof of ASIO usage.
- Live telemetry-driven effects still require normal haptics/telemetry gates where applicable.
- BST-1 paddle gear pulse is off by default for safety and remains a short-duration bench/local validation path.
- P-HPR command bytes, HID report shape, paddle mapping, direct runtime command format, F1 25 parser, UDP forwarding, and recording/replay raw bytes were not changed.
- No automated test requires M-Audio, Fosi, Dayton BST-1, ASIO driver installation, Simagic hardware, F1 25, or live telemetry.

Verification:

- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.Runtime.Tests\HapticDrive.Asio.Runtime.Tests.csproj` passed with 31 passing tests.
- Focused `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.App.Tests\HapticDrive.Asio.App.Tests.csproj /p:BaseOutputPath=artifacts\app-test-bin\` passed with 61 passing tests.
- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- Standard app-output `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` remained blocked by a stale exited `HapticDrive.Asio.App` process, PID 10912, holding old app-output DLLs under `src\HapticDrive.Asio.App\bin\Debug\net8.0-windows`.
- Alternate-output `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore /p:BaseOutputPath=artifacts\stage18i-build-bin\` passed with 0 warnings and 0 errors.
- Full serialized alternate-output `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build -m:1 /p:BaseOutputPath=artifacts\stage18i-build-bin\` passed with 576 passing tests.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- ASIO/BST-1 manual pulse no longer requires Start Haptics, but it still requires ASIO Output, M-Audio/M-Track driver selection, ASIO arm, valid selected channel, clear emergency mute, clear normal mute, and the existing mixer/safety/limiter path.
- BST-1 paddle gear pulse uses accepted Paddle Gear Bench `Pressed` events only and remains disabled unless the user enables it.
- Live-driving telemetry effects remain gated by haptics/telemetry freshness.
- Null output remains the startup/default safe target.
- No generated local validation logs were committed.

## Stage 18g - Rapid Paddle Gear-Pulse Retriggering

Date: 2026-06-11

Status: Complete.

Goal: Make Direct Paddle Gear Bench usable for rapid spam-shift validation by allowing each accepted paddle Pressed edge to retrigger the selected P-HPR gear pulse immediately, without queued late pulses or older scheduled stops cancelling newer pulses.

Missing Items Addressed:

- Added internal `Conservative` / `RetriggerLatestPressWins` gear-pulse mode and made Direct Paddle Gear Bench use latest-press-wins behavior while preserving conservative manual blue-button pulse behavior.
- Added per-module brake/throttle pulse generation IDs in the real P-HPR output device; every scheduled stop captures the generation it belongs to and is ignored if a newer generation has started.
- Changed Direct Bench runtime preflight and start sequencing so an active bench pulse and pending stop do not reject the next accepted paddle press.
- Added stale runtime observer protection so an old bench observer cannot force Stop All while a newer pulse is active.
- Added an 80 ms stale-paddle drop threshold for Direct Bench starts so delayed paddle work is recorded and dropped instead of played late.
- Kept Emergency Stop and Stop All overriding all generations and pending stops immediately.
- Lowered the default paddle debounce to 5 ms, preserved per-button debounce behavior, and added debounce-suppressed diagnostics.
- Extended UI diagnostics and `phpr-direct-bench-flight-recorder.jsonl` records with generation IDs, retrigger counts, stale-stop ignores, stale runtime observer ignores, stale-output drops, busy rejects, debounce suppressions, inter-press interval, and paddle-to-write timing fields.
- Added tests for active-pulse retrigger acceptance, stale stop ignore, latest stop success, stale output drop, per-button debounce, independent brake/throttle generation behavior, and Stop All override.

Notes:

- Only Direct Paddle Gear Bench P-HPR behavior changed. ASIO, BST-1, bass shaker effects, road/slip/lock routing, F1 25 parser, UDP forwarding, recording/replay, SimPro/SimHub coexistence, and confirmed P-HPR report bytes were not changed.
- Older scheduled stops can no longer cancel newer brake or throttle pulses because they must match the current per-module generation before writing a stop report.
- Direct Bench still only accepts mapped `Pressed` paddle events from the visible listener path; release/held/repeat/unknown states do not trigger gear haptics.
- No physical P-HPR spam-shift validation was performed by Codex; physical latency, stop feel, safe gain, and real rapid downshift feel remain Ethan-local validation items.

Verification:

- `.\.dotnet\dotnet.exe restore` passed.
- `.\.dotnet\dotnet.exe build --no-restore` passed with 0 warnings and 0 errors.
- Full `.\.dotnet\dotnet.exe test --no-build` passed with 574 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format --verify-no-changes --no-restore` passed.

## Stage 18j - BST-1 Local Gear Test And Duration Sync

Date: 2026-06-12

Status: Complete.

Goal: Fix BST-1 manual/local gear pulse gating after Stage 18i, make ASIO status describe ready/armed versus stream-running truthfully, sync P-HPR and BST-1 gear pulse duration, and add a local gear-test workflow that does not depend on Start Haptics or live telemetry.

Missing Items Addressed:

- Kept manual `Test BST-1 Pulse` independent from Start Haptics, live/replay telemetry, UDP, and `DrivingArmed`, while preserving ASIO Output, M-Audio/M-Track driver, channel, arm, mute, emergency, strength, frequency, duration, mixer, safety, and limiter gates.
- Added a short standalone ASIO drain delay before stopping a temporary manual pulse session so native ASIO has time to consume the submitted bounded pulse.
- Replaced the confusing single True ASIO line with separate ASIO selected, driver, armed, stream-running, callback-active, last manual pulse used ASIO, last gear pulse used ASIO, channel, blocked reason, last error, and last pulse proof diagnostics.
- Added shared gear-pulse duration for brake P-HPR, throttle P-HPR, Direct Paddle Gear Bench, and BST-1 sync mode; BST-1 custom duration remains available only when sync is unchecked.
- Added Local Gear Test Mode and Start Gear Test Listener controls for mapped-paddle bench validation without Start Haptics, UDP, live F1 telemetry, replay, or `DrivingArmed`.
- Hardened output timing diagnostics so a valid zero-tick callback jitter reports as a real `TimeSpan.Zero` value instead of being mistaken for an absent status field.
- Added readiness helpers and tests for BST-1 ASIO status formatting, shared duration normalization, BST-1 sync/custom effective duration, local gear-test readiness, and manual versus gear ASIO proof.
- Updated user-facing ASIO, quick-start, troubleshooting, roadmap, and known-issues documentation.

Notes:

- Local Gear Test mode does not start continuous ASIO output or live telemetry effects. It only makes the local mapped-paddle bench workflow easier to start.
- P-HPR Direct output still requires the existing direct-control gates. BST-1 output still requires ASIO Output, selected M-Audio/M-Track driver, valid selected channel, ASIO arm, and clear mute/emergency state.
- No physical shaker feel, safe gain, physical latency, P-HPR behavior, or final tuning claim is made by this stage.
- F1 25 parser, UDP forwarding, recording/replay raw-byte preservation, confirmed P-HPR report bytes, P-HPR paddle mappings, and normal telemetry `DrivingArmed` routing are unchanged.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- Standard app-output `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` remained blocked by stale `HapticDrive.Asio.App` PID 24776 holding app-output DLLs under `src\HapticDrive.Asio.App\bin\Debug\net8.0-windows`.
- Alternate-output `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore /p:BaseOutputPath=artifacts\stage18j-build-bin\` passed with 0 warnings and 0 errors.
- Focused runtime tests passed: `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.Runtime.Tests\HapticDrive.Asio.Runtime.Tests.csproj --no-build /p:BaseOutputPath=artifacts\stage18j-build-bin\ --verbosity minimal` with 32 passing tests.
- Focused app tests passed: `.\.dotnet\dotnet.exe test tests\HapticDrive.Asio.App.Tests\HapticDrive.Asio.App.Tests.csproj --no-build /p:BaseOutputPath=artifacts\stage18j-build-bin\ --verbosity minimal` with 69 passing tests.
- First full serialized alternate-output test run hit the existing audio callback cadence timing diagnostic edge in `HapticDrive.Asio.Audio.Tests.OutputStreamingTests.NullOutput_OutputOwnedStreamingReportsCallbackCadence`.
- Focused audio rerun passed with 102 passing tests.
- After hardening zero-tick callback jitter status, `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- Alternate-output rebuild passed with 0 warnings and 0 errors.
- Full serialized alternate-output `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build /p:BaseOutputPath=artifacts\stage18j-build-bin\ --verbosity minimal -m:1` passed with 585 passing tests and 0 skipped tests.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Manual BST-1 and local gear pulses use the same mixer/safety/limiter path and remain explicit local validation actions.
- The status text no longer treats a stopped continuous stream as "not true ASIO" when the selected/armed bounded-pulse ASIO path is ready.
- Automated coverage uses fake ASIO, fake/read-only input, and fake P-HPR paths. No M-Audio, Fosi, Dayton BST-1, Simagic hardware, F1 25, live telemetry, HID report, or physical vibration was required.

## Stage 18k - BST-1 Standalone ASIO Local Pulse

Date: 2026-06-12

Status: Complete.

Goal: Make BST-1 manual and local paddle gear pulses physically independent from Start Haptics, default the app to the locally validated ASIO setup when available without startup output, compact normal ASIO status, and add BST-1-only output trim.

Missing Items Addressed:

- Added startup ASIO default selection: when `M-Audio M-Track Solo and Duo ASIO` is discoverable, the app selects ASIO Output, that driver, channel `1`, and Arm ASIO without opening, starting, or emitting output.
- Kept safe fallback to Null output when the M-Audio ASIO driver is not discoverable.
- Changed standalone BST-1 manual/local paddle pulse rendering so a stopped global haptics pipeline opens ASIO, primes safety-processed pulse buffers before playback, starts only for the bounded pulse, paces remaining buffers, drains briefly, and stops again.
- Preserved the existing global Start Haptics stream path for live telemetry/replay-driven effects.
- Added BST-1 output trim with default `200%`, valid `25-400%`, and diagnostics for requested strength, trim, effective pre-limiter amplitude, post-limiter peak, and limiter activity.
- Kept trim BST-1-only; P-HPR strength scaling, P-HPR report bytes, P-HPR mappings, and P-HPR direct protocol were not changed.
- Replaced normal Devices-page ASIO diagnostic wall with compact `ASIO READY` / `ASIO ACTIVE` / `ASIO NOT READY` status and moved detailed callback/frame/drop/pulse proof diagnostics to Advanced / Diagnostics.
- Made `Select channel 1` a pure channel selector that does not vibrate.
- Added fake-backed app/runtime tests for ASIO startup defaults, no startup stream/output, compact versus detailed ASIO status, standalone pulse queue priming without Start Haptics, blocked no-output behavior, output trim scaling, limiter retention, and non-vibrating channel selection.

Notes:

- Manual BST-1 pulse and enabled local BST-1 paddle gear pulse do not require Start Haptics, UDP, replay, F1 telemetry, `VehicleState`, or `DrivingArmed`.
- Local gear testing remains separate from live telemetry effects; Start Haptics is still for live telemetry/replay-driven haptics.
- Channel `1` remains the locally validated BST-1 ASIO output channel.
- No output is emitted on startup, even when the app auto-selects the M-Audio ASIO path.
- No physical shaker feel, final safe gain, or physical latency claim is made by this stage.

Verification:

- Confirmed no stale `HapticDrive.Asio.App` process was running before normal app-output verification.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 593 passing tests and 0 skipped tests.

Self-review:

- Standalone manual and local paddle BST-1 pulses share the same bounded ASIO session path when global haptics are stopped.
- Blocked manual pulses do not submit partial ASIO buffers.
- ASIO startup defaults do not open/start output and do not create continuous output.
- Output trim is applied only to BST-1 pulse requests before the existing safety chain and limiter.

## Stage 18l - BST-1 ASIO Pulse Queue And Shutdown Fix

Date: 2026-06-12

Status: Complete.

Goal: Diagnose and fix the standalone/manual BST-1 ASIO queue-full/drop failure, route local paddle BST-1 pulses through the same fixed path, expand local ASIO pulse diagnostics, and make app close dispose background ASIO/listener resources.

Missing Items Addressed:

- Diagnosed the Stage 18k standalone path as pre-submitting a full pulse into the native ASIO backend's bounded 3-buffer queue. At the default 48 kHz / 480-frame shape, one buffer is about 10 ms, so 100 ms requires about 10 buffers and 300 ms requires about 30 buffers.
- Removed the pre-start/priming standalone pulse submit. The standalone path now starts ASIO, waits for callback activity, then renders and submits only when the native queue reports room.
- Kept manual `Test BST-1 Pulse` and enabled local BST-1 paddle gear pulse independent from Start Haptics, UDP, replay, F1 telemetry, `VehicleState`, and `DrivingArmed`.
- Added queue capacity/count, callback counts, accepted/dropped buffer counts, limiter peak, timestamps, and exception stack details to the local JSONL recorder at `local-validation-results/bst1-asio-pulse-flight-recorder.jsonl`.
- Added shutdown diagnostics to that same recorder and moved window close cleanup to an async close path that stops timers/listeners/manual pulse state and disposes test bench, paddle listener, UDP receiver, P-HPR output, and the haptic pipeline/ASIO output.
- Kept normal Devices UI compact with `Last BST-1 pulse: succeeded` or `Last BST-1 pulse blocked: queue full`; detailed queue/callback information remains in Advanced / Diagnostics and the local recorder.
- Added fake-backed tests for callback-before-render, bounded queue behavior, 100 ms and 300 ms full-frame rendering, queue-full logging without partial submit, failed-start no-output behavior, and paddle gear source use of the fixed standalone path.

Notes:

- P-HPR HID protocol, P-HPR direct runtime behavior, road vibration, wheel slip, wheel lock, kerbs, live telemetry effects, telemetry parser code, WASAPI output, and SimHub integration were not changed.
- The native ASIO queue remains intentionally bounded. The fix is lifecycle/pacing: wait for callback-active and queue room before submitting normal short BST-1 pulse buffers.
- Closing the app now records shutdown-requested and shutdown-completed diagnostics. The disabled minimize-to-tray placeholder remains disabled; normal close is expected to terminate the app after cleanup.
- No final shaker feel, safe physical gain, physical latency, or tuning claim is made by this stage.

Verification:

- Confirmed no stale `HapticDrive.Asio.App` process was running before normal app-output verification.
- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 599 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- No output is emitted on startup; startup still only selects/arms the discoverable M-Audio/channel-1 defaults.
- Manual BST-1 pulse no longer pre-fills the native ASIO queue in fake-backed tests and blocks without partial submit when ASIO cannot start or callback/queue gates do not pass.
- Local paddle BST-1 pulse continues to use the same standalone ASIO path and does not depend on Start Haptics, telemetry, `VehicleState`, or `DrivingArmed`.
- Output trim still scales the BST-1 request before the safety chain, and the limiter remains active.
- P-HPR behavior, report protocol, direct runtime routing, road/slip/lock routes, parser code, UDP forwarding, and WASAPI output were not changed.
- No generated `local-validation-results` logs are committed.

## Stage 18m - BST-1 ASIO State And Pulse Consistency

Date: 2026-06-12

Status: Complete.

Goal: Use the attached local BST-1 ASIO pulse flight-recorder evidence to fix stale ASIO state hydration, false pulse completion records, haptics-on/off local pulse mismatch, and close behavior when tray minimize is unchecked.

Evidence Reviewed:

- `local-validation-results/bst1-asio-pulse-flight-recorder.jsonl`
- `local-validation-results/bst1-asio-pulse-flight-recorder.jsonl.1`
- These files remain local validation evidence only and were not committed.

Evidence Summary:

- Full 150 ms completions: pulse IDs `94-103` and `126-128`, each reaching 57 accepted/submitted buffers and 7,200 rendered frames at 48 kHz.
- Early/truncated completions: pulse IDs `105-107` and `109-124`, where records marked `pulse-completed` after only 19-24 accepted/submitted 128-frame buffers while still claiming 7,200 rendered/generated frames.
- Queue-full failures: pulse IDs `104`, `108`, and `125`, each failed with `ASIO output dropped a buffer: Native ASIO backend queue is full; buffer dropped.`
- Pulse completion was wrong for the early completion group: accepted/submitted buffers were below the 57-buffer requirement for 150 ms at 48 kHz.
- The evidence showed the same requested frequency/duration/trim values, but haptics-on records were using a competing manual submit path while the running callback also consumed the pulse.
- ASIO state evidence included stale-looking `AsioCallbackActive=true` while `AsioRunning=false`, caused by historical callback counts being treated as active.
- Shutdown requested/completed records were present and reported `minimizeToTrayEnabled=false` with ASIO, standalone pulse, paddle listener, UDP listener, and timers disposed; the remaining Task Manager process report still required tightening close behavior and resource shutdown semantics.

Missing Items Addressed:

- Added ASIO readiness hydration that opens the selected ASIO output for capability discovery without starting output or emitting startup buffers.
- Prevented a fresh pre-open `DeviceOutputChannelCount=0` snapshot from blocking channel `1` as outside zero channels; real open/capability failures now surface their actual error.
- Split BST-1 local pulse execution by state: stopped haptics use the bounded standalone ASIO submit path, while running output-owned haptics inject into the existing callback and wait for the exact rendered-frame count.
- Added completion invariants so `pulse-completed` is recorded only when expected frames are rendered and accepted by the relevant path. Truncated pulses record `pulse-truncated` and fail safely instead of being reported as success.
- Extended the local BST-1 ASIO pulse recorder with expected frame count, accepted frame count, rendered frame count, and completion reason (`completed-full`, `truncated`, or `failed`).
- Tightened `AsioCallbackActive` diagnostics so historical callback counts do not make a stopped stream look active.
- Changed close handling so the unchecked tray-minimize path does not cancel the close event; normal close performs bounded cleanup and lets WPF close normally.
- Kept P-HPR direct output, HID report bytes, road/slip/lock routing, F1 25 parsing, UDP forwarding, WASAPI output, and SimHub integration unchanged.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused runtime ASIO readiness tests passed with 28 passing tests.
- Focused app tests passed with 76 passing tests.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 607 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- No output is emitted on startup; readiness hydration opens ASIO only to cache capability/state and does not start the stream.
- Manual `Test BST-1 Pulse` remains a valid first action after app launch when ASIO Output, M-Audio/M-Track driver, channel `1`, arm, and mute gates are ready.
- Haptics-on and haptics-off local BST-1 paddle gear pulses use the same pulse settings and renderer path, with only the transport path differing between standalone submit and running callback injection.
- A pulse cannot be marked `completed-full` unless the expected frame count has rendered.
- Closing with tray minimize unchecked is not cancelled by tray logic.
- No generated `local-validation-results` logs are committed.

## Stage 18n-B - Persistent BST-1 Local ASIO Engine And Retrigger Limiter

Date: 2026-06-12

Status: Complete.

Goal: Move local/manual BST-1 ASIO pulses onto a persistent output-owned callback path, prove completion from pulse-owned frame and post-limiter energy counters, and restore rapid Direct Paddle Gear Bench retrigger behavior without opening an ordinary limiter rejection runaway path.

Changes:

- Local/manual BST-1 ASIO pulses now lazy-start the same output-owned render callback used by live haptics and keep that callback warm for stopped-haptics local pulses instead of using the old standalone submit/stop loop.
- Pulse completion now requires pulse-owned generated frames, consumed frames, and non-zero post-limiter peak/RMS energy for non-zero requests. Global callback progress alone cannot mark a pulse complete.
- BST-1 pulse flight records now include pulse source ID, renderer instance ID, transport path, haptics-running-at-start state, pulse-owned pre/post limiter frame and energy proofs, global callback delta, live gear suppression, dropped/superseded local pulse counts, and latest-press-wins replacement status.
- Haptics-on and haptics-off local BST-1 paddle pulses use the same generator, mixer, safety, limiter, output trim, and selected-channel routing; only the transport label differs between `local-persistent-callback` and `live-haptics-callback`.
- Direct Paddle Gear Bench uses a 40 starts/s direct-control limiter profile so ten 5 ms-spaced left/downshift pulses targeting both P-HPR modules are accepted in fake tests without command-rate rejection.
- Direct P-HPR start rejection recovery now differentiates ordinary no-write limiter rejections from partial-write unsafe failures. Stop All is reserved for partial/unsafe starts instead of firing on every start rejection.
- Removed the disabled `Minimize to tray on close` footer checkbox from the WPF shell until a real tray mode exists.

Verification:

- Stopped stale `HapticDrive.Asio.App` process before the first successful build because the existing executable lock blocked output replacement.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- Focused rebuilt runtime ASIO readiness tests passed with 30 passing tests.
- Focused rebuilt app `PHprDirectRuntimeTests` passed with 11 passing tests.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 610 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- No startup output is emitted; the local BST-1 pulse stream starts only on an explicit manual/local pulse or normal Start Haptics.
- Physical shaker feel, safe gain, physical latency, and final frequency tuning remain Ethan-local validation items.
- P-HPR fake/direct tests still use fake HID writers and fake clocks; this stage does not claim physical P-HPR stop feel or latency.
- Confirmed P-HPR report bytes, F1 25 parsing, UDP forwarding, recording/replay raw-byte preservation, and normal telemetry `DrivingArmed` gates were not changed.
- No generated `local-validation-results` logs are committed.

## Stage 18o-B - Shared Road Texture Signal And Gear Ducking

Date: 2026-06-12

Status: Complete.

Goal: Replace the split BST-1 and P-HPR road-vibration heuristics with one shared `RoadTextureSignal`, let both output paths consume that same signal, and keep accepted local gear pulses dominant by briefly ducking/suppressing road texture after a gear event.

Changes:

- Added a shared Core road texture evaluator that derives `RoadTextureSignal` from `VehicleState`, `m_surfaceType[4]`, speed, suspension acceleration, wheel vertical-force deltas, vertical G, haptics-running state, telemetry freshness, `DrivingArmed`, and recent accepted gear-pulse time.
- Moved the BST-1 road texture effect onto the shared evaluator while preserving deterministic audio rendering, effect snapshots, mixer/safety/limiter routing, and existing road texture profile controls.
- Reworked P-HPR road vibration routing so live routing consumes `pipelineSnapshot.Effects.RoadTexture.Signal` instead of maintaining a second duplicated road heuristic.
- Added gear-priority ducking: accepted local BST-1/manual gear pulses and accepted Direct Paddle Gear Bench P-HPR pulses notify the road evaluator/router, causing the shared road signal to duck and P-HPR road commands to suppress during the short priority window.
- Let Direct Paddle Gear Bench coexist with enabled road vibration while keeping emergency stop, slip, and lock gates in place.
- Added shared-signal diagnostics on road snapshots so the UI and routing tests can inspect the exact road signal used by both output paths.
- Kept P-HPR HID report bytes, direct gear-pulse command encoding, emergency stop/Stop All behavior, F1 25 parsing, UDP forwarding, and raw recording/replay preservation unchanged.

Verification:

- Targeted rebuilt Core road texture tests passed.
- Targeted rebuilt audio effect tests passed with 104 passing tests.
- Targeted rebuilt actuation tests passed with 90 passing tests.
- Targeted rebuilt app tests passed with 77 passing tests.
- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 625 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- The shared signal is a software arbitration and diagnostics contract, not a physical proof of final road feel, safe gain, physical latency, or frequency tuning.
- Gear ducking is fake-backed and deterministic in automated tests; real mixed BST-1/P-HPR priority feel still requires Ethan-local validation.
- Normal telemetry `DrivingArmed` routing remains required for live road vibration unless an explicitly local/manual evaluation context allows otherwise.
- No generated `local-validation-results` logs are committed.

## Stage 18p-A - Product UI Architecture And Replay Timing Diagnostic

Date: 2026-06-12

Status: Complete.

Goal: Inspect the current WPF UI, settings/profile persistence, and recording/replay implementation before any broad product UI rewrite.

Changes:

- Added `docs/STAGE_18P_A_PRODUCT_UI_ARCHITECTURE_AND_REPLAY_TIMING.md`.
- Documented that the current WPF app can support a modern dark/sidebar/card product UI, but the safe path is to extract shared theme/style resources first and then split large panels into smaller components.
- Mapped current UI sections to Dashboard, Devices, Effects, Routing / Mixer, Telemetry / UDP, Profiles, and Advanced / Diagnostics targets.
- Recommended a hybrid Effects layout with BST-1, Brake P-HPR, and Throttle P-HPR hardware sections containing effect cards, plus shared gear-duration and shared road-signal concepts.
- Reviewed persistence boundaries across `HapticDriveProfile`, `PhprEffectProfile`, and `AppSettings`, including runtime/private states that must not be persisted.
- Identified the replay timing root cause: `.hdrec` files already store relative packet timing, but the WPF replay path calls `TelemetryReplayOptions.Fast` for normal Replay Latest and Replay Selected actions.
- Split follow-up work into 18p-B replay/delete cleanup, 18p-C app shell/theme/cards, 18p-D Effects restructure, 18p-E Devices/Advanced cleanup, and 18p-F Routing/Mixer polish.

Verification:

- Report-only stage. No runtime code, parser layouts, ASIO backend, P-HPR HID/report behavior, or haptic effect math was changed.

Self-review:

- Stage 18p-A intentionally does not claim product UI implementation is complete.
- Physical shaker feel, safe gain, physical latency, P-HPR physical behavior, and final frequency tuning remain local validation items.
- No generated `local-validation-results` logs are committed.

## Stage 18p-B - Real-Time Replay And Recording Delete

Date: 2026-06-12

Status: Complete.

Goal: Make normal Telemetry / UDP replay preserve recorded packet timing by default, keep fast replay explicit for parser/debug work, and add guarded Delete Selected recording behavior.

Changes:

- Added a Replay mode selector to the Telemetry / UDP recording library with `Real-time` as the default and `Fast debug` as the explicit non-default parser/debug mode.
- Changed Replay Latest and Replay Selected to pass explicit replay options from the UI; normal replay now uses `TelemetryReplayOptions.TimePreserving`.
- Kept the `TelemetryReplayService` omitted-options default as fast mode for deterministic service callers and tests; UI calls no longer rely on that default.
- Added a replay delay scheduler seam so time-preserving replay delay requests can be tested without sleeping.
- Added guarded recording-library delete support that only deletes `.hdrec` files inside the local recordings folder, blocks active recording output, handles missing files gracefully, and reports locked/unauthorized failures without crashing.
- Added Delete Selected to the Telemetry / UDP recording library and refreshes the library after delete attempts.
- Updated recording/replay docs and stage trackers.

Verification:

- Focused recording tests passed with 19 passing tests.
- Focused app tests passed with 84 passing tests.
- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 635 passing tests and 0 skipped tests after rerunning one transient app-test timing failure that passed immediately on focused rerun.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Normal Replay Latest and Replay Selected are time-preserving by default.
- Fast replay remains available only through the explicit `Fast debug` UI mode and is labelled unsuitable for feel/latency testing.
- Delete Selected cannot remove arbitrary files outside the recordings folder and cannot delete the active recording output.
- F1 25 parser offsets, ASIO backend behavior, P-HPR HID/report behavior, gear-pulse logic, and road-effect logic were not changed.
- No generated `local-validation-results` logs are committed.

## Stage 18p-C - App Shell, Dark Theme, Sidebar, And Cards

Date: 2026-06-12

Status: Complete.

Goal: Implement the first visual product shell pass with a maintainable WPF dark theme, sidebar navigation, top status/action bar, and card-based visual system without restructuring Effects, Devices, Advanced, or haptic runtime logic.

Changes:

- Extracted the app visual system from inline `App.xaml` resources into `src/HapticDrive.Asio.App/Resources/Theme.xaml` and `src/HapticDrive.Asio.App/Resources/Styles.xaml`.
- Kept `App.xaml` as the merged resource entry point for theme and style dictionaries.
- Added dark-first WPF theme tokens for app chrome, sidebar, top bar, cards, inputs, red accent states, danger states, status colors, radii, and card padding.
- Added reusable styles for cards, metric cards, top status badges, primary/secondary/top-bar/danger buttons, inputs, sidebar navigation, headings, muted text, and status badges.
- Restyled `MainWindow.xaml` in place with the new sidebar brand/navigation, top status/action bar, shared dashboard metric cards, shared panel cards, and footer chrome while preserving named controls and event handlers.
- Updated `MainWindow.xaml.cs` theme palette handling so the existing light/dark toggle updates the new resource tokens and the top bar reflects the selected page context.
- Added app resource tests covering `App.xaml` dictionary merger and the theme/style keys required by the shell.
- Updated the product UI architecture report, roadmap, and known issues for the completed 18p-C shell/theme stage.

Verification:

- Focused app tests passed with 86 passing tests.
- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 637 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 18p-C is intentionally a visual shell/style foundation, not the 18p-D Effects restructure or the 18p-E Devices/Advanced cleanup.
- Haptic runtime behavior, ASIO backend behavior, P-HPR HID/report bytes, parser layouts, recording/replay scheduling, gear routing, and road/slip/lock routing were not changed.
- The current pages remain large in-place XAML panels; later stages should split normal controls into smaller cards/components where that reduces UI complexity.
- Physical shaker feel, safe gain, physical latency, final frequency tuning, and physical P-HPR behavior remain Ethan-local validation items.
- No generated `local-validation-results` logs are committed.

## Stage 18p-D - Effects Hardware Card Restructure

Date: 2026-06-12

Status: Complete.

Goal: Restructure the Effects page into a hardware-first product layout without changing haptic runtime behavior, parser layouts, ASIO backend behavior, P-HPR HID/report bytes, direct bench routing, or command-rate limiter logic.

Changes:

- Rebuilt the Effects page around Shared / Global Effect Settings, BST-1 Seat Shaker, Brake P-HPR, and Throttle P-HPR sections.
- Moved normal BST-1 paddle gear pulse controls and P-HPR gear controls from Devices into Effects while keeping the same existing `x:Name` controls and handlers.
- Moved normal P-HPR road, brake lock, and throttle slip enable/strength controls from Advanced into Effects while leaving low-level min strength, min/max Hz, internal command duration, target overrides, raw direct controls, validation harnesses, mock routers, and diagnostics in Advanced.
- Kept Devices focused on ASIO/P-HPR readiness, emergency recovery, Stop All, wheel/paddle input, and manual BST-1/brake/throttle pulse buttons.
- Made the Effects copy explicit that gear effects are pulse-like and use shared duration by default, while road texture is continuous/synthetic and has no normal pulse duration.
- Added source-XAML tests covering the hardware/effect grouping, relocated normal controls, and absence of road pulse-duration controls from the normal Effects cards.

Verification:

- Confirmed no stale `HapticDrive.Asio.App` process was running before verification.
- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 639 passing tests and 0 skipped tests after fixing one new source-XAML assertion that incorrectly counted distinct text values instead of occurrences.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 18p-D stayed within UI structure and settings-control relocation.
- Existing runtime handlers remained wired to the same control names; no new settings model was introduced.
- ASIO runtime code, P-HPR HID/report/protocol code, F1 25 parser offsets, gear pulse runtime logic, replay/delete behavior, and command-rate limiter logic were not changed.
- Devices cleanup and Advanced diagnostics cleanup remain staged for 18p-E.

## Stage 18p-E - Devices And Advanced Cleanup

Date: 2026-06-12

Status: Complete.

Goal: Keep Devices focused on hardware setup, readiness, and manual tests while moving detailed local bench validation internals into Advanced / Diagnostics without changing runtime behavior or persisted safety gates.

Changes:

- Removed the Local Gear Test / Paddle Gear Bench validation internals from the Devices wheel/paddle hardware card.
- Added Devices copy pointing detailed Local Gear Test and Paddle Gear Bench validation to Advanced Diagnostics while retaining wheel input setup, mapping, and readiness status.
- Moved the existing Local Gear Test and Paddle Gear Bench controls into the Advanced diagnostics content gate near the real direct-control runtime context.
- Preserved the same `x:Name` controls and handlers for local gear test mode, auto-start listener, start listener, bench enable/target/status, and clear bench counters.
- Added source-XAML tests that enforce Devices hardware/readiness/manual-test boundaries and Advanced ownership of bench, direct-control, mock-routing, and low-level P-HPR diagnostic controls.

Verification:

- Confirmed no stale `HapticDrive.Asio.App` process was running before verification.
- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 642 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 18p-E stayed within UI grouping and source-XAML coverage.
- Existing runtime handlers remain wired to the same named controls; no new settings model was introduced.
- ASIO runtime code, P-HPR HID/report/protocol code, F1 25 parser offsets, gear pulse runtime logic, replay/delete behavior, and command-rate limiter logic were not changed.
- Routing / Mixer polish and final visual review remain staged for 18p-F.

## Stage 18p-F - Routing / Mixer Final UI Polish

Date: 2026-06-12

Status: Complete.

Goal: Polish Routing / Mixer as the output route, gain, limiter, mute, priority, ducking, and active-effects summary page while completing the Stage 18p product UI pass.

Changes:

- Reworked Routing / Mixer from a single mixer form into a focused page with mixer/safety controls, output route summaries, active-effects summary, and priority/ducking summary.
- Kept master gain, normal mute, safety output gain, conservative output ceiling, and limiter controls on their existing names and `TuningControl_Changed` handlers.
- Added read-only summaries for output peak, limiter activity, emergency mute, BST-1 / ASIO routing, Brake P-HPR routing, Throttle P-HPR routing, active effects, and product-level priority/ducking behavior using existing state only.
- Added a small shared style polish for visible keyboard focus and disabled states on common controls.
- Added source-XAML tests for the Routing / Mixer mixer/safety controls, route summary sections, active-effects summary, and priority/ducking summary.
- Updated the 18p report, roadmap, and known issues for the completed final Stage 18p UI pass.

Verification:

- Confirmed no stale `HapticDrive.Asio.App` process was running before verification.
- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 644 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Routing / Mixer is now output-routing, software gain, safety, priority, and summary focused.
- Effects remains the normal per-hardware effect tuning page.
- Devices remains hardware readiness and manual-test focused.
- Advanced remains diagnostics, validation, mock routing, and direct-control focused.
- ASIO runtime code, P-HPR HID/report/protocol code, F1 25 parser offsets, gear/road runtime logic, replay/delete behavior, and command-rate limiter logic were not changed.
- Emergency controls remain visible in the top bar and Devices/Advanced surfaces; no direct-control safety state is persisted.
- Future work can focus on Ethan-local physical road-texture validation and tuning rather than further Stage 18p UI restructuring.

## Stage 18q-B - Road Texture Diagnostics And Flight Recorder

Date: 2026-06-13

Status: Complete.

Goal: Add diagnostics and an optional local flight recorder that can prove whether BST-1 road texture is present, estimate where it is lost in the mixer/safety chain, and explain P-HPR road routing/suppression behavior without changing road feel tuning, P-HPR road cadence, gear priority, ASIO behavior, parser layouts, or P-HPR HID/report bytes.

Changes:

- Added road texture diagnostic fields for speed scale, suspension acceleration contribution, wheel vertical-force contribution, and vertical-G contribution to the shared `RoadTextureSignal`.
- Added P-HPR road routing counters for route attempts, stale telemetry suppressions, gear-ducking suppressions, command-rate suppressions, first/last attempt timestamps, last routed-command timestamp, and last ignored reason.
- Added an app-side road texture diagnostic snapshot that emits compact report lines for road signal inputs, BST-1 road contribution estimates, P-HPR routing/suppression counters, and recorder state.
- Added an off-by-default road texture flight recorder toggle in Advanced / Diagnostics. When enabled, it writes local JSONL records to `local-validation-results/road-texture-flight-recorder.jsonl` and rotates locally at 1 MB.
- Updated the road texture, manual hardware, and Simagic P-HPR road vibration guides to document the Stage 18q-B evidence workflow and its diagnostics-only boundary.
- Added fake-backed tests for smooth tarmac diagnostic signal fields, P-HPR road suppression counters, diagnostic report text, recorder JSONL output, and local ignored validation output.

Verification:

- Confirmed no stale `HapticDrive.Asio.App` process was running before verification.
- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 654 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 18q-B is instrumentation only. It does not tune BST-1 road feel, change P-HPR road cadence, alter gear priority, or modify runtime safety gates.
- BST-1 road diagnostics are software estimates from the shared road signal and safety-chain state; physical shaker feel, safe gain, latency, and final tuning remain Ethan-local validation items.
- P-HPR route diagnostics expose why road commands are or are not routed, but they do not fix the earlier physical sparse 3-5 second gaps or occasional thumps.
- The flight recorder is disabled by default, writes only under `local-validation-results`, and does not touch the audio callback path.

## Stage 18q-C/D/E/F - Road Retune, Output Separation, P-HPR Cadence, And Validation Docs

Date: 2026-06-13

Status: Complete.

Goal: Implement the Stage 18q-C/D/E/F weekly road-texture follow-up from the Stage 18q-A/B evidence: widen BST-1 road tuning headroom, split the shared road signal from per-output road toggles, replace sparse P-HPR road pulses with a bounded continuous cadence model, and document the required local validation order.

Changes:

- Stage 18q-C widened the BST-1 / ASIO road output gain range to 100% while keeping the previous 25% setting as the conservative default/start point and preserving the existing mixer, safety gain, limiter, emergency mute, and selected ASIO output chain.
- Stage 18q-D separated shared road signal enablement from BST-1, brake P-HPR, and throttle P-HPR road output toggles. P-HPR road can now consume the shared road signal while BST-1 road output is disabled, and diagnostics expose the shared/output split.
- Stage 18q-E moved real P-HPR road routing off the 500 ms UI/status timer and onto a background 100 ms cadence task with overlapping bounded road commands, default 220 ms duration clamped to at least 180 ms, a 350 ms hold timeout, explicit stop commands, watchdog diagnostics, stale/haptics-stopped/disabled stops, and gear/slip/lock priority preserved.
- Stage 18q-E added fake-backed router coverage for minimum road duration, bounded cadence, gear-ducking stops, stale telemetry stops, hold-timeout watchdog stops, and low-intensity tactile scaling.
- Stage 18q-F updated the manual hardware checklist, road texture guide, Simagic P-HPR road guide, roadmap, and known issues for the new tuning/cadence workflow.

Verification:

- Confirmed no stale `HapticDrive.Asio.App` process was running before verification.
- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 665 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 18q-C/D/E/F changes road behavior in software, but they do not prove physical BST-1 or P-HPR road feel, safe gain, physical latency, stop behavior, or final frequency tuning.
- BST-1 100% road output gain is tuning headroom, not a recommended universal setting.
- P-HPR road now has a cadence/hold/watchdog model, but Ethan-local validation is still required to prove the sparse 3-5 second gap/thump symptom is resolved physically.
- Raw UDP preservation, F1 25 packet parsing, confirmed P-HPR HID/report bytes, and ASIO/BST-1 hardware absence behavior remain protected by the existing boundaries.

## Stage 18r-B - User Settings Persistence, Defaults Cleanup, UI Safety Simplification, P-HPR Wording Cleanup, And Replay Rename

Date: 2026-06-15

Status: Complete.

Goal: Persist normal user tuning/settings across launches, clean up current-rig defaults, simplify confusing normal-user mixer safety controls, clarify P-HPR road/slip/lock wording, and add rename-selected recording support without changing gear runtime behavior, ASIO backend behavior, P-HPR HID/runtime behavior, or road/slip DSP logic.

Changes:

- Added default audio-profile auto-load and auto-save through `default.hdprofile.json`, so normal ASIO/BST-1 tuning changes now restore on the next launch without restoring runtime-active output state.
- Extended app settings persistence for safe UI/device state including preferred output mode, replay timing mode, and BST-1 local paddle gear settings while keeping ASIO armed, haptics running, emergency mute, bench-active, active-pulse, pending-stop, and direct P-HPR enable/arm/private-device state runtime-only.
- Updated current-rig defaults: BST-1 road stays on at 100% gain, other BST-1 effects default off with 50% gain headroom, BST-1 local paddle gear defaults on at `50%`, `50 Hz`, shared duration, and BST-1 gain sliders now allow the full `0-100%` range.
- Simplified Routing / Mixer normal controls to output gain only, with limiter kept internally on and the conservative ceiling normalized internally to `100%`.
- Cleaned up normal P-HPR Effects wording to `Brake road texture enabled`, `Throttle road texture enabled`, `Brake wheel lock enabled`, and `Throttle wheel slip enabled`.
- Added `Rename Selected` to the recordings library with filename sanitization, `.hdrec` enforcement, overwrite blocking, active-recording blocking, and post-rename refresh.
- Updated diagnostics/docs to list what persists, what remains runtime-only, and where those values live.

Persistence summary:

- `default.hdprofile.json`: audio effect enabled states, gains, frequencies, durations, mixer mute/gain, and normal safety output gain.
- `appsettings.json`: theme, Advanced visibility, safe output selection, replay mode, forwarding destinations, paddle mapping/debounce, BST-1 local paddle gear settings, shift intent, and safe P-HPR/mock preferences.
- `p-hpr.hdphprprofile.json`: manual P-HPR effect-preferences snapshot only.

Runtime-only by design:

- Haptics running.
- Emergency mute and emergency-stop state.
- ASIO armed state.
- Direct P-HPR enable/arm/private device selection.
- Active pulses, pending stops, and bench-active state.
- Flight-recorder/mock history and diagnostics counters.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 675 passing tests and 0 skipped tests after updating one runtime test to opt into an engine-enabled profile instead of relying on the new Stage 18r-B default-off engine setting.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 18r-B stayed within persistence/defaults/UI wording/recording-library scope.
- Gear runtime behavior was not intentionally changed.
- No physical validation, safe physical gain claim, shaker feel claim, or latency claim is made here.

## Stage 18r-C - BST-1 Road Speed/Frequency/Grain Tuning Controls And Devices-Tab Persistence Hotfix

Date: 2026-06-16

Status: Complete.

Goal: Fix the remaining safe Devices-tab persistence gaps from Stage 18r-B and extend BST-1 road texture so speed affects the road feel more clearly through frequency and grain while preserving the shared road signal architecture, gear ducking, and P-HPR separation.

Changes:

- Persisted the remaining safe Devices-tab preferences: Arm ASIO readiness, selected ASIO driver/channel, and paddle debounce now round-trip through app settings without restoring haptics-running or other live output state.
- Added a normal LostFocus save path for left/right paddle button mapping and debounce so text edits commit without requiring a different control change first.
- Startup restore now keeps a saved Arm ASIO readiness preference only as readiness. Restoring that preference does not start haptics, start the ASIO stream, or emit output.
- Extended the road profile/options model with BST-1 low-speed frequency, high-speed frequency, speed-frequency influence, grain amount, and a `330 km/h` default speed reference while preserving compatibility with older profiles that lack the new fields.
- Reworked the shared road evaluator so road speed continues to change usefully beyond `160 km/h` through a bounded nonlinear speed scale plus speed-driven BST-1 frequency and grain changes, while gear ducking and telemetry/stale gating remain intact.
- Updated the BST-1 Road Texture card with user-facing controls for low-speed frequency, high-speed frequency, speed reference, speed-frequency influence, and grain amount. Existing BST-1 road gain and shared minimum-speed gate remain in place.
- Updated diagnostics, roadmap, known issues, the road texture guide, user guide, hardware-absent/profile docs, and related fake-backed tests for the new road tuning fields and safe Arm ASIO persistence behavior.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 681 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 18r-C stayed within the requested scope: BST-1 road tuning plus the minor safe Devices-tab persistence hotfix only.
- Gear runtime timing/logic was not intentionally changed.
- P-HPR direct HID/protocol/runtime, slip/lock tuning, and ASIO backend behavior were not intentionally changed.
- No physical validation, safe physical gain claim, shaker feel claim, or latency claim is made here. The new road controls are software starting points only.

## Stage 18r-D - BST-1 Wheel Slip / Wheel Lock Tuning Controls And Diagnostics

Date: 2026-06-16

Status: Complete.

Goal: Split the BST-1 wheel slip and wheel lock tuning path into separate normal-user controls with safe profile persistence, migration/clamping, and clearer diagnostics while leaving P-HPR slip/lock, road behavior, gear behavior, parser layouts, and ASIO backend behavior unchanged.

Changes:

- Kept one shared BST-1 slip/lock evaluator/render path but split its options into independent wheel-slip and wheel-lock enabled, gain, frequency, and roughness settings.
- Extended the audio profile model and validator so the new slip/lock fields persist safely, clamp to conservative bounds, and migrate older combined slip profiles by inheriting legacy combined enabled/gain settings where appropriate.
- Reworked the BST-1 Effects card to expose separate wheel-slip and wheel-lock controls plus user-facing slip ratio threshold and lock sensitivity tuning.
- Expanded BST-1 slip/lock runtime status text and copied diagnostics to show active source, inactive reason, slip/lock intensities, raw slip ratio/angle, wheel-speed ratio, frequency, roughness, and peak.
- Added fake-backed coverage for separate slip-vs-lock tuning behavior, profile round-trip/migration/clamping, updated Effects-page source-XAML expectations, and the widened slip snapshot used by road diagnostics tests.
- Updated the haptic-effects and profiles/diagnostics docs for the new Stage 18r-D BST-1 slip/lock tuning model.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 684 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 18r-D stayed within the requested BST-1 slip/lock tuning, persistence, diagnostics, tests, and docs scope.
- The BST-1 path still uses one shared software evaluator, so separate slip and lock feel remain software tuning inputs rather than proof of final physical shaker behavior.
- P-HPR slip/lock routing, direct HID/runtime behavior, road tuning, gear timing, parser fields, and ASIO backend behavior were not intentionally changed.
- No physical validation, safe physical gain claim, shaker feel claim, or latency claim is made here.

## Stage 18r-E/F - P-HPR Wheel Slip / Wheel Lock Continuous Texture Model And Targeted Priority Validation

Date: 2026-06-16

Status: Complete.

Goal: Replace the old real P-HPR slip/lock pulse-train feel with a bounded continuous cadence model, add the targeted diagnostics needed to validate slip/lock routing, and ensure P-HPR road yields while gear behavior remains protected and intentionally unchanged.

Changes:

- Reworked `PHprSlipLockRouter` from a short one-shot `50 ms` pulse path into a bounded continuous update model with a `100 ms` cadence gate, `350 ms` hold timeout, explicit stop commands, stale/DrivingArmed/emergency/coexistence suppression, gear-protection suppression, and per-effect runtime state.
- Kept the existing direct P-HPR command protocol, report family, safety limiter, and HID writer path untouched. The new model still routes through the same direct-control stack and uses safe stop commands when slip/lock becomes inactive.
- Moved real P-HPR slip/lock routing off the `500 ms` WPF telemetry status timer and onto its own background runtime loop, matching the road runtime pattern more closely and avoiding the previous pulse-gap-pulse-gap cadence.
- Updated real P-HPR slip defaults to throttle-targeted continuous slip at `45-50 Hz` and real lock defaults to brake-targeted continuous lock at `50 Hz`, with the new minimum continuous duration clamp at `100 ms`. Existing persistence/profile paths continue to round-trip safely and clamp invalid values.
- Expanded P-HPR slip/lock diagnostics with runtime state, active modules, cadence/hold settings, routed/safety/stale/interval/command-rate/stop counts, gear-protection suppression, stop reason, per-effect reason/intensity/strength/frequency/duration, raw telemetry inputs, and last start/update/stop age.
- Tightened targeted priority behavior so real P-HPR road yields while slip/lock is actively holding a module, and accepted gear pulses now notify both road and slip/lock routers so gear remains protected without changing the gear route itself.
- Updated fake-backed router/app-settings tests to cover continuous cadence, explicit stops, stale/DrivingArmed suppression, gear protection, watchdog behavior, command-rate reporting, and the new slip/lock duration defaults.
- Updated roadmap/known-issues/user docs to record that BST-1 road/slip/lock behavior remains intentionally unchanged and that the new real P-HPR cadence values are still software starting points pending Ethan-local validation.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 690 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 18r-E/F stayed within the requested real P-HPR slip/lock cadence, diagnostics, targeted road-yield/gear-protection validation, tests, and docs scope only.
- BST-1 road, BST-1 slip/lock tuning, gear timing, direct HID/report bytes, parser layouts, and ASIO safety behavior were intentionally left unchanged.
- No physical validation, safe physical gain claim, tyre realism claim, or latency claim is made here. The new real P-HPR slip/lock values are software starting points only.

## Stage 19A - Runtime Ownership Guardrails And Extraction Plan

Date: 2026-06-16

Status: Complete.

Goal: Verify the external Gemini architecture findings against the live repo, protect the current dependency direction with tests, and take only the lowest-risk extraction-preparation step without moving validated runtime code into the wrong project.

Changes:

- Verified the main Gemini findings against live code. `MainWindow.xaml.cs` still starts and owns the real P-HPR slip/lock loop in `StartRealSlipLockRuntime` / `RunRealSlipLockRuntimeAsync`, still starts and owns the real road loop in `StartRealRoadVibrationRuntime` / `RunRealRoadVibrationRuntimeAsync`, and still routes paddle input through `PaddleInputSource_PaddleInputReceived`.
- Verified that `PHprDirectRuntime.cs` and `PhprDeviceCardPulseService.cs` remain in `HapticDrive.Asio.App`, and that both depend on non-UI P-HPR/input types plus the concrete `SimagicPhprOutputDevice`.
- Verified that `SlipEffect` and `PHprSlipLockRouter` still duplicate the core slip/lock threshold and assisted-input attenuation logic, so a shared evaluator remains a later stage.
- Confirmed the current production project graph is `HapticDrive.Asio.App -> HapticDrive.Asio.Runtime`, `HapticDrive.Asio.App -> HapticDrive.Actuation`, and `HapticDrive.Actuation -> HapticDrive.Asio.Runtime`. A direct move of `PHprDirectRuntime.cs` into `HapticDrive.Asio.Runtime` would require `HapticDrive.Asio.Runtime -> HapticDrive.Actuation` because the file currently consumes `PaddleGearBenchTestResult`, `PaddleGearBenchTestOptions`, and `PHprGearPulseTarget`, which would create a cycle.
- Added project-graph guardrail tests in `HapticDrive.Asio.Runtime.Tests` that assert the runtime assembly does not reference the App assembly, the current App/Actuation/Runtime direction remains explicit, and the production project graph stays acyclic.
- Added App-level guardrail tests proving manual brake and throttle direct pulses still route through the same validated `PhprDeviceCardPulseService` / `SimagicPhprOutputDevice` path as the direct bench workflow, and strengthened the bench success test to assert the same route name explicitly.
- Updated `ARCHITECTURE.md`, `ROADMAP.md`, and `KNOWN_ISSUES.md` with the verified findings, the reason Stage 19A stops at guardrails, and the recommended Stage 19B extraction order.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 695 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.

Self-review:

- Stage 19A intentionally did not move `PHprDirectRuntime.cs`, `PhprDeviceCardPulseService.cs`, the real road/slip/lock loop ownership, or paddle routing out of `MainWindow`.
- UI layout/XAML, ASIO backend behavior, P-HPR HID protocol bytes, F1 25 parser offsets, gear pulse tuning, and road/slip/lock tuning were intentionally left unchanged.
- No physical validation, safe physical gain claim, pedal feel claim, or latency claim is made here. Stage 19A is guardrails and extraction planning only.

## Stage 19B - Runtime Ownership Dependency Inversion And Safe Direct Runtime Extraction

Date: 2026-06-16

Status: Complete.

Goal: Remove the Stage 19A dependency-cycle blocker, preserve the validated direct P-HPR pulse path, and move the first safe non-UI direct runtime slice out of `HapticDrive.Asio.App` without pulling Windows HID output code into the generic runtime layer.

Changes:

- Re-checked the Stage 19A blocker against live code before moving anything. `PHprDirectRuntime.cs` consumed `PaddleGearBenchTestResult`, `PaddleGearBenchTestOptions`, and `PHprGearPulseTarget`, while `PHprDirectRuntime.cs` also depended on the concrete `SimagicPhprOutputDevice`. That made a direct move into `HapticDrive.Asio.Runtime` the wrong home even after contract inversion.
- Moved the pure P-HPR routing/bench contract surface out of `HapticDrive.Actuation.PHpr` into `HapticDrive.Simagic.PHPR.Abstractions.Routing`: `PHprGearPulseTarget`, `PHprGearPulseProfile`, `PaddleGearBenchTestOutputMode`, `PaddleGearBenchTestOptions`, and `PaddleGearBenchTestResult`.
- Added `HapticDrive.Input.Abstractions` as the only new dependency of `HapticDrive.Simagic.PHPR.Abstractions` so `PaddleGearBenchTestResult` can still carry mapped paddle and accepted shift-intent facts without forcing `HapticDrive.Asio.Runtime -> HapticDrive.Actuation`.
- Moved `PHprDirectRuntime.cs` and `PhprDeviceCardPulseService.cs` out of `HapticDrive.Asio.App` into `HapticDrive.Simagic.PHPR.Output.Windows`, which is the narrower non-UI home for direct P-HPR orchestration because it already owns the concrete Windows HID output device.
- Moved the hidden non-UI helper `PaddleGearBenchDirectGate.cs` with the direct runtime after the first post-move build exposed it as an App-only seam.
- Added `InternalsVisibleTo` entries for `HapticDrive.Asio.App` and `HapticDrive.Asio.App.Tests` in `HapticDrive.Simagic.PHPR.Output.Windows`, and added small `GlobalUsings.cs` files in the affected production/test projects to keep namespace churn low after the contract move.
- Updated the Stage 19A graph guardrails so Runtime still must not reference App or Actuation, the selected contract/runtime projects must not reference App, the graph remains acyclic, and App tests now prove the relocated direct runtime types and contract types live in the intended assemblies.
- Updated `ARCHITECTURE.md`, `ROADMAP.md`, and `KNOWN_ISSUES.md` with the Stage 19B dependency inversion result plus the remaining Stage 19C, Stage 19D, and Stage 20 targets.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 697 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed.

Self-review:

- Stage 19B changed dependency direction and non-UI ownership only. It intentionally did not change UI/XAML, ASIO/BST-1 behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, F1 25 parser layouts, gear tuning, or road/slip/lock cadence.
- `MainWindow.xaml.cs` still owns the continuous real road/slip/lock loops and the GT Neo paddle-entry event path. Those remain explicit next-stage work.
- No physical validation, safe physical gain claim, pedal feel claim, or latency claim is made here. The stage is architecture/risk-reduction work only.

## Stage 19C - Extract Continuous Real P-HPR Road/Slip/Lock Runtime Ownership Out Of MainWindow

Date: 2026-06-16

Status: Complete.

Goal: Move the continuous real P-HPR road/slip/lock background loop ownership out of `MainWindow.xaml.cs` into a non-UI coordinator while preserving the same cadence, stop behavior, diagnostics meaning, and safety gating.

Changes:

- Re-checked the live Stage 19B baseline before moving anything. `MainWindow.xaml.cs` still owned `StartRealSlipLockRuntime`, `RunRealSlipLockRuntimeAsync`, `StartRealRoadVibrationRuntime`, and `RunRealRoadVibrationRuntimeAsync`, but those methods were only orchestrating `HapticPipelineSnapshot`, readiness gates, safety-context builders, router calls, and hold-timeout stops.
- Added `PHprContinuousEffectsRuntimeCoordinator` in `HapticDrive.Actuation.PHpr` together with small runtime input/snapshot/stop-result models and a loop-clock abstraction for deterministic fake-backed testing.
- Moved continuous real road/slip/lock loop ownership, cancellation token ownership, shutdown waits, in-flight suppression state, road-yield suppression counting, and last real road/slip routing results out of `MainWindow.xaml.cs` and into the new coordinator.
- Kept `MainWindow.xaml.cs` as the thin consumer for this slice: it now constructs the coordinator, provides the latest `HapticPipelineSnapshot` plus real-road/real-slip safety contexts and readiness state, starts the coordinator on load, stops/disposes it on shutdown, and reads coordinator diagnostics for UI/report text.
- Preserved the existing router instances, the existing `100 ms` continuous cadence, `150 ms` road-yield-after-slip window, existing `StopIfHoldExpiredAsync` calls, and the existing stop messages/gates for disabled, not-ready, stale, haptics-stopped, gear-priority, and coexistence paths.
- Updated App/UI diagnostics and road flight-recorder snapshot building to read coordinator-owned runtime counters/results instead of App-owned loop fields.
- Added Actuation-side coordinator tests for no-startup-write, disabled-start no-op, road/slip routing through the existing router path, road yield while slip/lock owns priority, stale/haptics/emergency blocking cases, coexistence blocking, and deterministic stop/dispose with a fake runtime clock.
- Added guardrails proving the new coordinator lives in `HapticDrive.Actuation`, has no App/WPF dependency, and that `MainWindow.xaml.cs` no longer declares the old loop body methods. Updated the production project-graph guardrail so `HapticDrive.Actuation` must not reference `HapticDrive.Asio.App`.
- Updated `ARCHITECTURE.md`, `ROADMAP.md`, and `KNOWN_ISSUES.md` with the Stage 19C extraction result and the remaining Stage 19D / Stage 20 targets.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 712 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed.

Self-review:

- Stage 19C moved continuous loop ownership only. It intentionally did not change UI/XAML, ASIO/BST-1 behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, F1 25 parser layouts, gear routing, road/slip/lock tuning, cadence, hold-timeout, or coexistence semantics.
- GT Neo paddle input routing still enters through `MainWindow.xaml.cs`. That remains the explicit Stage 19D target.
- The shared BST-1/P-HPR slip/lock evaluator still does not exist. That remains the explicit Stage 20 target.
- No physical validation, safe physical gain claim, pedal feel claim, or latency claim is made here. The stage is architecture/risk-reduction work only.

## Stage 19D - Extract Paddle Input Routing Ownership Out Of MainWindow

Date: 2026-06-17

Status: Complete.

Goal: Move the remaining paddle input routing orchestration out of `MainWindow.xaml.cs` into a non-UI coordinator/service boundary while preserving the same mapped paddle, direct bench, real direct gear, and BST-1 local gear behavior.

Changes:

- Re-checked the live Stage 19C baseline before moving anything. `MainWindow.xaml.cs` still owned the substantive `PaddleInputSource_PaddleInputReceived` body plus the `RoutePaddleGearBenchAsync`, `RoutePaddleGearBenchMockAsync`, `RouteBst1PaddleGearBenchAsync`, `RoutePaddleGearBenchDirectAsync`, and paddle-input exception recovery helpers.
- Confirmed the live route crosses both P-HPR and BST-1/ASIO local gear paths. The method evaluated `ShiftIntentProcessor`, evaluated `PaddleGearBenchTestController`, notified accepted gear pulses into `_hapticPipeline` plus the real road/slip routers, routed accepted live events into mock and real direct P-HPR gear paths, routed accepted bench events into mock/direct P-HPR bench paths and optional BST-1 manual ASIO injection, and performed safe recovery through `IPHprDirectRuntime`.
- Added `PaddleInputRoutingCoordinator` in `HapticDrive.Asio.App` together with narrow non-UI dependency/result records. The coordinator owns paddle-event runtime handling, bench branching, accepted live-shift notifications, direct/mock route calls, optional BST-1 manual ASIO injection, and exception recovery.
- Kept the coordinator inside `HapticDrive.Asio.App` intentionally. The live route still depends on internal `IPHprDirectRuntime` plus App-owned `HapticPipelineCoordinator.StartManualAsioHardwareTestAsync`, so forcing it into `HapticDrive.Actuation` or `HapticDrive.Asio.Runtime` today would create a worse dependency direction or require widening the internal direct-runtime surface.
- Reduced `MainWindow.xaml.cs` to a thin consumer for this slice: it now constructs the coordinator, forwards `PaddleInputReceived` events into it, supplies current mapping/BST-1/direct-runtime/safety delegates, and performs UI-thread status refresh plus footer formatting from coordinator results.
- Removed the old paddle bench routing helper methods and the old paddle-input exception helper from `MainWindow.xaml.cs`. UI-only helpers such as status updates, footer formatting, settings parsing, and safety-context builders intentionally stayed in `MainWindow`.
- Added App-side coordinator tests covering no-startup-route behavior, left/right accepted live routes, disabled direct bench, disabled BST-1 bench, accepted direct bench routing into the existing shared direct runtime path, safe capture of route exceptions, stop-all recovery for direct-bench failures, and UI-update recovery with `stopAllIfPulseMayHaveStarted: false`.
- Added App-side guardrails proving the temporary App coordinator has no WPF/control references in source and that `MainWindow.xaml.cs` no longer declares the old substantive paddle bench helper methods.
- Updated `ARCHITECTURE.md`, `ROADMAP.md`, and `KNOWN_ISSUES.md` with the Stage 19D extraction result, the temporary App-home rationale, and the remaining Stage 20 target.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 724 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed.

Self-review:

- Stage 19D moved paddle routing ownership only. It intentionally did not change UI/XAML, paddle mappings, debounce defaults, left/right semantics, ASIO/BST-1 behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, direct gear-pulse tuning, latest-press-wins retrigger semantics, parser layouts, continuous road/slip/lock runtime behavior, or coexistence semantics.
- The temporary coordinator home is App-only but non-WPF by design. That is a deliberate stop point to avoid forcing concrete Windows direct-output and App-owned manual ASIO test dependencies into a broader runtime project.
- The shared BST-1/P-HPR slip/lock evaluator still does not exist. That remains the explicit Stage 20 target.
- No physical validation, safe physical gain claim, pedal feel claim, or latency claim is made here. The stage is architecture/risk-reduction work only.

## Stage 20 - Shared Slip/Lock Evaluator For BST-1 And P-HPR

Date: 2026-06-17

Status: Complete.

Goal: Replace the duplicated BST-1 and P-HPR slip/lock detection math with one shared, deterministic evaluator while preserving existing output-specific shaping, routing, safety, cadence, and user-facing behavior.

Changes:

- Re-checked the live Stage 19D baseline before coding. `SlipEffect.cs` and `PHprSlipLockRouter.cs` were both still evaluating driving-state mute, frame freshness, wheel slip ratio/angle thresholds, wheel-speed lock thresholds, speed scaling, low-pedal attenuation, TC attenuation, and ABS attenuation independently.
- Added `SlipLockEvaluator` under `HapticDrive.Asio.Core.Haptics` together with `SlipLockEvaluationOptions`, `SlipLockEvaluationInput`, `SlipLockEvaluationResult`, `SlipLockSignalResult`, `SlipLockSuppressionReason`, and `SlipLockWheelContribution`.
- Chose Core as the shared home because `HapticDrive.Asio.Audio -> HapticDrive.Asio.Core` and `HapticDrive.Actuation -> HapticDrive.Asio.Core` already exist, while Core still has no App, WPF, ASIO backend, or HID-output dependency.
- Moved the shared slip/lock driving-state mute, frame-freshness, value sanitization, wheel-order-preserving extraction, speed-scale, threshold, and TC/ABS attenuation math out of `SlipEffect.cs`.
- Refactored `SlipEffect.cs` to consume the shared evaluator for slip/lock detection and normalized intensity only while keeping BST-1 amplitude/frequency/noise shaping, dominant-source choice, response smoothing, sample generation, and snapshot wording in the audio layer.
- Moved the shared slip/lock telemetry extraction and normalized intensity evaluation out of `PHprSlipLockRouter.cs`.
- Refactored `PHprSlipLockRouter.cs` to consume the shared evaluator for direct slip/lock detection only while keeping direct safety-context gating, target-module mapping, per-module priority, minimum-route interval, gear-protection window, hold-timeout watchdog, explicit stop commands, road-yield interaction, command creation, and direct diagnostics wording in the router.
- Also refactored the older mock-only `PHprPedalEffectsRouter.cs` to consume the same shared evaluator for wheel slip / wheel lock detection so BST-1, replay-safe mock P-HPR, and real direct P-HPR now share one slip/lock model.
- Added Core-side evaluator tests for missing state, frame-lag freshness, low-speed suppression, normal no-slip/no-lock cases, active slip, active lock, TC/ABS attenuation, invalid-value sanitization, and wheel-order-preserving contribution diagnostics.
- Added integration tests proving `SlipEffect`, `PHprSlipLockRouter`, and `PHprPedalEffectsRouter` expose intensities consistent with the shared evaluator, plus project-graph guardrails for Core, Audio, Actuation, Runtime, and the real output assembly.
- Updated `ARCHITECTURE.md`, `ROADMAP.md`, and `KNOWN_ISSUES.md` with the new shared evaluator boundary, the consumer changes, and the unchanged behavior guarantees.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 740 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed.

Self-review:

- Stage 20 stayed focused on shared evaluation ownership. It intentionally did not change UI/XAML, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, direct gear-pulse tuning, gear routing, road cadence, slip/lock cadence, hold-timeout/watchdog durations, command-rate limiter behavior, parser layouts, or persistence schema.
- `SlipEffect` still owns BST-1 sample rendering and audio-facing diagnostics semantics, while `PHprSlipLockRouter` still owns direct-routing cadence, priority, road-yield, stop behavior, and HID-output handoff.
- The mock P-HPR slip/lock path now shares the same evaluator as the BST-1 and real direct paths, but this stage still does not attempt broader UI/runtime decomposition or MVVM work.
- No physical BST-1 shaker feel claim, physical P-HPR slip/lock feel claim, safe physical gain claim, or latency claim is made here. Local validation and later tuning are still required.

## Stage 21A - MainWindow Residual Orchestration Audit And Safe Workflow-Status Extraction

Date: 2026-06-17

Status: Complete.

Goal: Audit the remaining post-Stage-20 `MainWindow.xaml.cs` ownership, pick the lowest-risk non-MVVM extraction target, move only that slice into a testable non-WPF boundary, and add guardrails so workflow/report logic does not drift back into the window code-behind.

Changes:

- Re-audited the live Stage 20 baseline before coding. `MainWindow.xaml.cs` was 7027 lines and still owned startup/load wiring, shutdown/dispose ordering, settings/profile hydration, ASIO/BST-1/P-HPR control parsing, safety-context construction, diagnostics/report assembly, recording/replay UI workflow, and general WPF status/footer/dashboard updates.
- Confirmed the safe first extraction was not runtime ownership, hardware lifecycle, or safety-context building. Those still cross more volatile App/runtime/output seams. The lowest-risk path was the P-HPR workflow/status report assembly because it was already snapshot-driven and UI-only.
- Added `PhprWorkflowStatusSnapshotBuilder`, `PhprWorkflowStatusPresenter`, `PhprWorkflowStatusBuildInputs`, `PhprWorkflowStatusSnapshot`, and `PhprWorkflowStatusPresentation` in `src/HapticDrive.Asio.App/PhprWorkflowStatusPresenter.cs`.
- Moved the substantive P-HPR workflow summary/item text assembly out of `UpdatePhprWorkflowStatus()` and into the new non-WPF presenter. The presenter now owns safe fallback text, mock/direct/road/slip workflow line assembly, and reuse of the existing `PhprLiveF1ValidationGuide` output.
- Reduced `MainWindow.xaml.cs` to collecting current live snapshots/settings for that slice, calling the snapshot builder and presenter, and assigning the returned status/checklist strings to WPF controls.
- Intentionally kept `MainWindow.xaml.cs` responsible for live snapshot collection, `BuildPhprLiveF1ValidationSnapshot()`, WPF control assignment, settings parsing, safety-context builders, startup/shutdown sequencing, recording/replay UI flow, and the much larger diagnostics-panel assembly path.
- Added `PhprWorkflowStatusPresenterTests` for representative output equivalence, safe fallback behavior for missing/invalid snapshots, and emergency-stop propagation through workflow and validation text.
- Added `PhprWorkflowStatusPresenterGuardrailTests` to prove the new builder/presenter source stays free of WPF/control/hardware-writer references and that `MainWindow.xaml.cs` now routes the extracted workflow strings through the presenter instead of owning those inline strings directly.
- Updated `ARCHITECTURE.md`, `ROADMAP.md`, and `KNOWN_ISSUES.md` with the Stage 21A audit result, the new workflow-status seam, the explicit no-MVVM scope choice, and the recommended Stage 21B follow-up.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 746 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed.

Self-review:

- Stage 21A intentionally avoided a broad MVVM rewrite. It moved only one low-risk diagnostics/status seam and left runtime behavior ownership untouched.
- `MainWindow.xaml.cs` is still large after this stage because the bigger remaining responsibilities are startup/shutdown, settings parsing, safety-context construction, diagnostics-panel assembly, and recording/replay orchestration. Those are better Stage 21B+ targets than forcing them into this smaller stage.
- Stage 21A intentionally does not change UI/XAML, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, recording format, or replay timing.
- No physical BST-1 feel claim, physical P-HPR feel claim, safe physical gain claim, or latency claim is made here. Local validation is still required later.

## Stage 21B - Diagnostics Status Extraction Out Of MainWindow

Date: 2026-06-17

Status: Complete.

Goal: Extract the broader diagnostics/status report assembly around `UpdateDiagnosticsStatus()` out of `MainWindow.xaml.cs` into a testable non-WPF App-layer boundary while preserving the existing diagnostics meaning, copy-report content, and privacy/redaction behavior.

Changes:

- Re-audited the live Stage 21A baseline before coding. `MainWindow.xaml.cs` was 7044 lines and still owned diagnostics-panel visibility gating, snapshot gathering from pipeline/receiver/output/workflow state, road-recorder status text, the full ordered diagnostics item list, and clipboard-report generation.
- Confirmed the safe extraction target was the diagnostics/status assembly itself, not startup/shutdown orchestration, settings hydration, safety-context builders, ASIO start/stop ownership, Stop All / Emergency Stop handlers, or P-HPR runtime coordination.
- Added `DiagnosticsStatusSnapshotBuilder`, `DiagnosticsStatusPresenter`, `DiagnosticsStatusBuildInputs`, `DiagnosticsStatusSnapshot`, and `DiagnosticsStatusPresentation` in `src/HapticDrive.Asio.App/DiagnosticsStatusPresenter.cs`.
- Moved diagnostics summary text assembly, road-recorder status text assembly, ordered diagnostics item/report assembly, clipboard report generation, and safe fallback text into the new non-WPF presenter.
- Reduced `MainWindow.xaml.cs` to diagnostics visibility gating, live snapshot collection, helper subsection formatting, presenter input construction, and WPF control assignment for diagnostics output.
- Refactored `CopyDiagnosticsButton_Click` to use the presenter-generated clipboard report text instead of rebuilding the report from WPF control state with an inline `StringBuilder`.
- Extended `PhprWorkflowStatusPresenter` so it now also emits the already-sanitized diagnostics lines for profile persistence, workflow mode/report state, and live F1 validation. `UpdateDiagnosticsStatus()` now reuses that Stage 21A presenter output instead of calling `PhprWorkflowDiagnosticsReport` / `PhprLiveF1ValidationGuide` inline.
- Added `DiagnosticsStatusPresenterTests` for representative summary/order/report output plus safe fallback behavior, and `DiagnosticsStatusPresenterGuardrailTests` to prove the new presenter stays free of WPF/control/hardware-write references while `MainWindow.xaml.cs` routes the diagnostics workflow strings through the new presenter path.
- Expanded `PhprWorkflowStatusPresenterTests` to cover the new diagnostics-line outputs so the Stage 21A presenter remains the single formatting owner for those sanitized workflow diagnostics strings.
- Updated `ARCHITECTURE.md`, `ROADMAP.md`, and `KNOWN_ISSUES.md` with the new diagnostics presenter seam, the continued non-MVVM scope, and the recommended Stage 21C follow-up.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 750 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed.

Self-review:

- Stage 21B intentionally does not move hardware/runtime ownership, change diagnostics wording semantics, or alter privacy boundaries. Local paths remain filename-only where already sanitized, private HID paths remain withheld, and clipboard reports still avoid raw captures, serials, and unsanitized inventories.
- `MainWindow.xaml.cs` is still large after this stage because snapshot gathering, helper subsection formatting, settings shaping, safety-context construction, startup/shutdown orchestration, and recording/replay workflow still live there. Those remain follow-up work rather than being forced into this stage.
- `MainWindow.xaml.cs` line count moved from 7044 to 7043 in this stage. The benefit here is ownership clarity and testability rather than a dramatic line-count drop.
- Stage 21B intentionally does not change UI/XAML, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, recording format, or replay timing.
- No physical BST-1 feel claim, physical P-HPR feel claim, safe physical gain claim, or latency claim is made here. Local validation is still required later.

## Stage 21C - Settings Hydration And Persisted-Settings Status Extraction

Date: 2026-06-17

Status: Complete.

Goal: Extract the safest remaining app/settings hydration and persisted-settings status-shaping seam out of `MainWindow.xaml.cs` into a testable App-layer boundary while preserving all current persistence semantics, privacy boundaries, and startup-output safety guarantees.

Changes:

- Re-audited the live Stage 21B baseline before coding. `MainWindow.xaml.cs` was 7043 lines and still owned `AppSettings` load/save shaping, persisted-settings footer text, persisted-settings diagnostics text, safe P-HPR settings mapping, and a large block of App-only settings conversion helpers.
- Confirmed the safe extraction target was the app-settings hydration/save mapping plus the persisted-settings status/diagnostics text, not startup/shutdown sequencing, WPF control assignment, safety-context construction, ASIO start/stop ownership, profile lifecycle ownership, or P-HPR runtime coordination.
- Added `AppSettingsSnapshotBuilder`, `PersistedSettingsStatusPresenter`, `AppSettingsHydrationSnapshot`, `AppSettingsSaveInputs`, `PersistedSettingsStatusSnapshot`, and `PersistedSettingsStatusPresentation` in `src/HapticDrive.Asio.App/AppSettingsSnapshotBuilder.cs`.
- Moved the safe `AppSettings` sanitization/hydration shaping for output mode, ASIO driver/channel readiness preference, replay timing preference, forwarding destinations, paddle mapping, BST-1 local gear settings, shift-intent settings, mock P-HPR routing settings, and real P-HPR effect settings out of `MainWindow.xaml.cs`.
- Moved the save-side shaping from current shell/runtime preference snapshots back into `AppSettings` out of `SaveAppSettings()` and the old inline conversion helpers in `MainWindow.xaml.cs`.
- Moved the persisted-settings footer text and diagnostics text assembly out of `UpdateProfileStatus()` and `BuildDiagnosticsStatusPresentation()` into `PersistedSettingsStatusPresenter`, preserving the existing wording about runtime-only state not being saved.
- Reduced `MainWindow.xaml.cs` to loading the hydration snapshot, gathering current settings/runtime snapshots for save/status presentation, calling the new builder/presenter, and assigning the returned values to WPF controls or existing runtime/profile paths.
- Refactored `BuildPhprEffectProfileFromCurrentSettings()` and `ApplyPhprEffectProfileToRuntime()` to reuse the same App-layer settings builder conversions instead of keeping duplicate `MainWindow` conversion logic.
- Added `AppSettingsSnapshotBuilderTests` for representative safe hydration mapping, safe save mapping that drops runtime-only direct-control/private-device state, and representative persisted-settings status/diagnostics text equivalence.
- Added `AppSettingsSnapshotBuilderGuardrailTests` to prove the new builder/presenter source stays free of WPF-control references, HID writers, ASIO output classes, runtime start/stop calls, and `EmergencyStop`, and to prove `MainWindow.xaml.cs` now routes the extracted settings/status shaping through the builder/presenter path.
- Expanded `AppSettingsStoreTests` with an additional privacy test proving app-settings JSON still does not persist private HID-like paths, validation-artifact paths, or capture-artifact strings.
- Updated `ARCHITECTURE.md`, `ROADMAP.md`, and `KNOWN_ISSUES.md` with the Stage 21C extraction result, the App-home rationale, the allowed persisted-settings set, the runtime-only non-persisted state list, and the recommended Stage 21D follow-up.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 756 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed.

Self-review:

- Stage 21C intentionally avoided a broad MVVM rewrite. It moved one persistence-focused App seam and left startup/shutdown, safety-context, runtime coordination, and WPF control ownership where they already were.
- `MainWindow.xaml.cs` remains large after this stage because it still owns control assignment, replay-control reads, profile lifecycle, live snapshot gathering, safety-context builders, startup/shutdown sequencing, and output/runtime ownership. Those remain better Stage 21D+ targets than forcing them into this settings-focused stage.
- `MainWindow.xaml.cs` line count moved from 7043 to 6834 in this stage. The drop comes mostly from removing the large in-window settings conversion block while keeping behavior unchanged.
- Stage 21C intentionally does not change UI/XAML, app-settings/profile schemas, `.hdrec` format, replay timing behavior, startup/shutdown ordering, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, or privacy/redaction boundaries.
- No physical BST-1 feel claim, physical P-HPR feel claim, safe physical gain claim, or latency claim is made here. Local validation is still required later.

## Stage 21D - Control Settings Parsing And Hydration-Application Extraction

Date: 2026-06-17

Status: Complete.

Goal: Extract the remaining pure control-to-settings parsing, clamp/default shaping, and settings-to-plain-control-values hydration helpers out of `MainWindow.xaml.cs` into a testable App-layer boundary while leaving direct WPF reads/writes, runtime configure calls, lifecycle, and safety ownership unchanged.

Changes:

- Re-audited the live Stage 21C baseline before coding. `MainWindow.xaml.cs` was 6834 lines and still owned replay timing selection mapping, forwarding editor validation, paddle mapping parsing, mock routing parsing, real P-HPR control parsing, BST-1 manual/local pulse parsing, and several pure control-value formatting helpers.
- Confirmed the safe extraction target was not startup/shutdown, profile lifecycle, local gear test readiness, runtime configure calls, Stop All / Emergency Stop handlers, or safety-context construction. The low-risk seam was the primitive parsing and control-value formatting that could be expressed with plain records.
- Added `ControlSettingsSnapshotBuilder` and supporting records in `src/HapticDrive.Asio.App/ControlSettingsSnapshotBuilder.cs`.
- Moved pure replay timing mapping, shift-intent option construction, forwarding editor validation, paddle mapping parsing, mock gear/pedal routing parsing, normal P-HPR gear parsing, real direct-control report/selector parsing, real road/slip-lock parsing, BST-1 manual pulse parsing, and BST-1 local gear parsing out of `MainWindow.xaml.cs`.
- Moved the corresponding plain control-value formatting for paddle mapping, paddle bench settings, mock routing, real P-HPR controls, normal P-HPR controls, and BST-1 control hydration out of `MainWindow.xaml.cs`.
- Reduced `MainWindow.xaml.cs` to reading WPF primitives, passing them into `ControlSettingsSnapshotBuilder`, assigning the returned values back to WPF controls, and invoking the existing runtime/profile configure paths.
- Intentionally kept ComboBox item population, direct WPF writes, local gear test readiness, runtime/device/router `Configure(...)` calls, startup/shutdown sequencing, profile lifecycle, and safety-context builders in `MainWindow.xaml.cs`.
- Added `ControlSettingsSnapshotBuilderTests` for representative replay/forwarding, paddle mapping, mock routing, real P-HPR option parsing, and BST-1 parsing/hydration equivalence.
- Added `ControlSettingsSnapshotBuilderGuardrailTests` to prove the new builder stays free of WPF and hardware/runtime calls and that `MainWindow.xaml.cs` now routes the moved parsing/application logic through the new builder path.
- Updated `ARCHITECTURE.md`, `ROADMAP.md`, and `KNOWN_ISSUES.md` with the Stage 21D extraction result, the remaining deferred control seams, the App-home rationale, and the recommended Stage 21E follow-up.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 763 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed.

Self-review:

- Stage 21D intentionally avoided MVVM and lifecycle extraction. It moved only deterministic parsing/formatting seams and kept hardware/runtime ownership obvious in `MainWindow.xaml.cs`.
- `MainWindow.xaml.cs` remains large after this stage because it still owns direct WPF reads/writes, candidate/item-list binding, profile lifecycle, local gear test readiness, startup/shutdown sequencing, safety-context builders, and runtime/output configure calls. Those remain better Stage 21E+ targets than forcing them into this control-parsing stage.
- `MainWindow.xaml.cs` line count moved from 6834 to 6152 in this stage. Most of that drop comes from removing the large WPF-bound parsing and formatting helpers while preserving behavior.
- Stage 21D intentionally does not change UI/XAML, app-settings/profile schemas, `.hdrec` format, replay timing behavior, startup/shutdown ordering, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, or privacy/redaction boundaries.
- No physical BST-1 feel claim, physical P-HPR feel claim, safe physical gain claim, or latency claim is made here. Local validation is still required later.

## Stage 21E - Audio Profile Control Parsing And Application Extraction

Date: 2026-06-17

Status: Complete.

Goal: Extract the remaining pure audio-profile control parsing, validated profile application planning, clamp/default shaping, and profile display-text formatting out of `MainWindow.xaml.cs` into a testable App-layer boundary while leaving direct WPF reads/writes, profile file lifecycle, runtime apply/configure calls, lifecycle, and safety ownership unchanged.

Changes:

- Re-audited the live Stage 21D baseline before coding. `MainWindow.xaml.cs` was 6152 lines and still owned the full `BuildProfileFromControls()` / `ApplyProfileToControls()` audio-profile mapping path plus inline profile text formatting.
- Confirmed the safe extraction target was not save/load/reset event flow, profile store IO, `ApplyProfileToRuntime(...)`, startup/shutdown sequencing, local gear readiness, Stop All / Emergency Stop handlers, or safety-context construction. The low-risk seam was the deterministic audio-profile primitive mapping and profile-to-control/text planning.
- Added `AudioProfileControlSnapshotBuilder` and supporting records in `src/HapticDrive.Asio.App/AudioProfileControlSnapshotBuilder.cs`.
- Moved profile-name fallback, engine max-frequency pairing, effect/mixer/safety primitive mapping, and validated `HapticDriveProfile` construction out of `MainWindow.xaml.cs`.
- Moved the corresponding validated profile-to-plain-control-value hydration plan and profile display-text formatting out of `MainWindow.xaml.cs`.
- Reduced `MainWindow.xaml.cs` to reading WPF primitives into `AudioProfileControlInputs`, passing them into `AudioProfileControlSnapshotBuilder`, assigning returned control values/text back to WPF controls, and invoking the existing runtime/profile lifecycle paths.
- Intentionally kept direct WPF writes, profile file lifecycle, `ApplyProfileToRuntime(...)`, footer/profile status assignment, local gear test readiness, startup/shutdown sequencing, and safety-context builders in `MainWindow.xaml.cs`.
- Added `AudioProfileControlSnapshotBuilderTests` for representative audio-profile control mapping, clamp/default equivalence, legacy slip/lock fallback hydration, null/default profile planning, and safe serialized persistence boundaries.
- Added `AudioProfileControlSnapshotBuilderGuardrailTests` to prove the new builder stays free of WPF and hardware/runtime calls and that `MainWindow.xaml.cs` now routes the moved audio-profile parsing/application logic through the new builder path.
- Updated `ARCHITECTURE.md`, `ROADMAP.md`, and `KNOWN_ISSUES.md` with the Stage 21E extraction result, the remaining deferred orchestration seams, the App-home rationale, and the recommended Stage 21F follow-up.

Verification:

- `.\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config` passed.
- `.\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore` passed with 0 warnings and 0 errors.
- `.\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build` passed with 769 passing tests and 0 skipped tests.
- `.\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore` passed.
- `.\Run-HapticDrive.cmd -NoBuild -CheckOnly` passed and confirmed the WPF executable path.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- --help` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples` passed.
- `.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples` passed.

Self-review:

- Stage 21E intentionally avoided MVVM and lifecycle extraction. It moved only deterministic audio-profile parsing/application planning seams and kept hardware/runtime ownership obvious in `MainWindow.xaml.cs`.
- `MainWindow.xaml.cs` remains large after this stage because it still owns direct WPF reads/writes, profile lifecycle, runtime apply/configure calls, local gear test readiness, startup/shutdown sequencing, safety-context builders, and runtime/output ownership. Those are better Stage 21F+ audit targets than forcing them into this profile-mapping stage.
- `MainWindow.xaml.cs` line count moved from 6152 to 6124 in this stage. The reduction is intentionally modest because most of the safety here came from extracting mapping ownership rather than chasing raw line count.
- Stage 21E intentionally does not change UI/XAML, app-settings/profile schemas, `.hdrec` format, replay timing behavior, startup/shutdown ordering, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, or privacy/redaction boundaries.
- No physical BST-1 feel claim, physical P-HPR feel claim, safe physical gain claim, or latency claim is made here. Local validation is still required later.
