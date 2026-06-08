# Haptic Drive ASIO User Guide

## Running The App

Use the repo launcher:

```powershell
.\Run-HapticDrive.cmd
```

The app starts in hardware-absent safe mode. `NullAudioOutputDevice` remains the default output.

## ASIO / BST-1 Path

ASIO output requires explicit output-mode selection, driver selection, output-channel selection, arming, and Start Haptics.

The ASIO/BST-1 path is separate from Simagic P-HPR output.

## Simagic P-HPR Direct Control

Use the Devices page.

`P-HPR Real Direct Control` starts disabled and unarmed every launch. Device path, enable state, armed state, emergency stop latch, and write history are not persisted.

Manual direct-control pulse buttons require:

- direct control enabled,
- direct control armed,
- selected device/interface/report,
- SimPro/SimHub coexistence `Clear`,
- emergency stop clear,
- safety limiter acceptance.

Phase 3A adds hardened output-adapter diagnostics. The Devices page shows connection state, writer-open state, open/close counters, last open/write/stop/close status, disconnect count, timeout count, invalid-report count, and the active write timeout. These are diagnostics only; they do not auto-open the device or auto-trigger vibration.

Phase 3B adds production instant paddle gear-pulse integration. Accepted GT Neo paddle presses can route immediately to brake and/or throttle P-HPR gear pulses when direct control is explicitly enabled and armed. Brake and throttle gear-pulse enabled state, strength, frequency, and duration are persisted as safe preferences; direct-control enable, arm, device path, emergency stop, and write history remain runtime-only.

The instant route does not wait for F1 25 telemetry gear-change confirmation. Cached `DrivingArmed` / Menu Safe state still suppresses paddle pulses in menus, stale telemetry, stopped haptics, emergency mute, and other unsafe states.

The Devices page reports software latency timestamps for the last real gear-pulse route: paddle event, accepted shift intent, command creation, and write completion. These are diagnostics only, not physical latency measurements.

Phase 3C adds real road-vibration routing, disabled by default. Real road settings persist safely for brake and throttle independently, with minimum/maximum strength, minimum/maximum frequency, and duration. The route uses the existing telemetry/status path and does not touch the ASIO/BST-1 road texture effect.

Phase 3D adds real wheel-slip and wheel-lock routing, disabled by default. Wheel slip defaults to throttle, wheel lock defaults to brake, and each effect can target brake, throttle, or both. Slip/lock settings persist safely for enabled state, target, strength, frequency, and duration. Gear pulse remains highest priority, slip/lock stay above road vibration, and the ASIO/BST-1 slip effect remains separate.

Stage 2Q through Phase 3D do not prove physical safety, latency, pedal mapping, road feel, slip feel, or lock feel. Use only supervised local validation.

## Controlled Validation Harness

The Devices page includes `P-HPR Controlled Validation Harness`.

Use it to:

- confirm user presence,
- confirm P700 connection,
- confirm brake/throttle module installation,
- check direct-control readiness,
- record brake/throttle/emergency-stop/paddle results,
- export a private local Markdown result.

The harness does not trigger hardware output. It only evaluates readiness and exports notes.

Private results are written under `local-validation-results/` when the repo root is found. Do not commit private results.

## Safety Reminders

- Do not run unattended P-HPR output.
- Do not use high strength for first tests.
- Do not loop pulses for first tests.
- Keep real road vibration disabled until manual one-pulse brake/throttle validation has passed.
- Keep real slip/lock routing disabled until manual one-pulse brake/throttle validation has passed.
- Stop immediately if the wrong pedal vibrates, both pedals vibrate unexpectedly, vibration continues after stop, output feels too strong, or SimPro/SimHub conflict appears.
- Do not commit raw captures, private device paths, serial numbers, or unsanitized hardware inventories.
