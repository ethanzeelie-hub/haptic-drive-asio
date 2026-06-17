# Architecture

Haptic Drive ASIO is organized around clear boundaries between telemetry input, shared vehicle state, haptic effects, audio output, recording, and UI.

## Initial Project Boundaries

- `HapticDrive.Asio.App`: WPF app shell and presentation layer.
- `HapticDrive.Asio.Core`: shared models, audio/output interfaces, and domain rules.
- `HapticDrive.Asio.Telemetry.F1_25`: official F1 25 UDP packet parsing and mapping into shared state.
- `HapticDrive.Asio.Audio`: output device abstractions, mixer, safety processors, test bench, haptic effect generation, and debug output paths.
- `HapticDrive.Asio.Recording`: raw packet recording and deterministic replay.

## Target Flow

```text
F1 25 UDP packets
-> game-specific parser
-> game-specific adapter
-> shared VehicleState
-> haptic effect engine
-> mixer and safety chain
-> audio output device
```

Manual ASIO hardware validation reuses the same audio boundary:

```text
Manual 40/50 Hz test request
-> synthetic sine generator
-> mixer and safety chain
-> limiter
-> selected ASIO output device/channel
```

The deterministic synthetic benchmark remains a separate Null-output test bench for automated validation.

## Phase 2 Planned Actuator Boundary

Phase 2 adds planned Simagic P-HPR pedal support as a separate non-audio actuator path. Stage 2Q includes read-only paddle/input diagnostics, cached `DrivingArmed` shift-intent diagnostics, read-only P700 / P-HPR inventory tooling, capture metadata workflow tooling, read-only capture analysis tooling, analysis-only protocol hypotheses, mock-only protocol/output diagnostics, a reusable P-HPR safety limiter, mock-only gear pulse routing, mock-only road/slip/lock pedal-effect routing, read-only SimPro / SimHub coexistence detection, a controlled-write readiness model/runbook, and a gated minimal Windows HID real-output adapter. Stage 2R adds a controlled validation harness. Phase 3A hardens the real-output adapter lifecycle and diagnostics. Phase 3B completes instant paddle gear-pulse production integration with safe per-pedal settings persistence and latency trace diagnostics. Phase 3C completes real road-vibration production integration with safe per-pedal road scaling and route-interval suppression. Phase 3D completes real wheel-slip and wheel-lock production integration with safe per-effect settings, route-interval suppression, and priority above road and below gear pulse. Phase 3E adds UI workflow summaries, safe P-HPR effect profiles, and diagnostics report coverage around those existing routes. Phase 3F validates replay-driven road/slip/lock software routing and replay-source diagnostics with mock output only. Phase 3G adds a passive live F1 25 validation checklist and diagnostics line. Phase 3H packages final quick-start, troubleshooting, acceptance, and safety documentation. Phase 3I simplifies normal app navigation and moves P-HPR research internals behind persisted Advanced diagnostics while keeping the same actuator boundaries. The real adapter is disabled/unarmed by default and is not physically validated.

P-HPR modules must not be routed through ASIO and must not implement `IAudioOutputDevice`.

Planned separation:

```text
F1 25 UDP packets
-> VehicleState
-> audio haptic effects
-> mixer and safety chain
-> ASIO/BST-1 output

GT Neo paddle input and VehicleState
-> shift intent / pedal effect routing
-> actuator safety limiter
-> P-HPR pedal output
```

The default P-HPR gear-pulse path is `InstantPaddleOnly`: read-only GT Neo paddle press, cached `DrivingArmed` gate, then immediate pedal gear pulse. It does not wait for a fresh telemetry packet at paddle-press time and does not fire a default second telemetry-confirmed pulse.

The Paddle Gear Bench Test is a runtime-only validation branch, not a replacement for normal shift intent:

```text
mapped GT Neo paddle input
-> local bench enable/arm gate
-> bench ShiftIntentEvent
-> mock P-HPR gear router or strict direct P-HPR gate
-> P-HPR output path
```

Normal live-driving shift intent still uses `ShiftIntentProcessor` and cached `DrivingArmed` telemetry gating. The bench branch exists so mapped paddle input, mock routing, and later strictly gated direct pulses can be validated without live F1 telemetry.

Real P-HPR USB writes are gated behind the exact approval phrase in `docs/SIMAGIC_P_HPR_SAFETY_PLAN.md`.

## Stage 2M Mock Gear Pulse Routing

Stage 2M adds `HapticDrive.Actuation.PHpr` as the mock-only routing layer after accepted shift intent.

The implemented mock gear path is:

```text
Stage 2E mapped paddle input
-> Stage 2F ShiftIntentProcessor
-> accepted ShiftIntentEvent
-> PHprGearPulseRouter
-> SafetyLimitedPhprOutputDevice
-> MockPhprOutputDevice
-> in-memory mock command/frame diagnostics
```

`PHprGearPulseRouter` creates conservative `PaddleShiftIntent` commands only when the event has already passed cached `DrivingArmed` gating. The default target is both brake and throttle modules with strength `0.05`, frequency `50 Hz`, duration `50 ms`, and priority `100`. `SafetyLimitedPhprOutputDevice` still applies Stage 2L context gates, clamps, command-rate limits, continuous-duration limits, and emergency-stop latching before the mock output can record frames.

The WPF app owns a private mock stack for diagnostics:

- `MockPhprOutputDevice`
- `SafetyLimitedPhprOutputDevice`
- `PHprGearPulseRouter`
- `PHprPedalEffectsRouter`

Advanced diagnostics controls expose mock routing enabled/disabled, target, strength, frequency, duration, clear diagnostics, mock emergency stop, and clear mock emergency stop. Normal Devices controls expose the simplified P-HPR pedal mode, brake/throttle test pulses, and emergency stop. Persisted settings are limited to safe preferences. Emergency-stop state and mock histories are not persisted.

Stage 2M does not route road vibration, wheel slip, wheel lock, `VehicleState`, audio effects, ASIO output, or mixer output to P-HPR. Stage 2N adds road/slip/lock mock routing separately. Neither stage opens device handles, sends HID output reports, sends HID feature reports, writes USB data, controls SimPro Manager, controls SimHub, or creates a production protocol adapter.

## Stage 2N Mock Pedal Effects Routing

Stage 2N adds `PHprPedalEffectsRouter` beside the Stage 2M gear router.

The implemented mock pedal-effect path is:

```text
F1 25 telemetry / latest VehicleState
-> PHprPedalEffectsRouter
-> SafetyLimitedPhprOutputDevice
-> MockPhprOutputDevice
-> in-memory mock command/frame diagnostics
```

The router consumes existing `VehicleState` / `HapticPipelineSnapshot` data and does not parse new F1 25 packet fields. It evaluates from the WPF telemetry/status update path rather than the audio render callback.

Default mock effects are:

- road vibration: target both modules, strength `0.01` to `0.04`, frequency `25` to `45 Hz`, duration `50 ms`, source `RoadTexture`;
- wheel slip: target throttle, strength `0.03` to `0.08`, frequency `35` to `50 Hz`, duration `50 ms`, source `WheelSlip`;
- wheel lock: target brake, strength `0.04` to `0.10`, frequency `40` to `50 Hz`, duration `50 ms`, source `WheelLock`.

Priority is per target module: wheel lock, then wheel slip, then road vibration. A deterministic minimum interval per effect/module prevents command storms before commands reach the safety limiter.

The WPF app shares one mock `MockPhprOutputDevice` and one `SafetyLimitedPhprOutputDevice` between Stage 2M gear routing and Stage 2N pedal effects. This keeps safety state, emergency stop, mock command/frame counts, and pending scheduled stops global for the mock P-HPR path.

Persisted pedal-effect settings are limited to safe mock preferences: global enabled state plus road/slip/lock enabled state, target, strength, frequency, and duration. Emergency-stop state, safety latch state, mock histories, real-write approval, real-write enabled state, and real-write armed state are not persisted.

Stage 2N does not change the ASIO/BST-1 road/slip/lock audio path.

## Stage 2O SimPro / SimHub Coexistence Detection

Stage 2O adds read-only coexistence detection under `HapticDrive.Simagic.PHPR.Abstractions.Coexistence`.

The detector stack is:

```text
WindowsProcessSnapshotProvider
-> PHprSoftwareCoexistenceDetector
-> PHprSoftwareCoexistenceSnapshot
-> PHprSafetyContext.SoftwareConflictStatus
-> PHprSafetyLimiter
```

The implementation enumerates process names only. It reports `Unknown`, `Clear`, `SimProRunning`, `SimHubRunning`, or `ActiveConflict`. `ActiveConflict` is passed into P-HPR safety contexts and rejects start commands through the existing `SimProConflict` safety violation.

The WPF Devices and Diagnostics pages show SimPro Manager running state, SimHub running state, coexistence status, last scan time, direct-control block status, matching process names, and the read-only detection statement.

Stage 2O does not kill, hook, inject into, patch, inspect memory, IPC-control, or modify SimPro Manager or SimHub. It does not add real P-HPR output, USB writes, HID reports, controlled write testing, or ASIO/BST-1 routing.

## Stage 2P Controlled Write Readiness

Stage 2P adds no-write readiness planning under `HapticDrive.Simagic.PHPR.Abstractions.Readiness`.

