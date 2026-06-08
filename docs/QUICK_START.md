# Haptic Drive ASIO Quick Start

## Run

```powershell
cd "C:\Users\ethan\OneDrive\Documents\ASIO Haptic Engine Program"
.\Run-HapticDrive.cmd
```

The app starts with `NullAudioOutputDevice` as the safe default. ASIO and real P-HPR direct control require explicit selection and arming.

## Confirm F1 25 Telemetry

1. In F1 25, enable UDP telemetry and use port `20778`.
2. Open Haptic Drive ASIO.
3. Check Dashboard or Diagnostics for UDP packet count, parser valid count, VehicleState updates, and telemetry age.
4. Unknown packets are ignored safely; forwarding and recording preserve raw packet bytes.

## Confirm Wheel / Paddle Input

1. Open Devices.
2. Use Refresh Input Devices.
3. Select the GT Neo / wheel input candidate.
4. Press left and right paddles and confirm last-changed button diagnostics.
5. Map left and right paddles.
6. Confirm Shift Intent Diagnostics show accepted or suppressed events with the current `DrivingArmed` reason.

## Mock P-HPR First

Use mock mode before real direct control.

1. Enable mock gear routing.
2. Keep conservative strength, frequency, and duration.
3. Confirm accepted paddle presses create mock P-HPR gear-pulse diagnostics.
4. Confirm mock road, wheel slip, and wheel lock diagnostics only after telemetry and `DrivingArmed` are sensible.

Mock mode records commands and frames in memory only. It does not open hardware or vibrate P-HPR modules.

## Real Direct Mode

Use real direct mode only under local supervision.

1. Confirm SimPro Manager and SimHub coexistence status is `Clear`.
2. Select the P-HPR device/interface/report for this session.
3. Enable real direct control.
4. Arm real direct control.
5. Start with one low-strength brake pulse, then one low-strength throttle pulse.
6. Keep emergency stop visible.

Direct-control enablement, arming, selected private device path, emergency-stop latch, command history, and write history are not persisted.

## Configure P-HPR Effects

Use Devices to configure:

- brake gear pulse enabled, strength, frequency, duration,
- throttle gear pulse enabled, strength, frequency, duration,
- road vibration brake/throttle min/max strength, min/max frequency, duration,
- wheel slip target, strength range, frequency range, duration,
- wheel lock target, strength range, frequency range, duration.

Gear pulse priority is highest, then wheel lock, wheel slip, and road vibration.

## Replay And Live Validation

Use Recordings to replay `.hdrec` files for deterministic software validation of road/slip/lock routing.

Use Devices `P-HPR Live F1 Validation` for the supervised live checklist. It does not trigger hardware output or prove physical validation by itself.

## Final Docs

- User guide: `docs\USER_GUIDE.md`
- Troubleshooting: `docs\TROUBLESHOOTING.md`
- Final acceptance: `docs\FINAL_P_HPR_ACCEPTANCE.md`
