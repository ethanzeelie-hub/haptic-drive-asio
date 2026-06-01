# Haptic Drive ASIO

Haptic Drive ASIO is a standalone Windows desktop application for low-latency sim-racing haptics through ASIO audio output.

The first target game is EA F1 25. The first intended hardware path is an M-Audio M-Track Solo / Duo ASIO interface feeding a Fosi BT20A amplifier and Dayton BST-1 bass shaker.

Physical shaker hardware is not available yet. Development must work without it by using deterministic null output, manual WASAPI debug output, graceful ASIO handling, and telemetry replay.

## Current Stage

Stage 01: app shell complete. Next stage is Stage 02, output abstractions and hardware-absent mode.

The app currently opens to a WPF shell with dashboard, navigation pages, global start/stop placeholder, emergency mute placeholder, dark theme default, light theme scaffolding, and a close/minimize-to-tray setting placeholder.

It does not yet implement telemetry parsing, UDP receive, audio output devices, audio generation, recording, replay, or haptic effects.

## Solution Layout

- `src/HapticDrive.Asio.App`: WPF desktop app.
- `src/HapticDrive.Asio.Core`: shared domain models and interfaces.
- `src/HapticDrive.Asio.Telemetry.F1_25`: F1 25 telemetry parser and adapter.
- `src/HapticDrive.Asio.Audio`: audio output abstractions, mixer, and safety chain.
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
