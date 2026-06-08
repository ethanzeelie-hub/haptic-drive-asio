# Haptic Drive ASIO

Haptic Drive ASIO is a standalone Windows desktop application for low-latency sim-racing haptics through ASIO audio output.

The first target game is EA F1 25. The first intended hardware path is an M-Audio M-Track Solo / Duo ASIO interface feeding a Fosi BT20A amplifier and Dayton BST-1 bass shaker.

The M-Audio M-Track Solo interface and Fosi Audio BT20A amplifier are now available locally, but the Dayton BST-1 shaker has not arrived yet. Development must still work without any physical haptic hardware by using deterministic null output, manual WASAPI debug output, graceful ASIO handling, and telemetry replay.

## Current Stage

Stage 2O: SimPro / SimHub coexistence detection complete. Stage 18 remains the final Phase 1 pre-shaker readiness package.

The app currently opens to a WPF shell with dashboard, navigation pages, global start/stop, emergency mute, dark theme default, persisted light/dark theme setting, safe tuning controls, profile save/load/reset, runtime diagnostics, recording/replay library controls, persisted UDP forwarding destination controls, ASIO driver visibility diagnostics, and explicit ASIO output readiness controls.

The selected output mode is `NullAudioOutputDevice` by default, so the app can open and tests can run without ASIO hardware or shaker hardware.

The official F1 25 v3 PDF has been extracted into implementation notes under `docs/`. The app now starts a raw UDP listener on port `20778` by default, counts incoming datagrams, tracks packet rate, shows a no-packet warning in the dashboard, offers each raw packet to the UDP forwarder, can record incoming raw UDP payload bytes to a versioned replay file, validates F1 25 packet headers, parses the Stage 07 core packet bodies, and maps parsed packets into shared last-known `VehicleState` samples.

Forwarding and recording preserve exact packet payload bytes and do not depend on parser or VehicleState success. Replay emits recorded packets back as `UdpTelemetryPacket` values without UDP sockets. Stage 15 adds a runtime coordinator that feeds both live and replayed packets through the same parser, VehicleState adapter, existing effects, mixer, safety chain, and `NullAudioOutputDevice` path. Start/Stop Haptics controls that software pipeline, Emergency Mute immediately silences it, profile tuning affects the active effect/mixer/safety configuration, and Null output diagnostics report deterministic consumed buffers, samples, and peaks.

Stage 16 adds Windows ASIO driver-name discovery, explicit output-mode selection, explicit ASIO driver selection, explicit output-channel selection, explicit arming, mono-to-selected-channel routing behind the output abstraction, readiness diagnostics, and fake ASIO lifecycle/failure tests.

Stage 17 adds an NAudio-backed native ASIO streaming backend behind `IAsioOutputBackend`, moves live haptic rendering off the WPF `DispatcherTimer` into an output-owned render loop, and adds stale telemetry wall-clock mute plus callback/render diagnostics for render callbacks, backend callbacks, submitted buffers, dropped buffers, underruns, render duration, jitter, and telemetry age. The audio render callback fills in-memory buffers only; UI, disk IO, logging, networking, blocking waits, and async continuations stay outside that path. The default output remains `NullAudioOutputDevice`; the app never auto-switches to ASIO or WASAPI. The M-Audio M-Track Solo is available on the user's PC, but M-Audio absence must not break build/test/CI. Windows sound output visibility is not proof of ASIO usage; ASIO must be confirmed through the app's ASIO driver/output path.

Stage 18 adds a root launch script with .NET 8 Desktop Runtime preflight, app-settings persistence separate from haptic profiles, persisted UDP forwarding destination editing, a recordings library with metadata summaries and selected replay, packet-ID diagnostics, diagnostics copy/report support, and final pre-shaker UI/documentation cleanup. ASIO output still requires explicit output mode selection, driver selection, channel selection, arming, and Start Haptics.

