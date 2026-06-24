# Simagic P-HPR Controlled Real Validation

## Stage 2R / Phase 3J Purpose

Stage 2R adds the validation harness for later supervised real P-HPR testing.

Phase 3J adds a final controlled CLI smoke-test command with session-only authorization from the exact controlled-write phrase. It still does not run hardware vibration automatically and does not mark physical validation as passed.

The Phase 3J direct-output picker follow-up fixes the validation blocker where sanitized inventory exports could not provide a pasteable private HID path. The app now refreshes local HID candidates, shows safe labels only, keeps the private path in memory, and applies it internally when selected.

The second Phase 3J picker follow-up fixes the Raw Input metadata blocker. Raw Input metadata can identify a Simagic-family device, but it is not treated as an openable HID output target. Direct real output now requires a Windows HID device-interface candidate and a successful no-report HID open-check before `can pulse` can become true.

The third Phase 3J picker follow-up fixes the HID report-write blocker observed after open-check success. Open-check only proves the selected path can be opened and closed without sending a report. Direct real output now also requires known HID output-report capability, or a successful no-command report-shape validation, before `can pulse` can become true. If output-report length is unavailable, real pulses stay blocked.

The fourth Phase 3J picker follow-up adds explicit HID FeatureReport support for the current local evidence. The `VID_3670/PID_0905` HID device-interface candidate exposes a 64-byte feature report and report ID `0xF1`, which likely matches the known F1 EC SET_REPORT-style command family. Dry-run can validate that feature-report shape and show expected first bytes without sending a report. Dry-run does not authorize writes. Real pulses remain blocked unless the selected transport, report ID, report length, open-check, session authorization, coexistence, and emergency-stop gates all pass.

## Implemented Harness

The harness includes:

- `PHprManualValidationChecklist`
- `PHprManualValidationReadiness`
- `PHprManualValidationResult`
- `PHprManualValidationResultEvaluation`
- `PHprManualValidationResultExporter`
- WPF Devices-page checklist and private local result export

The app checklist combines user confirmations with current runtime state:

- user physically present,
- P700 connected,
- brake module installed,
- throttle module installed,
- direct control enabled,
- direct control armed,
- session authorization active for the current session,
- selected device/interface/report,
- visible safety limits,
- visible emergency stop,
- emergency stop latch clear,
- SimPro/SimHub coexistence `Clear`,
- brake/throttle pulse buttons available,
- gear paddle test planned.

The harness never triggers a pulse. Brake, throttle, and paddle tests remain manual actions through the Stage 2Q direct-control controls.

## Local Direct-Output Candidate Picker

The Advanced / Diagnostics direct-control section includes a local-only candidate picker:

- Refresh Candidates enumerates local HID candidates without opening the HID writer.
- Candidate labels show VID/PID, display name, class, interface, collection, report lengths when available, and confidence.
- For `VID_3670` HID device-interface candidates, read-only HID capability discovery attempts to surface input/output/feature report byte lengths, report IDs when available, and usage page/usage.
- `VID_3670` candidates are classified as Simagic-family candidates, including observed `PID_0500`, `PID_0905`, `PID_B500`, and `PID_B905`.
- Safe HID registry metadata is surfaced separately so observed `VID_3670` family PIDs do not disappear from the picker merely because they are not openable HID device-interface paths.
- Raw Input metadata-only candidates are labeled as such and cannot pass real direct-output gates.
- HID device-interface candidates with output or feature report capability are preferred over input-only/game-controller-style candidates.
- The selected report transport is explicit: `OutputReport` or `FeatureReport`. Feature report ID `0xF1` is shown when advertised and is treated as the likely F1 EC family shape.
- The private HID path stays in memory only and is applied internally when a candidate is selected.
- Copied diagnostics, docs, tests, and sanitized exports must not contain private HID paths.
- Open Check opens and immediately closes the selected HID writer without sending any output report or feature report. Open-check is real hardware access even though it sends no reports.
- Dry Run Gates validates selected candidate, source method, Raw Input-only status, openable HID path status, selected transport, selected report ID, report capability/shape validation, open-check result, report length, expected first bytes, direct-control enable/arm, session authorization state, coexistence, and emergency-stop gates without opening the HID writer. Dry-run does not authorize writes.

## Controlled CLI Smoke Test

