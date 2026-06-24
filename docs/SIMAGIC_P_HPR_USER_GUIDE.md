# Simagic P-HPR User Guide

## Status

Stage 2Q adds a gated direct-control UI and write-capable adapter for later manual testing. Phase 3A hardens that adapter with explicit writer lifecycle, timeout handling, disconnect diagnostics, report validation, and close-on-dispose behavior. Phase 3B completes instant paddle gear-pulse production integration through that same gated backend. Phase 3C adds road-vibration production routing through that same gated backend. Phase 3D adds wheel-slip and wheel-lock production routing through that same gated backend. Phase 3E adds the P-HPR workflow summary, P-HPR effect profiles, and fuller diagnostics/report coverage. Phase 3F validates replay-driven road/slip/lock software routing and replay-source diagnostics with mock/fake output only. Phase 3I simplifies normal Devices controls and moves research internals behind Advanced diagnostics. Phase 3J adds the final controlled CLI smoke-test command and zero-skip readiness reporting. The Stage 18 follow-up adds a runtime-only Paddle Gear Bench Test for mapped-paddle validation without live telemetry.

This repository does not claim completed physical P-HPR safety validation. Manual-local evidence is still required for pulse correctness, stop behavior, safety envelope, coexistence behavior, safe gain, physical latency, and sustained-vibration behavior.

## Devices Page Controls

Open the Devices page and find `Simagic P-HPR Pedals`.

The normal P-HPR pedals section shows the current mode (`Disabled`, `Mock`, or `Direct`), brake/throttle pulse settings, emergency-stop controls, selected-output status, and last pulse result without printing private device paths. Detailed workflow, direct-control, validation, and mock-routing internals are under `Advanced / Diagnostics` and hidden by default.

The section starts disabled and unarmed every app launch. The selected device path is not saved.

Controls:

- `Enable real direct control`: allows the direct-control path to be considered.
- `Arm direct control`: required in addition to enablement; cleared when enablement is off and never persisted.
- `Device path`: manually selected Windows HID path for the P700/P-HPR interface.
- `Interface`: short manual label for the selected interface.
- `Report ID`: optional report ID if later descriptor evidence requires one.
- `Report bytes`: expected report length; Stage 2Q encoder emits 64-byte SimHub F1 EC payloads.
- Brake and throttle pulse settings: enabled, strength, frequency, and duration.
- Brake and throttle road-vibration settings: enabled, minimum strength, maximum strength, minimum frequency, maximum frequency, and duration.
- Wheel-slip and wheel-lock settings: enabled, target pedal, minimum strength, maximum strength, minimum frequency, maximum frequency, and duration.
- `Test Brake Pulse` and `Test Throttle Pulse`: one manual pulse only, no loop.
- `Emergency Stop`: attempts brake and throttle stop reports when a device is selected, then latches.
- `Clear Emergency Stop`: clears the latch, but does not enable or arm direct control.

The pulse buttons remain disabled until direct control is enabled, armed, a device is selected, session authorization is active, coexistence status is `Clear`, the global interlock is clear, and emergency stop is clear. Direct mode selection does not authorize writes. Arm state does not authorize writes. The approval phrase authorizes only the current session.

Phase 3A diagnostics include connection state, writer-open state, open/close counts, last open/write/stop/close status, disconnect count, timeout count, invalid-report count, and write timeout. Phase 3B adds last gear-pulse latency diagnostics: paddle event time, accepted shift-intent time, command creation time, write completion time, and per-command traces. Phase 3C adds real road-vibration enabled state, per-pedal road settings, and last road route result diagnostics. Phase 3D adds real slip/lock enabled state, effect settings, and last slip/lock route result diagnostics. These diagnostics do not auto-run output and do not prove physical latency, road feel, slip feel, or lock feel.

## Profiles And Diagnostics

The Profiles page saves and loads a P-HPR effect profile beside the existing audio profile.

The P-HPR profile includes:

