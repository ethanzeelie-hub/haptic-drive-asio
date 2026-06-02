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
