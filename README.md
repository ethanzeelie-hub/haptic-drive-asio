# Haptic Drive ASIO

Haptic Drive ASIO is a standalone Windows desktop application for low-latency sim-racing haptics through ASIO audio output.

The first target game is EA F1 25. The first intended hardware path is an M-Audio M-Track Solo / Duo ASIO interface feeding a Fosi BT20A amplifier and Dayton BST-1 bass shaker.

The M-Audio M-Track Solo interface, Fosi Audio BT20A amplifier, and Dayton BST-1 shaker chain are available locally, and the physical chain has been proven through SimHub. Haptic Drive ASIO still treats that hardware as optional: development and automated tests must work without any physical haptic hardware by using deterministic null output, fake ASIO backends, manual WASAPI debug fallbacks, graceful ASIO handling, and telemetry replay.

## Current Stage

Stage 25AF: Effects-status snapshot seam complete.

## Current Architecture Baseline

- F1 25 is still the only production game integration. Future game support is planned, runtime now consumes an injected game-telemetry adapter, and the app resolves the active adapter through a selected-game catalog path. The current catalog still contains F1 25 only.
- `HapticEffectEngine` now composes the shipped BST-1 effects through an internal registered-slot seam instead of hand-wired per-effect orchestration, and its snapshot now exposes a generic effect-activity summary list for presenter/report surfaces. Options, profiles, tuning panels, and detailed diagnostics still remain explicitly typed to the current effect set.
- BST-1 diagnostics and routing/mixer reporting now also share a structured effect-summary seam instead of each presenter assembling its own fixed-list effect string, which gives future effect additions one cleaner app-side reporting contract even though tuning UI and persisted options still remain explicitly typed.
- The Effects page status strip now also consumes a typed effect-summary list instead of keeping one more presenter-local fixed fallback string, further reducing app-side drift between effect surfaces without changing the current WPF card layout.
- The audio-profile control path now groups BST-1 effect control values and display text behind one typed app-side snapshot, so profile-load/save/hydration work no longer has to keep its effect control contract completely flat even though the current WPF controls and persisted JSON schema stay unchanged.
- That same audio-profile path now also groups the BST-1 effect input capture contract, so profile-apply/save flow no longer passes one giant flat effect-input bag through the builder even though the current WPF control layout and persisted JSON schema stay unchanged.
- The Effects page status presentation path now also builds its typed effect-status snapshot through a dedicated app-side builder instead of leaving the runtime/options-to-status mapping embedded in `MainWindow`, which gives future BST-1 effect growth one cleaner status assembly seam without changing the current WPF cards or presenter text.
- Replay from `.hdrec` files now streams packets directly from disk through the replay service instead of fully materializing the whole recording first.
- Live recording now uses a bounded background queue with queue-capacity and dropped-packet diagnostics instead of the earlier unbounded queue model, and the recording library now surfaces streamed duration/payload/sequence-gap health summaries plus in-app filterable query text without loading whole recordings into memory.
- Recording summaries now also expose streamed sequence-range and approximate packet-rate metadata, so the library can surface richer per-file health/search hints without coupling the recording core to a game-specific parser.
- The Telemetry / UDP recording library now adds on-demand selected-recording packet histograms plus a first-pass packet preview for F1 25 captures in the app layer, and the selected detail can now be copied or exported as a sanitized local text artifact. That app-side inspection path now also has a structured analysis seam underneath the text formatting, which sets up deeper future browse/index features without pushing game-specific analysis down into the generic recording assembly.
- App settings, audio profiles, and P-HPR effect profiles now save through atomic same-directory temp-file replacement, and app settings now persist an explicit schema version marker for future migrations.
- App settings, audio profiles, and P-HPR effect profiles now share a version-migration planning seam, and legacy version-0 documents are upgraded safely to the current schema baseline instead of each store hand-rolling its own fallback.
- App settings, audio profiles, and P-HPR effect profiles now also refresh a last-known-good backup snapshot plus a small retained backup-history set after successful saves, and those stores can recover from either fallback path when the primary persisted document is missing, corrupt, or unsupported.
- Local and GitHub Actions packaging now share a real publish path through `Publish-HapticDrive.ps1`, producing a `win-x64` framework-dependent zip artifact plus checksum, JSON manifest, and Markdown release summary under `artifacts/release/`.
- Release packaging now also includes a repo-native smoke check through `Test-ReleaseArtifact.ps1`, which verifies the publish folder, zip payload, checksum, manifest, release summary, and extracted artifact structure both locally and in the packaging workflow.
- Local release preparation now also has a single staged-release command through `Prepare-ReleaseArtifact.ps1`, which runs restore/build/test/format/launch-preflight, publishes, smoke-checks, and gathers the final release files into `artifacts/staged-release/`.
- Advanced / Diagnostics can now export a private local support-bundle zip under `local-validation-results/support-bundles/`, containing sanitized diagnostics text plus structured summary/manifest files and optional selected-recording detail text without attaching raw captures or private device paths.
- `NullAudioOutputDevice` remains the default output so the app and automated tests work without ASIO hardware, shaker hardware, or Simagic hardware.
- ASIO remains explicit opt-in. The app does not auto-start ASIO, auto-arm ASIO, or auto-switch away from Null output.
- Simagic P-HPR remains a separate non-audio actuator path. It is not routed through ASIO or `IAudioOutputDevice`.
- `MainWindow.xaml.cs` still remains the deliberate composition/runtime shell for startup, shutdown, Start/Stop/Emergency execution, telemetry startup, and cross-page orchestration. The Stage 23/24 extraction stream reduced presentation ownership without forcing a broad MVVM rewrite.
- Raw UDP forwarding and recording still preserve packet bytes independently of parser or `VehicleState` success.
- Hardware-capable and physical-validation claims remain intentionally conservative. Physical shaker feel, safe gain, physical latency, and final tuning still require local hardware validation.