- shift-intent mode,
- mock gear routing,
- mock road/slip/lock routing,
- real gear-pulse preferences,
- real road-vibration preferences,
- real wheel-slip and wheel-lock preferences.

The P-HPR profile does not include direct-control enablement, arming, selected private HID path, emergency-stop latch, command history, write history, or validation result data.

The Diagnostics page and copied report include P-HPR workflow mode, profile paths, mock status, real status, coexistence state, validation status, live F1 validation checklist status, gear/road/slip/lock settings, last write status, and persistence boundary notes.

Phase 3F diagnostics also include pipeline input source, replay source file name or in-memory replay status, and replay packet count. Replay does not synthesize GT Neo paddle events.

Phase 3G diagnostics add a passive live F1 25 validation checklist covering telemetry, `DrivingArmed`, paddle listener, shift-intent acceptance, P-HPR mode, selected output readiness, SimPro/SimHub coexistence, emergency stop, mock gear pulse, real manual arming, road, slip/lock, and menu suppression.

## Paddle Gear Bench Test

Use `Paddle Gear Bench Test` on Devices when you need to validate the GT Neo paddle-to-gear-pulse path without live F1 telemetry.

Bench mode:

- starts disabled and unarmed,
- is not persisted,
- requires mapped left/right paddles,
- does not change normal `DrivingArmed` gating outside the bench,
- records accepted/suppressed counts, last accepted event, last suppression reason, and output mode.

Start in `Mock` output mode. Mock bench routing sends no HID reports and does not touch ASIO/BST-1.

Use `Direct` output mode only when direct gates are green: selected openable HID device-interface path, FeatureReport transport, report ID `0xF1`, 64-byte report length, successful open-check, report shape/capability accepted, session authorization active for the current session, coexistence `Clear`, emergency stop clear, road vibration disabled, and slip/lock disabled.

Initial direct bench defaults are brake only, `10%`, `50 Hz`, `50 ms`, one paddle press, no loop.

## First Safe Manual Settings

For later supervised local validation:

- one pedal at a time,
- start from `10%` strength,
- start from `50 ms` duration,
- conservative frequency such as `50 Hz` and never above the user-facing `50 Hz` cap,
- no loop,
- emergency stop visible,
- SimPro Manager closed,
- SimHub closed,
- selected device/interface/report confirmed.

If the wrong pedal moves, both pedals move unexpectedly, output feels too strong, vibration does not stop, the app stalls, or the device disconnects, use emergency stop and stop testing.

## Gear Pulse Routing

Accepted GT Neo paddle shift intent can route to the direct P-HPR adapter only when direct control is explicitly enabled and armed and the current session is authorized.

Default future gear-pulse behavior remains `InstantPaddleOnly`:

```text
mapped paddle press
-> cached DrivingArmed/Menu Safe gate
-> accepted ShiftIntentEvent
-> PHprDirectGearPulseRouter
-> safety limiter
-> real direct adapter
```

There is no telemetry gear-confirmation wait and no default second confirmation pulse.

Brake and throttle gear-pulse settings are independent. Each pedal can be enabled or disabled and can use its own strength, frequency, and duration. Upshift and downshift use the same default pulse; the direction is still visible in diagnostics.

Safe gear-pulse preferences are persisted. Direct-control enablement, arming, selected HID path, emergency-stop latch, command history, write history, and validation result data are not persisted.

## Road Vibration Routing

Real road vibration is disabled by default.

When enabled, the app can route road vibration to brake, throttle, or both pedals. Each pedal has independent minimum/maximum strength, minimum/maximum frequency, and duration settings. The route scales between those values from the current road intensity.

Road vibration requires direct control to be enabled and armed and the current session to be authorized. It is also blocked by stale telemetry, stopped haptics, emergency mute, cached `DrivingArmed` false, SimPro/SimHub conflict, missing selected output, emergency stop, global interlock latch, safety-limiter rejection, and the deterministic route interval.

