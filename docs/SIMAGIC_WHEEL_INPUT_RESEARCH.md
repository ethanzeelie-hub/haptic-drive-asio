# Simagic Wheel Input Research

Stage 2A records the intended read-only input discovery path for the GT Neo paddles. Stage 2D implements read-only device discovery and candidate scoring. It does not add an input listener.

## Goal

Detect left and right GT Neo paddle button press events with the least practical latency, without requiring SimPro Manager and without sending writes to the wheelbase or wheel.

## Discovery Priority

1. Windows Raw Input.
2. DirectInput or game controller APIs.
3. HID read-only input reports.
4. Simagic-specific read-only discovery only if required.

Raw Input is preferred first because it can observe device input without routing through a polling-oriented gamepad abstraction. DirectInput/game-controller APIs are useful for mapping and fallback. HID input reports are allowed for read-only observation, but output reports and write-capable feature reports are not allowed in Phase 2 before approval.

## Stage 2D Implemented Discovery

Stage 2D implements two safe Windows discovery paths:

- Raw Input metadata discovery through `RawInputDeviceEnumerator`.
- Built-in Windows game-controller capability discovery through `WindowsGameControllerDeviceEnumerator`.

Raw Input discovery provides broad device class, redacted device path / instance text, HID VID/PID where available, and HID usage page / usage. It does not register for live input events in this stage.

Windows game-controller discovery provides visible controller name, button count, axis count, and read-only control slots where the built-in Windows API exposes them. This helps correlate future left/right paddle button numbers with Windows controller tester screenshots.

Stage 2D also adds `WheelInputCandidateProvider` scoring for:

- likely Simagic wheelbase candidates,
- likely GT Neo / wheel input path candidates,
- likely P700 pedal candidates,
- unknown HID/game-controller candidates.

The scoring is non-authoritative. It uses names, device class, HID usage metadata, and broad API source only until the user supplies exact Alpha Evo / GT Neo / P700 Device Manager and USBView data.

## Deferred From Stage 2D

- DirectInput-specific enumeration is deferred because the built-in Windows game-controller capability API gives a maintained, dependency-free mapping view for this stage.
- HID input-report reading is deferred because Stage 2D only needs metadata and candidate selection. Stage 2E can add input-report or Raw Input event observation if normal button state is not visible.
- Simagic-specific read-only discovery is deferred until Raw Input and Windows game-controller data prove insufficient.
- Live rising-edge paddle detection, debouncing, left/right mapping, `ShiftIntentEvent` creation, and haptic routing are all Stage 2E or later.

## Planned Diagnostics

Stage 2D exposes manual read-only input discovery status in the WPF Devices page:

- last refresh time,
- discovery method names,
- number of devices found,
- likely Simagic wheelbase candidates,
- likely GT Neo / wheel input candidates,
- likely P700 pedal candidates,
- unknown HID/game-controller candidates,
- discovery errors,
- and a safety note that no commands are sent.

Later listener/routing stages should expose:

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
