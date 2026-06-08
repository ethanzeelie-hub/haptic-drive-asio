# Simagic P-HPR Replay Validation

## Phase 3F Status

Phase 3F validates that recorded or synthetic replay telemetry can drive P-HPR road vibration, wheel slip, and wheel lock software routing without requiring live F1 25.

Replay remains a software validation path. It does not send real HID output reports by itself, does not create real hardware vibration, and does not synthesize GT Neo paddle events.

## Validated Path

The tested replay path is:

```text
.hdrec / synthetic TelemetryRecording
-> TelemetryReplayService
-> HapticPipelineCoordinator.OfferReplayTelemetryPacket
-> F1 25 v3 parser
-> VehicleState adapter
-> DrivingArmedStateService
-> P-HPR road/slip/lock routers
-> SafetyLimitedPhprOutputDevice
-> MockPhprOutputDevice
```

The replay tests use synthetic F1 25 v3 packets built from the parser's existing packet definitions. They do not rely on raw private captures and do not add new packet fields to the production parser.

## Coverage

Automated Phase 3F tests cover:

- replay packets setting `HapticPipelineInputSource.Replay`,
- replay packet counts in diagnostics,
- replay telemetry updating `VehicleState`,
- replay snapshots arming cached `DrivingArmed` when active-driving telemetry is fresh,
- road vibration routing from replayed telemetry,
- wheel-slip routing from replayed telemetry plus Motion Ex data,
- wheel-lock routing from replayed telemetry plus Motion Ex data,
- profile-style settings such as road target and per-effect enable flags,
- stale replay telemetry rejection,
- emergency mute rejection,
- no `PaddleShiftIntent` command source from replay-only tests.

## Diagnostics

The app now surfaces replay source context in P-HPR workflow and diagnostics text:

- current pipeline input source,
- replay source file name or `in-memory replay`,
- replay packet count,
- note that replay does not synthesize gear-paddle events.

Only the replay file name is shown in P-HPR workflow text. Raw private paths, raw captures, serial numbers, and device inventories are not added.

## Non-Claims

Phase 3F does not prove:

- physical P-HPR vibration,
- real pedal mapping,
- safe gain,
- stop behavior,
- sustained-vibration behavior,
- physical latency,
- live F1 25 behavior,
- GT Neo paddle input behavior.

Those remain manual/local validation items.
