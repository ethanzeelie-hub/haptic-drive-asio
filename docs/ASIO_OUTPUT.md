# ASIO Output

ASIO is the intended low-latency output path for the real bass shaker chain. Stage 02 adds the abstraction and graceful failure behavior. Stage 10 adds internal sample buffers, mixer processing, safety processing, and null-output sample consumption, but not real ASIO streaming.

## Stage 02 Implementation

- `IAudioOutputDevice` lives in `HapticDrive.Asio.Core.Audio`.
- `AsioAudioOutputDevice` lives in `HapticDrive.Asio.Audio.Devices`.
- The preferred driver name is `M-Audio M-Track Solo and Duo ASIO`.
- Driver discovery is behind `IAsioDriverCatalog`.
- The default driver catalog reports no drivers, so ASIO fails safely when hardware/driver discovery is unavailable.
- A fake driver catalog is used in automated tests to validate driver selection without hardware.

## Current Limitations

- No NAudio ASIO callback is wired yet.
- No audio samples are streamed to a real ASIO or WASAPI device.
- Buffer size, channel selection, and real device sample streaming are still future work.
- ASIO failure does not select WASAPI automatically.

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