The app currently opens to a WPF shell with dashboard, navigation pages, global start/stop, emergency mute, dark theme default, persisted light/dark theme setting, safe tuning controls, profile save/load/reset, runtime diagnostics, recording/replay library controls, persisted UDP forwarding destination controls, ASIO driver visibility diagnostics, and explicit ASIO output readiness controls.

The selected output mode is `NullAudioOutputDevice` by default, so the app can open and tests can run without ASIO hardware or shaker hardware.

The official F1 25 v3 PDF has been extracted into implementation notes under `docs/`. The app now starts a raw UDP listener on port `20778` by default, counts incoming datagrams, tracks packet rate, shows a no-packet warning in the dashboard, offers each raw packet to the UDP forwarder, can record incoming raw UDP payload bytes to a versioned replay file, validates F1 25 packet headers, parses the Stage 07 core packet bodies, and maps parsed packets into shared last-known `VehicleState` samples.

Forwarding and recording preserve exact packet payload bytes and do not depend on parser or VehicleState success. Replay emits recorded packets back as `UdpTelemetryPacket` values without UDP sockets. Stage 15 adds a runtime coordinator that feeds both live and replayed packets through the same parser, VehicleState adapter, existing effects, mixer, safety chain, and `NullAudioOutputDevice` path. Start/Stop Haptics controls that software pipeline, Emergency Mute immediately silences it, profile tuning affects the active effect/mixer/safety configuration, and Null output diagnostics report deterministic consumed buffers, samples, and peaks.

Stage 16 adds Windows ASIO driver-name discovery, explicit output-mode selection, explicit ASIO driver selection, explicit output-channel selection, explicit arming, mono-to-selected-channel routing behind the output abstraction, readiness diagnostics, and fake ASIO lifecycle/failure tests.

