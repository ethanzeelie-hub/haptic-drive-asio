# Hardware-Absent Mode

Hardware-absent mode is the default development and automated-test posture until the real M-Audio interface, amplifier, and Dayton BST-1 chain is fully available and manually validated. The M-Audio M-Track Solo and Fosi amplifier are now available locally, but the Dayton BST-1 shaker has not arrived or been physically validated.

## Current Behavior

- The app starts with `NullAudioOutputDevice`.
- `NullAudioOutputDevice` opens, starts, and stops without audio hardware.
- Null output consumes Stage 10 sample buffers deterministically, discards them, and produces no sound.
- `WasapiDebugOutputDevice` exists as a manual debug placeholder only.
- `AsioAudioOutputDevice` exists behind the same `IAudioOutputDevice` interface.
- ASIO open attempts fail gracefully when no matching driver, explicit driver selection, explicit output channel, arming, or backend support is available.
- `WindowsRegistryAsioDriverCatalog` can report local ASIO driver names on Windows without making drivers required for startup or CI.
- Fake ASIO catalogs/backends cover readiness behavior in automated tests.
- No automated test requires an ASIO driver, M-Audio interface, Fosi amplifier, or Dayton BST-1.
- The Stage 10 mixer and safety chain are covered by automated tests using null output only.
- The Stage 11 test bench generates deterministic synthetic validation buffers and feeds null output by default.
- The Stage 12 gear shift and engine effects generate deterministic source buffers and feed the existing mixer, safety chain, and null output in tests.
- Stage 13 kerb, impact, road texture, and slip effects use the same null-output path.
- Stage 15 live/replay mock pipeline orchestration uses `NullAudioOutputDevice` by default and can be validated without F1 25, UDP sockets, ASIO hardware, WASAPI hardware, M-Audio hardware, Fosi amplifier, or Dayton BST-1.
- M-Audio / ASIO visibility diagnostics use fake catalogs in automated tests and must not be treated as proof of real ASIO streaming.
- Native ASIO streaming is implemented behind `IAsioOutputBackend` in Stage 17, but Null output remains the default and fake backends cover automated streaming tests.
- Output-owned rendering has replaced the WPF haptic render timer for the live pipeline.
- Stale telemetry wall-clock mute prevents old live telemetry from continuing to drive effects indefinitely.
- Stage 18 adds a launch wrapper/script that sets `DOTNET_ROOT` to the repo-local .NET 8 runtime and checks for `Microsoft.WindowsDesktop.App 8.x` before starting the WPF executable.
- Stage 18 app settings persist theme, forwarding destinations, and last ASIO driver/channel selection, but never persist ASIO armed state or haptic auto-start.
- Stage 18 forwarding and recording-library UI work without shaker hardware and do not require ASIO output.

## Output Modes

| Mode | Automated tests | Manual use | Hardware required | Notes |
| --- | --- | --- | --- | --- |
| Null | Yes | Yes | No | Default safe output. Consumes and discards sample buffers. |
| WASAPI Debug | No | Later manual debug only | Normal Windows audio endpoint later | Must not be selected automatically if ASIO fails. |
| ASIO | Fake only | Manual readiness/streaming path | Yes for physical use | Intended low-latency target. Requires explicit selection, driver, channel, arming, and Start Haptics. |

## Rules

- Do not block development because physical shaker hardware is missing.
- Do not claim final haptic feel, safe gain, physical latency, or frequency tuning.
- Do not make automated tests depend on output hardware.
- Do not fall back from ASIO to WASAPI automatically.
- Keep hardware-dependent tests hardware-safe by default. Readiness/pending tests may run with zero skipped tests, but they must not energize ASIO/BST-1 or P-HPR hardware without explicit local flags or a controlled manual command.
- Do not treat Windows sound output selector visibility as proof of ASIO usage.
- Do not treat callback/drop/underrun diagnostics as final physical latency or safe gain measurements.
- Do not treat persisted ASIO driver/channel selection as hardware arming.
