# ASIO Output

ASIO is the intended low-latency output path for the real bass shaker chain. Stage 02 adds the abstraction and graceful failure behavior. Stage 10 adds internal sample buffers, mixer processing, safety processing, and null-output sample consumption. Stage 11 adds deterministic test-bench signals. Stage 12 and Stage 13 add VehicleState-driven effect source buffers. Stage 15 adds optional ASIO driver-catalog visibility diagnostics. Stage 16 adds Windows ASIO driver-name discovery, explicit ASIO selection/arming/channel routing, readiness diagnostics, and fake-backend tests. Stage 17 adds native ASIO streaming behind `IAsioOutputBackend`, output-owned render cadence, stale telemetry mute, and render/backend diagnostics. Stage 18 adds launch/runtime prerequisite handling, app settings persistence, forwarding/recording/diagnostics polish, and final pre-shaker readiness cleanup. The Stage 18 follow-up adds manual ASIO hardware tests, Stage 18i adds configurable BST-1 10-80 Hz short pulses plus optional accepted-paddle bench synchronization through the selected ASIO channel, and Stage 18j separates ready/armed ASIO status from stream-running status for bounded manual/local gear pulses.

## Stage 02 Implementation

- `IAudioOutputDevice` lives in `HapticDrive.Asio.Core.Audio`.
- `AsioAudioOutputDevice` lives in `HapticDrive.Asio.Audio.Devices`.
- The preferred driver name is `M-Audio M-Track Solo and Duo ASIO`.
- Driver discovery is behind `IAsioDriverCatalog`.
- `WindowsRegistryAsioDriverCatalog` reads standard Windows ASIO registry locations when available.
- A fake driver catalog is used in automated tests to validate driver selection without hardware.
- Visibility/readiness diagnostics can report whether a fake or real catalog lists an M-Audio / M-Track-like ASIO driver.

## Current Limitations

- Native ASIO streaming is wired through `NativeAsioOutputBackend` and `NAudio.Asio`.
- The M-Audio -> Fosi -> Dayton BST-1 chain has been locally proven through SimHub, and app-driven ASIO test pulses have locally validated channel 1 as the BST-1 output channel. Safe gain, final feel, and physical latency remain manual validation items.
- The deterministic synthetic benchmark, Stage 12/13 effect buffers, and automated pipeline renders use `NullAudioOutputDevice` by default and do not prove physical latency, safe gain, or shaker response.
- Manual ASIO hardware pulses can energize the BST-1 through the selected ASIO output, but they are still deliberate local validation steps rather than final tuning claims.
- ASIO failure does not select WASAPI automatically.
- Windows sound output visibility is not proof of ASIO usage.
- Persisted ASIO driver/channel settings are convenience selections only; ASIO armed state and auto-start are not persisted.
- Direct executable launch requires .NET 8 Desktop Runtime visibility to the app host. `Run-HapticDrive.cmd` runs the PowerShell launcher, which sets `DOTNET_ROOT` to the repo-local runtime before launching.

## Target Defaults

- Sample rate: 48000 Hz.
- Channels: 1 mono channel for the first BST-1 target.
- Buffer size: 128 samples as the default future ASIO target.
- Buffer size 64 remains experimental.
- Buffer size 256 remains a fallback.

## Stage 10 Sample Pipeline

- `AudioSampleBuffer` stores interleaved floating-point samples.
- `AudioMixer` combines source buffers with per-source gain, master gain, mute, and emergency mute.
- `AudioSafetyProcessor` sanitises invalid samples, applies conservative gain, limits peaks, and hard-clips overflow.
- `AudioRenderPipeline` makes the mixer/safety chain hand a final buffer to `IAudioOutputDevice.SubmitBufferAsync`.
- `NullAudioOutputDevice` accepts matching sample buffers after start and discards them deterministically.
- `AsioAudioOutputDevice` consumes only the final safety-processed buffer when selected and started.

## Stage 17 Streaming

- `IAudioOutputDevice.StartStreamingAsync` lets the selected output own the low-latency render cadence.
- The WPF shell no longer drives live haptic rendering through a `DispatcherTimer`.
- `HapticPipelineCoordinator` still keeps manual render submission for deterministic tests, but the app uses output-owned rendering.
- The render callback only fills the provided in-memory audio buffer from current effect state, mixer, and safety settings.
- UI, disk IO, logging, networking, blocking waits, and async continuations stay outside the callback.
- `NativeAsioOutputBackend` uses NAudio `AsioOut` and a small preallocated queue between app rendering and driver callbacks.
- Diagnostics include render callbacks, backend callbacks, submitted buffers, dropped buffers, underruns, render duration, callback jitter, and telemetry age.
- Stale telemetry wall-clock timeout mutes effects until fresh parsed `VehicleState` arrives.

## Stage 11 Test Bench

- Synthetic silence, sine tone, sweep, pulse, and constant-value signals are available for internal path validation.
- Generated buffers pass through the Stage 10 mixer and safety chain before null-output submission.
- WASAPI remains manual/debug only and ASIO remains the later intended hardware path.

## Manual BST-1 ASIO Pulse

