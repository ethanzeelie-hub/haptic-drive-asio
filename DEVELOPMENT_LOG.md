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
