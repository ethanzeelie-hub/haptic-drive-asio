# Haptic Drive ASIO

Haptic Drive ASIO is a standalone Windows desktop application for low-latency sim-racing haptics through ASIO audio output.

The first target game is EA F1 25. The first intended hardware path is an M-Audio M-Track Solo / Duo ASIO interface feeding a Fosi BT20A amplifier and Dayton BST-1 bass shaker.

The M-Audio M-Track Solo interface and Fosi Audio BT20A amplifier are now available locally, but the Dayton BST-1 shaker has not arrived yet. Development must still work without any physical haptic hardware by using deterministic null output, manual WASAPI debug output, graceful ASIO handling, and telemetry replay.

## Current Stage

Stage 2C: cached `DrivingArmed` state service complete. Stage 18 remains the final Phase 1 pre-shaker readiness package.

The app currently opens to a WPF shell with dashboard, navigation pages, global start/stop, emergency mute, dark theme default, persisted light/dark theme setting, safe tuning controls, profile save/load/reset, runtime diagnostics, recording/replay library controls, persisted UDP forwarding destination controls, ASIO driver visibility diagnostics, and explicit ASIO output readiness controls.

The selected output mode is `NullAudioOutputDevice` by default, so the app can open and tests can run without ASIO hardware or shaker hardware.

The official F1 25 v3 PDF has been extracted into implementation notes under `docs/`. The app now starts a raw UDP listener on port `20778` by default, counts incoming datagrams, tracks packet rate, shows a no-packet warning in the dashboard, offers each raw packet to the UDP forwarder, can record incoming raw UDP payload bytes to a versioned replay file, validates F1 25 packet headers, parses the Stage 07 core packet bodies, and maps parsed packets into shared last-known `VehicleState` samples.

Forwarding and recording preserve exact packet payload bytes and do not depend on parser or VehicleState success. Replay emits recorded packets back as `UdpTelemetryPacket` values without UDP sockets. Stage 15 adds a runtime coordinator that feeds both live and replayed packets through the same parser, VehicleState adapter, existing effects, mixer, safety chain, and `NullAudioOutputDevice` path. Start/Stop Haptics controls that software pipeline, Emergency Mute immediately silences it, profile tuning affects the active effect/mixer/safety configuration, and Null output diagnostics report deterministic consumed buffers, samples, and peaks.

Stage 16 adds Windows ASIO driver-name discovery, explicit output-mode selection, explicit ASIO driver selection, explicit output-channel selection, explicit arming, mono-to-selected-channel routing behind the output abstraction, readiness diagnostics, and fake ASIO lifecycle/failure tests.

Stage 17 adds an NAudio-backed native ASIO streaming backend behind `IAsioOutputBackend`, moves live haptic rendering off the WPF `DispatcherTimer` into an output-owned render loop, and adds stale telemetry wall-clock mute plus callback/render diagnostics for render callbacks, backend callbacks, submitted buffers, dropped buffers, underruns, render duration, jitter, and telemetry age. The audio render callback fills in-memory buffers only; UI, disk IO, logging, networking, blocking waits, and async continuations stay outside that path. The default output remains `NullAudioOutputDevice`; the app never auto-switches to ASIO or WASAPI. The M-Audio M-Track Solo is available on the user's PC, but M-Audio absence must not break build/test/CI. Windows sound output visibility is not proof of ASIO usage; ASIO must be confirmed through the app's ASIO driver/output path.

Stage 18 adds a root launch script with .NET 8 Desktop Runtime preflight, app-settings persistence separate from haptic profiles, persisted UDP forwarding destination editing, a recordings library with metadata summaries and selected replay, packet-ID diagnostics, diagnostics copy/report support, and final pre-shaker UI/documentation cleanup. ASIO output still requires explicit output mode selection, driver selection, channel selection, arming, and Start Haptics.

Stage 2A starts the Simagic P-HPR / GT Neo paddle-input phase with documentation and safety gates only. Stage 2B adds safe input and P-HPR abstraction projects, a mock-only P-HPR output skeleton, conservative P-HPR safety defaults, and focused model tests. Stage 2C adds a cached `DrivingArmedStateService` that evaluates existing `VehicleState` and runtime snapshots for fresh active-driving telemetry before future paddle pulses may route. P-HPR is a separate non-audio actuator path, not an ASIO or `IAudioOutputDevice` output. No real P-HPR USB writes or vibration commands are implemented, and future real writes are gated behind the exact approval phrase documented in `docs/SIMAGIC_P_HPR_SAFETY_PLAN.md`.

The app does not yet implement advanced routing matrices, live graphing, real WASAPI output, physical shaker calibration, or physical shaker validation. Physical shaker feel, safe gain, physical latency, and final frequency tuning remain unvalidated until the Dayton BST-1 arrives and the full chain is tested locally.

## Solution Layout

- `src/HapticDrive.Asio.App`: WPF desktop app.
- `src/HapticDrive.Asio.Core`: shared domain models and interfaces.
- `src/HapticDrive.Asio.Telemetry.F1_25`: F1 25 telemetry parser and adapter.
- `src/HapticDrive.Asio.Audio`: audio output abstractions, ASIO readiness seams, native ASIO backend, output-owned render loop support, mixer, safety chain, test bench, Stage 12 / Stage 13 effect generators, Stage 14 profiles, and audio diagnostics.
- `src/HapticDrive.Asio.Runtime`: end-to-end pipeline coordinator for live/replay telemetry, parser, VehicleState, effects, mixer, safety, recording, forwarding, output-owned rendering, and stale telemetry mute.
- `src/HapticDrive.Asio.Recording`: telemetry recording and replay.
- `src/HapticDrive.Input.Abstractions`: read-only input, paddle shift-intent, and cached driving-state contracts for Phase 2.
- `src/HapticDrive.Input.Windows`: placeholder Windows input project for later read-only Raw Input / DirectInput / HID discovery.
- `src/HapticDrive.Simagic.PHPR.Abstractions`: non-audio P-HPR command, safety, output, and mock-output contracts.
- `src/HapticDrive.Actuation`: cached driving-state and future actuator routing logic that stays separate from the ASIO audio path.
- `tests/*`: xUnit test projects.

## Simagic Phase 2A Docs

- `docs/SIMAGIC_P_HPR_PHASE_2_RESEARCH.md`: Phase 2 baseline, hardware context, boundaries, and readiness notes.
- `docs/SIMAGIC_USER_DATA_REQUEST.md`: requested SimPro, SimHub, Windows, USBView, and mapping data.
- `docs/SIMAGIC_CAPTURE_GUIDE.md`: capture naming, metadata, and raw-capture handling rules.
- `docs/SIMAGIC_WHEEL_INPUT_RESEARCH.md`: read-only GT Neo paddle input discovery plan.
- `docs/SIMAGIC_SHIFT_INTENT_DESIGN.md`: instant paddle shift-intent design and `DrivingArmed` gating.
- `docs/SIMAGIC_P_HPR_SAFETY_PLAN.md`: P-HPR write gate and actuator safety plan.

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
