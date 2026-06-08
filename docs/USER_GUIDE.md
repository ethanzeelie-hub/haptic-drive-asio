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

Stage 2Q/2R do not prove physical safety or pedal mapping. Use only supervised local validation.

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
- Stop immediately if the wrong pedal vibrates, both pedals vibrate unexpectedly, vibration continues after stop, output feels too strong, or SimPro/SimHub conflict appears.
- Do not commit raw captures, private device paths, serial numbers, or unsanitized hardware inventories.
