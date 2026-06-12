# Haptic Drive ASIO User Guide

## Running The App

Use the repo launcher:

```powershell
.\Run-HapticDrive.cmd
```

If the M-Audio M-Track Solo and Duo ASIO driver is discoverable, the app starts with ASIO Output, that driver, channel `1`, and Arm ASIO selected, but it does not emit output. If that driver is not discoverable, the app falls back to `NullAudioOutputDevice`.

## ASIO / BST-1 Path

ASIO output requires the M-Audio/M-Track driver, output channel, arming, clear mutes, and a deliberate output action. Live telemetry effects also require Start Haptics and valid telemetry. Manual BST-1 pulse testing does not require Start Haptics.

The ASIO/BST-1 path is separate from Simagic P-HPR output. ASIO uses the audio effect engine, mixer, audio safety processor, and `IAudioOutputDevice`. P-HPR uses a separate actuator output path and never routes through ASIO.

Channel 1 is the locally validated BST-1 ASIO output channel. Physical Dayton BST-1 safe gain, latency, and final feel/tuning remain local validation items.

Windows Sound Settings visibility does not prove ASIO usage. `ASIO READY` means the selected/armed/channel/error gates are ready while the stream can still be stopped. `ASIO ACTIVE` appears only while the stream and callback are actually active. Detailed callback, submitted/dropped frame, last manual-pulse proof, last gear-pulse proof, and error diagnostics live under Advanced / Diagnostics.

## Manual BST-1 ASIO Pulse

Use Devices `BST-1 ASIO Pulse Control` only when you deliberately want this app to energize the connected BST-1.

Before pressing `Test BST-1 Pulse`, confirm:

- Output mode is `ASIO Output`,
- the selected driver is the M-Audio / M-Track ASIO driver,
- channel `1` is selected unless you are deliberately testing another channel,
- ASIO is armed,
- Emergency Mute is clear,
- normal mute is off.

Set BST-1 strength from `0-100%`, output trim from `25-400%` (`200%` default), frequency in the Dayton BST-1 normal control range `10-80 Hz`, and duration as a short millisecond pulse. The pulse runs through the app mixer, safety chain, limiter, and selected ASIO output channel. Output trim calibrates ASIO bass-shaker level without changing P-HPR strength. It is separate from the Null synthetic benchmark, which remains the safe automated-test path.

The status text separates ASIO readiness from the continuous stream. `ASIO READY` while the stream is stopped is expected for a stopped app that is still ready to run a bounded manual pulse.

## BST-1 Paddle Gear Pulse

In Devices `Bass Shaker / ASIO`, `BST-1 paddle gear pulse` is off by default. When enabled for local bench validation, each accepted Paddle Gear Bench mapped `Pressed` event can fire a short BST-1 ASIO pulse alongside the P-HPR bench pulse.

Controls:

- strength percent,
- shared BST-1 output trim,
- frequency Hz in the `10-80 Hz` normal range,
- duration synced to the shared P-HPR gear pulse duration or a custom BST-1 duration,
- selected ASIO channel, with channel `1` locally validated.

This bench path does not wait for F1 telemetry gear-change confirmation and does not require Start Haptics. Live driving effects still use their normal haptics and telemetry gates.

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

## Wheel / Paddle Input

Use Devices to refresh input devices, select the GT Neo / wheel input candidate, and map the left and right paddles from last-changed button diagnostics.

The paddle listener is read-only. It observes button changes and feeds the Shift Intent layer; it does not write to wheel hardware.

Shift Intent Diagnostics show:

- listener state,
- selected device,
- mapped left/right buttons,
- last raw button change,
- last mapped paddle event,
- accepted shift-intent count,
- suppressed shift-intent count,
- current `DrivingArmed` state and reason.

Keep `InstantPaddleOnly` as the default mode for immediate gear pulse routing. It still requires cached `DrivingArmed` true.

## Paddle Gear Bench Test

Use Devices `Paddle Gear Bench Test` for local validation when live F1 telemetry is unavailable.

Bench mode is disabled and unarmed by default, is not persisted, and still requires mapped paddles. A mapped left or right GT Neo paddle can create a local bench shift-intent event without recent telemetry. Normal live-driving shift intent is unchanged and still suppresses paddle events when cached `DrivingArmed` is false.

`Enable Local Gear Test Mode` is the local bench workflow for mapped paddles. It may auto-start the paddle listener and does not require Start Haptics, UDP telemetry, live F1 25, replay, or cached `DrivingArmed`. It still requires valid listener/mapping state, clear emergency stop, P-HPR Direct readiness for P-HPR output, and ASIO Output plus selected M-Audio/M-Track driver, channel, and arm state for BST-1 output.

Start with output mode `Mock`. Mock bench routing increments mock gear routing diagnostics only; it does not send HID reports and does not route to ASIO.

Use output mode `Direct` only after the normal direct P-HPR gates are green: selected device, FeatureReport transport, report ID `0xF1`, 64-byte report length, open-check succeeded, report shape/capability accepted, approval confirmed, coexistence `Clear`, emergency stop clear, road vibration disabled, and slip/lock disabled. Direct Bench uses the same shared P-HPR gear-pulse duration and brake/throttle pulse settings as the blue Test Brake/Throttle buttons and records local recovery diagnostics under `local-validation-results/`. Stage 18g allows latest-press-wins retriggering with generation-guarded stops for rapid bench shifts; physical stop feel, safe gain, and latency still require local validation.

## UDP Forwarding

Use the Telemetry / UDP Router page to add forwarding destinations.

