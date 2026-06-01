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

## Stage 04

- The UDP listener receives and counts raw datagrams only; no F1 25 packet parser is attached yet.
- UDP forwarding is not implemented yet, so the listener does not currently relay packets to other tools.
- Packet counters, packet rate, timestamps, and no-packet warning are in memory only and reset when the listener restarts.
- The app defaults to port `20778`; startup reports an unavailable listener if the port is already in use.
- Listen port, bind address, and no-packet warning threshold are not configurable through the UI yet.
- The app still has no recording, replay, mixer, safety processor, generated audio, real WASAPI output, real ASIO streaming, or haptic effect behavior.

## Stage 05

- The UDP forwarder is implemented in Core, but the shell does not yet provide UI controls for adding or editing destinations.
- The shell currently starts with zero forwarding destinations configured, so forwarding status is visible but disabled by default.
- Forwarding counters are in memory only and reset when the app restarts.
- Forwarding is raw-byte only and does not validate whether packets are real F1 25 packets yet.
- Parser work, packet ID diagnostics, recording, replay, mixer, safety processor, generated audio, real WASAPI output, real ASIO streaming, and haptic effects remain unimplemented.
