# Simagic Wheel Input Research

Stage 2A records the intended read-only input discovery path for the GT Neo paddles. It does not add an input listener.

## Goal

Detect left and right GT Neo paddle button press events with the least practical latency, without requiring SimPro Manager and without sending writes to the wheelbase or wheel.

## Discovery Priority

1. Windows Raw Input.
2. DirectInput or game controller APIs.
3. HID read-only input reports.
4. Simagic-specific read-only discovery only if required.

Raw Input is preferred first because it can observe device input without routing through a polling-oriented gamepad abstraction. DirectInput/game-controller APIs are useful for mapping and fallback. HID input reports are allowed for read-only observation, but output reports and write-capable feature reports are not allowed in Phase 2 before approval.

## Planned Diagnostics

Later stages should expose:

- Input device detected.
- Selected input device.
- Left paddle pressed.
- Right paddle pressed.
- Last paddle timestamp.
- Input event count.
- Last event latency estimate where possible.
- Last `DrivingArmed` state.
- Last suppressed input reason.
- Last shift pulse routed.
- Last telemetry gear.
- Optional gear confirmation/rejection result.

## Mapping Data Needed

From Windows controller tools or a gamepad tester:

- Left paddle button number.
- Right paddle button number.
- Whether each state is visible as a normal button.
- Whether the wheelbase/GT Neo appears separately from the P700 pedals.
- Device name shown by Windows.
- VID/PID and Hardware IDs from Device Manager or USBView.

## Safety Boundary

Paddle input reading is read-only and allowed. It must not:

- Send output reports.
- Send feature reports that can change state.
- Require SimPro Manager.
- Hook or inject into SimPro Manager.
- Take control of the wheelbase.

Later event handling must use cached `DrivingArmed` state so menu tabbing does not fire pedal gear pulses.
