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