Stage 17 adds an NAudio-backed native ASIO streaming backend behind `IAsioOutputBackend`, moves live haptic rendering off the WPF `DispatcherTimer` into an output-owned render loop, and adds stale telemetry wall-clock mute plus callback/render diagnostics for render callbacks, backend callbacks, submitted buffers, dropped buffers, underruns, render duration, jitter, and telemetry age. The audio render callback fills in-memory buffers only; UI, disk IO, logging, networking, blocking waits, and async continuations stay outside that path. The default output remains `NullAudioOutputDevice`; the app never auto-switches to ASIO or WASAPI. The M-Audio M-Track Solo is available on the user's PC, but M-Audio absence must not break build/test/CI. Windows sound output visibility is not proof of ASIO usage; ASIO must be confirmed through the app's ASIO driver/output path.

Stage 18 adds a root launch script with .NET 8 Desktop Runtime preflight, app-settings persistence separate from haptic profiles, persisted UDP forwarding destination editing, a recordings library with metadata summaries and selected replay, packet-ID diagnostics, diagnostics copy/report support, and final pre-shaker UI/documentation cleanup. ASIO output still requires explicit output mode selection, driver selection, channel selection, arming, and Start Haptics.

Stage 18 manual validation follow-up adds two local validation surfaces. `Manual ASIO Bass Shaker Test` routes short 40 Hz or 50 Hz sine pulses through the selected M-Audio / M-Track ASIO driver, selected output channel, Stage 10 mixer, safety chain, limiter, and ASIO output device after ASIO output is selected, armed, and running. The existing synthetic benchmark remains bound to Null output for deterministic automated checks. Stage 18b simplifies `Paddle Gear Bench Test` for mapped GT Neo paddles: it is enabled, auto-armed, and Direct-mode by default, uses Devices-page brake/throttle P-HPR gear-pulse strength/frequency/duration values as the single source of truth, and defaults left/right paddle buttons to 14/13. Startup may auto-refresh input and direct P-HPR candidates, auto-select the known `VID_3670/PID_0905` HID device-interface candidate by FeatureReport `0xF1` / 64-byte capability, then run no-output open-check and dry-run readiness checks in the background. Startup never sends a P-HPR vibration command. Direct bench output still requires FeatureReport `0xF1`, 64-byte shape, successful open-check, clear coexistence, clear emergency stop, and disabled road/slip/lock routes. Normal live-driving shift intent still requires `DrivingArmed` and recent valid telemetry.

