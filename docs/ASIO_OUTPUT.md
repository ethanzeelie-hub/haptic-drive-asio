# ASIO Output

ASIO is the intended low-latency output path for the real bass shaker chain. Stage 02 adds the abstraction and graceful failure behavior. Stage 10 adds internal sample buffers, mixer processing, safety processing, and null-output sample consumption. Stage 11 adds deterministic test-bench signals. Stage 12 and Stage 13 add VehicleState-driven effect source buffers. Stage 15 adds optional ASIO driver-catalog visibility diagnostics. Stage 16 adds Windows ASIO driver-name discovery, explicit ASIO selection/arming/channel routing, readiness diagnostics, and fake-backend tests. Stage 17 adds native ASIO streaming behind `IAsioOutputBackend`, output-owned render cadence, stale telemetry mute, and render/backend diagnostics.

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
- Physical Dayton BST-1 output has not been validated yet.
- Test bench, Stage 12/13 effect buffers, and Stage 15/16 pipeline renders use `NullAudioOutputDevice` by default and do not prove physical latency, safe gain, or shaker response.
- Real device sample streaming remains local Windows validation work before any shaker claims.
- ASIO failure does not select WASAPI automatically.
- Windows sound output visibility is not proof of ASIO usage.

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
- Hardware-dependent validation remains manual and skipped by default. Dayton BST-1 physical output testing is deferred until the shaker arrives.

## Stage 17 Manual Streaming

- Use `docs/STAGE_17_NATIVE_ASIO_STREAMING.md` for the streaming checklist.
- ASIO output remains explicit and armed; it is never selected automatically.
- Drops and underruns should be treated as readiness/debugging signals, not final hardware latency data.
- Final shaker feel, safe physical gain, physical latency, and final frequency tuning remain unvalidated until the Dayton BST-1 chain is tested locally.