Stage 18o-B makes P-HPR road vibration and the ASIO/BST-1 road texture effect consume the same shared software road signal. The outputs remain separate: P-HPR still routes through the P-HPR safety limiter and HID output path, while BST-1 still renders through the mixer and audio safety chain. Accepted gear pulses briefly duck/suppress road texture for priority.

## Wheel Slip And Wheel Lock Routing

Real wheel slip and wheel lock routing is disabled by default.

When enabled, wheel slip defaults to the throttle pedal and wheel lock defaults to the brake pedal. Each effect can be enabled independently and can target brake, throttle, or both pedals. Each effect has its own minimum/maximum strength, minimum/maximum frequency, and duration settings.

Wheel lock priority is above wheel slip. Both are above road vibration and below instant gear pulse. Road vibration yields in the same WPF routing tick when a higher-priority slip/lock command just routed.

Slip and lock routing require the same gates as road vibration: direct control enabled and armed, current-session authorization active, fresh telemetry, haptics running, emergency mute clear, cached `DrivingArmed` true, SimPro/SimHub coexistence `Clear`, selected output ready, emergency stop clear, global interlock clear, safety-limiter acceptance, and the deterministic route interval.

The ASIO/BST-1 slip and brake-lock effect remains separate and unchanged.

## Controlled Validation Harness

Stage 2R adds a `P-HPR Controlled Validation Harness` section on the Devices page.

It does not trigger hardware output. It evaluates readiness and exports private local notes.

Use it to record:

- user present,
- P700 connected,
- brake and throttle modules installed,
- selected device/interface/report,
- brake pulse result,
- throttle pulse result,
- emergency stop result,
- paddle upshift result,
- paddle downshift result,
- wrong-pedal behavior,
- sustained-vibration behavior,
- notes,
- pass/fail decision.

If `pass` is entered, export is blocked until the required fields and hardware confirmations are complete.

Private exports go under `local-validation-results/` when the repo root is available. Do not commit those results.

## Controlled CLI Smoke Test

Use direct-output dry-run and open-check before executing any real CLI pulse:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- direct-output-dry-run --candidate-index 0 --enable --arm --approval "I approve Phase 2 controlled P-HPR write testing"
```

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- direct-output-open-check --candidate-index 0 --enable --arm --approval "I approve Phase 2 controlled P-HPR write testing"
```

Use this controlled-write dry-run before adding `--execute`:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- controlled-write-test --approval "I approve Phase 2 controlled P-HPR write testing" --device-path "<private-hid-path>" --target sequence --strength-percent 10 --frequency-hz 50 --duration-ms 50
```

Open-check is real hardware access even though it sends no reports. Dry-run does not authorize writes. Add `--execute` only when physically present, the selected private HID path has passed no-report open-check, SimPro/SimHub coexistence is clear, and emergency stop is visible. The command hides the private HID path in console output, requests emergency stop at the end, and does not export local validation evidence.

## Live F1 25 Validation Workflow

Phase 3G adds `P-HPR Live F1 Validation` to the Devices page.

Use it for the final supervised live session order:

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

The checklist is passive. It does not trigger output, open hardware, or prove physical behavior.

## What Is Not Saved

These runtime states are not persisted:

- direct control enabled,
- direct control armed,
- selected P-HPR HID device path,
- emergency stop latch,
- command history,
- write history,
- safety latch state.

Mock routing preferences and input mapping remain separate from real direct-control arming.

## What Stage 2Q Does Not Prove

Stage 2Q through Phase 3J do not prove physical pedal mapping, safe output strength, real stop behavior, sustained-vibration behavior, SimPro/SimHub coexistence on the device, report descriptor details, road feel, slip feel, lock feel, or latency.

## Final Reference Docs

- Quick start: `docs\QUICK_START.md`
- App user guide: `docs\USER_GUIDE.md`
- Troubleshooting: `docs\TROUBLESHOOTING.md`
- Final acceptance: `docs\FINAL_P_HPR_ACCEPTANCE.md`

Phase 3J adds the final controlled CLI smoke-test path. It does not complete physical validation, and it does not claim completed physical P-HPR safety validation.