Forwarding preserves packet payload bytes exactly. It does not depend on parser success, haptics running state, ASIO output, or P-HPR output.

The app blocks obvious forwarding loops back to the local listener port.

## Recording And Replay

Use the Recordings page to record raw telemetry packets and replay saved `.hdrec` files.

Replay feeds recorded packets through the same parser, VehicleState adapter, effects, mixer, and diagnostics path as live telemetry. Automated replay does not generate real P-HPR hardware writes.

For P-HPR, replay can validate road vibration, wheel slip, and wheel lock software routing through mock/fake output because those effects come from `VehicleState`. Replay does not create GT Neo paddle events, so it does not validate instant gear-pulse input unless a later explicit synthetic-input test path is added.

The Devices P-HPR workflow summary and Diagnostics report show the current pipeline input source, replay source file name or in-memory replay status, and replay packet count.

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

Paddle Gear Bench Direct uses the same real direct backend plus additional validation gates for the known FeatureReport `0xF1` / 64-byte path and for disabling road, slip, and lock routes during bench pulses.

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

Brake and throttle gear-pulse settings are independent for enabled state, strength, and frequency. Duration is shared by brake P-HPR, throttle P-HPR, Direct Paddle Gear Bench, and BST-1 sync mode.

To configure brake or throttle gear pulse, use Devices `P-HPR Real Direct Control`:

- enable or disable the brake pedal pulse,
- set brake strength and frequency,
- enable or disable the throttle pedal pulse,
- set throttle strength and frequency,
- set the shared P-HPR gear pulse duration.

Use low strength and short duration first. Upshift and downshift use the same default pulse.

## Road Vibration

Real road vibration is disabled by default.

Road settings are independent for brake and throttle:

- enabled,
- minimum strength,
- maximum strength,
- minimum frequency,
- maximum frequency,
- duration.

Road routing requires fresh telemetry, haptics running, cached `DrivingArmed` true, clear coexistence, selected real output, clear emergency stop, and safety-limiter acceptance. P-HPR road vibration and ASIO/BST-1 road texture consume the same shared software road signal, but their output paths and safety chains remain separate.

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

`P-HPR Stop All / Clear Device State` is the Direct Bench recovery button. It sends stop-only brake/throttle reports through the same direct output path and clears the Direct Bench unclean-shutdown marker only after the runtime reports success. It does not start vibration, enable direct control, or prove physical stop response without local observation.

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
- live F1 validation checklist status,
- profile paths,
- persistence boundary notes.

Diagnostics do not include raw captures, serial numbers, private validation results, or unsanitized hardware inventories.

## Controlled Validation Harness

The Devices page includes `P-HPR Controlled Validation Harness`.

It does not trigger hardware output. It evaluates readiness and exports private local notes under `local-validation-results/`.

Do not commit private validation results.

## Live F1 25 P-HPR Validation

The Devices page includes `P-HPR Live F1 Validation`.

Use it after replay/mock checks when you are physically present. The checklist shows live telemetry status, `DrivingArmed`, paddle listener status, P-HPR mode, selected output readiness, SimPro/SimHub coexistence, emergency stop, road vibration, and slip/lock status.

Manual order:

1. App open, direct control disabled.
2. F1 25 telemetry active.
3. `DrivingArmed` true in session.
4. Paddle press accepted.
5. Mock mode gear pulse diagnostics.
6. Real mode armed manually.
7. Brake/throttle gear pulse test.
8. Road vibration test.
9. Slip/lock test if safe.
10. Menu/tabbing suppression.
11. Emergency stop.
12. SimPro/SimHub conflict warning.

The checklist and diagnostics do not trigger hardware output. They do not prove physical safety, latency, pedal mapping, road feel, slip feel, lock feel, or SimPro/SimHub real-device coexistence until a supervised local run is completed and recorded.

## Safety Reminders

- Do not run unattended P-HPR output.
- Do not use high strength for first tests.
- Do not loop pulses for first tests.
- Keep real road vibration and slip/lock routing disabled until manual one-pulse brake/throttle validation has passed.
- Stop immediately if behavior is wrong or stronger than expected.
- Do not commit raw captures, private device paths, serial numbers, or unsanitized hardware inventories.

Stage 2Q through Phase 3H do not prove physical safety, latency, pedal mapping, road feel, slip feel, or lock feel. Use only supervised local validation.

## Troubleshooting Summary

No vibration:

- confirm mock diagnostics update first,
- confirm F1 25 telemetry is live and parsed,
- confirm `DrivingArmed` is true,
- confirm paddle presses are accepted,
- confirm direct control is enabled, armed, selected, coexistence `Clear`, and emergency stop clear.

Wrong pedal:

- press emergency stop,
- disable direct control,
- verify brake/throttle module wiring and selected report/interface,
- retry one low-strength pedal at a time only after the issue is understood.

Menu suppression:

- check `DrivingArmed` reason,
- keep Menu Safe Mode enabled,
- confirm shift-intent diagnostics show suppressed events when paused, in menus, garage, results, or tabbed out.

SimPro/SimHub conflicts:

- close SimPro Manager and SimHub for first tests,
- refresh coexistence diagnostics,
- keep real direct control disabled unless status is `Clear`.

Device/interface selection:

- confirm report ID and report length,
- reselect the device for the current session,
- do not commit private device paths or serial numbers.

More detail: `docs\TROUBLESHOOTING.md`.

## Final Reference Docs

- Quick start: `docs\QUICK_START.md`
- Troubleshooting: `docs\TROUBLESHOOTING.md`
- Final acceptance: `docs\FINAL_P_HPR_ACCEPTANCE.md`