The readiness stack is:

```text
PHprControlledWriteChecklist
-> PHprControlledWriteReadiness
-> WPF Direct Write Readiness diagnostics
-> manual runbook / result template
```

`PHprControlledWriteReadiness` always reports Stage 2P as blocked for real output. This remains true even if every future manual checklist item is marked true. The model exists to make blockers explicit before Stage 2Q, not to enable output.

The WPF Devices and Diagnostics pages show disabled direct-write readiness, checklist blockers, and the no-write statement. Stage 2P does not add a real adapter, HID writer, write-capable button, automatic pulse, persisted armed state, or hardware output.

## Stage 2Q Gated Real Direct Output

Stage 2Q adds `HapticDrive.Simagic.PHPR.Output.Windows` as the minimal write-capable direct-control backend for later manual validation.

The real direct-control stack is:

```text
manual test pulse or accepted ShiftIntentEvent
-> PHprCommand
-> SimagicPhprOutputDevice
-> PHprSafetyLimiter
-> SimHubF1EcRealReportEncoder
-> IPhprHidReportWriter
-> selected Windows HID device path
```

`WindowsHidReportWriter` is configured only from runtime UI/manual selection. Device path, enabled state, armed state, emergency-stop latch, and histories are not persisted.

`SimHubF1EcRealReportEncoder` emits the Stage 2J/2P preferred SimHub `F1 EC` start/stop family:

- brake module `01`,
- throttle module `02`,
- start state `01`,
- stop state `00`,
- frequency byte as direct Hz,
- strength byte as direct percent,
- software-timed duration through delayed stop reports.

SimPro Manager `80 1E 89` detailed writes remain unsupported.

The WPF Devices page exposes real direct-control enable, arm, manual device/interface/report selection, per-pedal brake/throttle settings, one-pulse brake/throttle test buttons, emergency stop, clear emergency stop, and last write diagnostics. Pulse buttons are disabled unless direct control is enabled, armed, a device is selected, coexistence is `Clear`, and emergency stop is not latched.

Accepted `ShiftIntentEvent` values are offered to `PHprDirectGearPulseRouter`, but real direct gear routing stays inert unless enabled and armed for the current session. The route does not wait for telemetry gear confirmation.

Stage 2Q does not validate physical P-HPR behavior, safe gain, stop behavior, pedal mapping, or latency. Automated verification uses fake HID writers only.

## Stage 2R Controlled Validation Harness

Stage 2R adds validation models under `HapticDrive.Simagic.PHPR.Abstractions.Validation`:

- `PHprManualValidationChecklist`
- `PHprManualValidationReadiness`
- `PHprManualValidationResult`
- `PHprManualValidationResultEvaluation`
- `PHprManualValidationResultExporter`

The WPF Devices page includes a controlled validation harness below the direct-control panel. It reads current direct-control/coexistence/emergency-stop state, combines it with user confirmations, reports readiness, and exports private local Markdown results.

The harness does not send P-HPR commands, does not call the HID writer, and does not trigger brake, throttle, or paddle pulses. It only records checklist and result data for manual validation.

Exports go to `local-validation-results/` when the repo root is available, otherwise LocalAppData. That folder is ignored and should not be committed when it contains private hardware data.

`pass` decisions are blocked unless required manual result fields and hardware confirmations are present. The app still cannot independently verify physical truth; Stage 2R does not mark real P-HPR validation complete without user-supplied results.

## Phase 3A Production P-HPR Output Adapter Hardening

Phase 3A hardens the existing `HapticDrive.Simagic.PHPR.Output.Windows` backend instead of adding another output path.

The hardened direct-control stack is:

```text
manual test pulse or accepted ShiftIntentEvent
-> PHprCommand
-> SimagicPhprOutputDevice gates
-> PHprSafetyLimiter
-> SimHubF1EcRealReportEncoder
-> IPhprHidReportWriter.OpenAsync / WriteReportAsync / CloseAsync
-> selected Windows HID device path
```

The writer boundary now separates lifecycle from command routing. `SimagicPhprOutputDevice` lazily opens the writer only for explicit start, stop, or emergency-stop operations; configuration and app startup remain inert. `WindowsHidReportWriter` owns the selected file handle and validates the selected 64-byte SimHub F1 EC report length before writing.

Adapter diagnostics include connection state, writer-open state, open/close attempts and successes, write/stop status, report counters, stop-report counters, timeout counters, disconnect counters, invalid-report counters, and last error. Dispose attempts emergency-stop-style brake/throttle stop reports only when a selected device is armed or a stop is pending, then closes the writer where possible.

Start commands still require explicit enable, explicit arm, selected device/interface/report, clear SimPro/SimHub coexistence, clear emergency stop, and `PHprSafetyLimiter` acceptance. Stop and emergency-stop paths can attempt safe stop reports with a selected valid interface. Phase 3A does not add startup output, persisted arming, automated hardware writes, or ASIO/BST-1 routing.

## Phase 3B Instant Paddle Gear Pulse Production Integration

Phase 3B keeps the Phase 3A output adapter as the only real direct-output backend and completes the instant gear-pulse route.

The production gear-pulse path is:

```text
Stage 2E mapped paddle input
-> Stage 2F ShiftIntentProcessor
-> accepted ShiftIntentEvent
-> PHprDirectGearPulseRouter
-> SimagicPhprOutputDevice gates
-> PHprSafetyLimiter
-> SimHubF1EcRealReportEncoder
-> IPhprHidReportWriter
```

`ShiftIntentEvent` now carries the accepted timestamp so diagnostics can distinguish paddle event time from accepted shift-intent time. `PHprDirectGearPulseRouter` stamps command creation, records per-command traces, and surfaces first write completion time from the output diagnostics. These are software timestamps for route visibility, not physical latency measurements.

Brake and throttle gear-pulse profiles are independent for real direct control. Each pedal has enabled, strength, frequency, and duration settings. Safe values persist through app settings; real direct-control enabled state, armed state, selected private HID path, emergency-stop state, command history, and write history remain runtime-only.

Phase 3B does not route road vibration, wheel slip, or wheel lock through the real direct-output backend. Those remain later production-integration stages. The ASIO/BST-1 audio path remains independent and unchanged.

## Phase 3C P-HPR Road Vibration Production Integration

Phase 3C adds `PHprRoadVibrationRouter` in `HapticDrive.Actuation.PHpr` beside the existing mock pedal-effects router and real gear-pulse route.

The production road-vibration path is:

```text
F1 25 telemetry / latest VehicleState
-> cached DrivingArmed/Menu Safe state
-> PHprRoadVibrationRouter
-> SimagicPhprOutputDevice gates
-> PHprSafetyLimiter
-> SimHubF1EcRealReportEncoder
-> IPhprHidReportWriter
```

The router consumes existing `VehicleState` / `HapticPipelineSnapshot` state and does not add new F1 25 packet parsing. It is evaluated from the WPF telemetry/status update path rather than the audio render callback. It creates `RoadTexture` P-HPR commands only when the road effect is active, haptics are running, telemetry is fresh, cached `DrivingArmed` is true, direct control is enabled and armed, selected output is ready, coexistence is `Clear`, emergency stop is clear, and the deterministic per-module interval allows another command.

Brake and throttle road settings are independent. Each pedal has enabled, minimum strength, maximum strength, minimum frequency, maximum frequency, and duration settings. Safe preferences persist through app settings; real direct-control enabled state, armed state, selected private HID path, emergency-stop state, command history, and write history remain runtime-only.

Road-vibration priority remains below gear pulse, wheel slip, and wheel lock. Phase 3C itself does not route wheel slip or wheel lock through the real direct-output backend; Phase 3D adds that route separately. The ASIO/BST-1 road texture effect remains a separate audio effect path and is unchanged.

## Phase 3D P-HPR Wheel Slip And Wheel Lock Production Integration

Phase 3D adds `PHprSlipLockRouter` in `HapticDrive.Actuation.PHpr` beside the road-vibration router and real gear-pulse route.

The production slip/lock path is:

```text
F1 25 telemetry / latest VehicleState
-> cached DrivingArmed/Menu Safe state
-> PHprSlipLockRouter
-> SimagicPhprOutputDevice gates
-> PHprSafetyLimiter
-> SimHubF1EcRealReportEncoder
-> IPhprHidReportWriter
```

The router consumes existing `VehicleState` / `HapticPipelineSnapshot` state and does not add new F1 25 packet parsing. It is evaluated from the WPF telemetry/status update path rather than the audio render callback. It creates `WheelSlip` and `WheelLock` P-HPR commands only when the relevant effect is active, haptics are running, telemetry is fresh, cached `DrivingArmed` is true, direct control is enabled and armed, selected output is ready, coexistence is `Clear`, emergency stop is clear, and the deterministic per-module interval allows another command.

Slip and lock settings are independent. Each effect has enabled state, target module, minimum strength, maximum strength, minimum frequency, maximum frequency, and duration settings. Safe preferences persist through app settings; real direct-control enabled state, armed state, selected private HID path, emergency-stop state, command history, and write history remain runtime-only.

Priority stays per target module: gear pulse remains highest, wheel lock is above wheel slip, and both are above road vibration. The WPF routing tick lets road vibration yield when a slip/lock command has just routed. The ASIO/BST-1 slip and brake-lock audio effect remains separate and unchanged.

