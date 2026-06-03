# Hardware-Absent Mode

Hardware-absent mode is the default development and automated-test posture until the real M-Audio interface, amplifier, and Dayton BST-1 chain is fully available and manually validated. The M-Audio M-Track Solo and Fosi amplifier may now be present locally, but the Dayton BST-1 shaker has not been physically validated.

## Current Behavior

- The app starts with `NullAudioOutputDevice`.
- `NullAudioOutputDevice` opens, starts, and stops without audio hardware.
- Null output consumes Stage 10 sample buffers deterministically, discards them, and produces no sound.
- `WasapiDebugOutputDevice` exists as a manual debug placeholder only.
- `AsioAudioOutputDevice` exists behind the same `IAudioOutputDevice` interface.
- ASIO open attempts fail gracefully when no matching driver is available.
- No automated test requires an ASIO driver, M-Audio interface, Fosi amplifier, or Dayton BST-1.
- The Stage 10 mixer and safety chain are covered by automated tests using null output only.
- The Stage 11 test bench generates deterministic synthetic validation buffers and feeds null output by default.
- The Stage 12 gear shift and engine effects generate deterministic source buffers and feed the existing mixer, safety chain, and null output in tests.
- Stage 13 kerb, impact, road texture, and slip effects use the same null-output path.
- Stage 15 live/replay mock pipeline orchestration uses `NullAudioOutputDevice` by default and can be validated without F1 25, UDP sockets, ASIO hardware, WASAPI hardware, M-Audio hardware, Fosi amplifier, or Dayton BST-1.
- Optional M-Audio / ASIO visibility diagnostics use fake catalogs in automated tests and must not be treated as proof of real ASIO streaming.

## Output Modes

| Mode | Automated tests | Manual use | Hardware required | Notes |
| --- | --- | --- | --- | --- |
| Null | Yes | Yes | No | Default safe output. Consumes and discards sample buffers. |
| WASAPI Debug | No | Later manual debug only | Normal Windows audio endpoint later | Must not be selected automatically if ASIO fails. |
| ASIO | No | Later manual hardware path | Yes | Intended low-latency target. Fails gracefully when unavailable. |

## Rules

- Do not block development because physical shaker hardware is missing.
- Do not claim final haptic feel, safe gain, physical latency, or frequency tuning.
- Do not make automated tests depend on output hardware.
- Do not fall back from ASIO to WASAPI automatically.
- Keep hardware-dependent tests skipped by default.
- Do not treat Windows sound output selector visibility as proof of ASIO usage.
