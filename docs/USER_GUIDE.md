# Haptic Drive ASIO User Guide

## Running The App

Use the repo launcher:

```powershell
.\Run-HapticDrive.cmd
```

The app starts in hardware-absent safe mode. `NullAudioOutputDevice` remains the default output.

## ASIO / BST-1 Path

ASIO output requires explicit output-mode selection, driver selection, output-channel selection, arming, and Start Haptics.

The ASIO/BST-1 path is separate from Simagic P-HPR output. ASIO uses the audio effect engine, mixer, audio safety processor, and `IAudioOutputDevice`. P-HPR uses a separate actuator output path and never routes through ASIO.

Physical Dayton BST-1 feel, safe gain, latency, and final frequency tuning remain pending local hardware validation.

## F1 25 Telemetry

The app listens for F1 25 UDP telemetry on port `20778` by default.

Use the Dashboard, Telemetry / UDP Router page, and Diagnostics page to confirm:

- listener running state,
- packet count,
- packet rate,
- parser valid / ignored / failed counts,
- VehicleState update count,
- telemetry age,
- stale-telemetry mute state.

Unknown or malformed packets are ignored safely. Raw UDP bytes are preserved for recording, replay, and forwarding.

## UDP Forwarding

Use the Telemetry / UDP Router page to add forwarding destinations.

Forwarding preserves packet payload bytes exactly. It does not depend on parser success, haptics running state, ASIO output, or P-HPR output.

The app blocks obvious forwarding loops back to the local listener port.

## Recording And Replay

Use the Recordings page to record raw telemetry packets and replay saved `.hdrec` files.

Replay feeds recorded packets through the same parser, VehicleState adapter, effects, mixer, and diagnostics path as live telemetry. Automated replay does not generate real P-HPR hardware writes.

## P-HPR Mock Mode

Use the Devices page.

Mock P-HPR routing is hardware-safe and records in-memory mock commands and mock frames only. It does not open devices, write HID reports, or vibrate hardware.

Mock controls cover:

- instant gear pulse from accepted shift intent,
- road vibration,
- wheel slip,
- wheel lock,
- target pedal,
- strength,
- frequency,
- duration,
- route counts,
- safety rejections,
- mock emergency stop.

## P-HPR Real Direct Mode

`P-HPR Real Direct Control` starts disabled and unarmed every launch.

Manual real output requires:

- direct control enabled,
- direct control armed,
- selected device/interface/report,
- SimPro/SimHub coexistence `Clear`,
- emergency stop clear,
- safety limiter acceptance.

Device path, enable state, armed state, emergency stop latch, command history, and write history are not persisted.

## Instant Gear Pulse

Accepted GT Neo paddle presses can route immediately to brake and/or throttle P-HPR gear pulses when direct control is explicitly enabled and armed.

The route is:

```text
mapped paddle press
-> cached DrivingArmed/Menu Safe gate
-> accepted ShiftIntentEvent
-> P-HPR gear pulse
```

It does not wait for F1 25 telemetry gear-change confirmation. Upshift and downshift use the same default pulse.

Brake and throttle gear-pulse settings are independent: enabled, strength, frequency, and duration.

## Road Vibration

Real road vibration is disabled by default.

Road settings are independent for brake and throttle:

- enabled,
- minimum strength,
- maximum strength,
- minimum frequency,
- maximum frequency,
- duration.

Road routing requires fresh telemetry, haptics running, cached `DrivingArmed` true, clear coexistence, selected real output, clear emergency stop, and safety-limiter acceptance. The ASIO/BST-1 road texture effect remains separate.

## Wheel Slip And Wheel Lock

Real wheel slip and wheel lock routing is disabled by default.

Wheel slip defaults to throttle. Wheel lock defaults to brake. Each effect can target brake, throttle, or both pedals and has independent strength, frequency, and duration settings.

Priority is:

1. instant gear pulse,
2. wheel lock,
3. wheel slip,
4. road vibration.

The ASIO/BST-1 slip and brake-lock effect remains separate.

## Emergency Stop

P-HPR emergency stop attempts brake and throttle stop reports when a real device is selected, then latches the safety state.

Clearing emergency stop only clears the latch. It does not enable or arm direct control.

Use emergency stop immediately if the wrong pedal vibrates, both pedals vibrate unexpectedly, output feels too strong, vibration continues after stop, or the device disconnects.

## SimPro / SimHub Warnings

The app uses read-only process detection for SimPro Manager and SimHub.

It does not kill, hook, inject into, patch, control, or modify either application.

Direct real P-HPR starts are blocked unless coexistence status is `Clear`.

## Profiles

Use the Profiles page to save/load:

- the audio profile for ASIO/BST-1 effect, mixer, and audio safety settings,
- the P-HPR effect profile for shift intent, mock routing, real gear pulse, road vibration, wheel slip, and wheel lock preferences.

Profiles do not save:

- emergency mute,
- ASIO armed state,
- haptics running state,
- direct-control enablement,
- direct-control arming,
- selected private P-HPR HID path,
- emergency-stop latch,
- command/write history.

Loading a P-HPR profile applies safe effect preferences while preserving runtime-only arm/device state.

## Diagnostics

The Diagnostics page and copied report include:

- UDP, parser, VehicleState, recording, replay, audio, and output status,
- input discovery and paddle listener status,
- shift-intent status,
- P-HPR workflow mode,
- coexistence state,
- mock and real P-HPR settings,
- real write status and last error,
- validation status,
- profile paths,
- persistence boundary notes.

Diagnostics do not include raw captures, serial numbers, private validation results, or unsanitized hardware inventories.

## Controlled Validation Harness

The Devices page includes `P-HPR Controlled Validation Harness`.

It does not trigger hardware output. It evaluates readiness and exports private local notes under `local-validation-results/`.

Do not commit private validation results.

## Safety Reminders

- Do not run unattended P-HPR output.
- Do not use high strength for first tests.
- Do not loop pulses for first tests.
- Keep real road vibration and slip/lock routing disabled until manual one-pulse brake/throttle validation has passed.
- Stop immediately if behavior is wrong or stronger than expected.
- Do not commit raw captures, private device paths, serial numbers, or unsanitized hardware inventories.

Stage 2Q through Phase 3E do not prove physical safety, latency, pedal mapping, road feel, slip feel, or lock feel. Use only supervised local validation.