## Phase 3E P-HPR UI, Profiles, And Diagnostics

Phase 3E stays in the WPF app layer and does not add a new output backend.

The UI/profile stack is:

```text
Devices page controls
-> safe app settings / P-HPR effect profile
-> mock routers or gated real routers
-> diagnostics report
```

`PhprEffectProfileStore` saves a separate local JSON file beside the existing audio profile. The P-HPR profile contains shift-intent, mock routing, real gear, road, slip, and lock effect preferences only. It does not contain direct-control enablement, direct-control arming, selected private HID path, emergency-stop state, command history, write history, or validation result data.

The Devices page now exposes normal P-HPR pedal controls that report `Disabled`, `Mock`, or `Direct` mode, use percentage strength, cap frequency at `1-50 Hz`, default test pulses to `10%`, `50 Hz`, and `50 ms`, and keep emergency stop visible. Detailed research sections remain behind Advanced diagnostics and stay hidden by default.

The Diagnostics page and copied report include profile paths, workflow mode, coexistence, mock/real settings, validation status, last write status, and persistence boundary notes while avoiding raw captures, serial numbers, private validation results, and unsanitized device inventories.

## Phase 3F P-HPR Replay Validation

Phase 3F stays in automated replay validation and app diagnostics. It does not add a new output backend, parser field, or real hardware path.

The replay validation path is:

```text
TelemetryRecording / .hdrec
-> TelemetryReplayService
-> HapticPipelineCoordinator
-> F1 25 v3 parser
-> VehicleState adapter
-> DrivingArmedStateService
-> P-HPR road/slip/lock routers
-> SafetyLimitedPhprOutputDevice
-> MockPhprOutputDevice
```

Runtime tests build synthetic F1 25 v3 packets from the existing parser definitions and verify road, slip, and lock routing from replayed telemetry. They also verify stale telemetry, emergency mute, profile-style target/enable settings, and that replay does not create `PaddleShiftIntent` commands.

The WPF P-HPR workflow and copied diagnostics report include pipeline input source, replay source file name or in-memory replay status, and replay packet count. Raw private paths, captures, serial numbers, and hardware inventories remain out of P-HPR replay diagnostics.

## Phase 3G Live F1 25 P-HPR Validation Workflow

Phase 3G adds `PhprLiveF1ValidationGuide`, a passive app-level checklist builder used by the Devices P-HPR workflow section and the copied Diagnostics report.

The live validation summary consumes existing snapshots only:

```text
HapticPipelineSnapshot + UDP receiver snapshot
-> DrivingArmedStateService
-> paddle listener diagnostics
-> shift-intent diagnostics
-> P-HPR output/coexistence/emergency diagnostics
-> live F1 validation checklist
```

The checklist covers app startup with direct control disabled, live F1 25 telemetry, `DrivingArmed`, paddle acceptance, mock gear-pulse diagnostics, manual real arming, brake/throttle gear tests, road vibration, slip/lock, menu/tabbing suppression, emergency stop, and SimPro/SimHub warnings.

Phase 3G does not add a new output route, does not open HID devices, does not send reports, does not generate synthetic paddle events, does not validate physical hardware, and does not claim live F1 25 validation. Automated tests exercise checklist text and diagnostics only.

## Phase 3H Final P-HPR Acceptance Package

Phase 3H is documentation and acceptance packaging. It adds quick-start, troubleshooting, final acceptance, final safety review, and final user-guide coverage around the existing implementation.

No runtime architecture changes are made in Phase 3H. It does not add output routes, device access, parser fields, synthetic inputs, or ASIO/BST-1 changes. Physical validation remains pending Ethan's supervised local run.

## Phase 3I Simplified P-HPR Controls And Routing UI

Phase 3I stays in the WPF app layer. It simplifies the shell into Dashboard, Devices, Effects, Routing / Mixer, Telemetry / UDP, Profiles, and Advanced / Diagnostics.

Normal Devices UI is split into Bass Shaker / ASIO, Simagic P-HPR Pedals, and Simagic Wheel / Shift Paddles. P-HPR pedals expose Disabled, Mock, and Direct modes, brake/throttle enabled state, percentage strength, 1-50 Hz frequency, 10-1000 ms duration, default 10% / 50 Hz / 50 ms test pulses, and emergency stop / clear controls.

Advanced / Diagnostics is hidden by default and persisted as an app preference. It owns the detailed P-HPR workflow summary, live validation checklist, coexistence diagnostics, direct-control internals, validation harness, mock gear routing internals, and mock pedal-effects internals.

Phase 3I does not add a new output backend, does not persist real direct-control enablement/arming/device paths, does not execute hardware writes, does not validate physical P-HPR behavior, and does not touch the ASIO/BST-1 audio path.

## Stage 2B Input and P-HPR Abstractions

Stage 2B adds contract-only projects for the future actuator path:

- `HapticDrive.Input.Abstractions` defines input-device descriptors, read-only discovery contracts, paddle shift-intent contracts, `ShiftIntentEvent`, `PaddleSide`, `ShiftIntentDirection`, `ShiftIntentMode`, `ShiftIntentSource`, `DrivingArmedState`, and `IDrivingArmedStateProvider`.
- `HapticDrive.Input.Windows` is the Windows read-only input discovery and button-state reading home. Stage 2D implements Raw Input metadata enumeration and Windows game-controller capability enumeration there; Stage 2E adds read-only Windows game-controller button-state polling for paddle diagnostics.
- `HapticDrive.Simagic.PHPR.Abstractions` defines `PHprCommand`, module/source enums, safety flags/defaults, mock protocol records/encoders/decoders/schedulers, `IPHprOutputDevice`, output snapshots/results, and a `MockPhprOutputDevice`.

`MockPhprOutputDevice` is mock-only. It records clamped commands and generated mock protocol frames in memory for tests and diagnostics, marks commands as `MockOnly`, and performs no hardware writes.

## Stage 2C DrivingArmed State Service

Stage 2C adds `HapticDrive.Actuation` as the home for cached non-audio actuator gating.

`DrivingArmedStateService` consumes existing `VehicleState` and `HapticPipelineSnapshot` data. It keeps `DrivingArmed` false until recent valid telemetry proves active driving, then suppresses future paddle haptics when cached state indicates:

- no telemetry,
- stale telemetry,
- stopped haptics,
- emergency mute,
- game pause,
- network pause,
- garage/menu/result state,
- invalid vehicle state,
- or not-moving/inactive state when zero-speed active driving is disabled.

The service is in-memory and event-driven. It does not block waiting for telemetry at paddle-event time and is not yet connected to an input listener or shift-intent router.

## Stage 2D Read-Only Input Discovery

Stage 2D extends `HapticDrive.Input.Abstractions` with richer read-only input discovery models:

- `InputDeviceInfo`
- `InputDeviceKind`
- `InputDiscoveryMethod`
- `InputControlInfo`
- `InputDeviceDiscoverySnapshot`
- `IInputDeviceDiscovery`
- `IWheelInputCandidateProvider`

`HapticDrive.Input.Windows` implements `WindowsInputDeviceDiscovery` with two read-only enumerators:

- `RawInputDeviceEnumerator` uses Windows Raw Input APIs to enumerate device metadata, safe redacted device paths, HID VID/PID where available, HID usage page/usage, and broad Raw Input device class.
- `WindowsGameControllerDeviceEnumerator` uses the built-in Windows game-controller capability API to enumerate connected controller names, button count, axis count, and read-only control slots.

`WheelInputCandidateProvider` scores synthetic and discovered devices as likely Simagic wheelbase, likely GT Neo / wheel input path, likely P700 pedals, or unknown HID/game-controller candidates. The scoring is intentionally non-authoritative until the user supplies exact Device Manager / USBView / controller tester data.

The WPF Devices page exposes a manual Refresh Input Devices button and read-only candidate summary. This does not start live input event listening, map left/right paddles, create `ShiftIntentEvent` values, route haptics, send USB output reports, send feature reports, or send P-HPR commands.

Stage 2E uses these discovery snapshots to choose and map the read-only paddle input listener without changing the no-write P-HPR gate.

## Stage 2E Read-Only Paddle Listener

Stage 2E extends the input boundary without touching the ASIO/BST-1 audio path or the future P-HPR output path.

`HapticDrive.Input.Abstractions` now owns the read-only paddle listener model:

- `InputDeviceSelection`
- `WheelPaddleMapping`
- `InputButtonState`
- `InputEventTimestamp`
- `InputListenerStatus`
- `WheelPaddleRawButtonEvent`
- `WheelPaddleInputEvent`
- `WheelPaddleInputSnapshot`
- `IInputButtonStateReader`
- `PollingWheelPaddleInputSource`
- `WheelPaddleInputProcessor`

The processor handles raw button changes, rising-edge detection, release-to-rearm behavior, conservative debounce, UTC timestamps, stopwatch ticks, last raw-button diagnostics, mapped left/right paddle events, listener status, and captured errors. These mapped paddle events are diagnostics only in Stage 2E. `PollingWheelPaddleInputSource` implements `IWheelPaddleInputSource` for future compatibility but deliberately does not raise hardware-derived `ShiftIntentEvent` values in this stage.

