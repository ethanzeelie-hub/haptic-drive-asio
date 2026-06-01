# Known Issues

## Stage 00

- No product functionality exists yet; this stage only creates the repository foundation.
- The F1 25 PDF has not yet been extracted into implementation notes.
- No audio output abstractions, telemetry listener, parser, recording, replay, or haptic effects are implemented yet.
- The physical Dayton BST-1 hardware is not available, so no physical tuning claims can be made.

## Stage 01

- The app shell is static; navigation pages contain placeholders only.
- Start/Stop Haptics and Emergency Mute are UI state placeholders and are not connected to an audio pipeline.
- The light theme button demonstrates theme scaffolding, but theme persistence is not implemented.
- Close/minimize-to-tray is represented as a disabled setting placeholder.
- No telemetry, parser, recording, replay, output device, mixer, safety processor, or haptic effect behavior exists yet.

## Stage 02

- `NullAudioOutputDevice` changes state but does not consume or render audio samples yet.
- `WasapiDebugOutputDevice` is a manual debug placeholder; it does not output sound yet.
- `AsioAudioOutputDevice` can select a driver through a catalog seam, but real ASIO streaming is not implemented.
- The default ASIO driver catalog reports no drivers until a real discovery implementation is added.
- Manual hardware tests are present only as skipped markers.
- The app still has no telemetry, parser, recording, replay, mixer, safety processor, or haptic effect behavior.

## Stage 03

- The F1 25 PDF has been summarized into implementation notes, but no parser code exists yet.
- Packet field offsets beyond `PacketHeader` have not been encoded in code yet.
- Parser tests listed in `docs/F1_25_PACKET_SPEC_IMPLEMENTATION.md` are not implemented yet.
- The PDF remains outside the repository; future work depends on the extracted notes unless the source PDF is supplied again.
- The app still has no UDP listener, packet parser, recording, replay, mixer, safety processor, or haptic effect behavior.
