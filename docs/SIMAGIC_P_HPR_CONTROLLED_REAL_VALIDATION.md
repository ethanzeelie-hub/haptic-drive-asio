# Simagic P-HPR Controlled Real Validation

## Stage 2R Purpose

Stage 2R adds the validation harness for later supervised real P-HPR testing.

It does not run hardware vibration automatically, does not mark physical validation as passed, and does not add automated hardware tests.

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
- selected device/interface/report,
- visible safety limits,
- visible emergency stop,
- emergency stop latch clear,
- SimPro/SimHub coexistence `Clear`,
- brake/throttle pulse buttons available,
- gear paddle test planned.

The harness never triggers a pulse. Brake, throttle, and paddle tests remain manual actions through the Stage 2Q direct-control controls.

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

Stage 2R tests are fake/model-only. They cover:

- checklist blocking when coexistence is not clear,
- checklist readiness when gates are complete,
- pass blocking when manual fields are missing,
- pass blocking when hardware confirmations are missing,
- complete pass-ready result evaluation,
- private local export formatting,
- local Markdown file export.

No test opens a real HID device or sends a real P-HPR report.

## Physical Validation Status

Physical validation is pending Ethan's local supervised run.

Until real results are supplied, do not claim:

- correct P-HPR pedal mapping,
- safe strength,
- real stop behavior,
- emergency stop physical effectiveness,
- direct gear-pulse latency,
- SimPro/SimHub coexistence behavior on hardware.
