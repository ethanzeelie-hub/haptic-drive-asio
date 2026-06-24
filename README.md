# Haptic Drive ASIO

Haptic Drive ASIO is a Windows desktop application for low-latency sim-racing haptics.

Current implementation focus:

- F1 25 telemetry,
- ASIO-first bass-shaker output,
- a separate non-audio Simagic P-HPR actuation path,
- hardware-safe defaults,
- production-hardening architecture.

## Current production-hardening status

Audited remediation stages 1 through 12 are complete for software hardening.

The repo now has:

- bounded UDP ingress with loopback-safe defaults,
- session-aware telemetry freshness and reset protection,
- canonical `HapticFrame` normalization,
- functional descriptor-based effect runtimes and profile schema v2,
- a global output interlock across audio, manual-test, and actuation paths,
- resilient recording/replay v2,
- structured diagnostics and redacted support bundles,
- release packaging with dependency governance and vulnerability checks.

## Safety defaults

- `NullAudioOutputDevice` is the default output.
- ASIO is explicit opt-in and must be selected/armed deliberately.
- Local driver/channel convenience selection does not start the stream or emit output.
- WASAPI debug output remains manual/experimental only and is not a production streaming path.
- LAN telemetry is opt-in; loopback is the default bind mode.
- Real Simagic P-HPR non-stop writes require session-only authorization, direct enable/arm, a clear global interlock, and explicit manual operator action at the physical write boundary.

## Current architecture

The live system is organized as:

```text
UDP ingress -> game adapter -> VehicleState -> canonical HapticFrame -> effect engine -> audio output
                                                   \-> actuation routing
```

Key boundaries:

- game-specific parsing stays behind adapters,
- effects render from canonical `HapticRenderFrame` data built from `HapticFrame`,
- actuation consumes canonical `HapticFrame` plus driving context,
- raw UDP bytes stay untouched for recording/replay/forwarding,
- the real-time render path stays isolated from UI, disk, logging, and networking work.

## Documentation map

- Current architecture: [ARCHITECTURE.md](/C:/Users/ethan/OneDrive/Documents/ASIO%20Haptic%20Engine%20Program/ARCHITECTURE.md)
- Active issues only: [KNOWN_ISSUES.md](/C:/Users/ethan/OneDrive/Documents/ASIO%20Haptic%20Engine%20Program/KNOWN_ISSUES.md)
- Future work: [ROADMAP.md](/C:/Users/ethan/OneDrive/Documents/ASIO%20Haptic%20Engine%20Program/ROADMAP.md)
- Chronological implementation history: [DEVELOPMENT_LOG.md](/C:/Users/ethan/OneDrive/Documents/ASIO%20Haptic%20Engine%20Program/DEVELOPMENT_LOG.md)
- Architecture decisions: [docs/adr](/C:/Users/ethan/OneDrive/Documents/ASIO%20Haptic%20Engine%20Program/docs/adr)
- Historical stage-detail archive: [docs/archive](/C:/Users/ethan/OneDrive/Documents/ASIO%20Haptic%20Engine%20Program/docs/archive)

## Verification

Use the production-hardening validation flow:

```powershell
$dotnet = if (Test-Path ".\.dotnet\dotnet.exe") { ".\.dotnet\dotnet.exe" } else { "dotnet" }

& $dotnet restore HapticDrive.Asio.sln --locked-mode
& $dotnet build HapticDrive.Asio.sln -c Release --no-restore -warnaserror
& $dotnet test HapticDrive.Asio.sln -c Release --no-build
& $dotnet test HapticDrive.Asio.sln -c Release --no-build
& $dotnet format HapticDrive.Asio.sln --verify-no-changes --no-restore
.\Run-HapticDrive.ps1 -Configuration Release -NoBuild -CheckOnly
```

## Release and licensing status

- Release packaging is hardened and auditable.
- Public redistribution is still blocked until the owner chooses license terms.
- Real P-HPR hardware safety, stop/off behavior, coexistence behavior, and final tuning still require supervised manual local validation.
- Physical shaker feel, safe gain, physical latency, and final tuning remain unclaimed until local hardware validation is complete.
