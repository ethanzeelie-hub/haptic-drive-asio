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

## Manual ASIO Bass Shaker Test

Use this only when the connected BST-1 chain is ready for a short app-driven pulse.

1. Open Devices.
2. Select `ASIO Output`.
3. Select the M-Audio / M-Track ASIO driver.
4. Select channel 0 or 1 deliberately.
5. Arm ASIO.
6. Press Start Haptics.
7. In `Manual ASIO Bass Shaker Test`, start with 250 ms at 40 Hz or 50 Hz.

The Null synthetic benchmark remains the automated-test path and does not energize the shaker.

## Paddle Gear Bench Test

Use this when mapped paddles need validation without live F1 telemetry.

1. Open Devices.
2. Enable and arm `Paddle Gear Bench Test`.
3. Keep output mode `Mock` first.
4. Press one mapped paddle and confirm accepted bench gear events plus mock gear routing count increase.
5. Use `Direct` only after the FeatureReport `0xF1` / 64-byte direct gates, coexistence, emergency stop, approval, road, slip, and lock checks are green.

Bench enable/arm state is not persisted, and normal live-driving shift intent still requires cached `DrivingArmed`.

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
2. Refresh direct-output candidates and select a HID device-interface candidate, not a Raw Input metadata-only candidate.
3. Run Open Check so the HID writer opens and closes without sending an output report.
4. Enable real direct control.
5. Arm real direct control.
6. Start with one low-strength brake pulse, then one low-strength throttle pulse.
7. Keep emergency stop visible.

Direct-control enablement, arming, selected private device path, emergency-stop latch, command history, and write history are not persisted.

## Controlled P-HPR Smoke Test

Before using real direct mode in a full session, run direct-output dry-run and open-check:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- direct-output-dry-run --candidate-index 0 --enable --arm --approval "I approve Phase 2 controlled P-HPR write testing"
```

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- direct-output-open-check --candidate-index 0 --enable --arm --approval "I approve Phase 2 controlled P-HPR write testing"
```

The controlled-write CLI remains dry-run by default:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- controlled-write-test --approval "I approve Phase 2 controlled P-HPR write testing" --device-path "<private-hid-path>" --target sequence --strength-percent 10 --frequency-hz 50 --duration-ms 50
```

Add `--execute` only when physically present, SimPro/SimHub coexistence is clear, emergency stop is visible, and the selected HID path has already passed no-report open-check.

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
