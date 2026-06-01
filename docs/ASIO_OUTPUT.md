# ASIO Output

ASIO is the intended low-latency output path for the real bass shaker chain. Stage 02 adds the abstraction and graceful failure behavior, not real sample streaming.

## Stage 02 Implementation

- `IAudioOutputDevice` lives in `HapticDrive.Asio.Core.Audio`.
- `AsioAudioOutputDevice` lives in `HapticDrive.Asio.Audio.Devices`.
- The preferred driver name is `M-Audio M-Track Solo and Duo ASIO`.
- Driver discovery is behind `IAsioDriverCatalog`.
- The default driver catalog reports no drivers, so ASIO fails safely when hardware/driver discovery is unavailable.
- A fake driver catalog is used in automated tests to validate driver selection without hardware.

## Current Limitations

- No NAudio ASIO callback is wired yet.
- No audio samples are generated or sent to any device.
- Buffer size, channel selection, and sample streaming are still future work.
- ASIO failure does not select WASAPI automatically.

## Target Defaults

- Sample rate: 48000 Hz.
- Channels: 1 mono channel for the first BST-1 target.
- Buffer size: 128 samples as the default future ASIO target.
- Buffer size 64 remains experimental.
- Buffer size 256 remains a fallback.
