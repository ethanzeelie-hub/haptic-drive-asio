# ASIO Output

ASIO is the intended low-latency output path for the real bass shaker chain. Stage 02 adds the abstraction and graceful failure behavior. Stage 10 adds internal sample buffers, mixer processing, safety processing, and null-output sample consumption. Stage 11 adds deterministic test-bench signals. Stage 12 and Stage 13 add VehicleState-driven effect source buffers. Stage 15 adds optional ASIO driver-catalog visibility diagnostics, but not real ASIO streaming.

## Stage 02 Implementation

- `IAudioOutputDevice` lives in `HapticDrive.Asio.Core.Audio`.
- `AsioAudioOutputDevice` lives in `HapticDrive.Asio.Audio.Devices`.
- The preferred driver name is `M-Audio M-Track Solo and Duo ASIO`.
- Driver discovery is behind `IAsioDriverCatalog`.
- The default driver catalog reports no drivers, so ASIO fails safely when hardware/driver discovery is unavailable.
- A fake driver catalog is used in automated tests to validate driver selection without hardware.
- Stage 15 visibility diagnostics can report whether a fake or future real catalog lists an M-Audio / M-Track-like ASIO driver.

## Current Limitations

- No NAudio ASIO callback is wired yet.
- No audio samples are streamed to a real ASIO or WASAPI device.
- Test bench, Stage 12/13 effect buffers, and Stage 15 mock pipeline renders use `NullAudioOutputDevice` by default and do not prove physical latency, safe gain, or shaker response.
- Buffer size, channel selection, and real device sample streaming are still future work.
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
- `AsioAudioOutputDevice` still fails safely when no driver is available and does not implement real callback streaming.

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
