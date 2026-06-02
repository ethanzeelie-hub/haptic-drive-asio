# Haptic Drive ASIO

Haptic Drive ASIO is a standalone Windows desktop application for low-latency sim-racing haptics through ASIO audio output.

The first target game is EA F1 25. The first intended hardware path is an M-Audio M-Track Solo / Duo ASIO interface feeding a Fosi BT20A amplifier and Dayton BST-1 bass shaker.

Physical shaker hardware is not available yet. Development must work without it by using deterministic null output, manual WASAPI debug output, graceful ASIO handling, and telemetry replay.

## Current Stage

Stage 14: UI tuning, profiles, and diagnostics complete. Next stage is Stage 15, first playable mock output milestone.

The app currently opens to a WPF shell with dashboard, navigation pages, global start/stop, emergency mute, dark theme default, light theme scaffolding, safe tuning controls, profile save/load/reset, and runtime diagnostics.

The selected output mode is `NullAudioOutputDevice` by default, so the app can open and tests can run without ASIO hardware or shaker hardware.

The official F1 25 v3 PDF has been extracted into implementation notes under `docs/`. The app now starts a raw UDP listener on port `20778` by default, counts incoming datagrams, tracks packet rate, shows a no-packet warning in the dashboard, offers each raw packet to the UDP forwarder, can record incoming raw UDP payload bytes to a versioned replay file, validates F1 25 packet headers, parses the Stage 07 core packet bodies, and maps parsed packets into shared last-known `VehicleState` samples.

Forwarding and recording preserve exact packet payload bytes and do not depend on parser or VehicleState success. Replay emits recorded packets back as `UdpTelemetryPacket` values so tests can reuse the existing parser and VehicleState adapter path without live UDP traffic. Stage 10 adds interleaved floating-point audio sample buffers, a deterministic source-buffer mixer, conservative safety processing, and null-output sample consumption. Stage 11 adds deterministic test-bench signals for validating the internal audio path through the existing mixer, safety chain, and `NullAudioOutputDevice`. Stage 12 adds conservative engine vibration and gear shift effect generators that consume shared `VehicleState`, render deterministic source buffers, and feed the existing mixer/safety/null-output path. Stage 13 adds conservative kerb, impact, road texture, and slip / brake-lock effect generators through the same effect engine and safety path. Stage 14 exposes practical tuning for those existing effects, master mixer/safety controls, versioned JSON profiles, replay status snapshots, and runtime diagnostics without adding new effects or hardware output.

The app does not yet implement forwarding destination configuration in the UI, a polished recordings library UI, advanced routing matrices, live graphing, a real-time audio callback loop, real WASAPI output, real ASIO streaming, or physical shaker calibration.

## Solution Layout

- `src/HapticDrive.Asio.App`: WPF desktop app.
- `src/HapticDrive.Asio.Core`: shared domain models and interfaces.
- `src/HapticDrive.Asio.Telemetry.F1_25`: F1 25 telemetry parser and adapter.
- `src/HapticDrive.Asio.Audio`: audio output abstractions, mixer, safety chain, test bench, Stage 12 / Stage 13 effect generators, Stage 14 profiles, and audio diagnostics.
- `src/HapticDrive.Asio.Recording`: telemetry recording and replay.
- `tests/*`: xUnit test projects.

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