Stage 2A starts the Simagic P-HPR / GT Neo paddle-input phase with documentation and safety gates only. Stage 2B adds safe input and P-HPR abstraction projects, a mock-only P-HPR output skeleton, conservative P-HPR safety defaults, and focused model tests. Stage 2C adds a cached `DrivingArmedStateService` that evaluates existing `VehicleState` and runtime snapshots for fresh active-driving telemetry before future paddle pulses may route. Stage 2D adds read-only Windows input discovery snapshots, Raw Input metadata enumeration, Windows game-controller capability enumeration, candidate scoring for likely Simagic / Alpha / GT Neo / P700 devices, and a manual Devices-page diagnostics refresh. Stage 2E adds a read-only Windows game-controller paddle listener, manual left/right button mapping, rising-edge detection, conservative debounce, UTC plus stopwatch timestamps, safe disconnect/error diagnostics, and local app-settings persistence for input mapping only. Stage 2F adds the Shift Intent Event Layer: mapped paddle presses are evaluated against cached `DrivingArmed` state, accepted/suppressed diagnostics are recorded, `InstantPaddleOnly` is the default mode, `TelemetryConfirmedOnly` remains diagnostic-only, and `InstantWithRejectedShiftFeedback` records a future pending-confirmation count without feedback output. Stage 2G adds a read-only Simagic P700 / P-HPR research utility with sanitized inventory exports, PnP/HID/USB registry metadata collection, reuse of existing input discovery metadata, candidate classification, redaction, and hardware-free tests. Stage 2H adds capture workflow documentation, required scenario definitions, metadata templates, filename building, metadata validation, sanitized manifest export, CLI commands, ignored metadata output paths, and hardware-free tests. Stage 2I adds read-only capture analysis for Wireshark CSV/text summaries, payload fingerprinting, byte-diff observations, pcap/pcapng container summaries, sanitized JSON exports, and hardware-free tests. Stage 2J adds formal protocol hypothesis records, sanitized evidence docs, JSON/Markdown hypothesis export commands, and hardware-free tests. Stage 2K adds mock-only SimHub F1 EC protocol records, mock encoding/decoding, deterministic duration planning, SimProUnknownMock classification, mock output frame diagnostics, and safe mock protocol CLI examples. Stage 2L adds `PHprSafetyLimiter`, safety decisions/context/snapshots, deterministic rate and continuous-duration limiting, emergency-stop latching/clear behavior, real-write blocking diagnostics, and a safety-limited mock output wrapper. Stage 2M adds mock-only gear pulse routing from accepted `ShiftIntentEvent` values through the Stage 2L safety-limited mock output path, with Devices-page diagnostics and mock-only routing preferences. Stage 2N adds mock-only road vibration, wheel slip, and wheel lock routing from existing `VehicleState` / pipeline snapshots through the same safety-limited mock output stack, with priority, interval suppression, Devices-page diagnostics, and safe mock preferences. Stage 2O adds read-only SimPro Manager / SimHub process detection, WPF coexistence diagnostics, and safety-context conflict status wiring. Stage 2P adds the controlled write test plan, manual validation runbook, no-write readiness model, and disabled WPF direct-write readiness diagnostics. Stage 2Q adds a gated write-capable Windows HID P-HPR adapter, SimHub F1 EC report encoder, runtime-only direct-control UI, fake-writer tests, emergency-stop stop reports, and accepted-paddle direct gear-pulse routing that remains disabled/unarmed by default. Stage 2R adds a controlled validation harness, checklist readiness model, WPF result-entry/export surface, private local Markdown result export, and fake-only tests. Phase 3A hardens the direct-output adapter with explicit open/write/close lifecycle, timeout handling, selected-interface/report validation, disconnect classification, close-on-dispose behavior, and richer WPF diagnostics. Phase 3B completes instant paddle gear-pulse production integration with independent brake/throttle settings, safe settings persistence, default same up/down pulse, no telemetry-confirmation wait, and software latency trace diagnostics. Phase 3C adds production road-vibration routing through `PHprRoadVibrationRouter`, independent brake/throttle road scaling settings, safe settings persistence, deterministic route-interval suppression, and fake-real writer tests while leaving the ASIO/BST-1 road texture effect unchanged. Phase 3D adds production wheel-slip and wheel-lock routing through `PHprSlipLockRouter`, safe target/strength/frequency/duration persistence, priority above road and below gear pulse, and mock/fake-real writer tests while leaving the ASIO/BST-1 slip effects unchanged. Phase 3E adds a P-HPR workflow summary, P-HPR effect profile save/load, richer diagnostics report coverage, and full user-guide workflow coverage while preserving runtime-only direct-control arming/device state. Phase 3F adds deterministic replay validation for P-HPR road, slip, and lock routing from recorded telemetry, replay-source diagnostics, `DrivingArmed` replay checks, stale/emergency/profile-setting tests, and no synthetic gear-paddle events. Phase 3G adds a passive live F1 25 validation checklist and diagnostics line covering telemetry, `DrivingArmed`, paddle listener, P-HPR mode, coexistence, emergency stop, mock gear diagnostics, real manual arming, road/slip/lock checks, menu suppression, and conflict warnings. Phase 3H adds final quick-start, troubleshooting, acceptance checklist, safety review, and final documentation package. Phase 3I simplifies the app UI into normal Devices controls for ASIO/BST-1, P-HPR pedals, and wheel paddles; moves research internals behind persisted Advanced diagnostics; and changes P-HPR UI settings to 0-100% strength, 1-50 Hz frequency, and 10-1000 ms duration. Phase 3J adds the final `controlled-write-test` CLI, fake-writer coverage, and zero-skip test reporting while keeping real P-HPR execution gated by `--execute`, selected private HID path, clear coexistence, and the exact approval phrase. P-HPR is a separate non-audio actuator path, not an ASIO or `IAudioOutputDevice` output. User-run local validation has confirmed brake and throttle direct pulses plus parameter response on the selected FeatureReport path, but no safe-gain, physical latency, sustained-output, road/slip/lock feel, or final tuning claim has been made.

