# Haptic Drive ASIO

Haptic Drive ASIO is a standalone Windows desktop application for low-latency sim-racing haptics through ASIO audio output.

The first target game is EA F1 25. The first intended hardware path is an M-Audio M-Track Solo / Duo ASIO interface feeding a Fosi BT20A amplifier and Dayton BST-1 bass shaker.

Physical shaker hardware is not available yet. Development must work without it by using deterministic null output, manual WASAPI debug output, graceful ASIO handling, and telemetry replay.

## Current Stage

Stage 12: Gear shift and engine effects complete. Next stage is Stage 13, kerb, impact, road texture, and slip effects.

The app currently opens to a WPF shell with dashboard, navigation pages, global start/stop, emergency mute, dark theme default, light theme scaffolding, and a close/minimize-to-tray setting placeholder.

The selected output mode is `NullAudioOutputDevice` by default, so the app can open and tests can run without ASIO hardware or shaker hardware.

The official F1 25 v3 PDF has been extracted into implementation notes under `docs/`. The app now starts a raw UDP listener on port `20778` by default, counts incoming datagrams, tracks packet rate, shows a no-packet warning in the dashboard, offers each raw packet to the UDP forwarder, can record incoming raw UDP payload bytes to a versioned replay file, validates F1 25 packet headers, parses the Stage 07 core packet bodies, and maps parsed packets into shared last-known `VehicleState` samples.

Forwarding and recording preserve exact packet payload bytes and do not depend on parser or VehicleState success. Replay emits recorded packets back as `UdpTelemetryPacket` values so tests can reuse the existing parser and VehicleState adapter path without live UDP traffic. Stage 10 adds interleaved floating-point audio sample buffers, a deterministic source-buffer mixer, conservative safety processing, and null-output sample consumption. Stage 11 adds deterministic test-bench signals for validating the internal audio path through the existing mixer, safety chain, and `NullAudioOutputDevice`. Stage 12 adds conservative engine vibration and gear shift effect generators that consume shared `VehicleState`, render deterministic source buffers, and feed the existing mixer/safety/null-output path.

The app does not yet implement forwarding destination configuration in the UI, a polished recordings library UI, Stage 13 road/kerb/slip/impact effects, a real-time audio callback loop, real WASAPI output, real ASIO streaming, or physical shaker calibration.

## Solution Layout

- `src/HapticDrive.Asio.App`: WPF desktop app.
- `src/HapticDrive.Asio.Core`: shared domain models and interfaces.
- `src/HapticDrive.Asio.Telemetry.F1_25`: F1 25 telemetry parser and adapter.
- `src/HapticDrive.Asio.Audio`: audio output abstractions, mixer, safety chain, test bench, and Stage 12 effect generators.
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
