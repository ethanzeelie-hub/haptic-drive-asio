# Simagic User Data Request

Stage 2A requests this data so Phase 2 can proceed from observed hardware behavior instead of guesses. None of this is required before the documentation and abstraction stages can begin, but each item lowers risk before any future protocol work.

Do not post public files that contain serial numbers, usernames, or private paths. Raw captures should stay local and uncommitted unless a sanitized summary is created.

Stage 2D adds read-only input discovery, but the exact Alpha Evo / GT Neo / P700 hardware identities and paddle button numbers are still required before Stage 2E can map live paddle input safely.

## Priority 1 - SimPro Manager V3 Screenshots

Please capture:

- P700 device page.
- Firmware/version page.
- P-HPR or haptic settings page.
- Brake module settings.
- Throttle module settings.
- Available effect list.
- Frequency controls.
- Strength controls.
- Pulse length or duration controls.
- Test/vibrate buttons.

Useful notes to include with screenshots:

- SimPro Manager V3 version.
- P700 firmware version.
- Whether both P-HPR modules are detected at the same time.
- Any warning shown when SimHub is also running.

## Priority 2 - SimHub Screenshots

Please capture:

- Simagic/P-HPR device detection.
- ShakeIt or haptic output pages.
- P-HPR effect mapping.
- Any Simagic-specific output settings.
- Any settings where lag was noticed.

Useful notes:

- SimHub version.
- Which effect felt laggy.
- Whether lag was present on gear shift, lock, slip, road vibration, or all effects.

## Priority 3 - Windows Device Manager Details

For each relevant device, please capture Hardware IDs, Compatible IDs, driver provider, and driver version:

- P700 pedal set.
- Alpha Evo wheelbase.
- GT Neo / wheelbase input device if it appears separately.

Also note whether each appears as:

- HID device.
- USB Input Device.
- Game controller.
- COM device.
- Vendor-specific device.
- Other device class.

## Priority 4 - USBView / USB Device Tree Viewer Export

Please export or screenshot descriptor details for:

- P700 pedals.
- Alpha Evo / GT Neo-visible device or interface.

Useful fields:

- VID/PID.
- Manufacturer/product strings.
- Interfaces.
- Endpoints.
- HID report descriptors where visible.
- Polling intervals where visible.
- Input/output/feature report lengths where visible.

## Priority 5 - Windows Game Controller / DirectInput Mapping

Please identify:

- Left paddle button number.
- Right paddle button number.
- Whether button state changes are visible in the Windows controller panel.
- Whether button state changes are visible in another gamepad tester.
- Whether the P700 pedals and Alpha Evo/GT Neo appear as separate controllers.
- Device display name for the controller whose buttons change when each GT Neo paddle is pressed.
- Whether the left/right paddle numbers are one-based or zero-based in the tester being used.
- Any Raw Input / HID / game-controller device names shown by Haptic Drive ASIO's Stage 2D Refresh Input Devices panel.

## Stage 2D Discovery Follow-Up

After pressing Refresh Input Devices in Haptic Drive ASIO, please capture or copy:

- likely Simagic wheelbase candidates,
- likely GT Neo / wheel input candidates,
- likely P700 pedal candidates,
- unknown HID/game-controller candidates,
- any discovery errors,
- and the Windows game-controller button numbers for left and right paddles if visible.

Stage 2D discovery is read-only. It does not start live paddle listening, send commands, or vibrate P-HPR modules.

## Later - USBPcap/Wireshark Captures

These are later protocol-research inputs, not Stage 2A blockers.

Capture scenarios requested later:

- SimPro opened with pedals connected.
- SimPro closed.
- Brake P-HPR test vibration.
- Throttle P-HPR test vibration.
- Brake strength changed only.
- Throttle strength changed only.
- Brake frequency changed only.
- Throttle frequency changed only.
- Brake pulse duration changed only.
- Throttle pulse duration changed only.
- SimHub P-HPR gear/lock/slip test where possible.

Raw `.pcap`, `.pcapng`, USB trace, and private inventory files must not be committed. Use `docs/SIMAGIC_CAPTURE_GUIDE.md` for naming and metadata.