Phase 3J follow-up: direct P-HPR output selection now distinguishes Raw Input metadata, safe HID registry metadata, and openable Windows HID device-interface candidates. Raw Input and registry metadata-only candidates cannot pass real direct-output gates. `can pulse` requires a successful no-report open-check plus selected HID output-report or feature-report capability, matching selected transport, successful no-command report-shape validation, clear coexistence, and clear emergency stop. The current `VID_3670/PID_0905` path can surface feature report ID `0xF1`, which is shown as the likely F1 EC SET_REPORT-style transport without changing the confirmed F1 EC protocol bytes. Stage 18b direct starts now schedule matching stop reports after `DurationMs`, and emergency stop or disposal cancels pending stop work and sends stop reports when active.

The app does not yet implement advanced routing matrices, live graphing, real WASAPI output, physical shaker calibration, or physical shaker validation. Physical shaker feel, safe gain, physical latency, and final frequency tuning remain unvalidated until the Dayton BST-1 arrives and the full chain is tested locally.

## Verification

Use the serial verification path below before closing a stage or opening a pull request:

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet-home'
& .\.dotnet\dotnet.exe restore HapticDrive.Asio.sln --configfile NuGet.Config
& .\.dotnet\dotnet.exe build HapticDrive.Asio.sln --no-restore -warnaserror
& .\.dotnet\dotnet.exe test HapticDrive.Asio.sln --no-build
& .\.dotnet\dotnet.exe format HapticDrive.Asio.sln --verify-no-changes --no-restore
.\Run-HapticDrive.cmd -NoBuild -CheckOnly
```

Warnings now fail the build by default. If a local investigation needs a temporary one-off escape hatch, use `-p:HapticDriveRelaxWarningsAsErrors=true` on that local build command rather than changing repository policy.

## Solution Layout

- `src/HapticDrive.Asio.App`: WPF desktop app.
- `src/HapticDrive.Asio.Core`: shared domain models and interfaces.
- `src/HapticDrive.Asio.Telemetry.F1_25`: F1 25 telemetry parser and adapter.
- `src/HapticDrive.Asio.Audio`: audio output abstractions, ASIO readiness seams, native ASIO backend, output-owned render loop support, mixer, safety chain, test bench, Stage 12 / Stage 13 effect generators, Stage 14 profiles, and audio diagnostics.
- `src/HapticDrive.Asio.Runtime`: end-to-end pipeline coordinator for live/replay telemetry, parser, VehicleState, effects, mixer, safety, recording, forwarding, output-owned rendering, stale telemetry mute, and the manual ASIO hardware test signal injection path.
- `src/HapticDrive.Asio.Recording`: telemetry recording and replay.
- `src/HapticDrive.Input.Abstractions`: read-only input discovery snapshots, candidate scoring, paddle mapping/listener diagnostics, paddle shift-intent contracts for later routing, and cached driving-state contracts for Phase 2.
- `src/HapticDrive.Input.Windows`: read-only Windows Raw Input and game-controller discovery plus Stage 2E Windows game-controller button-state reading; it does not send device commands or route haptics.
- `src/HapticDrive.Simagic.PHPR.Abstractions`: non-audio P-HPR command, coexistence detection, controlled-write readiness, safety limiter, safety-limited mock output, output, mock protocol, and mock-output contracts.
- `src/HapticDrive.Simagic.PHPR.Output.Windows`: gated Windows HID P-HPR direct-output adapter, SimHub F1 EC report encoder, HID output/feature-report transport selection, fakeable report writer lifecycle boundary, direct gear-pulse router, Phase 3A connection/write diagnostics, Phase 3B instant-shift latency traces, and the shared backend used by Phase 3C road-vibration and Phase 3D slip/lock routing.
- `src/HapticDrive.Simagic.PHPR.Research`: Stage 2G read-only P700 / P-HPR inventory utility, Stage 2H capture metadata tooling, Stage 2I sanitized capture analysis tooling, Stage 2J hypotheses, Stage 2K mock protocol examples, Stage 2L safety examples, and the Phase 3J `controlled-write-test` command.
- `src/HapticDrive.Actuation`: cached driving-state, Stage 2F shift-intent event evaluation, local paddle gear bench validation, Stage 2M mock-only P-HPR gear pulse routing, Stage 2N mock-only road/slip/lock pedal-effect routing, Phase 3C production road-vibration routing, Phase 3D production slip/lock routing, and Phase 3F replay-validation coverage that stay separate from the ASIO audio path.
- `tests/*`: xUnit test projects.

## Simagic Phase 2 Docs

- `docs/SIMAGIC_P_HPR_PHASE_2_RESEARCH.md`: Phase 2 baseline, hardware context, boundaries, and readiness notes.
- `docs/SIMAGIC_USER_DATA_REQUEST.md`: requested SimPro, SimHub, Windows, USBView, and mapping data.
- `docs/SIMAGIC_CAPTURE_GUIDE.md`: capture naming, metadata, and raw-capture handling rules.
- `docs/SIMAGIC_CAPTURE_ANALYSIS.md`: Stage 2I read-only capture analysis commands, outputs, and safety boundary.
- `docs/SIMAGIC_PROTOCOL_HYPOTHESES.md`: Stage 2J protocol hypotheses, confidence levels, Stage 2K mock boundary, and real-write blockers.
- `docs/SIMAGIC_P_HPR_MOCK_PROTOCOL.md`: Stage 2K mock-only protocol, SimHub F1 EC fixture bytes, duration scheduling, SimProUnknownMock status, and mock output diagnostics.
- `docs/SIMAGIC_P_HPR_SAFETY_LAYER.md`: Stage 2L safety limiter, context gates, emergency stop, diagnostics, and no-write boundary.
- `docs/SIMAGIC_P_HPR_MOCK_GEAR_ROUTING.md`: Stage 2M mock-only gear pulse routing from accepted shift intent through the safety-limited mock output path.
- `docs/SIMAGIC_P_HPR_MOCK_PEDAL_EFFECTS_ROUTING.md`: Stage 2N mock-only road vibration, wheel slip, and wheel lock routing through the safety-limited mock output path.
- `docs/SIMAGIC_SIMPRO_SIMHUB_COEXISTENCE.md`: Stage 2O read-only SimPro Manager / SimHub process detection and safety warning behaviour.
- `docs/SIMAGIC_P_HPR_CONTROLLED_WRITE_TEST_PLAN.md`: Stage 2P no-write controlled write test plan and evidence map.
- `docs/SIMAGIC_P_HPR_MANUAL_VALIDATION_RUNBOOK.md`: Stage 2P manual validation sequence and result template for later local testing.
- `docs/SIMAGIC_P_HPR_REAL_WRITE_IMPLEMENTATION.md`: Stage 2Q gated Windows HID implementation boundary and safety gates.
- `docs/SIMAGIC_P_HPR_OUTPUT_ADAPTER.md`: Phase 3A production adapter hardening, lifecycle, failure handling, and diagnostics.
- `docs/SIMAGIC_P_HPR_INSTANT_SHIFT_GUIDE.md`: Phase 3B instant paddle gear-pulse routing, per-pedal settings, persistence, diagnostics, and non-claims.
- `docs/SIMAGIC_P_HPR_ROAD_VIBRATION_GUIDE.md`: Phase 3C road-vibration routing, scaling settings, gates, persistence, and ASIO separation.
- `docs/SIMAGIC_P_HPR_SLIP_LOCK_GUIDE.md`: Phase 3D wheel-slip and wheel-lock routing, settings, gates, priority, persistence, and ASIO separation.
- `docs/SIMAGIC_P_HPR_UI_PROFILES_DIAGNOSTICS.md`: Phase 3E P-HPR workflow summary, profiles, diagnostics, and report coverage.
- `docs/SIMAGIC_P_HPR_REPLAY_VALIDATION.md`: Phase 3F replay-driven P-HPR road/slip/lock validation, diagnostics, and non-claims.
- `docs/SIMAGIC_P_HPR_LIVE_F1_VALIDATION.md`: Phase 3G manual live F1 25 validation workflow, app checklist, diagnostics, and non-claims.
- `docs/QUICK_START.md`: final app and P-HPR quick-start path.
- `docs/TROUBLESHOOTING.md`: no-vibration, wrong-pedal, menu-suppression, conflict, device-selection, telemetry, replay, and emergency-stop troubleshooting.
- `docs/FINAL_P_HPR_ACCEPTANCE.md`: final feature, safety, verification, and manual acceptance checklist.
- `docs/SIMAGIC_P_HPR_USER_GUIDE.md`: Stage 2Q Devices-page direct-control guide and manual-use cautions.
- `docs/SIMAGIC_P_HPR_CONTROLLED_REAL_VALIDATION.md`: Stage 2R validation harness, result export, pass gate, and physical-validation status.
- `docs/USER_GUIDE.md`: app-level user guide covering safe startup, ASIO separation, P-HPR direct control, and validation harness.
- `docs/SIMAGIC_USB_DEVICE_INVENTORY.md`: Stage 2G read-only P700 / P-HPR inventory status, tooling command, missing data, and optional user checklist.
- `docs/SIMAGIC_WHEEL_INPUT_RESEARCH.md`: read-only GT Neo paddle input discovery plan.
- `docs/SIMAGIC_SHIFT_INTENT_DESIGN.md`: instant paddle shift-intent design and `DrivingArmed` gating.
- `docs/SIMAGIC_P_HPR_SAFETY_PLAN.md`: P-HPR write gate and actuator safety plan.

## Simagic Stage 2H Capture Metadata Commands

These commands are workflow/metadata-only. They do not parse captures, send USB writes, issue output or feature reports, create P-HPR commands, or vibrate hardware.

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-scenarios
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-template --scenario BrakeTestVibration --target Brake
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- validate-capture-metadata <path>
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-manifest <metadata-folder>
```

Default generated metadata output is under ignored `capture-metadata/generated/`. Raw captures remain private and uncommitted.

## Simagic Stage 2I Capture Analysis Commands

These commands are read-only analysis tools. They summarize sanitized Wireshark exports and pcap containers, but do not send USB writes, issue output or feature reports, create P-HPR commands, create protocol hypotheses, or vibrate hardware.

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-analysis <capture-or-export-path>
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-diff <left-capture-or-export-path> <right-capture-or-export-path>
```

Default generated analysis output is under ignored `capture-metadata/generated/`. Stage 2J documents protocol hypotheses separately.

## Simagic Stage 2J Protocol Hypothesis Commands

These commands export sanitized hypothesis records only. They do not send USB writes, issue output or feature reports, create production protocol adapters, create live `PHprCommand` values, or vibrate hardware.

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- hypotheses-list
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- hypotheses-export --output capture-metadata\generated\simagic-protocol-hypotheses.json
```

Default generated hypothesis exports should remain under ignored `capture-metadata/generated/`.

## Simagic Stage 2K Mock Protocol Commands

These commands display or export sanitized mock protocol examples only. They do not send USB writes, issue output or feature reports, create production protocol adapters, route haptics, or vibrate hardware.

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-export --output capture-metadata\generated\simagic-mock-protocol-examples.json
```

Default generated mock protocol exports should remain under ignored `capture-metadata/generated/`.

## Simagic Stage 2L Safety Examples

This command displays mock safety-layer decisions only. It does not send USB writes, issue output or feature reports, create production protocol adapters, route haptics, or vibrate hardware.

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples
```

## Launch

Build/test commands do not open the desktop app. Use the Stage 18 launch wrapper:

```powershell
.\Run-HapticDrive.cmd
```

The wrapper runs `Run-HapticDrive.ps1` with a process-scoped PowerShell execution-policy bypass, so normal machine policy does not block launch. The script uses the repo-local `.dotnet` runtime, sets `DOTNET_ROOT`, checks for `Microsoft.WindowsDesktop.App 8.x`, builds the solution with `--no-restore`, and starts the WPF executable. If you have already built and only want to launch:

```powershell
.\Run-HapticDrive.cmd -NoBuild
```

To verify launch prerequisites without opening another app window:

```powershell
.\Run-HapticDrive.cmd -NoBuild -CheckOnly
```

Direct executable launch also works when .NET 8 Desktop Runtime is available to the app host:

```powershell
& .\src\HapticDrive.Asio.App\bin\Debug\net8.0-windows\HapticDrive.Asio.App.exe
```

## Mock Validation

Use `docs/STAGE_15_MOCK_PIPELINE.md` for the hardware-safe Stage 15 checklist. The short version: keep output on `NullAudioOutputDevice`, start haptics, use live UDP or replay/test bench, verify diagnostics and Emergency Mute, and do not treat M-Audio visibility or Windows sound output selection as proof of ASIO streaming.

## Manual ASIO Readiness

Use `docs/STAGE_16_ASIO_READINESS.md` and `docs/MANUAL_HARDWARE_TESTS.md` for the manual M-Audio/Fosi/BST-1 readiness checklist. Start from Null output, refresh ASIO diagnostics, select ASIO deliberately, select the M-Audio / M-Track driver deliberately, select one output channel deliberately, arm ASIO deliberately, then start haptics deliberately. Use `Manual ASIO Bass Shaker Test` only after the chain is physically connected and you deliberately want a short 40/50 Hz BST-1 pulse from this app.

## Stage 17 Streaming

Use `docs/STAGE_17_NATIVE_ASIO_STREAMING.md` for the pre-shaker streaming checklist and diagnostics. ASIO output is still explicit: select ASIO deliberately, select the driver, select one output channel, arm ASIO, then start haptics. Automated tests use Null output and fake ASIO backends; they do not require M-Audio, Fosi, Dayton BST-1, F1 25, or live telemetry.

## Stage 18 Final Pre-Shaker Package

Use `docs/STAGE_18_FINAL_PRE_SHAKER.md` for the final pre-shaker checklist. Stage 18 completes the pre-BT-1 software package around the existing engine: launch/runtime prerequisite handling, persisted app settings, forwarding destination UI, recording library UI, selected recording replay, packet-ID diagnostics, copyable diagnostics report, and final documentation cleanup. The follow-up adds manual ASIO hardware pulses and local paddle gear bench validation. Stage 18b keeps Null output as the audio default, leaves ASIO hardware opt-in and explicitly armed, and makes the paddle bench a runtime-only Direct-mode P-HPR workflow with startup readiness checks but no startup vibration.

## Build

This repository targets .NET 8 or newer on Windows.

```powershell
dotnet restore
dotnet build HapticDrive.Asio.sln
dotnet test HapticDrive.Asio.sln
```

If using the local SDK installed in this workspace by Codex:

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet-home'
& .\.dotnet\dotnet.exe test HapticDrive.Asio.sln
```

To produce a release-style publish zip locally:

```powershell
.\Publish-HapticDrive.ps1 -Configuration Release -Runtime win-x64
```

That writes the published app folder under `artifacts/publish/`, plus the zip package, SHA-256 checksum, JSON manifest, and Markdown release summary under `artifacts/release/`.

To smoke-check the produced release artifact locally:

```powershell
.\Test-ReleaseArtifact.ps1 -Runtime win-x64
```

To run the full local release-preparation flow and stage the final files together:

```powershell
.\Prepare-ReleaseArtifact.ps1 -Configuration Release -Runtime win-x64
```
