# Simagic Wheel Input Research

Stage 2A records the intended read-only input discovery path for the GT Neo paddles. Stage 2D implements read-only device discovery and candidate scoring. Stage 2E implements a read-only Windows game-controller paddle listener and manual mapping diagnostics. Stage 2F evaluates mapped paddle presses into accepted or suppressed shift-intent diagnostics through cached `DrivingArmed` state. Stage 2G adds read-only P700 / P-HPR inventory tooling that also helps correlate the Alpha Evo / GT Neo / P700 device identities before later capture work. Stage 2H adds capture workflow and metadata tooling. Stage 2I adds read-only USB capture analysis tooling and sanitized summary export. Stage 2J adds protocol hypotheses while keeping GT Neo paddle bits input-only and non-output. Stage 2K adds mock-only P-HPR protocol/output diagnostics, but still does not route paddle input.

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

## Stage 2E Implemented Listener

Stage 2E adds a live listener behind mockable input abstractions:

- `IInputButtonStateReader` is the low-level read-only button-state seam.
- `PollingWheelPaddleInputSource` runs the reader off the UI thread.
- `WheelPaddleInputProcessor` performs rising-edge detection, release-to-rearm behavior, debounce, mapped left/right diagnostics, UTC timestamps, stopwatch ticks, and safe error/disconnect snapshots.
- `WindowsGameControllerButtonStateReader` reads button states from the built-in Windows game-controller API by native joystick index.

This path was chosen for Stage 2E because Stage 2D's Windows game-controller capability discovery exposes button counts and one-based button IDs that line up with Windows controller-panel mapping. Raw Input remains useful for metadata, but live Raw Input HID report decoding needs report-descriptor data before button IDs can be mapped safely.

Stage 2E does not send output reports, feature reports, vibration commands, Simagic-specific control messages, or P-HPR commands.

## Manual Mapping Workflow

On the Devices page:

1. Press Refresh Input Devices.
2. Select the Windows game-controller device whose buttons change for the Alpha Evo / GT Neo path.
3. Press Start Listener.
4. Press the left paddle.
5. Verify the last changed raw button.
6. Press Set Left From Last Button.
7. Press the right paddle.
8. Verify the last changed raw button.
9. Press Set Right From Last Button.
10. Watch left/right current state, last mapped paddle event, timestamp, and paddle press count.

Mapped paddle presses from Stage 2E now feed Stage 2F shift-intent evaluation. Accepted Stage 2F intent is still diagnostics only and does not trigger audio haptics, P-HPR output, gear pulses, or any USB writes.

The app persists only safe input settings: selected input device ID, selected input method, left/right button IDs, and debounce duration.

## Deferred From Stage 2E

- DirectInput-specific enumeration is deferred because the built-in Windows game-controller capability API gives a maintained, dependency-free mapping view for this stage.
- HID input-report reading is deferred because the current listener can read normal game-controller button states, and HID report parsing should wait for USBView / HID descriptor data if needed.
- Live Raw Input button decoding is deferred because reliable button IDs require HID report-descriptor interpretation for the user's exact wheel input path.
- Simagic-specific read-only discovery is deferred until Raw Input and Windows game-controller data prove insufficient.
- Haptic routing from accepted `ShiftIntentEvent` values is Stage 2M or later.

## Stage 2F Implemented Shift Intent Evaluation

Stage 2F adds the event layer after the read-only listener:

- `WheelPaddleInputEvent` is passed to `ShiftIntentProcessor`.
- The processor reads cached `DrivingArmed` state only; it does not wait for telemetry at paddle-press time.
- `InstantPaddleOnly` is the default mode and accepts immediate intent when `DrivingArmed` is true.
- `TelemetryConfirmedOnly` observes mapped paddle presses but suppresses immediate accepted intent.
- `InstantWithRejectedShiftFeedback` accepts immediately and records pending confirmation diagnostics only.
- Left paddle is recorded as `Downshift`; right paddle is recorded as `Upshift`.
- Suppressed diagnostics preserve the `DrivingArmed` reason when the cached gate is false.

Stage 2F does not call `MockPhprOutputDevice`, `IPHprOutputDevice`, `PHprCommand`, `GearShiftEffect`, ASIO output, or the audio mixer.

## Stage 2G Implemented P700 / P-HPR Inventory

Stage 2G adds `HapticDrive.Simagic.PHPR.Research` for safe inventory:

- existing Stage 2D input discovery metadata,
- Windows HID registry metadata,
- Windows USB registry metadata,
- sanitized JSON/Markdown export,
- candidate scoring for P700, P-HPR module/controller, Alpha Evo, GT Neo, Simagic unknown, generic HID, and generic USB input.

The local Stage 2G run found no Simagic-specific P700/P-HPR/Alpha/GT Neo candidates, so the real hardware identity and P-HPR visibility remain awaiting user-provided Device Manager / USBView / tool output. P-HPR modules may appear only through the P700 pedal controller rather than as separate devices.

## Stage 2H Implemented Capture Metadata Workflow

Stage 2H adds capture scenarios, metadata templates, validation, sanitized manifests, and private-storage rules under `docs/SIMAGIC_CAPTURE_GUIDE.md`.

This does not change the read-only GT Neo paddle listener or Stage 2F shift-intent diagnostics. It does not analyze captures, infer protocols, send P-HPR writes, or route haptics.

## Stage 2J Implemented Protocol Hypotheses

Stage 2J documents protocol hypotheses under `docs/SIMAGIC_PROTOCOL_HYPOTHESES.md` and keeps the GT Neo paddle mapping as confirmed input evidence only.

Stage 2J does not route accepted `ShiftIntentEvent` values to any output device and does not create `PHprCommand` values.

## Stage 2K Implemented Mock Protocol

Stage 2K documents mock-only P-HPR protocol/output modelling under `docs/SIMAGIC_P_HPR_MOCK_PROTOCOL.md`.

Stage 2K still does not route accepted `ShiftIntentEvent` values to `MockPhprOutputDevice`, real P-HPR output, ASIO output, or any haptic effect.

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

Stage 2E exposes:

- Selected input device.
- Left paddle pressed.
- Right paddle pressed.
- Last paddle timestamp.
- Input event count.
- Last changed raw button.
- Last mapped paddle side.
- Listener status.
- Listener error message.
- Debounce duration.

Later routing stages should expose:

- Last event latency estimate where possible.
- Last `DrivingArmed` state. Implemented for shift-intent diagnostics in Stage 2F.
- Last suppressed input reason. Implemented for shift-intent diagnostics in Stage 2F.
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
- Haptic Drive ASIO last-changed button number for each paddle after Stage 2E listener mapping.

## Safety Boundary

Paddle input reading is read-only and allowed. It must not:

- Send output reports.
- Send feature reports that can change state.
- Require SimPro Manager.
- Hook or inject into SimPro Manager.
- Take control of the wheelbase.

Later event handling must use cached `DrivingArmed` state so menu tabbing does not fire pedal gear pulses.
