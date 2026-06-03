# Stage 15 Mock Pipeline Validation

Stage 15 is the first playable software-pipeline milestone. It proves that live-like F1 25 UDP packets or recorded replay packets can flow through parser, `VehicleState`, existing haptic effects, mixer, safety processor, and `NullAudioOutputDevice` without requiring hardware.

## Safe Defaults

- Default output remains `NullAudioOutputDevice`.
- No hardware is required for automated build, test, replay, or mock output validation.
- The M-Audio M-Track Solo may be present on the user's PC, but Stage 15 does not automatically select it or stream to it.
- Windows sound output visibility is not proof of ASIO usage.
- ASIO usage must be confirmed through the app's ASIO driver/output path, and real ASIO streaming remains Stage 16/manual work.
- The Fosi amplifier and Dayton BST-1 shaker are not required for Stage 15. Physical shaker feel, safe gain, latency, and final frequency tuning are not validated yet.

## App Checklist

1. Start the app and confirm the Devices page reports `NullAudioOutputDevice` as the current/default output.
2. Press `Start Haptics`. The pipeline should start and render safe buffers to Null output.
3. Press `Emergency Mute`. Diagnostics should show emergency mute active and output peak should remain zero.
4. Clear emergency mute and use the Test Bench if you want an independent synthetic signal check.
5. Optional live path: enable F1 25 UDP telemetry to the configured listen port. Raw packets should be counted, parser and `VehicleState` diagnostics should update for valid packets, and haptic effects should render through Null output.
6. Optional replay path: capture a recording, then use `Replay Latest` on the Recordings page. Replayed packets feed the same parser, `VehicleState`, effects, mixer, safety, and Null output path without opening UDP sockets.
7. Review Diagnostics for pipeline state, input source, parser counts, `VehicleState` updates, active effects, mixer/output peak, safety limiter counts, recording/replay state, output mode, Null buffer counts, and ASIO visibility status.

## M-Audio / ASIO Visibility

Stage 15 includes optional ASIO driver-catalog visibility diagnostics behind `IAsioDriverCatalog`. Automated tests use fake catalogs. The default local catalog still reports no drivers unless a real discovery implementation is added later.

If an M-Audio / M-Track driver name is reported by the catalog, treat that as visibility only. It does not mean the app is streaming through ASIO, does not prove physical latency, and does not validate shaker output.

## Deferred To Stage 16

- Real ASIO callback streaming.
- Manual M-Audio driver readiness workflow.
- Physical Dayton BST-1 testing once the shaker arrives.
- Physical gain calibration, latency measurement, and final frequency tuning.
- Any automatic output switching away from Null output.

