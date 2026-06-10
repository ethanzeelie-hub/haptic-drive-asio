# Stage 18 Final Pre-Shaker Readiness

Stage 18 is the final software package before app-driven Dayton BT-1/BST-1 validation. It completes launch, routing, replay, diagnostics, and documentation polish without requiring physical shaker output.

## Hardware State

- M-Audio M-Track Solo / Duo ASIO interface: available locally.
- Fosi amplifier: available locally.
- Dayton shaker: available locally and proven through SimHub; Haptic Drive ASIO app-driven output validation remains manual.
- Null output remains the default safe output.

## Launch

Use the root launch wrapper:

```powershell
.\Run-HapticDrive.cmd
```

The wrapper runs `Run-HapticDrive.ps1` with a process-scoped PowerShell execution-policy bypass. The script:

- Uses the repo-local `.dotnet` runtime.
- Sets `DOTNET_ROOT` for the WPF executable.
- Checks for `Microsoft.WindowsDesktop.App 8.x`.
- Builds the solution with `--no-restore`.
- Starts the WPF executable.

Build and test commands do not open the app window.

Use this to verify launch prerequisites without opening another window:

```powershell
.\Run-HapticDrive.cmd -NoBuild -CheckOnly
```

## Stage 18 Checks

Before deliberate app-driven shaker validation, verify:

- The app opens from `Run-HapticDrive.cmd`.
- Startup output is `NullAudioOutputDevice`.
- ASIO drivers can be refreshed without requiring ASIO to be selected.
- ASIO driver/channel selections can be made without persisting armed state.
- UDP forwarding destinations can be added, edited, removed, enabled, disabled, and persisted.
- Enabled loopback to the local listener port `20778` is blocked.
- Raw UDP recording starts and stops.
- The recordings library refreshes and reads `.hdrec` metadata summaries.
- Replay Latest and Replay Selected feed the same parser, VehicleState, effects, mixer, safety, and output-owned render path.
- Diagnostics can be refreshed and copied.
- Emergency Mute and Stop Haptics remain visible and reliable.

Stage 18 follow-up adds the deliberate `Manual ASIO Bass Shaker Test` for short 40/50 Hz pulses through the selected real ASIO output and the runtime-only `Paddle Gear Bench Test` for mapped-paddle validation without live telemetry. These follow-up controls do not change Null default startup, ASIO arming rules, P-HPR direct gates, or normal telemetry-driven `DrivingArmed` behavior.

## Explicit Non-Claims

- No final shaker feel is claimed.
- No safe physical gain is claimed.
- No physical latency is claimed.
- No final frequency tuning is claimed.
- No physical calibration is complete.

Those checks wait until the full M-Audio -> Fosi -> Dayton shaker chain is intentionally tested locally.