`HapticDrive.Input.Windows` adds `WindowsGameControllerButtonStateReader`, which reads button states through the built-in Windows game-controller API using the native joystick index discovered by Stage 2D. This is read-only polling. It does not send USB output reports, feature reports, vibration commands, or Simagic-specific control messages.

Raw Input remains the preferred metadata discovery path, but Stage 2E does not decode live Raw Input HID button reports because reliable button mapping requires HID report-descriptor data from the user's Alpha Evo / GT Neo path. If the Windows game-controller path is insufficient, a later stage can add a Raw Input or HID input-report reader behind `IInputButtonStateReader`.

The WPF Devices page now exposes manual paddle diagnostics:

- select a Windows game-controller input device,
- start/stop the read-only listener,
- observe the last changed raw button,
- map left/right paddles from that last changed button,
- display left/right current state,
- display last mapped paddle event timestamp and count,
- and persist safe input mapping settings separately from haptic profiles.

Persisted input settings include only selected input device ID, selected input method, left/right button IDs, and debounce duration. Haptic running state, emergency mute, ASIO arming, P-HPR control enablement, and unsafe hardware state are not persisted.

## Stage 2F Shift Intent Event Layer

Stage 2F adds `HapticDrive.Actuation.Shift` as the event/gating layer between mapped paddle diagnostics and later actuator routing.

`ShiftIntentProcessor` subscribes to mapped `WheelPaddleInputEvent` values from the Stage 2E listener in the WPF app. For each mapped paddle press it:

- reads `IDrivingArmedStateProvider.Current`,
- maps left paddle to `Downshift` and right paddle to `Upshift`,
- applies the selected `ShiftIntentMode`,
- emits an accepted `ShiftIntentEvent` only when the layer is enabled, `DrivingArmed` is true, and the mode allows immediate paddle intent,
- records suppressed diagnostics when `DrivingArmed` is false, the layer is disabled, or `TelemetryConfirmedOnly` is active,
- and writes accepted events only to an in-memory sink / event surface for diagnostics.

The default mode is `InstantPaddleOnly`. It accepts mapped left/right paddle presses immediately when cached `DrivingArmed` is true, does not wait for a fresh telemetry packet, does not wait for a gear-confirmation packet, and does not create a default confirmation event.

`TelemetryConfirmedOnly` is a secondary/debug mode in Stage 2F. It observes mapped paddle presses diagnostically but suppresses immediate accepted `ShiftIntentEvent` emission. The existing Phase 1 telemetry-confirmed ASIO gear-shift effect remains separate.

`InstantWithRejectedShiftFeedback` accepts immediate intent like `InstantPaddleOnly` and records a pending-confirmation diagnostic count. It does not implement rejected-shift detection or feedback output yet.

The WPF Devices and Diagnostics pages show shift-intent enabled state, mode, cached `DrivingArmed` state/reason, telemetry age, menu-safe/recent-telemetry settings, last paddle side, last direction, accepted/suppressed counters, last accepted event, last suppression reason, last known telemetry gear/speed/RPM/frame, pending confirmations, and errors. App settings persist only shift-intent enabled state and mode.

Stage 2F does not call `IPHprOutputDevice`, `MockPhprOutputDevice`, `PHprCommand`, `GearShiftEffect`, `AudioRenderPipeline`, `AudioMixer`, `AsioAudioOutputDevice`, or any USB write path. Stage 2M now routes accepted shift intents to mock P-HPR gear pulses in a separate router layer.

## Stage 2G Read-Only P700 / P-HPR Inventory

Stage 2G adds `HapticDrive.Simagic.PHPR.Research` as a console/reusable research utility for inventory only.

The project owns:

- `SimagicDeviceInventorySnapshot`
- `SimagicDeviceInventoryItem`
- `SimagicDeviceCandidateKind`
- `SimagicDeviceInventoryMethod`
- `SimagicDeviceInventoryError`
- `SimagicDeviceInventoryExport`
- `SimagicDeviceInventorySanitizer`
- `ISimagicDeviceInventoryProvider`
- `ISimagicDeviceInventoryExporter`

The default provider composes three read-only sources:

- existing Stage 2D input discovery metadata,
- Windows HID registry metadata under `HKLM\SYSTEM\CurrentControlSet\Enum\HID`,
- and Windows USB registry metadata under `HKLM\SYSTEM\CurrentControlSet\Enum\USB`.

The registry source reads only installed PnP metadata such as display name, manufacturer, service, class, class GUID, driver provider/version where available, VID/PID, MI/interface number, and collection number. It does not open HID device handles, send output reports, request feature reports, claim interfaces, install drivers, or take control from SimPro Manager or SimHub.

The CLI command is:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- inventory
```

By default it writes sanitized JSON and Markdown summaries to ignored `local-device-inventory/`. The exports preserve VID/PID and non-sensitive class/manufacturer/product data while redacting serial-like path segments and Windows usernames.

Stage 2G does not reference `HapticDrive.Simagic.PHPR.Abstractions`, `IPHprOutputDevice`, `MockPhprOutputDevice`, `PHprCommand`, `HapticDrive.Asio.Audio`, or the ASIO/BST-1 output path. The local Stage 2G run found zero Simagic-specific P700/P-HPR/Alpha/GT Neo candidates, so real hardware inventory remains awaiting user-provided Device Manager / USBView / tool output.

## Stage 2H Capture Workflow And Metadata Tooling

Stage 2H extends `HapticDrive.Simagic.PHPR.Research` with capture workflow and metadata tooling only.

The project owns:

- `SimagicCaptureScenario`
- `SimagicCaptureScenarioId`
- `SimagicCaptureMetadata`
- `SimagicCaptureSoftwareContext`
- `SimagicCaptureDeviceContext`
- `SimagicCaptureActionContext`
- `SimagicCaptureSettingSnapshot`
- `SimagicCaptureMetadataValidator`
- `SimagicCaptureFilenameBuilder`
- `SimagicCaptureSanitizer`
- `SimagicCaptureManifest`
- `SimagicCaptureManifestExporter`

The CLI commands are:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-scenarios
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-template --scenario BrakeTestVibration --target Brake
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- validate-capture-metadata <path>
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-manifest <metadata-folder>
```

Default generated metadata output goes under ignored `capture-metadata/generated/`. Raw captures remain under private ignored paths such as `captures/private/simagic/`.

Stage 2H does not parse `.pcap` or `.pcapng` files, inspect USB transfer bytes, infer report IDs, infer checksums, generate protocol hypotheses, create encoders/decoders, call `IPHprOutputDevice`, call `MockPhprOutputDevice`, create `PHprCommand`, send USB writes, send HID output reports, send HID feature reports, control SimPro Manager, control SimHub, or touch the ASIO/BST-1 output path.

## Stage 2I Capture Analysis Framework

Stage 2I extends `HapticDrive.Simagic.PHPR.Research` with read-only capture analysis tooling.

The project owns:

- `SimagicCaptureAnalysisReader` for recursive file/folder analysis.
- Wireshark CSV payload import for columns such as `payload_spaced`, `usb.data_fragment`, and `usbhid.data`.
- Wireshark text-summary import for payload counts and `payload=` records.
- Compare-summary import for byte-diff observations.
- `SimagicPayloadDiffAnalyzer` for closest-pair byte comparisons between two capture/export sources.
- pcap/pcapng container summaries that count sections, interfaces, packets, link types, and captured bytes without interpreting protocol fields.
- `SimagicCaptureAnalysisExporter` for sanitized JSON output under ignored `capture-metadata/generated/`.
- CLI commands `capture-analysis` and `capture-diff`.

Analysis exports contain source file names, payload counts, length counts, source-column counts, short payload previews, truncated SHA-256 fingerprints, byte-diff observations, pcap/pcapng container summaries, and warnings. Raw payload byte arrays are used only in memory for analysis and are not serialized to JSON.

Stage 2I may observe byte differences, but it does not name protocol fields, infer report IDs, infer checksums, classify commands, create protocol hypotheses, create encoders/decoders, call `IPHprOutputDevice`, call `MockPhprOutputDevice`, create `PHprCommand`, send USB writes, send HID output reports, send HID feature reports, control SimPro Manager, control SimHub, or touch the ASIO/BST-1 output path.

## Stage 2J Protocol Hypotheses

Stage 2J extends `HapticDrive.Simagic.PHPR.Research` with analysis-only hypothesis records under `HapticDrive.Simagic.PHPR.Research.Hypotheses`.

The project owns:

- `SimagicProtocolHypothesisSet`
- `SimagicProtocolHypothesis`
- `SimagicProtocolHypothesisField`
- `SimagicProtocolUnknown`
- confidence, status, family, source, and risk enums
- `BuiltInProtocolHypotheses`
- `SimagicProtocolHypothesisExporter`
- CLI commands `hypotheses-list` and `hypotheses-export`

The built-in hypotheses document confirmed input mappings, the SimHub `F1 EC` active/stop/duration observations, the separate SimPro `80 1E 89` family, runtime identity rules, Stage 2K mock-only boundaries, and real-write blockers.

Stage 2J does not create a production encoder or decoder, does not generate packets for live hardware, does not call `IPHprOutputDevice`, does not call `MockPhprOutputDevice`, does not create `PHprCommand`, does not send USB writes, does not send HID output or feature reports, does not control SimPro Manager or SimHub, and does not touch the ASIO/BST-1 output path.

