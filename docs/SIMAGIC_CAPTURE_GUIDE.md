# Simagic Capture Guide

Stage 2H prepares the Simagic P700 / P-HPR USB capture workflow. Stage 2I adds read-only capture analysis tooling. Neither stage sends USB writes or vibration commands.

## Stage 2H Purpose

Stage 2H creates the safe capture workflow, scenario checklist, metadata schema, metadata template command, validation command, and sanitized manifest export command needed before Stage 2I analysis.

Stage 2H can complete without real captures. Stage 2I can analyze actual local captures, sanitized Wireshark CSV/text exports, or sanitized transfer summaries when they are available.

## Safety Boundary

Allowed in Stage 2H:

- workflow documentation,
- capture scenario naming,
- metadata templates,
- metadata validation,
- sanitized metadata manifests,
- and private-storage instructions.

Forbidden in Stage 2H:

- USB capture analysis,
- protocol hypotheses,
- protocol decoder or encoder logic,
- real P-HPR vibration,
- USB writes,
- HID output reports,
- HID feature reports that can change device state,
- P-HPR commands,
- SimPro Manager or SimHub control,
- driver replacement,
- and any route from Haptic Drive ASIO to P-HPR output.

Haptic Drive ASIO must not send real P-HPR writes during these captures before the exact approval phrase documented in `docs/SIMAGIC_P_HPR_SAFETY_PLAN.md`.

## Required Tools

Prepare these tools before collecting real captures for Stage 2I:

- Wireshark.
- USBPcap.
- USB Device Tree Viewer or Microsoft USBView.
- Stage 2G Haptic Drive ASIO inventory command.

## Before-Capture Checklist

1. Confirm raw captures will be stored under an ignored private path such as `captures/private/simagic/YYYY-MM-DD/`.
2. Confirm `.pcap`, `.pcapng`, `.etl`, `.usbtrace`, `.usbpcap`, and `capture-metadata/` files remain uncommitted.
3. Run or review Stage 2G inventory with P700, P-HPR, Alpha Evo, and GT Neo connected.
4. Identify the likely P700 USB device/interface from Stage 2G, Device Manager, USBView, or USB Device Tree Viewer.
5. Note whether P-HPR modules appear separately or only through the P700 controller.
6. Record SimPro Manager version, SimHub version if used, and P700 firmware version if known.
7. Close unrelated USB-heavy applications where practical.
8. Decide one exact action for the capture before starting.
9. Start with short captures.
10. Stop immediately if a test produces unexpected strong or continuous vibration.

## Identifying The Correct Device Or Interface

Use Stage 2G first:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- inventory
```

The sanitized inventory exports to ignored `local-device-inventory/`:

- `simagic-device-inventory-sanitized.json`
- `simagic-device-inventory-summary.md`

Correlate the Stage 2G summary with Device Manager and USBView:

- VID/PID.
- Manufacturer/product strings.
- Interface number.
- HID usage page / usage.
- Input/output/feature report lengths if visible.
- Endpoint addresses if visible.
- Whether P-HPR modules appear separately.

If multiple devices have similar names, prefer the interface whose VID/PID, product string, and P700/P-HPR visibility match the Stage 2G inventory and USBView export. If still uncertain, capture a short quiet baseline first and mark the metadata as `Unknown` target.

## Capture Scenario Table

| Scenario ID | Action | Target | Software |
| --- | --- | --- | --- |
| `SimProOpened` | Open SimPro Manager with P700/P-HPR connected | Unknown | SimPro |
| `SimProClosed` | Close SimPro Manager with P700/P-HPR connected | Unknown | SimPro |
| `BrakeTestVibration` | Trigger brake P-HPR test vibration only | Brake | SimPro |
| `ThrottleTestVibration` | Trigger throttle P-HPR test vibration only | Throttle | SimPro |
| `BrakeStrengthChanged` | Change brake strength only | Brake | SimPro |
| `ThrottleStrengthChanged` | Change throttle strength only | Throttle | SimPro |
| `BrakeFrequencyChanged` | Change brake frequency only | Brake | SimPro |
| `ThrottleFrequencyChanged` | Change throttle frequency only | Throttle | SimPro |
| `BrakeDurationChanged` | Change brake pulse duration only | Brake | SimPro |
| `ThrottleDurationChanged` | Change throttle pulse duration only | Throttle | SimPro |
| `SimHubGearShiftTest` | Trigger SimHub P-HPR gear-shift test if available | Both | SimHub |
| `SimHubWheelLockTest` | Trigger SimHub P-HPR wheel-lock test if available | Brake | SimHub |
| `SimHubWheelSlipTest` | Trigger SimHub P-HPR wheel-slip test if available | Throttle | SimHub |

## One-Action-Per-Capture Rule

Every capture should contain exactly one intentional action. Do not combine strength and frequency changes in the same capture. Do not combine brake and throttle tests in the same capture unless the scenario explicitly targets both.

Good examples:

- brake test vibration only,
- throttle frequency changed from 24 Hz to 40 Hz only,
- SimPro opened only,
- SimHub gear-shift test only.

Poor examples:

- brake and throttle test in one capture,
- strength and frequency changed together,
- SimPro opened while also pressing test vibration,
- long idle captures with several unrelated actions.

## Capture Naming Convention

Use:

```text
YYYY-MM-DD_HHMMSS_<software>_<device>_<scenario>_<target>_<settings>.pcapng
```

Examples:

```text
2026-06-05_201530_simpro_p700_brake-test_brake_30hz_20pct_100ms.pcapng
2026-06-05_202000_simpro_p700_throttle-frequency_throttle_24-to-40hz.pcapng
2026-06-05_203000_simhub_p700_wheel-lock-test_brake.pcapng
```

Do not include serial numbers, Windows usernames, private folder names, or raw device paths in filenames.

## Metadata Fields

Every capture metadata file should record:

- `CaptureId`
- `ScenarioId`
- `ScenarioName`
- `CaptureFileName`
- `CaptureStartedAtUtc`
- `CaptureDuration`
- `Software.CaptureTool`
- `Software.CaptureToolVersion`
- `Software.SoftwareUnderTest`
- `Software.SoftwareUnderTestVersion`
- `Software.SimProVersion`
- `Software.SimHubVersion`
- `Software.SimProRunning`
- `Software.SimHubRunning`
- `Software.HapticDriveRunning`
- `Device.P700FirmwareVersion`
- `Device.DeviceInventoryReference`
- `Device.TargetModule`
- `Action.ActionPerformed`
- `Action.SettingBefore.StrengthPercent`
- `Action.SettingAfter.StrengthPercent`
- `Action.SettingBefore.FrequencyHz`
- `Action.SettingAfter.FrequencyHz`
- `Action.SettingBefore.DurationMs`
- `Action.SettingAfter.DurationMs`
- `Action.ExpectedVibrationObserved`
- `Action.ActualObservedBehaviour`
- `Notes`
- `RedactionStatus`
- `ContainsSerialNumbers`
- `ContainsPrivatePaths`
- `RawCapturePath`
- `SanitizedSummaryPath`

Set `Device.TargetModule` to `Brake`, `Throttle`, `Both`, or `Unknown`.

Set `RedactionStatus` to `ReviewedClean` or `Redacted` before sharing metadata for Stage 2I.

## Stage 2H Metadata Commands

List scenarios:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-scenarios
```