Stage 2A starts the Simagic P-HPR / GT Neo paddle-input phase with documentation and safety gates only. Stage 2B adds safe input and P-HPR abstraction projects, a mock-only P-HPR output skeleton, conservative P-HPR safety defaults, and focused model tests. Stage 2C adds a cached `DrivingArmedStateService` that evaluates existing `VehicleState` and runtime snapshots for fresh active-driving telemetry before future paddle pulses may route. Stage 2D adds read-only Windows input discovery snapshots, Raw Input metadata enumeration, Windows game-controller capability enumeration, candidate scoring for likely Simagic / Alpha / GT Neo / P700 devices, and a manual Devices-page diagnostics refresh. Stage 2E adds a read-only Windows game-controller paddle listener, manual left/right button mapping, rising-edge detection, conservative debounce, UTC plus stopwatch timestamps, safe disconnect/error diagnostics, and local app-settings persistence for input mapping only. Stage 2F adds the Shift Intent Event Layer: mapped paddle presses are evaluated against cached `DrivingArmed` state, accepted/suppressed diagnostics are recorded, `InstantPaddleOnly` is the default mode, `TelemetryConfirmedOnly` remains diagnostic-only, and `InstantWithRejectedShiftFeedback` records a future pending-confirmation count without feedback output. Stage 2G adds a read-only Simagic P700 / P-HPR research utility with sanitized inventory exports, PnP/HID/USB registry metadata collection, reuse of existing input discovery metadata, candidate classification, redaction, and hardware-free tests. Stage 2H adds capture workflow documentation, required scenario definitions, metadata templates, filename building, metadata validation, sanitized manifest export, CLI commands, ignored metadata output paths, and hardware-free tests. Stage 2I adds read-only capture analysis for Wireshark CSV/text summaries, payload fingerprinting, byte-diff observations, pcap/pcapng container summaries, sanitized JSON exports, and hardware-free tests. Stage 2J adds formal protocol hypothesis records, sanitized evidence docs, JSON/Markdown hypothesis export commands, and hardware-free tests. Stage 2K adds mock-only SimHub F1 EC protocol records, mock encoding/decoding, deterministic duration planning, SimProUnknownMock classification, mock output frame diagnostics, and safe mock protocol CLI examples. Stage 2L adds `PHprSafetyLimiter`, safety decisions/context/snapshots, deterministic rate and continuous-duration limiting, emergency-stop latching/clear behavior, real-write blocking diagnostics, and a safety-limited mock output wrapper. Stage 2M adds mock-only gear pulse routing from accepted `ShiftIntentEvent` values through the Stage 2L safety-limited mock output path, with Devices-page diagnostics and mock-only routing preferences. Stage 2N adds mock-only road vibration, wheel slip, and wheel lock routing from existing `VehicleState` / pipeline snapshots through the same safety-limited mock output stack, with priority, interval suppression, Devices-page diagnostics, and safe mock preferences. Stage 2O adds read-only SimPro Manager / SimHub process detection, WPF coexistence diagnostics, and safety-context conflict status wiring. P-HPR is a separate non-audio actuator path, not an ASIO or `IAudioOutputDevice` output. No real P-HPR USB writes, production protocol adapter, real vibration commands, or controlled write testing are implemented, and future real writes are gated behind the exact approval phrase documented in `docs/SIMAGIC_P_HPR_SAFETY_PLAN.md`.

The app does not yet implement advanced routing matrices, live graphing, real WASAPI output, physical shaker calibration, or physical shaker validation. Physical shaker feel, safe gain, physical latency, and final frequency tuning remain unvalidated until the Dayton BST-1 arrives and the full chain is tested locally.

## Solution Layout

- `src/HapticDrive.Asio.App`: WPF desktop app.
- `src/HapticDrive.Asio.Core`: shared domain models and interfaces.
- `src/HapticDrive.Asio.Telemetry.F1_25`: F1 25 telemetry parser and adapter.
- `src/HapticDrive.Asio.Audio`: audio output abstractions, ASIO readiness seams, native ASIO backend, output-owned render loop support, mixer, safety chain, test bench, Stage 12 / Stage 13 effect generators, Stage 14 profiles, and audio diagnostics.
- `src/HapticDrive.Asio.Runtime`: end-to-end pipeline coordinator for live/replay telemetry, parser, VehicleState, effects, mixer, safety, recording, forwarding, output-owned rendering, and stale telemetry mute.
- `src/HapticDrive.Asio.Recording`: telemetry recording and replay.
- `src/HapticDrive.Input.Abstractions`: read-only input discovery snapshots, candidate scoring, paddle mapping/listener diagnostics, paddle shift-intent contracts for later routing, and cached driving-state contracts for Phase 2.
- `src/HapticDrive.Input.Windows`: read-only Windows Raw Input and game-controller discovery plus Stage 2E Windows game-controller button-state reading; it does not send device commands or route haptics.
- `src/HapticDrive.Simagic.PHPR.Abstractions`: non-audio P-HPR command, coexistence detection, safety limiter, safety-limited mock output, output, mock protocol, and mock-output contracts.
- `src/HapticDrive.Simagic.PHPR.Research`: Stage 2G read-only P700 / P-HPR inventory utility, Stage 2H capture metadata tooling, Stage 2I sanitized capture analysis tooling, Stage 2J hypotheses, Stage 2K mock protocol examples, and Stage 2L safety examples.
- `src/HapticDrive.Actuation`: cached driving-state, Stage 2F shift-intent event evaluation, Stage 2M mock-only P-HPR gear pulse routing, and Stage 2N mock-only road/slip/lock pedal-effect routing that stay separate from the ASIO audio path.
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

Use `docs/STAGE_16_ASIO_READINESS.md` for the manual M-Audio/Fosi/BST-1 readiness checklist. Start from Null output, refresh ASIO diagnostics, select ASIO deliberately, select the M-Audio / M-Track driver deliberately, select one output channel deliberately, arm ASIO deliberately, then start haptics deliberately. Dayton BST-1 physical output testing is deferred until the shaker arrives.

## Stage 17 Streaming

Use `docs/STAGE_17_NATIVE_ASIO_STREAMING.md` for the pre-shaker streaming checklist and diagnostics. ASIO output is still explicit: select ASIO deliberately, select the driver, select one output channel, arm ASIO, then start haptics. Automated tests use Null output and fake ASIO backends; they do not require M-Audio, Fosi, Dayton BST-1, F1 25, or live telemetry.

## Stage 18 Final Pre-Shaker Package

Use `docs/STAGE_18_FINAL_PRE_SHAKER.md` for the final pre-shaker checklist. Stage 18 completes the pre-BT-1 software package around the existing engine: launch/runtime prerequisite handling, persisted app settings, forwarding destination UI, recording library UI, selected recording replay, packet-ID diagnostics, copyable diagnostics report, and final documentation cleanup. Null output remains default, and ASIO hardware remains opt-in and explicitly armed.

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