## Stage 2K Mock P-HPR Protocol And Output

Stage 2K extends `HapticDrive.Simagic.PHPR.Abstractions` with a mock-only protocol boundary under `MockProtocol`:

- `PHprMockProtocolCommand`
- `PHprMockProtocolFrame`
- mock family/state/support-status models
- `SimHubF1EcMockEncoder`
- `SimHubF1EcMockDecoder`
- `PHprMockDurationScheduler`
- `SimProUnknownMockFrame`
- `SimProUnknownMockEncoder`

The SimHub F1 EC mock encoder models the Stage 2J hypothesis as 64-byte mock payloads. Brake uses module byte `01`, throttle uses module byte `02`, start uses state byte `01`, stop uses state byte `00`, frequency is represented as direct Hz, and strength is represented as direct percent. A `Both` target expands into explicit brake and throttle frames; Stage 2K does not use module `00` as a both-module command.

Duration is modelled deterministically as start frames at offset zero plus stop frames at `DurationMs`. A zero-duration start request produces stop-only mock frames. Emergency stop produces immediate stop frames for brake and throttle.

The SimPro `80 1E 89` family remains `SimProUnknownMock` and `NeedsMoreCaptures`. Stage 2K can classify the prefix and safely refuse detailed mock encoding, but it does not infer SimPro module, strength, frequency, duration, checksum, or keepalive semantics.

`MockPhprOutputDevice` records generated mock protocol frames, simulated connection/module availability, rejected-command simulation, emergency-stop count, pending scheduled stop count, and last frame diagnostics. It still does not open device handles, send output reports, send feature reports, control SimPro Manager, control SimHub, route `ShiftIntentEvent` values, or touch the ASIO/BST-1 output path.

`HapticDrive.Simagic.PHPR.Research` now references the P-HPR abstraction project for safe Stage 2K CLI examples:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-examples
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- mock-protocol-export --output capture-metadata\generated\simagic-mock-protocol-examples.json
```

These commands print/export sanitized mock examples only. Nothing in the Stage 2K mock protocol may be sent to real hardware.

## Stage 2L P-HPR Safety Layer

Stage 2L extends `HapticDrive.Simagic.PHPR.Abstractions` with a reusable safety boundary under `Safety`:

- `PHprSafetyLimiter`
- `IPHprSafetyLimiter`
- `PHprSafetyContext`
- `PHprSafetyDecision`
- `PHprSafetySnapshot`
- `IPHprSafetyClock`

The limiter clamps strength, duration, and frequency to `PHprSafetyLimits`, rejects excessive command rate, rejects excessive estimated continuous duration, rejects unavailable modules, rejects disconnected-device starts, latches emergency stop, blocks starts while emergency stop is active, and exposes context gates for telemetry stale, haptics stopped, emergency mute active, `DrivingArmed` false, SimPro/SimHub conflict placeholder, and real-write blocking.

`SafetyLimitedPhprOutputDevice` wraps `MockPhprOutputDevice` so accepted or clamped commands can produce mock frames while rejected commands do not reach the mock output. Emergency stop forwards to the mock output, clears pending scheduled mock stop frames, records immediate brake/throttle stop frames, clears limiter timing state, and requires `ClearEmergencyStop` before later start commands are accepted.

`HapticDrive.Simagic.PHPR.Research` adds:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- safety-examples
```

This command prints mock safety decisions only. Stage 2L itself does not route `ShiftIntentEvent`, `VehicleState`, audio effects, ASIO output, or the mixer to P-HPR output. Stage 2M routes accepted shift intents through the Stage 2L safety-limited mock output only. Neither stage opens device handles, sends HID output reports, sends feature reports, writes USB data, controls SimPro Manager, controls SimHub, or creates a production protocol adapter.

## Early Development Rule

The app must build and test without ASIO hardware, M-Audio hardware, the Fosi amplifier, the Dayton BST-1, F1 25, or any live telemetry stream.

## Stage 01 App Shell

The WPF app shell provides navigation placeholders for Dashboard, Effects, Mixer / Routing, Devices, Telemetry / UDP Router, Recordings, Test Bench, Profiles, Settings, and Diagnostics.

Global haptics start/stop, emergency mute, theme selection, and close/minimize-to-tray setting placeholders exist in the shell. They are intentionally not connected to telemetry or audio behavior yet.

## Stage 02 Output Abstractions

Core owns the shared `IAudioOutputDevice` contract and related status/configuration records.

Audio owns concrete output device implementations:

- `NullAudioOutputDevice`: deterministic, hardware-free default for tests and startup.
- `WasapiDebugOutputDevice`: manual debug placeholder only.
- `AsioAudioOutputDevice`: ASIO abstraction/stub with graceful unavailable-driver handling.

ASIO driver discovery is isolated behind `IAsioDriverCatalog` so automated tests can use a fake catalog and later stages can add real driver enumeration without changing the output contract.

## Stage 03 F1 25 Spec Extraction

The official F1 25 v3 PDF has been converted into concise implementation notes in `docs/F1_25_PACKET_SPEC_IMPLEMENTATION.md`.

The parser boundary remains unchanged:

- F1-specific binary parsing belongs in `HapticDrive.Asio.Telemetry.F1_25`.
- Shared effect logic must consume a later `VehicleState` model, not raw F1 packet classes.
- Raw UDP bytes must be preserved for forwarding, recording, and replay.
- Packet parsing must validate format, year, ID, version, and exact byte length before body reads.

## Stage 04 UDP Listener

Core owns `IUdpTelemetryReceiver` and `UdpTelemetryReceiver`, a raw datagram listener that binds to port `20778` by default.

The listener:

- Preserves packet payload bytes exactly as received.
- Emits packet events with sequence number, remote endpoint, and receive timestamp.
- Tracks listener state, bound port, packet count, packet rate, last packet time, no-packet warning, and receive errors.
- Allows tests to bind an ephemeral port with `Port = 0`.

The WPF shell starts the listener on app load and surfaces high-level status on the dashboard and Telemetry / UDP Router page.

Parsing remains outside Stage 04. F1-specific binary parsing still belongs in `HapticDrive.Asio.Telemetry.F1_25`, and UDP forwarding is scheduled for Stage 05.

## Stage 05 UDP Forwarding

Core owns `IUdpTelemetryForwarder` and `UdpTelemetryForwarder`, a byte-preserving relay path that accepts `UdpTelemetryPacket` values from the raw listener.

The forwarder:

- Sends the exact received packet payload to each enabled destination.
- Keeps forwarding independent of F1 25 parser success, haptic output state, and audio hardware state.
- Tracks configured destinations, enabled destinations, input packet count, forwarded datagram count, forwarded bytes, forwarding errors, and last successful forward time.
- Skips disabled destinations and continues to later destinations if one send fails.

The WPF shell offers every received raw packet to the forwarder and surfaces forwarding status on the dashboard. Destination editing is intentionally not implemented in the shell yet.

Stage 06 should add the F1 25 packet header parser without changing the raw forwarding behavior.

## Stage 08 VehicleState Model

Core owns the shared `VehicleState` records under `HapticDrive.Asio.Core.Vehicle`.

`VehicleState` is a last-known snapshot with nullable samples for packet slices that have not arrived yet. Each populated sample carries a packet stamp with source packet name, session UID, session time, frame identifiers, and player car index. This lets later stages distinguish a real telemetry zero from data that is missing or stale.

`HapticDrive.Asio.Telemetry.F1_25` owns `F125VehicleStateAdapter`, which maps parsed Stage 07 packet bodies into shared state:

- Array-based packets select the player car through `PacketHeader.PlayerCarIndex`.
- Motion Ex remains player-car-only.
- Wheel arrays preserve official RL, RR, FL, FR order.
- Car Telemetry preserves raw surface type IDs instead of collapsing unknown future values.
- Failed or ignored parser results do not update `VehicleState`.

The WPF shell surfaces only high-level VehicleState diagnostics for now. Recording, replay, haptic effects, mixer, safety processors, real WASAPI output, and real ASIO streaming remain later stages.

## Stage 09 Recording and Replay

`HapticDrive.Asio.Recording` owns the raw telemetry capture and replay layer.

The recorder accepts `UdpTelemetryPacket` values, copies their payload bytes, stores packet sequence and relative receive timing, and writes a versioned `.hdrec` file through a background writer queue. Recording is intentionally parser-independent, so malformed or unsupported packets can still be captured exactly.

The replay service loads `.hdrec` files and emits `UdpTelemetryPacket` values in recorded order. Tests and later runtime paths can feed those packets through the same `F125PacketParser.Parse(packet.Payload)` and `F125VehicleStateAdapter.Apply(parseResult)` sequence used for live UDP packets.

The WPF shell adds only a minimal Start/Stop Recording control and status card. Replay controls, recording library management, profile snapshots, graphing, mixer work, safety processors, audio generation, and hardware output remain outside Stage 09.

## Stage 10 Audio Mixer and Safety Chain

Core owns the shared Stage 10 audio sample contracts:

- `AudioSampleFormat` records sample rate, channel count, frame count, and interleaved sample count.
- `AudioSampleBuffer` stores interleaved `float` samples and validates buffer shape.
- `IAudioOutputDevice.SubmitBufferAsync` is the narrow output handoff for final sample buffers.