Create a blank metadata template:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-template --scenario BrakeTestVibration --target Brake
```

The template is written by default under ignored `capture-metadata/generated/`.

Validate metadata:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- validate-capture-metadata capture-metadata\generated\braketestvibration-brake-metadata-template.json
```

Generate a sanitized manifest from metadata JSON files:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-manifest capture-metadata\private
```

The manifest is written by default to `capture-metadata/generated/simagic-capture-manifest-sanitized.json`.

All Stage 2H capture commands print a safety banner confirming metadata/template/manifest tooling only, no capture analysis, no USB writes, no output reports, no feature reports, no vibration commands, no P-HPR commands, and no SimPro/SimHub control.

## Stage 2I Analysis Commands

Analyze one capture/export file or a folder recursively:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-analysis <capture-or-export-path>
```

Compare two capture/export sources and report closest byte-level payload differences:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-diff <left-capture-or-export-path> <right-capture-or-export-path>
```

The analysis output is written by default to ignored `capture-metadata/generated/`.

Stage 2I analysis reports contain sanitized source file names, payload counts, payload-length counts, source-column counts, short payload previews, truncated payload fingerprints, byte-diff observations, pcap/pcapng container summaries, and warnings. They do not serialize raw payload byte arrays.

All Stage 2I analysis commands print a safety banner confirming read-only analysis only, no USB writes, no output reports, no feature reports, no vibration commands, no P-HPR commands, no protocol hypotheses, and no SimPro/SimHub control.

## Private Storage

Recommended layout:

```text
captures/private/simagic/YYYY-MM-DD/
capture-metadata/private/
capture-metadata/generated/
local-device-inventory/
```

Do not commit:

- `.pcap`
- `.pcapng`
- `.etl`
- `.usbtrace`
- `.usbpcap`
- raw USBView exports with serial numbers,
- screenshots with private serial numbers,
- unsanitized inventory files,
- unsanitized capture metadata,
- raw capture bytes pasted into metadata notes.

## What To Provide For Stage 2I

For Stage 2I, provide one of:

- actual local captures plus metadata if working in the private workspace,
- sanitized transfer summaries,
- or sanitized capture manifest plus enough private local context to inspect captures without committing them.

Stage 2I capture analysis can use actual captures in a private local workspace or sanitized transfer summaries. Stage 2H can complete without captures.

## Troubleshooting

Wrong interface captured:

- Recheck Stage 2G inventory, USBView interface number, and endpoint list.
- Capture a short quiet baseline on each likely interface and mark target as `Unknown` until validated.

Capture too long or noisy:

- Keep one capture per action.
- Stop after the action completes.
- Close unrelated USB-heavy applications where practical.

SimPro open/closed ambiguity:

- Record `Software.SimProRunning`.
- Start capture before opening or closing SimPro.
- Note exact timing in `Action.ActionPerformed`.

Multiple USB devices with similar names:

- Preserve VID/PID and interface number in sanitized metadata.
- Use `Device.DeviceInventoryReference` to point to the Stage 2G summary.

Serial numbers or private paths visible:

- Set `ContainsSerialNumbers` or `ContainsPrivatePaths` to `true`.
- Run the metadata through the Stage 2H sanitizer/manifest export before sharing.
- Do not commit raw metadata until reviewed.

P-HPR modules not appearing separately:

- Treat P-HPR visibility as unknown.
- Capture through the P700 controller if USBView and SimPro indicate modules are exposed there.
- Do not infer separate module protocol or endpoints without Stage 2I evidence.

## Stage 2I Boundary

Stage 2I performs read-only capture analysis and sanitized summary export only. It can observe payload counts, payload fingerprints, pcap container structure, and byte differences, but it does not infer protocol fields, create protocol hypotheses, create command encoders/decoders, route haptics, or send USB writes.

Stage 2J is next for protocol hypotheses.