`controlled-write-test` is the explicit command-line route for a final local P-HPR smoke test. It defaults to dry-run and does not open the HID writer unless `--execute` is supplied with the exact approval phrase that authorizes the current session only.

`direct-output-dry-run` is the local discovery-only companion command. It lists safe candidate labels and validates gates without opening the HID writer. A selected candidate cannot report `can pulse True` until `direct-output-open-check` has succeeded for that same openable HID device-interface candidate and known output-report or feature-report capability plus successful report-shape validation is present:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- direct-output-dry-run --candidate-index 0 --enable --arm --approval "I approve Phase 2 controlled P-HPR write testing"
```

Open-check, no output report or feature report sent:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- direct-output-open-check --candidate-index 0 --enable --arm --approval "I approve Phase 2 controlled P-HPR write testing"
```

Dry run:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- controlled-write-test --approval "I approve Phase 2 controlled P-HPR write testing" --device-path "<private-hid-path>" --target sequence --strength-percent 10 --frequency-hz 50 --duration-ms 50
```

Execute, only when physically ready:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- controlled-write-test --approval "I approve Phase 2 controlled P-HPR write testing" --device-path "<private-hid-path>" --target sequence --strength-percent 10 --frequency-hz 50 --duration-ms 50 --execute
```

The command:

- requires selected private HID path, clear SimPro/SimHub coexistence, and exact approval phrase for real writes,
- requires direct control enabled and armed for real writes, but direct mode selection and arm state do not authorize writes by themselves,
- uses normalized 0-100% strength, 1-50 Hz frequency, and 10-1000 ms duration,
- defaults to a 10%, 50 Hz, 50 ms brake-then-throttle sequence,
- requests emergency stop at the end,
- hides the private HID path in console output,
- does not commit or export local validation data.

## Result Export

Validation results are exported as private local Markdown files under:

```text
local-validation-results/
```

If the repo root cannot be found at runtime, the app falls back to a LocalAppData folder.

The export includes a JSON result block and a warning not to commit raw captures, serial numbers, private device paths, or unsanitized hardware data.

## Pass Gate

The result model blocks a `pass` decision unless required manual fields and hardware confirmations are complete:

- app branch/commit,
- P700 device info,
- P700 connected,
- brake module installed,
- throttle module installed,
- SimPro status,
- SimHub status,
- selected device/interface/report,
- brake pulse result,
- throttle pulse result,
- emergency stop result,
- paddle upshift result,
- paddle downshift result,
- wrong-pedal behavior,
- sustained-vibration behavior,
- pass/fail decision.

Draft and fail exports are allowed for local notes. A pass export is blocked until required evidence is present.

## Automated Tests

Stage 2R / Phase 3J tests are fake/model-only. They cover:

- checklist blocking when coexistence is not clear,
- checklist readiness when gates are complete,
- pass blocking when manual fields are missing,
- pass blocking when hardware confirmations are missing,
- complete pass-ready result evaluation,
- private local export formatting,
- local Markdown file export.
- controlled CLI dry-run output without private path leakage.
- controlled CLI execution blocking without the exact approval phrase.
- fake-writer brake/throttle sequence and emergency-stop reports.
- `VID_3670` classification as Simagic-family.
- direct-output candidate safe-label redaction while the selector retains the private path in memory.
- Raw Input metadata-only candidates blocked from real direct-output gates.
- private HID path preservation in memory without path leakage or relative-path fallback.
- direct-output open-check behavior that opens and closes without writing reports.
- direct-output dry-run gate validation requiring successful open-check plus known output-report capability or successful report-shape validation before `can pulse`.
- write-format diagnostics that classify Windows `IOException:0x80070057` as an invalid HID report shape rather than a plain disconnect.
- dry-run runner behavior that does not create or open a writer.

No automated test opens a real HID device or sends a real P-HPR report.

## Physical Validation Status

This repository does not claim completed physical P-HPR safety validation. Manual-local evidence is still required for brake/throttle pulse behavior, safety envelope, emergency-stop physical behavior, sustained-vibration behavior, physical latency, road/slip/lock feel, real coexistence, and live-session behavior.

Until real results are supplied, do not claim:

- correct P-HPR pedal mapping,
- safe strength,
- real stop behavior,
- emergency stop physical effectiveness,
- direct gear-pulse latency,
- SimPro/SimHub coexistence behavior on hardware.