Audio owns the deterministic processing implementation:

- `AudioMixer` combines explicit source buffers with per-source gain, master gain, normal mute, emergency mute, and invalid sample/gain sanitisation.
- `AudioSafetyProcessor` sanitises NaN/infinity values, applies conservative output gain, peak-limits buffers to the configured normalized ceiling, hard-clips any remaining overflow, and forces silence on emergency mute.
- `AudioRenderPipeline` keeps a reusable mix buffer, applies mixer and safety processing, and hands the final buffer to an `IAudioOutputDevice`.
- `NullAudioOutputDevice` consumes matching sample buffers after start and discards them deterministically for hardware-absent tests.

The WPF shell connects Start Haptics and Emergency Mute to the Stage 10 pipeline only by submitting safe silence to `NullAudioOutputDevice`. There is no continuous audio callback, generated haptic effect, Stage 11 test signal, real WASAPI output, or real ASIO streaming in Stage 10.

## Stage 11 Test Bench

Audio owns the Stage 11 synthetic test bench under `HapticDrive.Asio.Audio.TestBench`.

The test bench:

- Generates deterministic silence, sine tone, frequency sweep, pulse transient, and constant-value buffers.
- Keeps test signals separate from F1 25 telemetry, `VehicleState`, and future driving haptic effects.
- Wraps generated buffers as `AudioMixerInput` values and feeds the existing `AudioRenderPipeline`.
- Applies the Stage 10 mixer, normal mute, emergency mute, safety processor, limiter, and clipping protection before output handoff.
- Defaults to `NullAudioOutputDevice` so automated tests do not require ASIO, WASAPI, live telemetry, F1 25, or shaker hardware.
- Exposes diagnostics for selected signal, active state, sample format, output peak, limiter/clipping counts, rendered buffers, and output mode.

The WPF Test Bench page adds minimal controls for selecting a synthetic signal and rendering deterministic validation buffers. It does not implement a real-time audio callback, hardware calibration, frequency response graphs, profile editing, real WASAPI output, real ASIO streaming, or driving haptic effects.

## Stage 12 Gear Shift and Engine Effects

Audio owns the Stage 12 effect layer under `HapticDrive.Asio.Audio.Effects`.

The effect layer:

- Defines small renderable effect sources that consume shared `VehicleState` snapshots.
- Keeps F1 25 packet bodies out of the audio/effect layer.
- Synthesizes engine vibration from RPM, throttle, idle RPM, max RPM, and available pause/driver/pit/result status gates.
- Synthesizes gear shift pulses from valid forward gear changes.
- Renders deterministic `AudioSampleBuffer` sources that are wrapped as `AudioMixerInput` values.
- Feeds the existing Stage 10 mixer, safety processor, emergency mute, limiter, clipping protection, and output handoff.
- Defaults to conservative software gains and `NullAudioOutputDevice` validation.

The WPF Effects page adds minimal diagnostics for engine active state, RPM-derived frequency, gear pulse state, last observed gear, last shift frame, and default settings. It does not implement a full tuning UI, profile editor, live graphs, per-channel routing, physical calibration, real WASAPI output, or real ASIO streaming.

## Stage 13 Kerb, Impact, Road Texture, and Slip Effects

Audio extends the Stage 12 effect layer with four additional `VehicleState`-driven effect sources:

- `KerbEffect` synthesizes rumble from documented rumble strip / ridged surface IDs, speed, active wheel count, and optional Motion Ex contact / suspension data.
- `ImpactEffect` synthesizes short bounded pulses from player collision events and abrupt vertical-G, wheel-vertical-force, or suspension-acceleration changes.
- `RoadTextureEffect` synthesizes low-level deterministic texture from documented surface IDs, speed, and optional suspension / vertical-G motion.
- `SlipEffect` synthesizes slip, traction-loss, and minimal brake-lock vibration from wheel slip ratio, wheel slip angle, wheel speed, throttle, brake, speed, TC, and ABS state.

The effect layer still consumes shared `VehicleState` only and does not read F1 25 packet bodies directly. The new sources render deterministic `AudioSampleBuffer` values and feed the same Stage 10 mixer, safety processor, emergency mute, limiter, clipping protection, and output handoff used by Stage 12.

The WPF Effects page adds read-only diagnostics for kerb, impact, road texture, and slip state. It does not implement Stage 14 tuning controls, profiles, persistence, live graphs, per-channel routing, calibration, real WASAPI output, real ASIO streaming, Simagic P-HPR output, or physical hardware tuning.

## Stage 17 Native ASIO Streaming

Core extends `IAudioOutputDevice` with a synchronous output render callback and diagnostics fields for render callbacks, backend callbacks, dropped buffers, underruns, render duration, callback jitter, and telemetry age.

Audio owns the output cadence and backend implementation:

- `AudioOutputDeviceBase` can run an output-owned render loop for hardware-absent and fake-backend tests.
- `NullAudioOutputDevice` consumes callback-rendered buffers deterministically without physical hardware.
- `AsioAudioOutputDevice` preserves explicit driver selection, channel selection, and arming, then routes mono safety-processed buffers to the selected ASIO output channel.
- `NativeAsioOutputBackend` uses `NAudio.Asio`/`AsioOut` and a small preallocated queue to bridge app rendering to the driver callback.

Runtime owns stale telemetry policy:

- `HapticPipelineCoordinator` no longer depends on WPF `DispatcherTimer` for live rendering.
- The render callback reads current in-memory effect state, runs the mixer and safety chain, and fills the provided buffer.
- UI, disk IO, logging, networking, blocking waits, and async continuations stay outside the render callback.
- If no fresh parsed `VehicleState` arrives within the wall-clock timeout, the callback renders safety silence until telemetry updates again.

Automated tests still use Null output and fake ASIO backends. Stage 17 does not validate physical shaker feel, safe physical gain, physical latency, or final frequency tuning.

## Stage 06 F1 25 Packet Header Parser

`HapticDrive.Asio.Telemetry.F1_25` owns the first parser implementation:

- `F125PacketHeader` models the 29-byte official header.
- `F125PacketDefinitions` records packet IDs, packet names, exact packet sizes, packet version, and V1-required packet flags from the v3 PDF notes.
- `F125PacketHeaderParser` reads little-endian fields and validates packet format `2025`, game year `25`, known packet ID, packet version `1`, and exact datagram length.
- Unknown packet IDs return an ignored result instead of throwing.
- Malformed datagrams return failure results instead of throwing.
- Successful results preserve a copy of the raw datagram bytes.

The WPF shell parses headers from incoming UDP packets for diagnostics while forwarding still uses the original raw UDP payload. Packet body parsing and `VehicleState` mapping remain scheduled for later stages.

## Phase 3J Controlled Hardware Readiness

`HapticDrive.Simagic.PHPR.Research.ControlledWrite` owns the final controlled P-HPR smoke-test command.

The command:

- defaults to dry-run and does not open a HID writer unless `--execute` is supplied,
- requires the exact approval phrase `I approve Phase 2 controlled P-HPR write testing`,
- requires a selected private HID path and clear SimPro/SimHub coexistence,
- plans low-strength 0-100% user-facing pulse settings through the existing `SimagicPhprOutputDevice`,
- requests emergency stop at the end of execution,
- hides private HID paths in console output.

Automated coverage uses a fake HID writer and does not open real devices. Prior skipped ASIO manual tests were converted into readiness/pending tests so full-suite output can be zero-skip while keeping physical ASIO/BST-1 and P-HPR actuation opt-in. This does not change the ASIO/BST-1 audio path and does not validate physical P-HPR behavior.

## Stage 19A Runtime Ownership Guardrails

Stage 19A verified the external architecture review against the live repository before moving any runtime code.

Verified current ownership:

- `MainWindow.xaml.cs` still starts and owns the real P-HPR slip/lock background task through `StartRealSlipLockRuntime` and `RunRealSlipLockRuntimeAsync`.
- `MainWindow.xaml.cs` still starts and owns the real P-HPR road background task through `StartRealRoadVibrationRuntime` and `RunRealRoadVibrationRuntimeAsync`.
- `MainWindow.xaml.cs` still routes GT Neo paddle input through `PaddleInputSource_PaddleInputReceived`.
- `PHprDirectRuntime.cs` and `PhprDeviceCardPulseService.cs` are still compiled into `HapticDrive.Asio.App` even though they are non-UI runtime helpers.

Current production project direction:

- `HapticDrive.Asio.App` references `HapticDrive.Asio.Runtime`, `HapticDrive.Actuation`, `HapticDrive.Input.Abstractions`, `HapticDrive.Input.Windows`, `HapticDrive.Simagic.PHPR.Abstractions`, and `HapticDrive.Simagic.PHPR.Output.Windows`.
- `HapticDrive.Actuation` references `HapticDrive.Asio.Runtime`, `HapticDrive.Input.Abstractions`, and `HapticDrive.Simagic.PHPR.Abstractions`.
- `HapticDrive.Asio.Runtime` references `HapticDrive.Asio.Audio`, `HapticDrive.Asio.Core`, `HapticDrive.Asio.Recording`, and `HapticDrive.Asio.Telemetry.F1_25`.

