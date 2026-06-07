# Simagic USB Device Inventory

## Stage 2G Purpose

Stage 2G implements read-only Simagic P700 / P-HPR inventory tooling before any capture workflow, capture analysis, protocol hypothesis, mock routing, or output work begins.

The goal is to identify how the expected hardware appears to Windows:

- Simagic P700 2-pedal set connected directly by USB.
- Brake and throttle pedals.
- Two Simagic P-HPR modules connected through the built-in P700 haptic controller.
- Simagic Alpha Evo 12 Nm wheelbase connected separately by USB.
- Simagic GT Neo wheel connected through the Alpha Evo wheelbase.

## Current Status

Stage 2G tooling is complete. Real P700/P-HPR hardware inventory is awaiting user-provided Device Manager / USBView / tool output. No validated VID/PID, endpoint, report length, or P-HPR visibility claim is made yet.

Implemented tooling:

- `src/HapticDrive.Simagic.PHPR.Research`
- read-only Stage 2D input discovery reuse
- read-only Windows HID registry inventory
- read-only Windows USB registry inventory
- candidate classification
- redaction of serial-like path segments and Windows usernames
- sanitized JSON export
- sanitized Markdown summary export
- hardware-free tests

Local Stage 2G tool run on June 5, 2026:

| Result | Count |
| --- | ---: |
| Total inventory items | 168 |
| Generic HID/USB candidates | 166 |
| Specific Simagic P700/P-HPR/Alpha/GT Neo candidates | 0 |
| P700 candidates | 0 |
| P-HPR module/controller candidates | 0 |
| Discovery errors | 0 |

No generic HID/USB table is committed here because it does not identify the target hardware and could add noise without improving the sanitized project record.

## Safety Statement

Stage 2G is read-only only.

The inventory tool does not:

- send output reports,
- send feature writes,
- request P-HPR vibration,
- create P-HPR commands,
- call `IPHprOutputDevice`,
- call `MockPhprOutputDevice`,
- open P700/P-HPR device handles for control,
- claim USB interfaces,
- install drivers,
- replace drivers,
- control SimPro Manager,
- control SimHub,
- route accepted `ShiftIntentEvent` values to haptic output,
- or touch the ASIO/BST-1 audio output path.

## Tool Command

Run the inventory tool from the repository root:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- inventory
```

Default sanitized local exports are written to ignored `local-device-inventory/`:

- `simagic-device-inventory-sanitized.json`
- `simagic-device-inventory-summary.md`

Console-only mode:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- inventory --no-export
```

## Sanitized Observed Inventory

No Simagic-specific P700, P-HPR, Alpha Evo, or GT Neo inventory was observed in the local Stage 2G tool run.

| Candidate | VID/PID | Method | Confidence | Status |
| --- | --- | --- | --- | --- |
| P700 pedal controller | not validated | not observed | unknown | awaiting user inventory |
| P-HPR brake module | not validated | not observed | unknown | may be exposed only through P700 |
| P-HPR throttle module | not validated | not observed | unknown | may be exposed only through P700 |
| Alpha Evo wheelbase | not validated | not observed | unknown | awaiting user inventory |
| GT Neo wheel input path | not validated | not observed | unknown | awaiting user inventory |

## Missing Data

| Missing item | Why it matters | Current status |
| --- | --- | --- |
| P700 VID/PID | Identifies the pedal controller and future capture filter | not yet validated |
| P700 manufacturer/product strings | Helps correlate Windows, USBView, SimPro, and inventory tool names | not yet validated |
| P700 interfaces | Needed before capture workflow selects an interface | not yet validated |
| P700 endpoint addresses/directions/types | Needed for later capture interpretation | not yet validated |
| HID usage page / usage | Helps distinguish controller/input/vendor paths | not yet validated |
| Input report length | Needed before read-only input report tooling is considered | not yet validated |
| Output report length | Must be documented before any later write-gated protocol work | not yet validated |
| Feature report length | Must be documented carefully; no feature writes are allowed in Stage 2G | not yet validated |
| P-HPR separate device visibility | Determines whether modules enumerate separately or only through P700 | not yet validated |
| Alpha Evo / GT Neo-visible VID/PID | Correlates paddle input source with Device Manager / USBView | not yet validated |
| Driver provider/version | Helps document Windows stack and vendor driver dependency | not yet validated |

## P700 / P-HPR Visibility Notes

The P-HPR modules may not appear as separate USB devices. They may be exposed only through the P700 pedal controller. Stage 2G must not assume either P-HPR module has its own VID/PID, interface, endpoint, HID collection, or report length.

Current visibility status:

- P700 controller: unknown.
- Brake P-HPR module: unknown.
- Throttle P-HPR module: unknown.
- P-HPR modules as separate devices: unknown.
- P-HPR modules through P700 controller only: possible, not validated.

## Candidate Identification Confidence

| Device | Confidence | Basis |
| --- | --- | --- |
| P700 pedal controller | unknown | no local Simagic-specific inventory observed |
| Brake P-HPR module | unknown | no separate module inventory observed |
| Throttle P-HPR module | unknown | no separate module inventory observed |
| Alpha Evo wheelbase | unknown | no local Simagic-specific inventory observed |
| GT Neo wheel input path | unknown | no local Simagic-specific inventory observed |
| Generic HID/USB inventory | low for Simagic identification | generic metadata exists but is not target-specific |

## Optional User Input Still Requested

OPTIONAL USER INPUT REQUESTED - Stage 2G can proceed tooling-only, but these items will improve real P700/P-HPR identification.

1. Device Manager details for P700, Alpha Evo, GT Neo-visible path, USB Input Device entries, and HID-compliant game controller entries.
2. USB Device Tree Viewer or USBView export for P700 and Alpha Evo / GT Neo-visible devices, with serial numbers redacted.
3. Haptic Drive ASIO Stage 2G sanitized inventory summary or relevant sanitized fields.
4. Haptic Drive ASIO Devices page Refresh Input Devices output.
5. SimPro Manager V3 P700/P-HPR screenshots.
6. SimHub Simagic/P-HPR detection screenshots.

Do not commit raw USBView exports, screenshots containing private serial numbers, raw USB captures, private device paths, or unsanitized hardware data.

## Stage 2H Follow-Up

- Stage 2H Capture Workflow and Metadata Tooling is complete and can be used even if real inventory remains pending.
- Use `docs/SIMAGIC_CAPTURE_GUIDE.md` for the USBPcap/Wireshark workflow, scenario list, naming convention, metadata templates, validation, and sanitized manifests.
- Stage 2I Capture Analysis can summarize captures and sanitized Wireshark exports, but correct device/interface identification is still required before real capture summaries are treated as authoritative.
- Stage 2J Protocol Hypotheses are now documented from sanitized evidence, but they remain hypotheses and do not authorise writes.

Stage 2K Mock P-HPR Protocol and Output is complete. It does not require real P700/P-HPR hardware and does not send USB writes.

Stage 2G stops at inventory. Stage 2H stops at capture workflow and metadata tooling. Stage 2I stops at read-only capture analysis and sanitized summary export. Stage 2J stops at protocol hypotheses. Stage 2K stops at mock protocol/output. Stage 2L is next for the P-HPR safety layer.
