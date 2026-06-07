# Simagic User Data Request

Stage 2A requests this data so Phase 2 can proceed from observed hardware behavior instead of guesses. None of this is required before the documentation and abstraction stages can begin, but each item lowers risk before any future protocol work.

Do not post public files that contain serial numbers, usernames, or private paths. Raw captures should stay local and uncommitted unless a sanitized summary is created.

Stage 2D adds read-only input discovery. Stage 2E adds a read-only Windows game-controller paddle listener and manual mapping diagnostics. Stage 2F adds cached `DrivingArmed` shift-intent evaluation diagnostics only. Stage 2G adds read-only P700 / P-HPR inventory tooling and sanitized local exports. Stage 2H adds capture workflow and metadata tooling. Stage 2I adds read-only capture analysis tooling. Stage 2J adds protocol hypotheses and real-write blockers. The exact Alpha Evo / GT Neo / P700 hardware identities and paddle button numbers are still valuable for reliable mapping and later routing.

Stage 2H can complete without captures. Stage 2I capture analysis can use actual private local captures, sanitized Wireshark CSV/text exports, or sanitized transfer summaries.

## Priority 0 - Stage 2G Haptic Drive Inventory Tool

With the P700 pedals, P-HPR modules, Alpha Evo wheelbase, and GT Neo connected, run:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- inventory
```

The tool prints a read-only safety banner and writes sanitized files under ignored `local-device-inventory/`:

- `simagic-device-inventory-sanitized.json`
- `simagic-device-inventory-summary.md`

Please provide the sanitized summary or relevant sanitized fields only. Do not commit or share raw private device paths, serial numbers, or unsanitized USBView exports. If the tool reports no Simagic-specific candidates, that is still useful because it may mean the P-HPR modules are visible only through the P700 controller or that Windows exposes them under generic HID/USB names.

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

Stage 2D discovery is read-only. It does not send commands or vibrate P-HPR modules.

Stage 2G inventory is also read-only. It reads existing input-discovery metadata plus Windows PnP/HID/USB registry metadata and does not open device handles, send output reports, request feature writes, vibrate P-HPR modules, control SimPro Manager, or control SimHub.

## Stage 2E / 2F Paddle Mapping Follow-Up

After pressing Refresh Input Devices, selecting the likely Alpha Evo / GT Neo Windows game-controller device, and pressing Start Listener in Haptic Drive ASIO, please capture or copy:

- selected input device display name,
- selected input device ID shown by Haptic Drive ASIO if visible,
- selected input method,
- last changed button after pressing the left paddle,
- last changed button after pressing the right paddle,
- mapped left/right button IDs,
- whether holding a paddle repeats or only counts once,
- whether releasing and pressing again increments the mapped paddle count,
- listener error message if any,
- and whether Windows changes the device or joystick index after unplug/replug.
- Stage 2F shift-intent enabled state and mode.
- Stage 2F last accepted or suppressed shift-intent reason while haptics are running and fresh telemetry is present.
- Stage 2F `DrivingArmed` reason, telemetry age, and menu-safe/recent-telemetry status when a paddle press is suppressed.

Stage 2E paddle listening is read-only diagnostics only. Stage 2F evaluates mapped paddle presses into accepted/suppressed shift-intent diagnostics, but it still does not trigger audio haptics, P-HPR output, gear pulses, USB output reports, feature reports, `MockPhprOutputDevice`, or `PHprCommand`.

## Priority 6 - Stage 2H Capture Metadata Tooling

Before collecting captures, list the required scenarios:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-scenarios
```

Create one metadata template per planned capture:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-template --scenario BrakeTestVibration --target Brake
```

Generated templates are written under ignored `capture-metadata/generated/` by default. For real capture work, keep unsanitized metadata under ignored `capture-metadata/private/` until reviewed.

## Priority 7 - USBPcap/Wireshark Captures

These are Stage 2I analysis inputs, not Stage 2H blockers.

Capture scenarios requested before Stage 2I:

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

For every capture, record:

- capture filename,
- capture date/time,
- software used,
- software version,
- SimPro Manager version,
- SimHub version if used,
- P700 firmware version if known,
- P-HPR module targeted,
- exact action performed,
- setting before,
- setting after,
- strength,
- frequency,
- duration,
- whether brake/throttle/both vibrated,
- whether SimPro Manager was open,
- whether SimHub was open,
- whether Haptic Drive ASIO was open,
- observed behavior,
- whether serial numbers/private paths were redacted.

Analyze sanitized exports or local private captures with:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-analysis <capture-or-export-path>
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-diff <left-capture-or-export-path> <right-capture-or-export-path>
```

Stage 2I reports are sanitized summaries only. Stage 2J is where protocol hypotheses can be documented from reviewed analysis outputs.

## Optional Data Before Later Direct-Control Stages

Stage 2J can complete without more user data. The following optional items would improve later mock/direct-control confidence:

- additional SimPro Manager captures/summaries for brake/throttle test vibration,
- SimPro strength, frequency, and duration change summaries,
- exact P700 interface/report IDs from USBView,
- confirmation whether SimPro must be closed to access the P700,
- confirmation whether SimHub and SimPro can both see/control P-HPR at the same time,
- confirmation whether SimHub F1 EC commands were captured against the same P700/P-HPR hardware path,
- evidence of any report ID separate from payload bytes,
- and endpoint/interface details still missing from committed sanitized docs.

These are not mandatory for Stage 2K mock-only work.

Raw `.pcap`, `.pcapng`, USB trace, generated analysis summaries, and private inventory files must not be committed unless a sanitized summary is deliberately reviewed for inclusion. Use `docs/SIMAGIC_CAPTURE_GUIDE.md` and `docs/SIMAGIC_CAPTURE_ANALYSIS.md` for naming, metadata, analysis commands, and private storage.