Stage 19A implication:

- A direct move of `PHprDirectRuntime.cs` into `HapticDrive.Asio.Runtime` is not safe yet.
- `PHprDirectRuntime.cs` currently depends on `HapticDrive.Actuation.PHpr` bench and target types including `PaddleGearBenchTestResult`, `PaddleGearBenchTestOptions`, and `PHprGearPulseTarget`.
- Adding that dependency to `HapticDrive.Asio.Runtime` would create `HapticDrive.Actuation -> HapticDrive.Asio.Runtime -> HapticDrive.Actuation`.

Stage 19A response:

- keep validated runtime code in place,
- add project-graph guardrail tests,
- add explicit shared direct-pulse-path tests for manual brake/throttle pulses,
- defer runtime extraction until the dependency direction is inverted or a narrow non-UI orchestration home exists.

Recommended Stage 19B order:

1. Extract or invert the actuation-owned bench and target contract surface that `PHprDirectRuntime` consumes today.
2. Move continuous real road and slip/lock loop ownership out of `MainWindow`.
3. Move paddle input routing ownership out of `MainWindow`.
4. Introduce a shared slip/lock evaluator so BST-1 and P-HPR stop drifting apart.

## Stage 19B Runtime Dependency Inversion And Safe Direct Runtime Extraction

Stage 19B removes the Stage 19A dependency-cycle blocker and takes the first safe non-UI runtime extraction step without changing P-HPR direct behaviour.

Moved contract surface:

- `PHprGearPulseTarget`
- `PHprGearPulseProfile`
- `PaddleGearBenchTestOutputMode`
- `PaddleGearBenchTestOptions`
- `PaddleGearBenchTestResult`

These types now live under `HapticDrive.Simagic.PHPR.Abstractions.Routing`. They remain pure P-HPR routing and bench contracts rather than router/runtime behavior. `HapticDrive.Simagic.PHPR.Abstractions` now references `HapticDrive.Input.Abstractions` so `PaddleGearBenchTestResult` can continue carrying mapped paddle and accepted shift-intent facts without pulling those contracts back into App or Actuation.

Safe runtime relocation result:

- `PHprDirectRuntime.cs` moved out of `HapticDrive.Asio.App` into `HapticDrive.Simagic.PHPR.Output.Windows`.
- `PhprDeviceCardPulseService.cs` moved out of `HapticDrive.Asio.App` into `HapticDrive.Simagic.PHPR.Output.Windows`.
- `PaddleGearBenchDirectGate.cs` moved with them because it was a hidden non-UI direct-runtime dependency.

Stage 19B intentionally does not move that runtime into `HapticDrive.Asio.Runtime`. `PHprDirectRuntime` depends on the concrete `SimagicPhprOutputDevice`, so moving it into the generic runtime layer would have forced `HapticDrive.Asio.Runtime` to depend on Windows HID direct-output code. `HapticDrive.Simagic.PHPR.Output.Windows` is the narrower non-UI home for that orchestration.

Current production project direction after Stage 19B:

- `HapticDrive.Asio.App` references `HapticDrive.Asio.Runtime`, `HapticDrive.Actuation`, `HapticDrive.Input.Abstractions`, `HapticDrive.Input.Windows`, `HapticDrive.Simagic.PHPR.Abstractions`, and `HapticDrive.Simagic.PHPR.Output.Windows`.
- `HapticDrive.Actuation` references `HapticDrive.Asio.Runtime`, `HapticDrive.Input.Abstractions`, and `HapticDrive.Simagic.PHPR.Abstractions`.
- `HapticDrive.Asio.Runtime` still references `HapticDrive.Asio.Audio`, `HapticDrive.Asio.Core`, `HapticDrive.Asio.Recording`, and `HapticDrive.Asio.Telemetry.F1_25` only.
- `HapticDrive.Simagic.PHPR.Output.Windows` references `HapticDrive.Input.Abstractions` and `HapticDrive.Simagic.PHPR.Abstractions`, and does not reference `HapticDrive.Asio.App`.

Remaining extraction order:

1. Stage 19C should move continuous real road/slip/lock loop ownership out of `MainWindow`.
2. Stage 19D should move paddle input routing ownership out of `MainWindow`.
3. Stage 20 should introduce one shared slip/lock evaluator for BST-1 and P-HPR.

## Stage 19C Continuous Real P-HPR Runtime Extraction

Stage 19C moves continuous real P-HPR road/slip/lock loop ownership out of `MainWindow.xaml.cs` while preserving the same cadence, stop behavior, safety gating, and diagnostics meaning.

New coordinator:

- `PHprContinuousEffectsRuntimeCoordinator`
- project: `HapticDrive.Actuation`
- namespace: `HapticDrive.Actuation.PHpr`

The coordinator owns:

- the real slip/lock background loop,
- the real road background loop,
- loop cancellation and shutdown waits,
- in-flight suppression state,
- road-yield-after-slip/lock suppression counting,
- last real road/slip routing result snapshots.

The coordinator intentionally stays in `HapticDrive.Actuation.PHpr` because it orchestrates the existing P-HPR road and slip/lock routers around `HapticPipelineSnapshot` / `VehicleState`-derived state. That is actuator runtime logic, not WPF UI and not generic ASIO runtime.

`MainWindow.xaml.cs` now stays a thin consumer for this slice:

- it constructs the coordinator,
- provides the latest `HapticPipelineSnapshot` plus real-road/real-slip safety contexts and readiness gates,
- starts the coordinator on app load,
- stops/disposes the coordinator on app shutdown,
- reads coordinator diagnostics for status/report text.

What intentionally stayed in `MainWindow`:

- GT Neo paddle input routing through `PaddleInputSource_PaddleInputReceived`,
- direct P-HPR gear-pulse runtime ownership already moved in Stage 19B,
- router option editing, UI control reads/writes, and status text formatting,
- the final app-shutdown explicit road stop/output dispose sequence.

Stage 19C does not move continuous runtime ownership into `HapticDrive.Asio.Runtime`, because that would again blur the line between the generic pipeline runtime and actuator-specific orchestration. It also does not move paddle input routing; that remains Stage 19D.

Remaining extraction order:

1. Stage 19D should move paddle input routing ownership out of `MainWindow`.
2. Stage 20 should introduce one shared slip/lock evaluator for BST-1 and P-HPR.

## Stage 19D Paddle Input Routing Extraction

Stage 19D moves the remaining paddle input routing orchestration out of `MainWindow.xaml.cs` while preserving the same mapped-paddle, bench, direct-runtime, and BST-1 local gear behavior.

New coordinator:

- `PaddleInputRoutingCoordinator`
- project: `HapticDrive.Asio.App`
- namespace: `HapticDrive.Asio.App`

The coordinator owns:

- runtime handling of `WheelPaddleInputEvent` from the existing paddle input source,
- `ShiftIntentProcessor` evaluation for accepted live shift routing,
- paddle gear bench evaluation through `PaddleGearBenchTestController`,
- accepted live-shift notification into `_hapticPipeline`, `_realRoadVibrationRouter`, and `_realSlipLockRouter`,
- accepted live-shift routing into mock P-HPR gear and real direct P-HPR gear paths,
- accepted bench routing into mock/direct P-HPR bench paths and optional BST-1 local manual ASIO test injection,
- safe exception recovery through `IPHprDirectRuntime.HandlePaddleInputExceptionAsync`.

`MainWindow.xaml.cs` now stays a thin consumer for this slice:

- it constructs the coordinator,
- supplies current mapping, BST-1 bench settings, direct-runtime configuration, and safety-context delegates,
- forwards `PaddleInputReceived` events into the coordinator,
- performs the UI-thread status refresh and footer text update after each handled event,
- keeps UI-only text/status formatting and settings-control parsing.

What intentionally stayed in `MainWindow`:

- WPF status/control updates and footer text formatting,
- settings/control parsing such as `ApplyPhprPedalsNormalOptionsFromControlsAsync`,
- safety-context builders tied to current App/output state,
- UI diagnostics fields such as `_lastRealPhprGearPulseRoutingResult` and `_lastBst1PaddleGearPulseMessage`.

Stage 19D keeps the coordinator inside `HapticDrive.Asio.App` on purpose. The live route crosses `ShiftIntentProcessor`, mock P-HPR gear routing, real direct P-HPR gear routing, `PaddleGearBenchTestController`, `IPHprDirectRuntime`, and `HapticPipelineCoordinator.StartManualAsioHardwareTestAsync`. Forcing that coordinator into `HapticDrive.Actuation` or `HapticDrive.Asio.Runtime` today would either widen the internal `IPHprDirectRuntime` surface or pull concrete Windows direct-output / App-specific ASIO test dependencies into a broader runtime layer. Stage 19D therefore uses a temporary non-WPF App service boundary instead of a bad project-direction move.

Stage 19D does not change UI/XAML, paddle mappings, debounce defaults, left/right paddle semantics, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, direct gear-pulse tuning, latest-press-wins retrigger semantics, BST-1 local pulse timing/strength/frequency/duration, parser layouts, or continuous road/slip/lock runtime behavior.

Remaining extraction order:

1. Stage 20 should introduce one shared slip/lock evaluator for BST-1 and P-HPR.

## Stage 20 Shared Slip/Lock Evaluator