- The Devices page exposes `BST-1 ASIO Pulse Control` separately from the Null synthetic benchmark.
- The test supports user-controlled 0-100% strength, 10-80 Hz frequency, short millisecond durations, and a maximum continuous request of 1 second in the runtime API.
- The test is blocked unless Output mode is `ASIO Output`, the selected driver name is M-Audio / M-Track-like, ASIO is explicitly armed, emergency mute is clear, normal mute is off, and the selected output channel is valid.
- Manual BST-1 pulse testing does not require global Start Haptics; it may open/start a bounded temporary ASIO session, render and submit the short pulse, then stop again.
- The signal is injected into `HapticPipelineCoordinator` as an `AudioMixerInput`, then processed by the existing Stage 10 mixer, safety chain, limiter, and selected ASIO output channel routing.
- The manual test bypasses stale telemetry only for its own short pulse. Normal VehicleState-driven effects still require fresh telemetry.
- The active manual test state is runtime-only. It is not persisted and is never started automatically.
- The app reports ASIO status from internal output state: selected output mode, selected driver/channel, ASIO armed state, stream-running state, callback-active state, submitted/dropped frame counts, callback counts, last manual-pulse ASIO proof, last gear-pulse ASIO proof, and last ASIO error. Windows Sound Settings visibility is not proof of ASIO usage.
- `local-validation-results/bst1-asio-gear-flight-recorder.jsonl` records accepted, blocked, completed, and failed BST-1 manual and paddle-bench pulse attempts and is ignored local validation output.
- Automated tests use fake ASIO backends for this path and do not require M-Audio, Fosi, BST-1, SimHub, SimPro, or live F1 telemetry.

## BST-1 Paddle Gear Pulse

- Devices `BST-1 paddle gear pulse` is off by default.
- When enabled, accepted Paddle Gear Bench mapped `Pressed` events can fire a short BST-1 ASIO pulse from the same accepted bench event as the P-HPR gear pulse.
- The BST-1 pulse does not wait for F1 telemetry gear-change confirmation and does not require Start Haptics in bench mode.
- Duration can sync to the shared P-HPR gear-pulse duration or use a custom BST-1 duration because the audio shaker may feel different from the P-HPR modules. Custom BST-1 duration applies only when sync is unchecked.
- Release, held, repeat, unknown, unmapped, and suppressed events must not trigger BST-1 output.

## Local Gear Test Workflow

- Devices `Enable Local Gear Test Mode` is a runtime-only helper for local mapped-paddle testing.
- It can start the paddle listener and route local bench pulses without Start Haptics, UDP telemetry, replay, live F1 25, or cached `DrivingArmed`.
- It does not start continuous ASIO output or live telemetry effects.
- It still requires a usable selected listener, valid left/right paddle mapping, clear emergency state, P-HPR Direct readiness for P-HPR output, and ASIO Output plus selected M-Audio/M-Track driver, channel, and arm state for BST-1 output.

## Stage 12 Effects

- Gear shift and engine vibration generate deterministic source buffers from shared `VehicleState`.
- Generated effect buffers pass through the same Stage 10 mixer and safety chain before null-output submission.
- WASAPI remains manual/debug only and ASIO remains the later intended hardware path.

## Stage 15 Mock Pipeline And Visibility

- `HapticPipelineCoordinator` can render live or replayed F1 25 packets through parser, `VehicleState`, existing effects, mixer, safety processor, and `NullAudioOutputDevice`.
- `AsioDriverVisibilityDiagnostics` uses `IAsioDriverCatalog` and is non-blocking and hardware-absent safe.
- M-Audio / M-Track catalog visibility is only a preparation diagnostic for Stage 16; it does not select ASIO, start ASIO, or energize hardware.
- The M-Audio M-Track Solo may be connected locally, but automated tests use Null output and fake catalogs only.

## Stage 16 Manual Readiness

- The app can list ASIO driver names through `WindowsRegistryAsioDriverCatalog` when Windows exposes them.
- ASIO output must be selected deliberately; the app never auto-switches from Null to ASIO.
- A driver and single output channel must be selected deliberately.
- ASIO must be armed deliberately before Start Haptics can run it.
- Stop Haptics stops ASIO output, and switching away from ASIO must stop the old output path first.
- Stage 16 mono routing clears all routed channels and writes the safety-processed mono source only to the selected ASIO channel.
- Diagnostics report selected driver, sample rate, buffer size, output channel count when available, selected output channel, arming state, running state, buffer counters, drops, last error, and M-Audio / M-Track visibility.
- Hardware-dependent validation remains manual and opt-in. Prior skipped tests now run as readiness/pending checks, so the suite can report zero skipped tests without requiring physical output. Haptic Drive ASIO app-driven BST-1 output testing is performed only through deliberate local manual checks.

## Stage 17 Manual Streaming

- Use `docs/STAGE_17_NATIVE_ASIO_STREAMING.md` for the streaming checklist.
- ASIO output remains explicit and armed; it is never selected automatically.
- Drops and underruns should be treated as readiness/debugging signals, not final hardware latency data.
- Final shaker feel, safe physical gain, physical latency, and final frequency tuning remain unvalidated until the Dayton BST-1 chain is tested locally.

## Stage 18 Pre-Shaker Readiness

- `Run-HapticDrive.cmd` is the preferred launch path during development because it avoids normal PowerShell execution-policy blocks, checks the repo-local .NET 8 Desktop runtime, and starts the WPF executable.
- App settings persist theme, forwarding destinations, and last ASIO driver/channel selection only.
- Diagnostics can be copied and include output callback counters, packet-ID counts, forwarding state, recording/replay state, runtime prerequisite status, and ASIO readiness state.
- ASIO remains explicit: select output mode, select driver, select channel, arm, then Start Haptics.
