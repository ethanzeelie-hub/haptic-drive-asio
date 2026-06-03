# ASIO Output

ASIO is the intended low-latency output path for the real bass shaker chain. Stage 02 adds the abstraction and graceful failure behavior. Stage 10 adds internal sample buffers, mixer processing, safety processing, and null-output sample consumption. Stage 11 adds deterministic test-bench signals. Stage 12 and Stage 13 add VehicleState-driven effect source buffers. Stage 15 adds optional ASIO driver-catalog visibility diagnostics. Stage 16 adds Windows ASIO driver-name discovery, explicit ASIO selection/arming/channel routing, readiness diagnostics, and fake-backend tests.

## Stage 02 Implementation

- `IAudioOutputDevice` lives in `HapticDrive.Asio.Core.Audio`.
- `AsioAudioOutputDevice` lives in `HapticDrive.Asio.Audio.Devices`.
- The preferred driver name is `M-Audio M-Track Solo and Duo ASIO`.
- Driver discovery is behind `IAsioDriverCatalog`.
- `WindowsRegistryAsioDriverCatalog` reads standard Windows ASIO registry locations when available.
- A fake driver catalog is used in automated tests to validate driver selection without hardware.
- Visibility/readiness diagnostics can report whether a fake or real catalog lists an M-Audio / M-Track-like ASIO driver.

## Current Limitations

- No native ASIO callback streaming backend is wired yet.
- `IAsioOutputBackend` isolates the remaining native streaming work from the safety chain and app UI.
- Test bench, Stage 12/13 effect buffers, and Stage 15/16 pipeline renders use `NullAudioOutputDevice` by default and do not prove physical latency, safe gain, or shaker response.
- Real device sample streaming remains local Windows validation work.
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