Stage 20 introduces `SlipLockEvaluator` in `HapticDrive.Asio.Core.Haptics` together with `SlipLockEvaluationOptions`, `SlipLockEvaluationInput`, `SlipLockEvaluationResult`, `SlipLockSignalResult`, `SlipLockSuppressionReason`, and `SlipLockWheelContribution`.

`HapticDrive.Asio.Core.Haptics` is the shared home because:

- `HapticDrive.Asio.Audio` already references Core,
- `HapticDrive.Actuation` already references Core,
- Core still has no App, ASIO backend, HID output, or WPF dependency,
- placing the evaluator in Core keeps the production graph acyclic while letting both BST-1 and P-HPR consume one deterministic model.

The shared evaluator now owns:

- driving-state mute and frame-freshness checks for slip/lock detection,
- sanitized wheel slip ratio, slip angle, and wheel-speed extraction with preserved `RearLeft`, `RearRight`, `FrontLeft`, `FrontRight` order,
- shared speed scaling plus slip/lock threshold math,
- shared low-pedal, traction-control, and ABS attenuation,
- normalized slip and lock active state, intensity, suppression reason, and wheel-contribution diagnostics.

`SlipEffect` now consumes the shared evaluator for slip/lock detection and normalized intensity only. It still owns:

- BST-1 audio amplitude/frequency/noise shaping,
- dominant source choice between wheel slip and wheel lock,
- response smoothing and deterministic sample generation,
- mixer/safety handoff and BST-1-facing diagnostics wording.

`PHprSlipLockRouter` now consumes the shared evaluator for direct P-HPR slip/lock detection and normalized intensity only. It still owns:

- brake/throttle target-module mapping,
- wheel-lock-above-wheel-slip priority,
- minimum route interval, hold-timeout watchdog, and explicit stop commands,
- gear-protection timing and road-yield interaction,
- direct safety-context gating, command creation, HID-output handoff, and direct diagnostics wording.

The older mock `PHprPedalEffectsRouter` also now consumes the same shared evaluator for wheel slip / wheel lock detection so replay-safe mock routing stays aligned with BST-1 and the real direct route.

Stage 20 does not change UI/XAML, ASIO/BST-1 backend behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, direct gear-pulse tuning, road cadence, slip/lock runtime cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, or persistence schema.

## Stage 21A MainWindow Residual Orchestration Audit

Stage 21A re-checks the post-Stage-20 shell and confirms that `MainWindow.xaml.cs` is still the remaining concentration point for:

- startup/load wiring and initial device refresh,
- shutdown/dispose ordering and shutdown diagnostics,
- app-settings and profile hydration,
- control-to-options parsing for ASIO, BST-1, and P-HPR,
- safety-context construction from current runtime state,
- diagnostics/status/report string assembly,
- recording/replay UI workflow,
- general WPF status/footer/dashboard updates.

Stage 21A intentionally does not start a broad MVVM rewrite. The safe first extraction is the P-HPR workflow/status presentation path because it is pure report assembly and does not own:

- hardware device opening,
- ASIO render callbacks,
- P-HPR HID writes,
- background loops,
- cancellation-token ownership,
- startup/shutdown sequencing.

New App-only non-WPF workflow-status presentation boundary:

- `PhprWorkflowStatusSnapshotBuilder`
- `PhprWorkflowStatusPresenter`
- supporting records in `PhprWorkflowStatusPresenter.cs`

The extracted boundary now owns:

- P-HPR workflow status summary text assembly,
- replay/profile/direct/mock/road/slip workflow item text assembly,
- safe fallback text when the snapshot is missing or incomplete,
- handoff of the existing `PhprLiveF1ValidationGuide` output back to `MainWindow`.

`MainWindow.xaml.cs` intentionally still owns:

- live snapshot collection from `_hapticPipeline`, `_realPhprOutput`, `_mockGearPulseRouter`, `_mockPedalEffectsRouter`, paddle input, and validation state,
- WPF control assignment to `TextBlock` / `ItemsControl` targets,
- settings parsing and runtime configuration,
- safety-context builders,
- startup/shutdown lifecycle orchestration,
- the much larger diagnostics-panel assembly path.

Recommended Stage 21B:

1. Extract a broader `AppDiagnosticsReportService` / diagnostics snapshot builder from `UpdateDiagnosticsStatus`.
2. After that, consider `AppSettingsHydrationService` or dedicated P-HPR/ASIO settings snapshot builders.

Stage 21A does not change UI/XAML, ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, recording format, or replay timing.

## Stage 21B Diagnostics Status Extraction

Stage 21B takes the larger diagnostics/status assembly path out of `MainWindow.xaml.cs` without attempting MVVM or changing runtime ownership.

New App-only non-WPF diagnostics presentation boundary:

- `DiagnosticsStatusSnapshotBuilder`
- `DiagnosticsStatusPresenter`
- supporting records in `DiagnosticsStatusPresenter.cs`

The extracted diagnostics boundary now owns:

- diagnostics summary text assembly,
- road-recorder status text assembly,
- ordered diagnostics item/report assembly for the Advanced / Diagnostics page,
- clipboard report text generation,
- safe fallback text for missing/incomplete diagnostics snapshots.

`PhprWorkflowStatusPresenter` now also emits the already-sanitized diagnostics lines for:

- profile persistence,
- workflow mode/report state,
- live F1 validation diagnostics.

That keeps the diagnostics page reusing the Stage 21A workflow presentation boundary instead of rebuilding those strings directly in `MainWindow.xaml.cs`.

`MainWindow.xaml.cs` intentionally still owns:

- live snapshot collection from pipeline, receiver, output, workflow, input, and settings state,
- helper formatting for individual subsection diagnostics that still depend on current shell/runtime fields,
- WPF control assignment to `TextBlock` / `ItemsControl` targets,
- visibility gating around diagnostics refresh,
- startup/shutdown lifecycle orchestration, settings hydration, and safety-context builders.

Recommended Stage 21C:

1. Extract app/settings snapshot and hydration builders so `MainWindow.xaml.cs` stops owning the long persisted-settings/status line plus related restore/save shaping.
2. Re-audit startup/shutdown and safety-context construction only after the settings/diagnostics seams are stable.

Stage 21B does not change UI/XAML, ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, recording format, replay timing, or privacy/redaction boundaries.

## Stage 21C Settings Snapshot Hydration Extraction

Stage 21C takes the safest remaining app-settings seam out of `MainWindow.xaml.cs` without attempting MVVM or changing startup/runtime ownership.

New App-only non-WPF settings snapshot/presentation boundary:

- `AppSettingsSnapshotBuilder`
- `PersistedSettingsStatusPresenter`
- supporting records in `AppSettingsSnapshotBuilder.cs`

The extracted boundary now owns:

- safe `AppSettings` sanitization and hydration shaping for output-mode, ASIO, replay, forwarding, paddle, BST-1 local gear, shift-intent, mock P-HPR, and real P-HPR effect preferences,
- safe save-shaping back into `AppSettings` from current shell/runtime preference snapshots,
- the persisted-settings status/footer text and diagnostics text assembly,
- normalization/clamping handoff for restored P-HPR effect settings while keeping direct-control disabled and unselected on hydration.

`MainWindow.xaml.cs` intentionally still owns:

- WPF control assignment and event wiring,
- current shell/runtime snapshot gathering before save or status presentation,
- `LoadPersistedAudioProfile()` and audio-profile lifecycle,
- `PhprEffectProfileStore` manual load/save/reset workflow,
- replay timing reads from WPF controls,
- startup/shutdown sequencing,
- safety-context builders,
- ASIO start/stop ownership,
- P-HPR runtime coordination and direct-output hardware ownership.

Persisted settings intentionally still allowed:

- theme,
- Advanced / Diagnostics visibility,
- preferred output mode,
- selected ASIO driver/channel preference,
- Arm ASIO readiness preference only,
- replay timing preference,
- forwarding destinations,
- paddle device/mapping/debounce preference,
- BST-1 local gear test preference,
- shift-intent preference,
- mock P-HPR routing preferences,
- real P-HPR safe gear/road/slip/lock effect preferences.

Runtime-only / unsafe state intentionally still not persisted:

- haptics running state,
- emergency mute or emergency-stop latch state,
- Stop All state,
- ASIO stream running or output-active state,
- P-HPR direct-control enable/arm state,
- selected private HID path,
- direct/mock command history,
- validation result data and local validation paths,
- capture paths or raw HID inventory paths,
- active pulses, pending stops, or any other startup-energising state.

Why this stays in App:

- the extracted code still bridges `AppSettings`, profile models, shell preferences, and user-facing persisted-settings text,
- it has no business in Core, Audio, Runtime, Actuation, or the Windows P-HPR output assembly,
- keeping it App-local preserves the current project graph while reducing `MainWindow` ownership.

Recommended Stage 21D:

1. Extract the remaining pure settings/control parsing and hydration-application helpers that still live in `MainWindow.xaml.cs`.
2. Re-audit startup/shutdown sequencing and safety-context construction only after those smaller settings seams stabilize.

Stage 21C does not change UI/XAML, app-settings schema, audio-profile schema, P-HPR profile schema, `.hdrec` format, replay timing behavior, startup/shutdown ordering, ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, or privacy/redaction boundaries.
