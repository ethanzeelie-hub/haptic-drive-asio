# Haptic Drive ASIO

Haptic Drive ASIO is a standalone Windows desktop application for low-latency sim-racing haptics through ASIO audio output.

The first target game is EA F1 25. The first intended hardware path is an M-Audio M-Track Solo / Duo ASIO interface feeding a Fosi BT20A amplifier and Dayton BST-1 bass shaker.

The M-Audio M-Track Solo interface and Fosi Audio BT20A amplifier are now available locally, but the Dayton BST-1 shaker has not arrived yet. Development must still work without any physical haptic hardware by using deterministic null output, manual WASAPI debug output, graceful ASIO handling, and telemetry replay.

## Current Stage

Stage 15: first playable mock output milestone complete. Next stage is Stage 16, manual ASIO hardware readiness.

The app currently opens to a WPF shell with dashboard, navigation pages, global start/stop, emergency mute, dark theme default, light theme scaffolding, safe tuning controls, profile save/load/reset, runtime diagnostics, basic recording/replay controls, and optional ASIO driver visibility diagnostics.

The selected output mode is `NullAudioOutputDevice` by default, so the app can open and tests can run without ASIO hardware or shaker hardware.

The official F1 25 v3 PDF has been extracted into implementation notes under `docs/`. The app now starts a raw UDP listener on port `20778` by default, counts incoming datagrams, tracks packet rate, shows a no-packet warning in the dashboard, offers each raw packet to the UDP forwarder, can record incoming raw UDP payload bytes to a versioned replay file, validates F1 25 packet headers, parses the Stage 07 core packet bodies, and maps parsed packets into shared last-known `VehicleState` samples.

Forwarding and recording preserve exact packet payload bytes and do not depend on parser or VehicleState success. Replay emits recorded packets back as `UdpTelemetryPacket` values without UDP sockets. Stage 15 adds a runtime coordinator that feeds both live and replayed packets through the same parser, VehicleState adapter, existing effects, mixer, safety chain, and `NullAudioOutputDevice` path. Start/Stop Haptics now controls that mock software pipeline, Emergency Mute immediately silences it, profile tuning affects the active effect/mixer/safety configuration, and Null output diagnostics report deterministic consumed buffers, samples, and peaks.

The app does not yet implement forwarding destination configuration in the UI, a polished recordings library UI, advanced routing matrices, live graphing, a real-time audio callback loop, real WASAPI output, real ASIO streaming, M-Audio ASIO hardware readiness, or physical shaker calibration. Windows sound output visibility is not proof of ASIO usage; ASIO must be confirmed through the app's ASIO driver/output path. Physical shaker feel, safe gain, latency, and frequency tuning remain unvalidated until the Dayton BST-1 arrives and Stage 16/manual hardware testing is performed.

## Solution Layout

- `src/HapticDrive.Asio.App`: WPF desktop app.
- `src/HapticDrive.Asio.Core`: shared domain models and interfaces.
- `src/HapticDrive.Asio.Telemetry.F1_25`: F1 25 telemetry parser and adapter.
- `src/HapticDrive.Asio.Audio`: audio output abstractions, mixer, safety chain, test bench, Stage 12 / Stage 13 effect generators, Stage 14 profiles, and audio diagnostics.
- `src/HapticDrive.Asio.Runtime`: Stage 15 end-to-end mock pipeline coordinator for live/replay telemetry, parser, VehicleState, effects, mixer, safety, recording, forwarding, and output.
- `src/HapticDrive.Asio.Recording`: telemetry recording and replay.
- `tests/*`: xUnit test projects.

## Mock Validation

Use `docs/STAGE_15_MOCK_PIPELINE.md` for the hardware-safe Stage 15 checklist. The short version: keep output on `NullAudioOutputDevice`, start haptics, use live UDP or replay/test bench, verify diagnostics and Emergency Mute, and do not treat M-Audio visibility or Windows sound output selection as proof of ASIO streaming.

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
