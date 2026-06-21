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
-> game-specific normalizer
-> canonical HapticFrame
-> haptic effect engine
-> mixer and safety chain
-> audio output device
```

Current production-hardening shape for live telemetry:

```text
UDP receiver (loopback default, LAN opt-in)
-> bounded telemetry ingress worker
   -> bounded haptic-processing channel
   -> bounded forwarding channel
   -> bounded recording channel
-> shared VehicleState / freshness checks
-> game integration registry
-> VehicleState normalizer
-> canonical HapticFrame
-> haptic effect engine
-> mixer and safety chain
-> audio output device
```

The ingress worker exists so packet receive never creates one task per datagram and so forwarding/recording backpressure can stay visible without blocking the live haptic path. Forwarding continues to send `UdpTelemetryPacket.Payload` byte-for-byte, and recording continues to preserve the original UDP payload independently of parser success.

Stage 26E adds the first explicit future-game seam:

- `IGameIntegrationRegistry` now describes installed game integrations and owns adapter creation plus endpoint defaults.
- F1 25 is registered as the only shipped integration today (`f1-25`, UDP v3, loopback/20778 default).
- `IVehicleStateNormalizer` now turns parser/adaptor `VehicleState` into a canonical `HapticFrame` with:
  - game identity,
  - canonical driving context,
  - canonical surface kinds,
  - canonical telemetry signals,
  - centralized per-signal freshness.

That means effects and future actuator logic no longer need to read raw F1 packet enums or surface IDs directly on the live path. `VehicleState` remains the parser/adaptor boundary, while `HapticFrame` is now the intended cross-game effect/actuation boundary.

Runtime lifecycle is now serialized separately from live packet flow. `RuntimeLifecycleCoordinator` owns one gate and a generation counter for shell-triggered operations such as output rebuilds, haptics start/stop, recording start/stop, and shutdown. That keeps output-device selection, pipeline rebuild, and shutdown cleanup from overlapping each other while still letting the timer-driven UI and the bounded ingress worker stay responsive.

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

## Stage 21D Control Settings Parsing Extraction

Stage 21D takes the remaining low-risk control parsing and control-value hydration helpers out of `MainWindow.xaml.cs` without attempting MVVM or changing runtime/lifecycle ownership.

New App-only non-WPF control-settings boundary:

- `ControlSettingsSnapshotBuilder`
- supporting records in `ControlSettingsSnapshotBuilder.cs`

The extracted boundary now owns:

- primitive control parsing for replay timing, paddle mapping, forwarding destination editor values, shift-intent selection, mock gear routing, mock pedal-effect routing, normal P-HPR gear pulse controls, real direct-control selection/report fields, real road/slip-lock effect controls, BST-1 manual pulse controls, and BST-1 local gear controls,
- clamp/default/normalization shaping for those parsed inputs into typed App/runtime option snapshots,
- plain control-value formatting plans for paddle mapping, paddle bench controls, mock routing controls, real P-HPR controls, normal P-HPR controls, and BST-1 controls.

`MainWindow.xaml.cs` intentionally still owns:

- direct WPF reads and writes,
- ComboBox item population and selection against live candidate/item lists,
- event wiring,
- router/device/runtime `Configure(...)` calls,
- local gear test readiness evaluation,
- profile lifecycle,
- startup/shutdown sequencing,
- safety-context builders,
- ASIO start/stop ownership,
- P-HPR runtime coordination and direct-output hardware ownership.

What remains deferred after Stage 21D:

- broad audio-profile control parsing/application in `BuildProfileFromControls()` / `ApplyProfileToControls()`,
- forwarding-list selection/editor wiring,
- local gear test readiness/status orchestration,
- startup/readiness hydration orchestration,
- any lifecycle, Stop All / Emergency Stop, or safety-context extraction.

Why this stays in App:

- these builders still bridge WPF shell primitives and App-owned typed settings/options,
- they do not belong in Core, Audio, Runtime, Actuation, Input, or the Windows P-HPR output assembly,
- keeping them App-local improves testability without hiding the hardware/runtime call sites.

Recommended Stage 21E:

1. Extract the remaining pure audio-profile control parsing/application helpers if we still want more `MainWindow` reduction before lifecycle work.
2. Only after those seams settle, re-audit startup/shutdown and safety-context construction for a higher-risk follow-up stage.

Stage 21D does not change UI/XAML, app-settings schema, audio-profile schema, P-HPR profile schema, `.hdrec` format, replay timing behavior, startup/shutdown ordering, ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, or privacy/redaction boundaries.

## Stage 21E Audio Profile Control Extraction

Stage 21E takes the remaining low-risk audio-profile control parsing and profile-to-control application planning out of `MainWindow.xaml.cs` without attempting MVVM or changing runtime/lifecycle ownership.

New App-only non-WPF audio-profile boundary:

- `AudioProfileControlSnapshotBuilder`
- supporting records in `AudioProfileControlSnapshotBuilder.cs`

The extracted boundary now owns:

- primitive audio-profile control parsing for profile name, engine, gear shift, kerb, impact, road texture, slip/wheel lock, mixer, and safety output gain values,
- deterministic shaping from those primitives into a validated `HapticDriveProfile` while preserving the existing hidden profile fields that are not directly editable on the current WPF surface,
- plain control-value hydration plans for loaded/default audio profiles,
- plain profile-text presentation values that used to be formatted inline in `UpdateProfileControlText(...)`.

`MainWindow.xaml.cs` intentionally still owns:

- direct WPF reads and writes,
- event wiring,
- audio and P-HPR profile file lifecycle,
- `ApplyProfileToRuntime(...)` and all runtime `Configure(...)` calls,
- profile status/footer assignment,
- local gear test readiness evaluation,
- startup/shutdown sequencing,
- safety-context builders,
- ASIO start/stop ownership,
- P-HPR runtime coordination and direct-output hardware ownership.

What remains deferred after Stage 21E:

- profile lifecycle event flow and load/save/reset orchestration,
- local gear readiness/status orchestration,
- startup/readiness hydration orchestration,
- any lifecycle, Stop All / Emergency Stop, or safety-context extraction.

Why this stays in App:

- these builders still bridge WPF shell primitives and App-owned audio profile presentation/application flow,
- they do not belong in Core, Audio, Runtime, Actuation, Input, or the Windows P-HPR output assembly,
- keeping them App-local improves testability without hiding the live runtime ownership edges.

Recommended Stage 21F:

1. Re-audit the remaining `MainWindow` orchestration seams after Stage 21C, 21D, and 21E rather than assuming another extraction is automatically safe.
2. If a small deterministic follow-up still exists, prefer residual profile-status/readiness shaping before any startup/shutdown or safety-context move.

Stage 21E does not change UI/XAML, app-settings schema, audio-profile schema, P-HPR profile schema, `.hdrec` format, replay timing behavior, startup/shutdown ordering, ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, or privacy/redaction boundaries.

## Stage 21F Residual MainWindow Orchestration Audit

Stage 21F re-audits the residual `MainWindow.xaml.cs` ownership after Stages 21A through 21E and only extracts one tiny remaining safe presentation seam instead of forcing another broad deconstruction pass.

Audit result:

- the remaining large responsibilities are now mostly lifecycle-heavy, runtime-heavy, or safety-critical,
- profile load/save/reset lifecycle, startup/load ordering, shutdown/dispose ordering, Stop All / Emergency Stop handlers, safety-context builders, ASIO start/stop ownership, P-HPR runtime coordination, direct-output hardware ownership, and runtime `Configure(...)` calls still belong in `MainWindow`,
- the remaining low-risk seam was the local gear readiness text/button/tooltip shaping that sat on top of the already-extracted `LocalGearTestReadiness` evaluation.

New App-only non-WPF residual presenter boundary:

- `LocalGearReadinessPresenter`

The extracted boundary now owns:

- local gear readiness status text shaping,
- local gear start-listener enabled-state shaping,
- local gear start-listener tooltip shaping,
- safe fallback presentation when the readiness model is missing.

`MainWindow.xaml.cs` intentionally still owns:

- `EvaluateLocalGearTestReadiness()` and its runtime snapshot gathering,
- direct WPF reads and writes,
- profile file lifecycle,
- startup/load ordering,
- shutdown/dispose ordering,
- Stop All / Emergency Stop handlers,
- safety-context builders,
- ASIO start/stop ownership,
- P-HPR runtime coordination and direct-output hardware ownership,
- runtime `Configure(...)` calls,
- dashboard/status refresh triggering and live snapshot gathering.

What remains deferred after Stage 21F:

- profile lifecycle orchestration,
- startup/readiness hydration orchestration,
- shutdown cleanup ordering,
- safety-context construction,
- ASIO/BST-1 output lifecycle ownership,
- P-HPR runtime/device lifecycle ownership,
- larger telemetry/replay/dashboard orchestration.

Why broad extraction stops here:

- the residual size in `MainWindow` is now dominated by coordination rather than deterministic mapping,
- forcing more extraction at this point would mean moving lifecycle or hardware-adjacent ownership, which is higher risk than the earlier App-layer builder/presenter stages,
- the project now benefits more from a deliberate higher-risk architecture stage than from more tiny presentation-only moves.

Recommended Stage 21G:

1. Treat the next stage as explicitly higher risk and choose one orchestration domain, not a general cleanup pass.
2. The strongest candidate is a lifecycle-safe extraction plan around startup/shutdown/readiness or a runtime-ownership stage around direct-output orchestration, with dedicated fake-backed tests and manual validation planning.

Stage 21F does not change UI/XAML, app-settings schema, audio-profile schema, P-HPR profile schema, `.hdrec` format, replay timing behavior, startup/shutdown ordering, ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, or privacy/redaction boundaries.

## Stage 21G Startup/Readiness Orchestration Audit

Stage 21G re-audits only startup/load/readiness orchestration and extracts one narrow no-output planning seam instead of moving lifecycle or hardware ownership.

Audit result:

- constructor and `MainWindow_Loaded` still own mixed startup responsibilities across settings hydration, audio-profile hydration, WPF control assignment, ASIO visibility/readiness refresh, input discovery, P-HPR candidate discovery, startup cleanup, telemetry start, timer start, and continuous-runtime start,
- the safe no-output startup/readiness subset is deterministic planning around ASIO startup selection/default fallback and preferred P-HPR candidate auto-selection for readiness checks only,
- output-capable or hardware-adjacent calls such as pipeline rebuild/readiness hydration, P-HPR HID open-check execution, startup cleanup, telemetry start, timer start, and continuous runtime start remain explicit in `MainWindow.xaml.cs`.

New App-only non-WPF startup/readiness boundary:

- `StartupReadinessPlanner`
- supporting record `StartupAsioReadinessPlan`

The extracted boundary now owns:

- safe startup ASIO selection/default planning from persisted selection plus visible-driver inputs,
- safe fallback when the persisted ASIO driver is unavailable,
- startup preferred P-HPR candidate auto-selection planning for no-output readiness checks only.

`StartupReadinessPlanner` intentionally does not own:

- WPF control reads or writes,
- ASIO output start/stop,
- audio buffer submission,
- HID writer open/write/close,
- P-HPR start/stop report sending,
- startup cleanup,
- telemetry receiver start,
- timer ownership,
- direct-control enable/arm persistence,
- selected private HID path persistence,
- Stop All / Emergency Stop behavior.

`MainWindow.xaml.cs` intentionally still owns:

- `AppSettingsSnapshotBuilder` hydration application and `LoadPersistedAudioProfile()` execution,
- direct WPF control assignment and event wiring,
- `RebuildHapticPipelineForOutputSelectionAsync(...)` and the existing ASIO readiness hydration path,
- input discovery and P-HPR candidate discovery,
- execution of the existing no-output HID open-check/dry-run readiness path,
- `InitializeStartupCleanupAsync()` and all startup/shutdown cleanup ordering,
- telemetry receiver start, timer start, and continuous P-HPR runtime start,
- safety-context builders, Stop All / Emergency Stop handlers, ASIO start/stop ownership, and direct-output runtime ownership.

Protected startup behavior after Stage 21G:

- startup still sends no BST-1 output and no P-HPR output,
- startup still does not auto-start haptics or ASIO,
- startup still does not auto-enable or auto-arm P-HPR direct control,
- startup still does not persist or restore a private HID path as an unsafe active runtime selection,
- startup still does not restore emergency mute or Stop All / emergency latch state as active,
- startup cleanup remains explicit and unchanged rather than being hidden inside the new planner.

Why the extraction stops here:

- broader constructor/load decomposition would immediately cross into lifecycle-heavy and hardware-adjacent ownership,
- `StartupReadinessPlanner` is the smallest deterministic seam that improves testability without obscuring when output-capable code actually runs,
- avoiding MVVM or generic lifecycle coordinators keeps the current project graph and safety boundaries obvious.

Recommended Stage 21H:

1. Audit shutdown/cleanup ordering and stop-only lifecycle behavior as its own dedicated stage, including the explicit `InitializeStartupCleanupAsync()` / dispose path boundaries.
2. Only after that, consider a separate safety-context or Stop All / Emergency Stop audit rather than mixing multiple high-risk lifecycle seams together.

Stage 21G does not change UI/XAML, app-settings schema, audio-profile schema, P-HPR profile schema, `.hdrec` format, replay timing behavior, shutdown ordering, ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, or privacy/redaction boundaries.

## Stage 21H Shutdown/Cleanup Ordering Audit

Stage 21H re-audits only shutdown/cleanup ordering and extracts one narrow App-layer planner that describes the intended stop-only order without executing hardware or runtime calls.

Audit result:

- `MainWindow.xaml.cs` still owns mixed shutdown responsibilities across window closing, event/timer detachment, continuous P-HPR runtime stop, standalone BST-1 local/manual pulse cleanup, service disposal, real P-HPR road stop, real P-HPR output disposal, haptic-pipeline disposal, and shutdown diagnostic recording,
- `InitializeStartupCleanupAsync()` remains an output-capable stop-only startup path inside `PHprDirectRuntime`; it is intentionally not moved into App because it can attempt stop reports and unclean-marker recovery,
- Stop All / Emergency Stop handlers, safety-context builders, ASIO lifecycle ownership, and direct-output runtime ownership remain explicit and unsafe to move in this stage.

New App-only non-WPF shutdown boundary:

- `ShutdownCleanupPlanner`
- supporting records/enums in `ShutdownCleanupPlanner.cs`

The extracted boundary now owns:

- deterministic app-shutdown stop/dispose step ordering metadata,
- bounded timeout metadata for the explicit stop/dispose steps,
- readable step descriptions for tests and audit documentation.

`ShutdownCleanupPlanner` intentionally does not own:

- WPF controls, `Dispatcher`, `Window`, or event handlers,
- HID writer open/write/close calls,
- P-HPR start/stop report emission,
- ASIO output start/stop execution,
- audio buffer submission,
- telemetry receiver start,
- timer start,
- direct-control enable/arm mutation,
- Stop All / Emergency Stop execution,
- startup cleanup execution,
- safety-context construction,
- shutdown diagnostic file writes.

`MainWindow.xaml.cs` intentionally still owns:

- the actual `OnClosing` / `OnClosed` lifecycle,
- minimize-to-tray decision handling,
- the real `RunShutdownCleanupAsync()` execution body,
- event/timer detachment,
- explicit `_realPhprContinuousEffectsRuntime.StopAsync(...)`,
- explicit `_hapticPipeline.StopManualAsioHardwareTest(...)`,
- explicit `_testBench`, `_paddleInputSource`, `_telemetryReceiver`, `_realPhprOutput`, `_realPhprContinuousEffectsRuntime`, and `_hapticPipeline` disposal calls,
- explicit shutdown exception aggregation and shutdown diagnostic recording.

Protected behavior after Stage 21H:

- no startup BST-1 output and no startup P-HPR output are introduced,
- no shutdown start/arm/enable behavior is introduced,
- no Stop All / Emergency Stop ownership changes are introduced,
- no ASIO/P-HPR report/protocol/tuning/schema behavior changes are introduced,
- startup cleanup remains explicit in `PHprDirectRuntime` and unchanged,
- app shutdown still performs stop-only cleanup before final disposal in the same runtime order as before.

Why the extraction stops here:

- the shutdown path is still execution-heavy and safety-adjacent,
- only the high-level order is deterministic enough to extract safely,
- moving actual stop/dispose execution would hide output-capable behavior and weaken lifecycle clarity.

Recommended Stage 21I:

1. Audit either safety-context builders or Stop All / Emergency Stop ownership as a separate stage.
2. Do not combine both unless a later audit proves those seams are trivial and fully fake-testable.

Stage 21H does not change UI/XAML, app-settings schema, audio-profile schema, P-HPR profile schema, `.hdrec` format, replay timing behavior, startup behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, parser layouts, or privacy/redaction boundaries.

## Stage 21I Safety-Context Builder Audit

Stage 21I re-audits only the remaining App-shell safety-context builders and extracts one narrow pure snapshot builder without moving any Stop All, Emergency Stop, startup-cleanup, or runtime ownership.

Audit result:

- the remaining safety-context methods in `MainWindow.xaml.cs` were all concentrated in one cluster and mostly repeated the same deterministic `PHprSafetyContext` field mapping,
- the pure seam was the mapping from already-gathered runtime snapshots and booleans into immutable safety-context data,
- WPF reads/writes, runtime snapshot gathering, Stop All / Emergency Stop execution, direct-control mutation, startup-cleanup invocation, and safety-limiter/output execution remained outside that seam and unsafe to move.

New App-only non-WPF safety-context boundary:

- `SafetyContextSnapshotBuilder`
- supporting record `PhprSafetyContextSnapshot`

The extracted boundary now owns:

- deterministic mapping from mock/real output snapshots plus read-only runtime booleans into immutable safety-context snapshots,
- manual real-output safety-context snapshot shaping from selected-device readiness state,
- paddle-bench mock/direct safety-context snapshot shaping,
- conversion from the intermediate snapshot to the existing `PHprSafetyContext` value.

`SafetyContextSnapshotBuilder` intentionally does not own:

- WPF controls or `Dispatcher`,
- HID writer open/write/close calls,
- P-HPR start/stop report emission,
- ASIO output start/stop,
- audio buffer submission,
- telemetry receiver or timer start/stop,
- Stop All / Emergency Stop execution,
- startup cleanup execution,
- direct-control enable/arm mutation,
- safety-limiter execution or mutation,
- private HID path persistence,
- emergency/Stop All latch persistence.

`MainWindow.xaml.cs` intentionally still owns:

- `RefreshDrivingArmedAndShiftIntentTelemetry()` and other runtime snapshot reads,
- `_realPhprOutput.GetSnapshot()` / `_mockPhprSafetyOutput.GetSnapshot()` call sites,
- driving-armed state reads from `_drivingArmedStateService`,
- actual Stop All / Emergency Stop handlers,
- actual direct-control enable/arm mutation,
- actual mock/real routing calls and safety-limiter calls,
- `InitializeStartupCleanupAsync()` invocation,
- all ASIO lifecycle and direct-output runtime ownership.

Protected behavior after Stage 21I:

- Stop All / Emergency Stop execution did not move,
- startup cleanup remains explicit and unchanged in `PHprDirectRuntime`,
- no startup output or auto-arm behavior was introduced,
- existing `PHprSafetyContext` gate values remain the same for mock gear, mock pedal effects, real gear, bench mock/direct, real road/slip/lock, and manual real pulse call sites,
- no ASIO/P-HPR report, protocol, tuning, schema, parser, replay, or privacy behavior changed.

Why the extraction stops here:

- the pure mapping layer was safe to move, but the surrounding snapshot gathering and execution paths are still tied to live runtime state and safety-critical ownership,
- pushing further in this stage would start mixing safety-context extraction with Stop All / Emergency Stop or runtime ownership, which this stage explicitly avoids.

Recommended Stage 21J:

1. Audit Stop All / Emergency Stop ownership as its own stage now that the pure safety-context mapping is separated.
2. If that still looks unsafe to extract, add more guardrails around those handlers rather than forcing a broad lifecycle coordinator.

Stage 21I does not change UI/XAML, app-settings schema, audio-profile schema, P-HPR profile schema, `.hdrec` format, replay timing behavior, startup behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, safety-limit numeric defaults, parser layouts, or privacy/redaction boundaries.

## Stage 21J Stop All / Emergency Stop Ownership Audit

Stage 21J re-audits only the remaining Stop All / Emergency Stop ownership in `MainWindow.xaml.cs` and confirms the safe result is audit plus guardrails, not another extraction.

Audit result:

- the stop handlers are not just shaping text or deterministic plans; they sequence real stop and clear calls across mock routing, direct P-HPR runtime ownership, and UI/runtime recovery state,
- the direct `Stop All / Clear Device State` path also reapplies paddle-bench runtime block state after execution, which keeps startup-cleanup and unclean-marker recovery semantics tied to the same boundary,
- the surrounding work fans out into multiple WPF status surfaces and diagnostics updates, so moving only the handler shell would mostly hide safety-critical call order behind a coordinator without creating a truly pure seam,
- startup cleanup remains explicit in `PHprDirectRuntime` and shutdown cleanup execution remains explicit in `MainWindow`, so this stop boundary is still coupled to the broader lifecycle ownership that earlier stages intentionally left untouched.

New Stage 21J protection:

- `StopEmergencyOwnershipGuardrailTests`

The guardrail now proves:

- `MainWindow.xaml.cs` still visibly owns mock gear and mock pedal emergency-stop execution,
- `MainWindow.xaml.cs` still visibly owns real direct emergency-stop execution and direct-runtime `StopAllAsync(...)`,
- startup cleanup invocation and shutdown cleanup plan execution stay anchored around the same App-shell owner,
- previously extracted pure App planners/builders still do not absorb Stop All, emergency-stop, emergency-mute, or haptics start/stop execution.

`MainWindow.xaml.cs` intentionally still owns:

- `MockGearPulseEmergencyStopButton_Click`,
- `MockPedalEffectsEmergencyStopButton_Click`,
- `RealPhprEmergencyStopButton_Click`,
- `PhprPedalsEmergencyStopButton_Click`,
- `PhprPedalsStopAllClearDeviceStateButton_Click`,
- `InitializeStartupCleanupAsync()` invocation,
- shutdown cleanup execution from `ShutdownCleanupPlanner.BuildAppShutdownPlan()`,
- post-stop status refresh, diagnostics refresh, and paddle-bench runtime block recovery.

Protected behavior after Stage 21J:

- Stop All / Emergency Stop execution did not move,
- startup cleanup remains explicit and unchanged in `PHprDirectRuntime`,
- shutdown cleanup execution remains explicit and unchanged in `MainWindow`,
- no startup output, auto-arm, or hidden recovery output was introduced,
- no ASIO/P-HPR report, protocol, tuning, schema, parser, replay, or privacy behavior changed.

Why the extraction stops here:

- there is no meaningful pure planner/presenter seam left around Stop All / Emergency Stop without mixing in real runtime execution and lifecycle ownership,
- extracting only call-order wrappers would add indirection without reducing risk or shrinking the true safety surface,
- the honest next step is a separate audit of the adjacent Start Haptics / Emergency Mute ownership rather than broadening this stop boundary further.

Recommended Stage 21K:

1. Audit Start Haptics / Emergency Mute ownership as a separate lifecycle-control stage.
2. If any seam exists there, keep it limited to pure status/message planning and leave real start/stop/mute execution in `MainWindow`.

Stage 21J does not change UI/XAML, app-settings schema, audio-profile schema, P-HPR profile schema, `.hdrec` format, replay timing behavior, startup behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, safety-limit numeric defaults, parser layouts, or privacy/redaction boundaries.

## Stage 21K Start Haptics / Emergency Mute Ownership Audit

Stage 21K re-audits only the remaining Start Haptics / Stop Haptics / Emergency Mute ownership in `MainWindow.xaml.cs` and extracts one narrow pure presentation seam without moving any real execution.

Audit result:

- the actual Start Haptics / Stop Haptics handler still executes live `_hapticPipeline.StartAsync()` / `_hapticPipeline.StopAsync()` calls and remains output-capable, so that path is not safe to hide behind a broad coordinator,
- the actual Emergency Mute handler still mutates `_emergencyMuted`, calls `_hapticPipeline.SetEmergencyMuteAsync(...)`, mirrors that state into `_testBench.EmergencyMute`, and may trigger test-bench buffer refresh work, so mute execution must remain explicit,
- the safe seam is the deterministic control presentation layer around Start/Stop button text, Emergency Mute button text, the `HapticsStateText` label, and read-only start-readiness metadata derived from already-gathered snapshot values,
- Stop All / Emergency Stop execution, startup cleanup, and shutdown cleanup remain adjacent lifecycle/safety boundaries and stay intentionally untouched.

New App-only non-WPF control-state boundary:

- `HapticsControlStatePresenter`
- supporting records/enums `HapticsControlStateSnapshot`, `HapticsControlStatePresentation`, `HapticsDisplayState`, `HapticsMuteState`, and `HapticsStartReadinessState`

The extracted boundary now owns:

- deterministic Start/Stop button text selection from current haptics-running intent,
- deterministic Emergency Mute button text selection from current emergency-mute state,
- deterministic `HapticsStateText` shaping for stopped, emergency-muted, telemetry-stale-muted, mixer-idle, and active-effects states,
- read-only mute classification metadata that distinguishes normal mute, emergency mute, and telemetry-stale mute for tests/documentation,
- read-only start-readiness classification and text for running, Null output, unavailable output, unarmed hardware, faulted output, and ready-to-start states.

`HapticsControlStatePresenter` intentionally does not own:

- WPF controls, `Dispatcher`, or routed-event ownership,
- audio output open/start/stop calls,
- haptic pipeline start/stop calls,
- audio buffer submission,
- mixer or safety mutation,
- emergency-mute flag mutation,
- telemetry receiver or timer start/stop,
- HID writer open/write/close calls,
- P-HPR start/stop report emission,
- direct-control enable/arm mutation,
- Stop All execution,
- Emergency Stop execution,
- startup cleanup execution,
- shutdown cleanup execution.

`MainWindow.xaml.cs` intentionally still owns:

- `StartStopButton_Click`,
- `EmergencyMuteButton_Click`,
- `_hapticPipeline.StartAsync()` / `_hapticPipeline.StopAsync()` execution,
- `_hapticPipeline.SetEmergencyMuteAsync(...)` execution,
- `_testBench.EmergencyMute` mutation and test-bench refresh work,
- output-status refresh and related diagnostics fan-out,
- Stop All / Emergency Stop execution,
- `InitializeStartupCleanupAsync()` invocation,
- shutdown cleanup execution from `ShutdownCleanupPlanner.BuildAppShutdownPlan()`.

Protected behavior after Stage 21K:

- actual Start Haptics / Stop Haptics execution stayed visible and unchanged in `MainWindow`,
- actual Emergency Mute execution stayed visible and unchanged in `MainWindow`,
- Stop All / Emergency Stop execution stayed visible and unchanged,
- startup cleanup remained explicit and unchanged in `PHprDirectRuntime`,
- shutdown cleanup execution remained explicit and unchanged in `MainWindow`,
- no startup BST-1 output, startup P-HPR output, auto-start haptics, auto-start ASIO, auto-enable P-HPR direct control, or auto-arm P-HPR direct control was introduced,
- no ASIO/P-HPR report, protocol, tuning, schema, parser, replay, or privacy behavior changed.

Why the extraction stops here:

- the safe move was the presentation-only layer, not the start/stop/mute handlers themselves,
- pushing further would start mixing pure control text/state shaping with output-capable runtime execution, mute mutation, or broader lifecycle ownership,
- the remaining `MainWindow` residue is now mostly orchestration and safety ownership rather than another obviously pure control seam.

Recommended Stage 21L:

1. Run one final residual `MainWindow` orchestration audit after Stages 21A-21K.
2. Decide whether the Gemini review stream is complete enough or whether one last small dashboard/status presenter extraction is still justified.
3. Do not combine MVVM, ASIO lifecycle relocation, direct runtime relocation, Stop All / Emergency Stop execution, Start Haptics execution, and Emergency Mute execution into one stage.

Stage 21K does not change UI/XAML, app-settings schema, audio-profile schema, P-HPR profile schema, `.hdrec` format, replay timing behavior, startup behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, safety-limit numeric defaults, parser layouts, or privacy/redaction boundaries.

## Stage 21L Final Residual MainWindow Orchestration Audit

Stage 21L performs the final residual `MainWindow.xaml.cs` orchestration audit after Stages 21A through 21K and closes the Gemini review stream without forcing another extraction.

Audit result:

- the remaining shell methods are now dominated by direct WPF assignment/binding, event-handler entry points, startup/load wiring, shutdown cleanup execution, runtime snapshot gathering, and execution-heavy lifecycle/safety work,
- the remaining status-style methods such as device, telemetry, mixer, profile, and page-status updates still mix runtime snapshot reads, page-selection branching, direct WPF writes, and follow-on updater fan-out, so they are not clean final presenter seams,
- there is no remaining single pure dashboard/footer/status extraction that is both meaningful and lower-risk than simply documenting the residual ownership clearly,
- the correct final outcome is closure documentation plus one aggregate guardrail proving the extracted Stage 21 helper set stays pure while `MainWindow` keeps the execution-heavy entry points visible.

No Stage 21L code extraction:

- Stage 21L intentionally stays audit/guardrail/documentation-only.
- No new presenter/planner/builder is introduced.

Remaining `MainWindow.xaml.cs` responsibility map:

- intentional WPF assignment / control binding:
  - direct `Text` / `Content` / `IsEnabled` / `ToolTip` / `ItemsSource` assignment for shell controls,
  - page-specific `PageStatusText` assignment based on the selected navigation page,
  - UI list and combo-box population, selection reflection, and shell visibility toggles.
- event wiring and handler entry points:
  - click/change/selection handlers,
  - telemetry timer tick entry,
  - window lifecycle entry points such as closing/closed.
- lifecycle orchestration:
  - startup load/readiness sequencing,
  - output-selection pipeline rebuild flow,
  - shutdown cleanup sequencing and exception capture.
- hardware/runtime execution:
  - `_hapticPipeline.StartAsync()` / `_hapticPipeline.StopAsync()`,
  - `_hapticPipeline.SetEmergencyMuteAsync(...)`,
  - direct pulse/test-bench/manual ASIO execution paths,
  - direct runtime configure/open-check/stop/start entry points.
- safety-critical execution:
  - Stop All / Emergency Stop handlers,
  - startup cleanup invocation,
  - shutdown cleanup execution,
  - direct-output readiness and recovery entry points.
- profile/settings file lifecycle:
  - explicit load/save/reset orchestration and related user-triggered status flow.
- runtime snapshot gathering:
  - pipeline, output, telemetry receiver, test bench, direct runtime, router, and diagnostics snapshot collection to feed the extracted presenters and status builders.
- acceptable residual code-behind:
  - shell coordination that is necessarily WPF-anchored or execution-heavy and no longer obviously benefits from another pure extraction.

New Stage 21L protection:

- `GeminiReviewClosureGuardrailTests`

The final closure guardrail proves:

- the Stage 21 helper set (`PhprWorkflowStatusPresenter`, `DiagnosticsStatusPresenter`, `AppSettingsSnapshotBuilder`, `ControlSettingsSnapshotBuilder`, `AudioProfileControlSnapshotBuilder`, `LocalGearReadinessPresenter`, `StartupReadinessPlanner`, `ShutdownCleanupPlanner`, `SafetyContextSnapshotBuilder`, and `HapticsControlStatePresenter`) remains free of WPF references and execution-heavy lifecycle/hardware ownership,
- `MainWindow.xaml.cs` still visibly owns Start Haptics / Stop Haptics execution, Emergency Mute execution, Stop All / Emergency Stop execution, startup cleanup invocation, telemetry/timer startup, and shutdown cleanup execution.

Gemini review closure matrix:

| Gemini concern | Current result | Notes |
| --- | --- | --- |
| `MainWindow.xaml.cs` as a WPF God Object | Partially mitigated | The file is still large, but most deterministic workflow/diagnostics/settings/readiness/status shaping has been extracted. The remaining code is largely deliberate shell orchestration. |
| P-HPR background loops owned by UI | Fixed | Stage 19C moved continuous real road/slip/lock loop ownership out of `MainWindow`. |
| `PHprDirectRuntime` in App | Fixed | Stage 19B moved direct runtime ownership out of `HapticDrive.Asio.App`. |
| Paddle input routing in `MainWindow` | Fixed | Stage 19D moved the substantive routing body into `PaddleInputRoutingCoordinator`. |
| Duplicate slip/lock evaluation | Fixed | Stage 20 introduced shared slip/lock evaluation for BST-1 and P-HPR. |
| Debug-harness UI / product-readiness concerns | Partially mitigated | Status/workflow/readiness/presentation seams were extracted, but the app still intentionally carries advanced diagnostics and validation surfaces in WPF. |
| Stop All / Emergency Stop safety visibility | Guarded | Stage 21J kept execution visible in `MainWindow` and added explicit guardrails. |
| Start Haptics / Emergency Mute visibility | Guarded | Stage 21K kept execution visible in `MainWindow` and extracted only pure presentation. |
| Remaining `MainWindow` residual responsibilities | Deliberately deferred | Residual shell orchestration remains in code-behind because it is lifecycle-heavy, WPF-bound, or safety-critical. |
| Items deliberately deferred | Deliberately deferred | No MVVM rewrite, no broad lifecycle relocation, no hidden runtime ownership transfer, and no hardware-behavior claims are made in this stream. |

Why the remaining `MainWindow` ownership is acceptable:

- the high-risk runtime ownership findings from the Gemini review were already addressed in the earlier stages,
- the remaining shell code is now mostly coordinator glue between WPF controls, snapshot reads, and explicit lifecycle/safety entry points,
- hiding that residue behind extra wrappers would reduce visibility more than it would reduce risk,
- the current guardrails make the intended boundary explicit and testable.

Behavior explicitly not changed by Stage 21L:

- no startup BST-1 output,
- no startup P-HPR output,
- no auto-start haptics,
- no auto-start ASIO,
- no auto-enable or auto-arm P-HPR direct control,
- no Start Haptics / Stop Haptics execution move,
- no Emergency Mute execution move,
- no Stop All / Emergency Stop execution move,
- no startup-cleanup execution move out of `PHprDirectRuntime`,
- no shutdown-cleanup execution move out of `MainWindow`,
- no ASIO/BST-1 backend behavior change,
- no P-HPR HID/report byte change,
- no report ID `0xF1` change,
- no FeatureReport transport change,
- no command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout, command-rate-limiter, safety-limit, parser-layout, replay-timing, schema, or privacy/redaction change.

Stage 21M:

- Stage 21M is not required.
- If a later cleanup is still desired, it should be optional docs-only release-note consolidation rather than another implementation stage.

Stage 21L does not change UI/XAML, app-settings schema, audio-profile schema, P-HPR profile schema, `.hdrec` format, replay timing behavior, startup behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, gear routing, road cadence, slip/lock cadence, hold-timeout durations, command-rate limiter behavior, safety-limit numeric defaults, parser layouts, or privacy/redaction boundaries.

## Stage 22A Post-Gemini P-HPR Slip/Lock Feel Retune

Stage 22A happens after Stage 21L closed the Gemini review stream. It is a practical feel/tuning stage, not another architecture-cleanup stage.

The real slip/lock path remains:

```text
F1 25 telemetry / latest VehicleState
-> cached DrivingArmed/Menu Safe state
-> PHprSlipLockRouter
-> SimagicPhprOutputDevice gates
-> PHprSafetyLimiter
-> SimHubF1EcRealReportEncoder
-> IPhprHidReportWriter
```

Stage 22A keeps that ownership unchanged and retunes only the existing slip/lock settings seam:

- real wheel slip and wheel lock now carry independent `TextureCadenceMs` settings in addition to target, min/max strength, min/max frequency, and duration;
- the router now uses each effect's own cadence instead of one shared 100 ms slip/lock route interval, while the hold-timeout watchdog, explicit stop behavior, gear-priority protection, road-yield behavior, and direct-output safety gates stay unchanged;
- the normal Effects UI keeps only the brake wheel-lock and throttle wheel-slip checkboxes for real slip/lock enablement, and overall real slip/lock routing now follows those visible per-effect toggles instead of a separate normal-workflow master checkbox;
- normal users can now tune per-effect texture cadence directly, while advanced min/max strength, min/max frequency, target, and duration controls remain in Advanced diagnostics.

Stage 22A default feel retune:

- throttle wheel slip default texture cadence: `70 ms`;
- brake wheel lock default texture cadence: `60 ms`;
- continuous slip/lock duration remains `120 ms`.

This shifts the default feel toward tighter continuous texture without changing:

- ASIO/BST-1 behavior,
- P-HPR HID/report bytes,
- report ID `0xF1`,
- FeatureReport transport,
- command encoding,
- F1 25 parser layouts,
- replay timing,
- `.hdrec` format,
- startup output behavior,
- direct-control auto-enable/auto-arm behavior,
- runtime ownership boundaries introduced in Stages 19-21.

If a user selects an aggressive cadence, Stage 22A still relies on the existing `PHprSafetyLimiter` and command-rate limiter rather than bypassing them. Diagnostics now surface the configured slip/lock cadence per effect alongside existing command-rate suppression counters. Physical feel remains Ethan-local validation work.

## Stage 23A Product Workflow and Safe P-HPR Preference Persistence

Stage 23A keeps the runtime ownership introduced in earlier stages intact and changes only shell workflow/persistence boundaries.

Shell workflow result:

- `Dashboard`, `Devices`, `Effects`, `Routing / Mixer`, `Telemetry / UDP`, `Profiles`, `Testing / Validation`, and `Advanced / Diagnostics` now have clearer separation.
- `Testing / Validation` owns manual pulse checks, synthetic validation, paddle bench work, and local validation exports.
- `Devices` stays focused on hardware selection, readiness, wheel mapping, and emergency recovery.
- `Advanced / Diagnostics` stays focused on raw direct-control/mock-routing internals and copyable diagnostics.

Safe P-HPR persistence result:

- app settings now persist only the normal-user P-HPR preference:
  - enabled/disabled preference,
  - preferred mode `Disabled` / `Mock` / `Direct`.
- hydration still restores real direct-output options as a no-output preference snapshot first;
- startup candidate refresh/open-check still runs without sending output reports or feature reports;
- saved Direct preference is re-applied only as workflow intent on top of those existing readiness gates.

Stage 23A explicitly does not persist or restore:

- private HID path,
- active pulse/live output state,
- pending stops,
- emergency-stop latch state,
- startup output,
- haptics-running state.

Stage 23A does not change ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, report ID `0xF1`, FeatureReport transport, command encoding, parser layouts, replay format, or physical-validation boundaries.

## Stage 23B Product Workflow Polish and First-Run Clarity

Stage 23B keeps the Stage 23A shell boundaries and runtime ownership intact. It only refines the way the normal shell explains those existing boundaries.

Dashboard result:

- Dashboard now owns a dedicated ready-checklist / next-step card in addition to the shared top metric cards.
- That card is built only from existing shell/runtime state:
  - haptics running/stopped,
  - output selection and ASIO armed/stopped state,
  - UDP listener/no-packets-yet state,
  - replay active/idle state,
  - P-HPR Disabled / Mock / Direct readiness,
  - paddle listener/mapping readiness.
- No new dashboard runtime logic, graphs, or ownership was introduced.

Normal-page wording boundary:

- `Devices`, `Effects`, `Routing / Mixer`, `Telemetry / UDP`, `Profiles`, and `Testing / Validation` now favor operator-facing wording:
  - what is connected,
  - what is ready,
  - what is disabled,
  - what should happen next,
  - what remains runtime-only/not saved.
- Raw HID/report/candidate/debug detail remains intentionally concentrated in `Advanced / Diagnostics`.
- The page split introduced in Stage 23A remains the same:
  - `Devices` for setup/readiness,
  - `Effects` for normal tuning,
  - `Routing / Mixer` for output/gain/protection summary,
  - `Telemetry / UDP` for input/record/replay/forwarding,
  - `Profiles` for saved tuning/preferences,
  - `Testing / Validation` for deliberate manual tools,
  - `Advanced / Diagnostics` for low-level troubleshooting.

Persistence boundary:

- Stage 23B does not expand or relax persistence.
- Stage 23A safe P-HPR preference persistence remains unchanged:
  - `PreferredPhprPedalsEnabled`,
  - `PreferredPhprPedalsMode`.
- Live output, HID paths, emergency-stop state, arming, startup output, and haptics-running state remain runtime-only.

Stage 23B does not change ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, command encoding, parser layouts, replay format behavior, or physical-validation boundaries.

## Stage 23C Dashboard View Extraction and Shell Presentation Seam

Stage 23C begins reducing shell maintainability risk without changing shell ownership boundaries.

Dashboard extraction result:

- The Dashboard page now lives in a dedicated WPF component:
  - `Views/DashboardView.xaml`
  - `Views/DashboardView.xaml.cs`
- The extracted view still presents the same Stage 23B Dashboard role:
  - shared top metric cards,
  - ready-checklist summary,
  - next-step guidance.
- The extracted view does not own runtime objects, hardware calls, or snapshot gathering. It only renders already-shaped dashboard state.

Presentation-seam result:

- Dashboard-only display shaping now flows through `DashboardStatusPresenter` with immutable App-layer inputs:
  - `DashboardStatusSnapshot`,
  - `DashboardStatusPresentation`,
  - dashboard-local mode metadata for the P-HPR summary path.
- The presenter owns only deterministic wording/status shaping for:
  - output mode summary,
  - haptics state text already supplied from the haptics presenter,
  - UDP/no-packets-yet status,
  - parser/vehicle/recording summaries,
  - ready-checklist items,
  - next-step guidance.
- The presenter does not own WPF controls, `System.Windows` types, ASIO output classes, HID/report writers, or start/stop/mute execution.

Residual `MainWindow` boundary:

- `MainWindow` still owns:
  - app composition,
  - navigation/page selection,
  - runtime object ownership,
  - Start Haptics / Stop Haptics,
  - Emergency Mute / Stop All execution,
  - startup/shutdown cleanup,
  - live snapshot gathering from telemetry, replay, recording, P-HPR direct runtime, and paddle input.
- `MainWindow` now applies the shaped presentation through `DashboardViewControl.Apply(...)` rather than assigning every Dashboard text block directly.

Stage 23C intentionally does not start a broad MVVM rewrite. It extracts one low-risk page/component seam while leaving runtime ownership explicit and visible.

Stage 23C does not change ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, command encoding, parser layouts, replay format behavior, or physical-validation boundaries.

## Stage 23D Devices View Extraction and Hardware Setup Presentation Seam

Stage 23D continues the same page-by-page shell extraction strategy used in Stage 23C.

Devices extraction result:

- The normal Devices page now lives in a dedicated WPF component:
  - `Views/DevicesView.xaml`
  - `Views/DevicesView.xaml.cs`
- The extracted normal Devices view still presents the same setup/readiness role:
  - `Bass Shaker / ASIO`
  - `Simagic P-HPR Pedals`
  - `Simagic Wheel / Shift Paddles`
- The extracted Devices view does not own runtime objects, hardware calls, or snapshot gathering. It only renders already-shaped setup/readiness state and forwards user interactions back to the existing `MainWindow` handlers.

Presentation-seam result:

- Devices-only setup/readiness display shaping now flows through `DevicesStatusPresenter` with immutable App-layer inputs:
  - `DevicesStatusSnapshot`
  - `DevicesStatusPresentation`
- The presenter owns only deterministic Devices wording/status shaping for:
  - current output / ASIO readiness text
  - input discovery summaries
  - paddle listener badge/status/items
  - shift-intent status/items
  - Devices page-status summary
- The presenter does not own WPF controls, `System.Windows` types, ASIO backend classes, HID/report writers, or hardware start/stop/emergency execution.

Residual `MainWindow` boundary:

- `MainWindow` still owns:
  - app composition
  - navigation/page selection
  - runtime object ownership
  - ASIO selection/arming/start interactions
  - P-HPR direct-runtime interactions
  - paddle listener/routing interactions
  - Start Haptics / Stop Haptics
  - Emergency Mute / Stop All execution
  - startup/shutdown cleanup
  - live snapshot gathering
- `MainWindow` now applies shaped Devices presentation through `DevicesViewControl.Apply(...)` instead of keeping the full Devices layout inline in `MainWindow.xaml`.

Normal workflow boundary after Stage 23D:

- Devices remains setup/readiness only.
- Testing / Validation remains the deliberate home for manual pulse checks, paddle bench tooling, and controlled validation harnesses.
- Advanced / Diagnostics remains the home for raw HID/report/candidate internals and deeper troubleshooting.

Stage 23D intentionally does not start a broad MVVM rewrite. It extracts one additional low-risk page/component seam while leaving runtime ownership explicit and visible.

Stage 23D does not change ASIO/BST-1 runtime behavior, P-HPR HID/report bytes, command encoding, parser layouts, replay format behavior, or physical-validation boundaries.

## Stage 23G Telemetry / UDP View Extraction and Replay-Forwarding Presentation Seam

Stage 23G continues the same page-by-page shell extraction strategy used in Stages 23C, 23D, 23E, and 23F.

Telemetry / UDP extraction result:

- The normal Telemetry / UDP workflow page now lives in a dedicated WPF component:
  - `Views/TelemetryUdpView.xaml`
  - `Views/TelemetryUdpView.xaml.cs`
- The extracted normal Telemetry / UDP view still presents the same workflow role:
  - recording and replay,
  - recording library selection / rename / delete,
  - UDP forwarding destination editing and configured-destination summary.
- The extracted view does not own runtime objects, UDP sockets, parser integration, replay/recording file IO, or forwarding-destination persistence. It renders already-shaped state and forwards user interactions back to the existing `MainWindow` handlers.

Presentation-seam result:

- Telemetry / UDP display shaping now flows through `TelemetryUdpStatusPresenter` with immutable App-layer inputs:
  - `TelemetryUdpStatusSnapshot`
  - `TelemetryUdpStatusPresentation`
- The presenter owns only deterministic Telemetry / UDP wording/status shaping for:
  - replay timing help text,
  - recording button text,
  - replay button text,
  - recording detail text,
  - replay detail text,
  - forwarding-destination summary text,
  - Telemetry / UDP page-status summary.
- The presenter does not own WPF controls, `System.Windows` types, ASIO backend classes, HID/report writers, runtime start/stop calls, parser ownership, recording/replay services, or forwarding persistence execution.

Residual `MainWindow` boundary:

- `MainWindow` still owns:
  - app composition,
  - navigation/page selection,
  - runtime object ownership,
  - UDP receiver ownership,
  - parser / `VehicleState` adapter integration,
  - forwarding-destination mutation and persistence,
  - recording start/stop and library refresh execution,
  - replay latest / selected / timing-mode execution,
  - app settings save/load,
  - startup/shutdown cleanup,
  - live snapshot gathering,
  - Start Haptics / Stop Haptics,
  - Emergency Mute / Stop All execution.
- `MainWindow` now applies shaped Telemetry / UDP presentation through `TelemetryUdpViewControl.Apply(...)` instead of keeping the normal Telemetry / UDP page layout inline in `MainWindow.xaml`.

Normal workflow boundary after Stage 23G:

- Dashboard remains operational overview.
- Devices remains setup/readiness only.
- Effects remains normal effect tuning only.
- Routing / Mixer remains output routing, gain, mute, limiter summary, priority, ducking, and active-effect summary only.
- Telemetry / UDP remains normal F1 25 UDP, recording, replay, recording-library, and forwarding workflow only.
- Testing / Validation remains the deliberate home for manual tools.
- Advanced / Diagnostics remains the home for raw internals and troubleshooting.

Stage 23G intentionally does not start a broad MVVM rewrite. It extracts one additional low-risk page/component seam while leaving runtime ownership explicit and visible.

Stage 23G does not change UDP listener behavior, forwarding behavior, recording/replay format or timing behavior, parser / `VehicleState` behavior, profile/persistence behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, or physical-validation boundaries.

## Stage 23H Profiles View Extraction and Profile Workflow Presentation Seam

Stage 23H continues the same page-by-page shell extraction strategy used in Stages 23C, 23D, 23E, 23F, and 23G.

Profiles extraction result:

- The normal Profiles workflow page now lives in a dedicated WPF component:
  - `Views/ProfilesView.xaml`
  - `Views/ProfilesView.xaml.cs`
- The extracted normal Profiles view still presents the same workflow role:
  - audio / BST-1 profile name and save/load/reset workflow,
  - profile path and persistence boundary summary,
  - P-HPR profile persistence summary,
  - profile validation/status summary.
- The extracted view does not own profile stores, file IO, app-settings persistence, runtime application, ASIO interactions, P-HPR interactions, or hardware output. It renders already-shaped state and forwards user interactions back to the existing `MainWindow` handlers.

Presentation-seam result:

- Profiles display shaping now flows through `ProfilesStatusPresenter` with immutable App-layer inputs:
  - `ProfilesStatusSnapshot`
  - `ProfilesStatusPresentation`
- The presenter owns only deterministic Profiles wording/status shaping for:
  - active/saved profile status text,
  - audio profile path summary,
  - P-HPR profile path and persistence-boundary summary,
  - profile validation text,
  - Profiles page-status summary.
- The presenter does not own WPF controls, `System.Windows` types, ASIO backend classes, HID/report writers, runtime start/stop calls, profile-store execution, or file IO ownership.

Residual `MainWindow` boundary:

- `MainWindow` still owns:
  - app composition,
  - navigation/page selection,
  - runtime object ownership,
  - live snapshot gathering,
  - audio profile store calls and file IO,
  - P-HPR profile store calls and file IO,
  - audio profile save/load/reset execution,
  - P-HPR profile save/load/reset execution,
  - app settings save/load execution,
  - applying loaded profile settings to controls and runtime,
  - ASIO runtime interactions,
  - P-HPR runtime interactions,
  - startup/shutdown cleanup,
  - Start Haptics / Stop Haptics,
  - Emergency Mute / Stop All execution.
- `MainWindow` now applies shaped Profiles presentation through `ProfilesViewControl.Apply(...)` instead of keeping the normal Profiles page layout inline in `MainWindow.xaml`.

Normal workflow boundary after Stage 23H:

- Dashboard remains operational overview.
- Devices remains setup/readiness only.
- Effects remains normal effect tuning only.
- Routing / Mixer remains output routing, gain, mute, limiter summary, priority, ducking, and active-effect summary only.
- Telemetry / UDP remains normal F1 25 UDP, recording, replay, recording-library, and forwarding workflow only.
- Profiles remains the normal audio profile and P-HPR profile workflow only.
- Testing / Validation remains the deliberate home for manual tools.
- Advanced / Diagnostics remains the home for raw internals and troubleshooting.

Stage 23H intentionally does not start a broad MVVM rewrite. It extracts one additional low-risk page/component seam while leaving runtime ownership explicit and visible.

Stage 23H does not change profile schema behavior, profile save/load/reset behavior, default/tuning behavior, profile/persistence boundaries, UDP listener behavior, forwarding behavior, recording/replay behavior, parser / `VehicleState` behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, or physical-validation boundaries.

## Stage 23I Testing / Validation View Extraction and Manual-Tools Presentation Seam

Stage 23I continues the same page-by-page shell extraction strategy used in Stages 23C through 23H.

Testing / Validation extraction result:

- The Testing / Validation workflow now lives in a dedicated WPF component:
  - `Views/TestingValidationView.xaml`
  - `Views/TestingValidationView.xaml.cs`
- The extracted view still presents the same workflow role:
  - synthetic test bench,
  - manual BST-1 / ASIO pulse checks,
  - manual P-HPR pedal checks,
  - local paddle / gear bench tools,
  - controlled validation harness and local export workflow.
- The extracted view does not own runtime objects, hardware output, safety gates, validation/export execution, settings/profile persistence, or file IO. It renders already-shaped state and forwards user interactions back to the existing `MainWindow` handlers.

Presentation-seam result:

- Testing / Validation display shaping now flows through `TestingValidationStatusPresenter` with immutable App-layer inputs:
  - `TestingValidationStatusSnapshot`
  - `TestingValidationStatusPresentation`
- The presenter owns only deterministic synthetic-bench and page-summary wording for:
  - test bench start/stop button text,
  - test bench state text,
  - test bench peak/limiter/output text,
  - test bench warning text,
  - Testing / Validation page-status summary.
- The presenter does not own WPF controls, `System.Windows` types, ASIO backend classes, HID/report writers, manual test execution, validation/export execution, or hardware output calls.

Residual `MainWindow` boundary:

- `MainWindow` still owns:
  - app composition,
  - navigation/page selection,
  - runtime object ownership,
  - live snapshot gathering,
  - synthetic test bench execution,
  - manual BST-1 / ASIO test execution,
  - manual P-HPR pedal test execution,
  - local paddle / gear bench execution,
  - controlled validation harness evaluation and export execution,
  - profile lifecycle,
  - app settings save/load execution,
  - ASIO runtime interactions,
  - P-HPR runtime interactions,
  - paddle listener/routing coordinator interactions,
  - startup/shutdown cleanup,
  - Start Haptics / Stop Haptics,
  - Emergency Mute / Stop All execution.
- `MainWindow` now applies shaped Testing / Validation presentation through `TestingValidationViewControl.Apply(...)` instead of keeping the Testing / Validation page layout inline in `MainWindow.xaml`.

Normal workflow boundary after Stage 23I:

- Dashboard remains operational overview.
- Devices remains setup/readiness only.
- Effects remains normal effect tuning only.
- Routing / Mixer remains output routing, gain, mute, limiter summary, priority, ducking, and active-effect summary only.
- Telemetry / UDP remains normal F1 25 UDP, recording, replay, recording-library, and forwarding workflow only.
- Profiles remains the normal audio profile and P-HPR profile workflow only.
- Testing / Validation remains deliberate manual tools only.
- Advanced / Diagnostics remains the home for raw internals and troubleshooting.

Stage 23I intentionally does not start a broad MVVM rewrite. It extracts one additional low-risk page/component seam while leaving runtime ownership explicit and visible.

Stage 23I does not change manual test behavior, validation harness behavior, profile/persistence boundaries, UDP listener behavior, forwarding behavior, recording/replay behavior, parser / `VehicleState` behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, or physical-validation boundaries.

## Stage 23J Advanced / Diagnostics View Extraction and Raw-Internals Presentation Seam

Stage 23J continues the same page-by-page shell extraction strategy used in Stages 23C through 23I.

Advanced / Diagnostics extraction result:

- The Advanced / Diagnostics workflow now lives in a dedicated WPF component:
  - `Views/AdvancedDiagnosticsView.xaml`
  - `Views/AdvancedDiagnosticsView.xaml.cs`
- The extracted view still presents the same workflow role:
  - P-HPR workflow summary,
  - live F1 validation checklist,
  - coexistence diagnostics,
  - direct-write readiness,
  - real direct-control internals,
  - mock gear-routing internals,
  - mock pedal-effects internals,
  - settings,
  - runtime diagnostics,
  - copy-report and road flight-recorder controls.
- The extracted view does not own runtime objects, hardware output, diagnostics report assembly, settings persistence, validation/export execution, file IO, or safety gates. It renders already-shaped state and forwards user interactions back to the existing `MainWindow` handlers.

Presentation-seam result:

- Stage 23J reuses existing App-layer diagnostics presentation seams instead of creating a parallel rewrite:
  - `DiagnosticsStatusSnapshotBuilder`
  - `DiagnosticsStatusPresenter`
  - `PhprWorkflowStatusPresenter`
  - `PersistedSettingsStatusPresenter`
- Runtime diagnostics summary/items/clipboard report shaping still flows through `DiagnosticsStatusPresenter`.
- `MainWindow` now applies shaped diagnostics presentation through `AdvancedDiagnosticsViewControl.Apply(...)` instead of keeping the Advanced / Diagnostics page layout inline in `MainWindow.xaml`.

Residual `MainWindow` boundary:

- `MainWindow` still owns:
  - app composition,
  - navigation/page selection,
  - runtime object ownership,
  - live snapshot gathering,
  - diagnostics report copy execution,
  - advanced setting persistence execution,
  - road texture flight-recorder execution,
  - P-HPR raw/direct diagnostics execution,
  - mock routing diagnostics execution,
  - coexistence diagnostics execution,
  - validation harness execution and export execution,
  - profile lifecycle,
  - app settings save/load execution,
  - ASIO runtime interactions,
  - P-HPR runtime interactions,
  - paddle listener/routing coordinator interactions,
  - startup/shutdown cleanup,
  - Start Haptics / Stop Haptics,
  - Emergency Mute / Stop All execution.

Normal workflow boundary after Stage 23J:

- Dashboard remains operational overview.
- Devices remains setup/readiness only.
- Effects remains normal effect tuning only.
- Routing / Mixer remains output routing, gain, mute, limiter summary, priority, ducking, and active-effect summary only.
- Telemetry / UDP remains normal F1 25 UDP, recording, replay, recording-library, and forwarding workflow only.
- Profiles remains the normal audio profile and P-HPR profile workflow only.
- Testing / Validation remains deliberate manual tools only.
- Advanced / Diagnostics remains the home for raw internals and troubleshooting.

Stage 23J intentionally does not start a broad MVVM rewrite. It extracts one additional low-risk page/component seam while leaving runtime ownership explicit and visible.

Stage 23J does not change diagnostics report behavior, manual test behavior, validation harness behavior, profile/persistence boundaries, UDP listener behavior, forwarding behavior, recording/replay behavior, parser / `VehicleState` behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, or physical-validation boundaries.

## Stage 23K MainWindow Shell-Composition Audit and Gemini REC-01 Closure

Stage 23K closes the Stage 23C through 23J page-extraction stream as an audit, guardrail, and documentation stage rather than another feature or extraction stage.

Post-Stage-23 shell boundary:

- `MainWindow.xaml` is now a small shell host:
  - root `Window`,
  - shared top action/status bar,
  - navigation,
  - extracted page view hosts,
  - shared page-status summary card,
  - shared footer/status area.
- No full page workflow layout remains inline in `MainWindow.xaml`.
- All major pages now live behind dedicated view seams:
  - `DashboardView`
  - `DevicesView`
  - `EffectsView`
  - `RoutingMixerView`
  - `TelemetryUdpView`
  - `ProfilesView`
  - `TestingValidationView`
  - `AdvancedDiagnosticsView`

Residual `MainWindow.xaml.cs` boundary after Stage 23K:

- `MainWindow.xaml.cs` remains intentionally code-behind-driven for:
  - app construction/composition,
  - page navigation,
  - extracted-view event forwarding,
  - direct WPF control hydration/assignment where not already safely extracted,
  - runtime object ownership,
  - live snapshot gathering,
  - startup/shutdown cleanup,
  - Start Haptics / Stop Haptics execution,
  - Emergency Mute / Stop All execution,
  - ASIO runtime interactions,
  - P-HPR runtime interactions,
  - paddle listener/routing coordinator interactions,
  - telemetry receiver orchestration,
  - recording/replay/forwarding execution,
  - diagnostics report copy execution,
  - advanced setting persistence execution,
  - road texture flight-recorder execution,
  - validation harness execution/export execution,
  - profile lifecycle,
  - app settings save/load execution.
- Stage 23K deliberately does not hide this residual ownership behind a broad MVVM rewrite, because the remaining code is WPF-bound, lifecycle-heavy, or safety/hardware-capable rather than clearly pure presentation.

Gemini REC-01 status decision:

- Gemini REC-01 is considered materially addressed for the current phase.
- The project did not adopt full MVVM or `CommunityToolkit.Mvvm`; instead it chose a lower-risk pattern:
  - page-level `UserControl` extraction,
  - small pure presenters/builders where deterministic,
  - source-boundary and closure guardrails,
  - explicit retention of execution-heavy runtime/safety ownership in `MainWindow`.
- This lightweight UserControl plus presenter/builder pattern is considered sufficient until later hardware validation or future feature work reveals another clearly pure presentation seam.
- Stage 22B hardware validation/fine-tuning remains separate from REC-01 closure.
- Gemini REC-02 runtime-start ownership, if pursued later, should remain a separate audit-only stage rather than being mixed into the Stage 23 shell closure.

Stage 23K does not change runtime behavior, diagnostics report behavior, manual test behavior, validation harness behavior, profile/persistence boundaries, UDP listener behavior, forwarding behavior, recording/replay behavior, parser / `VehicleState` behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, or physical-validation boundaries.

## Stage 24A Gemini REC-02 Runtime-Start Ownership Audit and Closure

Stage 24A is an audit, guardrail, and documentation stage layered on top of Stage 23K rather than another runtime-extraction stage.

Runtime-start ownership decision after the audit:

- Gemini REC-01 was already closed by Stage 23K for the current phase.
- Gemini REC-02 was audited by Stage 24A and is considered closed for the current phase without moving runtime-start ownership out of `MainWindow`.
- This is deliberate because the remaining `MainWindow.xaml.cs` startup/shutdown and safety paths are still composition-heavy, WPF-bound, hardware-capable, or cross-runtime in nature rather than another clearly pure presentation seam.

Deliberate Stage 24A ownership split:

- Extracted view code-behind remains limited to presentation application and event forwarding. Views do not own Start/Stop, Emergency Mute, telemetry startup, ASIO startup, direct-runtime startup cleanup, or P-HPR output start/stop execution.
- `MainWindow.xaml.cs` remains the owner of:
  - runtime object composition,
  - extracted-view event hookups,
  - `InitializeStartupCleanupAsync` startup cleanup execution,
  - telemetry receiver startup,
  - continuous P-HPR runtime start/stop orchestration,
  - Start Haptics / Stop Haptics execution,
  - Emergency Mute execution,
  - P-HPR Stop All / Emergency Stop execution,
  - shutdown-plan execution.
- `PHprContinuousEffectsRuntimeCoordinator` remains the owner of the background continuous road/slip/lock loop mechanics and stop-timeout handling outside `HapticDrive.Asio.App`.
- `PaddleInputRoutingCoordinator` remains the owner of the paddle-routing body and bench/direct routing flow inside `HapticDrive.Asio.App` but outside `MainWindow`.
- `PHprDirectRuntimeCoordinator` remains outside `HapticDrive.Asio.App` and outside `MainWindow`, in the Windows P-HPR output assembly.
- `StartupReadinessPlanner` and `ShutdownCleanupPlanner` remain pure planning helpers. They describe readiness/cleanup plans but do not execute runtime start, stop, emergency, or hardware operations themselves.

Stage 24A does not change runtime behavior, diagnostics report behavior, manual test behavior, validation harness behavior, profile/persistence boundaries, UDP listener behavior, forwarding behavior, recording/replay behavior, parser / `VehicleState` behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, or physical-validation boundaries.

## Stage 25A Documentation Baseline and Audit Closure

Stage 25A is a documentation-only baseline alignment stage layered on top of Stage 24A.

Documentation baseline result:

- `README.md` now reflects the live Stage 24A state instead of the older Stage 18b milestone.
- The public architecture baseline is now stated explicitly:
  - F1 25 is the only current production game integration.
  - `NullAudioOutputDevice` remains the default and automated-test-safe output.
  - ASIO remains explicit opt-in and does not auto-start.
  - Simagic P-HPR remains a separate non-audio actuator path.
  - `MainWindow.xaml.cs` still remains the deliberate composition/runtime shell after the Stage 23/24 extraction stream.
- The remaining scale-up limitations are now documented directly rather than being left implicit in the codebase:
  - Runtime still needs a game-adapter abstraction before a second game can be added cleanly.
  - The current effect-engine structure is still a fixed-list design around the existing BST-1 effects.
  - Recording/replay still has future work around long-session queueing and streaming efficiency.
  - Settings/profile persistence still needs atomic writes and schema-versioned migration support.
  - Release automation, installer/signing/publication flow, and broader post-incident delivery tooling are still incomplete.

Stage 25A does not change runtime behavior, diagnostics report behavior, parser / `VehicleState` behavior, recording/replay behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, or physical-validation boundaries.

## Stage 25B Durable Quality Gates

Stage 25B is an engineering-hardening stage layered on top of the Stage 25A documentation baseline.

Stage 25B repository baseline:

- Build warnings now fail by default.
- The serial verification path is documented as the repository baseline.
- A Windows GitHub Actions workflow now runs restore, build, test, format verification, and launch preflight.
- A local one-off property escape hatch exists for warning investigation without weakening repository policy.

Stage 25B does not change runtime behavior, diagnostics report behavior, parser / `VehicleState` behavior, recording/replay behavior, ASIO/BST-1 runtime behavior, P-HPR HID/report behavior, persistence formats, or physical-validation boundaries.

## Stage 25C Runtime Game-Telemetry Adapter Seam

Stage 25C introduces the first explicit runtime seam for future multi-game support without claiming actual multi-game product support yet.

Stage 25C architecture result:

- `HapticPipelineCoordinator` now depends on a generic `IGameTelemetryAdapter` contract.
- The active game adapter now owns:
  - packet parsing,
  - packet descriptor publication for diagnostics,
  - `VehicleState` application/state retention.
- F1 25 remains the default shipped implementation through `F125GameTelemetryAdapter`.
- Runtime packet-result status is now expressed through a game-agnostic core parse-status enum instead of the F1 25-specific enum.

Stage 25C deliberately does not add a second game, app-level game selection, game-specific profile branching, or effect/plugin registration. It narrows the main coordinator boundary first so later game additions have a smaller blast radius.

## Stage 25D App-Side Game Telemetry Catalog Baseline

Stage 25D moves the production app path one step further toward future multi-game support without claiming a second shipped game.

Stage 25D architecture result:

- The app now owns the selected-game composition path through `GameTelemetryCatalog`.
- App settings now persist a normalized `SelectedGameId`.
- `MainWindow` now creates `HapticPipelineCoordinator` with an explicit adapter from the catalog instead of relying on the runtime default in the normal app path.
- Persisted-settings status/diagnostics now include the selected game identity.

Stage 25D deliberately does not add a visible game-selection UI, a second game adapter, game-specific recordings metadata, or game-specific profile branching. It establishes the settings/composition baseline first.

## Stage 25E Explicit Adapter Composition

Stage 25E completes the first composition cleanup that Stage 25C and 25D were preparing for.

Stage 25E architecture result:

- `HapticPipelineCoordinator` now requires an explicit `IGameTelemetryAdapter`.
- The runtime assembly no longer references `HapticDrive.Asio.Telemetry.F1_25`.
- App/test callers now compose the runtime with explicit adapters instead of relying on a hidden runtime fallback.
- Runtime project guardrails now protect that dependency direction.

Stage 25E deliberately does not add a second game, a visible game picker, or game-specific recordings/profile partitioning. It removes hidden composition behavior first so future work stays explicit.

## Stage 25F Effect-Engine Extensibility Seam

Stage 25F reduces the central orchestration pressure inside the BST-1 audio effect engine without changing its public behavior.

Stage 25F architecture result:

- `HapticEffectEngine` now owns the current effect set through an internal registered-slot seam.
- Reset, `VehicleState` update, effect render, peak aggregation, and mixer-input collection now flow through shared slot orchestration instead of repeated per-effect code paths.
- The road-texture gear-pulse hook remains explicit, but it now targets the registered road-texture slot rather than a separate hand-managed field/buffer pair.
- The public surfaces stay stable: `HapticEffectEngineOptions`, `HapticEffectEngineSnapshot`, profile records, diagnostics/report text, and existing callers still expose the shipped BST-1 effect set explicitly.

Stage 25F deliberately does not add new effects, a plugin marketplace, dynamic profile-driven effect discovery, or broader data-driven diagnostics/UI. It narrows the internal engine seam first so future effect additions have a smaller runtime blast radius.

## Stage 25G Replay-File Streaming Seam

Stage 25G reduces one of the larger recording/replay scaling costs without changing the replay behavior that the app and runtime already depend on.

Stage 25G architecture result:

- `TelemetryRecordingFile` now has an open-reader seam that validates header metadata once, then reads packet records sequentially from the underlying `.hdrec` stream.
- `TelemetryReplayService.ReplayFileAsync` now replays packet streams directly from disk through that reader instead of first loading the whole recording into an in-memory `TelemetryRecording`.
- `TelemetryRecordingFile.LoadAsync` now reuses the same reader path, so whole-recording loads and streaming replay share one packet-validation implementation.
- Replay still preserves raw payload bytes, packet ordering, relative timing, and corruption checks, including truncated-record and trailing-byte failures.

Stage 25G deliberately does not change the live recording writer queue model, add seek/index/query APIs for large recordings, or change the `.hdrec` format version. It narrows the replay/load seam first so future recording-library scaling work has a cleaner base.

## Stage 25H Live Recording Queue/Backpressure Hardening

Stage 25H hardens the live recording path itself after Stage 25G reduced replay-side memory pressure.

Stage 25H architecture result:

- `TelemetryRecordingService` now uses a bounded channel instead of an unbounded queue for background packet-to-disk handoff.
- The live telemetry path still remains non-blocking: when the recording queue is full, packets are dropped explicitly rather than stalling the caller.
- Recording snapshots now expose queue capacity, current queued packets, and dropped-packet count so runtime/app callers can see overload instead of inferring it from a future file discrepancy.
- App/runtime recording status now surfaces bounded-queue/drop warnings through the existing recording status text instead of requiring a new diagnostics page.

Stage 25H deliberately does not add large-recording browse/index APIs, retry/recovery policies for dropped packets, or a new `.hdrec` format version. It makes the live capture path bounded and observable first.

## Stage 25I Atomic Persistence Hardening

Stage 25I hardens the persisted JSON save path after the earlier recording/replay scale-up work.

Stage 25I architecture result:

- `HapticDrive.Asio.Core.Persistence.AtomicFileWriter` now provides a shared same-directory temp-file plus replace/move write path for persisted JSON documents.
- `AppSettingsStore`, `HapticProfileStore`, and `PhprEffectProfileStore` now save through that shared atomic path instead of writing directly to the final file.
- Existing on-disk files are now preserved if a save attempt fails after the temp file is created but before the final replace completes.
- `AppSettings` now persists an explicit `Version` marker so future migrations have a stable schema anchor even though current loading remains backward-compatible with older version-less files.

Stage 25I deliberately does not add a broad persistence-migration engine, cross-file transactional saves, backup retention/history, or new profile format versions. It hardens the current single-file save path first so future migration work starts from a safer baseline.

## Stage 25J Recording Library Health Summaries

Stage 25J improves the operator-facing recording library without changing the `.hdrec` format or replay behavior.

Stage 25J architecture result:

- `TelemetryRecordingFile.LoadSummaryAsync(...)` now performs a streamed packet-summary pass after header validation instead of stopping at header metadata only.
- Recording summaries now include duration, payload-byte total, missing-sequence count, and largest sequence gap without allocating the full packet list in memory.
- `RecordingLibraryManager.LoadAsync(...)` now surfaces that richer summary data in the app's recording-library display text and detail text.
- Sequence-gap visibility now gives the operator a first-pass signal for dropped/missing captured packets in completed recordings, complementing the live bounded-queue/drop diagnostics added in Stage 25H.

Stage 25J deliberately does not add packet-type histograms, random-access seek indexes, sidecar metadata caches, or full query/search/filter workflows. It strengthens the first-pass library summary first so deeper browse/index work has a more useful baseline.

## Stage 25K Release Packaging Automation

Stage 25K adds a repeatable packaging path on top of the earlier quality-gate work.

Stage 25K architecture result:

- `Publish-HapticDrive.ps1` now provides a repo-native publish path for the WPF app:
  - runtime-specific restore,
  - `Release` publish for `win-x64`,
  - publish output under `artifacts/publish/`,
  - zip artifact under `artifacts/release/`.
- `Publish-HapticDrive.cmd` provides the same path through a simple wrapper for local Windows shell usage.
- `.github/workflows/package.yml` now reruns restore/build/test/format/launch-preflight, then publishes and uploads the `win-x64` zip artifact.
- `artifacts/` is now treated as generated output and ignored from source control.

Stage 25K deliberately does not add MSI/installer generation, code signing, GitHub Releases publication, delta updates, or automated install/uninstall smoke tests. It establishes a real publish artifact path first so later delivery work has a stable base.

## Stage 25L Support Bundle Automation

Stage 25L turns the existing diagnostics-report seam into a repeatable local support artifact without changing runtime ownership or adding a parallel diagnostics pipeline.

Stage 25L architecture result:

- `SupportBundleExporter` now packages a private local zip under `local-validation-results/support-bundles/`.
- The export reuses `DiagnosticsStatusPresentation` as the single diagnostics-report source, then writes:
  - `diagnostics-report.txt`,
  - `diagnostics-summary.json`,
  - `manifest.json`,
  - `README.txt`.
- `AdvancedDiagnosticsView` remains an event-forwarding shell seam only; it now exposes an `Export Support Bundle` action beside refresh/copy, while `MainWindow` remains the executor that gathers selected-game metadata, builds the presentation, and runs the export.
- The bundle is intentionally sanitized and documentation-oriented:
  - no raw telemetry captures,
  - no private HID paths,
  - no serial numbers,
  - no hardware output.

Stage 25L deliberately does not add automatic log harvesting, opt-in recording attachment workflows, remote upload/report submission, or installer/runtime dump packaging. It establishes a safe local operator-support artifact first so later support tooling can build on a stable export format.

## Stage 25M Persistence Migration Baseline

Stage 25M extends the earlier atomic-save work by adding one shared migration-planning seam for versioned persisted documents.

Stage 25M architecture result:

- `VersionedDocumentMigration` now provides:
  - declared-version discovery from persisted JSON,
  - version-0 legacy upgrade planning,
  - unsupported-version classification,
  - shared migration messages for callers.
- `AppSettingsStore`, `HapticProfileStore`, and `PhprEffectProfileStore` now all use that same planner instead of each store deciding legacy version handling independently.
- Versionless or version-0 persisted documents now upgrade to the current schema baseline with explicit migration reporting rather than failing or silently diverging by store.
- Future persisted-format work now has one stable place to extend first:
  - new per-version upgrade steps,
  - shared unsupported-version policy,
  - shared migration diagnostics.

Stage 25M deliberately does not add cross-file transactional migrations, backup retention/history, automatic rollback across multiple documents, or broader persisted-artifact repair orchestration. It establishes the first shared migration seam so later schema growth starts from one explicit baseline.

## Stage 25N Recording-Library Query Baseline

Stage 25N extends the earlier recording-library summary work with a small, generic query seam instead of jumping straight to game-specific indexing.

Stage 25N architecture result:

- `TelemetryRecordingFile.LoadSummaryAsync(...)` still performs one streamed summary pass, but it now also reports:
  - first sequence number,
  - last sequence number,
  - approximate packet rate.
- `TelemetryRecordingSummary` remains game-agnostic. Stage 25N deliberately does not pull F1 25 packet parsing or packet-kind decoding into the recording assembly.
- `RecordingLibraryManager.LoadAsync(...)` now shapes that richer generic metadata into:
  - sequence-range text,
  - approximate packet-rate text,
  - a simple search corpus per library item.
- `RecordingLibraryManager.Filter(...)` adds a narrow in-memory query seam for the already-loaded library list:
  - whitespace token splitting,
  - all-terms match behavior,
  - filename/metadata/health text matching.
- `TelemetryUdpView` now exposes a filter textbox plus clear action for the recording library, while `MainWindow` remains the executor that loads the library, applies the filter, preserves selection when possible, and owns replay/rename/delete behavior.

Stage 25N deliberately does not add sidecar metadata indexes, random-access packet browsing, packet-type histograms, game-specific recording analysis, or a new `.hdrec` format version. It adds the first query/filter seam so later browse/index work starts from richer generic summaries and a stable UI path.

## Stage 25O Persistence Recovery Baseline

Stage 25O extends the earlier atomic-save plus migration work with one small recovery rung: single-file last-known-good fallback.

Stage 25O architecture result:

- `DocumentBackupFile` now provides a shared persisted-backup path convention plus backup refresh helper in `HapticDrive.Asio.Core.Persistence`.
- `AppSettingsStore`, `HapticProfileStore`, and `PhprEffectProfileStore` now refresh a `.lastgood` backup after each successful save.
- Those same stores now attempt recovery from the backup snapshot when the primary document is:
  - missing,
  - corrupt,
  - unsupported.
- Recovery remains intentionally single-file and local to each store:
  - no cross-file transaction coordinator,
  - no multi-document rollback ordering,
  - no retained history chain.
- Existing validation and migration seams still own content safety:
  - primary and backup documents both pass through the same migration path,
  - primary and backup documents both pass through the same sanitization/validation path,
  - recovery reuses those existing result surfaces instead of inventing a second persistence pipeline.

Stage 25O deliberately does not add backup retention/history, cross-file recovery orchestration, transactional restore points, or background repair of every persisted artifact in one pass. It gives the production app one practical last-known-good fallback layer first so broader persistence repair can build on a stable baseline.

## Stage 25P Effect-Activity Summary Seam

Stage 25P starts reducing app-side effect-surface coupling by generalizing the active-summary path before attempting a broader tuning/profile/UI rewrite.

Stage 25P architecture result:

- `HapticEffectEngineSnapshot` now exposes a generic `ActivityItems` list alongside the existing typed per-effect snapshots.
- The activity list is built at the engine snapshot boundary, so presenter/report callers can consume a stable effect-summary seam without maintaining their own parallel hardcoded effect-name lists.
- `EffectsStatusPresenter` and `RoutingMixerStatusPresenter` now consume that generic activity list for their active-effect summary text, while keeping the detailed typed BST-1 cards and state panels unchanged.
- The result narrows one real public-surface coupling point:
  - future effect additions no longer need presenter-local summary-text edits in every active-effect summary surface,
  - future effect additions still need explicit work in options, profiles, tuning UI, and detailed per-effect diagnostics.

Stage 25P deliberately does not add dynamic tuning-card generation, profile-driven effect discovery, data-driven effect editors, or a broader snapshot-schema rewrite. It clears the active-summary path first so later effect-surface generalization can build from one shared activity seam.

## Stage 25Q Release Artifact Smoke Baseline

Stage 25Q hardens the existing packaging path by proving that the produced publish output and uploaded release zip are structurally usable, instead of only proving that publish completed.

Stage 25Q architecture result:

- `Publish-HapticDrive.ps1` now drives restore and publish deterministically for the scripted packaging path:
  - explicit restore remains available,
  - publish always runs with `--no-restore`,
  - scripted restore/publish disable NuGet audit lookups so the repo-local packaging path stays deterministic in offline or sandboxed environments.
- `Test-ReleaseArtifact.ps1` adds one repo-native release validation seam:
  - it verifies the expected launchable app files exist in the publish directory,
  - it verifies the expected zip artifact exists,
  - it extracts that zip and verifies the same required payload exists after extraction.
- `.github/workflows/package.yml` now runs that smoke check before uploading the packaged artifact, so CI no longer treats "zip exists" as sufficient proof by itself.
- The result narrows one practical delivery blind spot:
  - local packaging now has an explicit post-publish validation command,
  - CI packaging now fails before artifact upload if the produced zip is structurally incomplete.

Stage 25Q deliberately does not add MSI/installer generation, code signing, GitHub Releases publication, install/uninstall automation, or full runtime launch-under-package validation. It establishes a first structural artifact-smoke baseline so later delivery work can build on a verified package shape instead of only a successful publish exit code.

## Stage 25R Release Manifest and Checksum Baseline

Stage 25R builds directly on the Stage 25Q structural smoke check by making each packaged release artifact self-describing and integrity-checkable.

Stage 25R architecture result:

- `Publish-HapticDrive.ps1` now emits two release-metadata artifacts alongside the existing zip:
  - a `.sha256` file for the packaged zip,
  - a `.manifest.json` file describing the package name, runtime, configuration, generated timestamp, required files, zip size, and zip hash.
- `Test-ReleaseArtifact.ps1` now validates that metadata seam against the actual artifact:
  - checksum content must match the computed zip hash,
  - manifest package/runtime/file-name/hash values must match the produced zip,
  - the earlier publish/zip/extract required-file validation still runs unchanged.
- `.github/workflows/package.yml` now uploads the zip, checksum, and manifest together so the CI artifact carries its own integrity and metadata envelope instead of only the binary payload.
- The result narrows one more delivery gap:
  - packaged output now has a stable machine-readable identity surface for later release automation,
  - consumers can verify the artifact without reverse-engineering the publish directory or recomputing ad hoc metadata by hand.

Stage 25R deliberately does not add code signing, MSI/installer generation, GitHub Releases publication, changelog/release-note automation, or install/uninstall validation. It establishes a minimal integrity-plus-metadata envelope first so later delivery automation can build on stable packaged artifact descriptors.

## Stage 25S Release Staging Command Baseline

Stage 25S turns the earlier packaging pieces into one repeatable local release-preparation path instead of leaving the operator to manually remember and rerun each step in the right order.

Stage 25S architecture result:

- `Prepare-ReleaseArtifact.ps1` now owns one repo-native release-staging orchestration seam:
  - solution restore with deterministic offline-friendly audit policy,
  - targeted app-project runtime restore for `win-x64` publish assets,
  - build with warnings as errors,
  - test,
  - format verification,
  - launch preflight,
  - publish,
  - release smoke check,
  - final staging-folder assembly.
- `Prepare-ReleaseArtifact.cmd` provides the same execution-policy-friendly wrapper style used by the other repo-native PowerShell entry points.
- Final staged output now lands under `artifacts/staged-release/<package-runtime>/`, containing the exact zip, checksum, and manifest produced by the publish path.
- The result narrows another practical delivery gap:
  - local release preparation is now one command instead of a hand-run checklist,
  - the runtime-specific restore requirement for packaged publish is now encoded in the tool instead of being tribal knowledge.

Stage 25S deliberately does not add installer generation, code signing, GitHub Releases publication, release-note authoring, or install/uninstall validation. It establishes one deterministic local staging command first so later delivery automation can build on a verified release-preparation workflow instead of an operator memory test.

## Stage 25T Release Summary Artifact Baseline

Stage 25T continues the delivery-hardening path by giving each release artifact set one human-readable handoff document instead of leaving publication context scattered across scripts, manifests, and operator memory.

Stage 25T architecture result:

- `Publish-HapticDrive.ps1` now emits a Markdown release summary alongside the existing zip, checksum, and JSON manifest.
- That summary carries a minimal release-publication envelope:
  - package/runtime/configuration,
  - generated UTC timestamp,
  - current commit hash and subject when git metadata is available,
  - staged artifact file names,
  - zip size and SHA-256,
  - required app payload file list,
  - the repo-native publish and smoke-check commands used to validate the package.
- `Test-ReleaseArtifact.ps1` now validates that the release summary exists and that its key identity/integrity fields match the actual produced artifact set.
- `.github/workflows/package.yml` and `Prepare-ReleaseArtifact.ps1` now carry that summary forward with the rest of the release artifact set, so CI and local staged-release output both produce the same handoff surface.
- The result narrows another small but real delivery gap:
  - release-preparation output is now easier to hand off for manual publication or review,
  - the summary is derived from the same actual artifact metadata that the smoke script validates, rather than being a manually maintained note.

Stage 25T deliberately does not add signed release publication, GitHub Releases API automation, changelog synthesis across multiple commits, installer generation, or install/uninstall validation. It establishes one trustworthy release-summary artifact first so later publication automation can build on a stable human-readable handoff document.

## Stage 25U Selected-Recording Packet Histogram Baseline

Stage 25U returns briefly to the recording-library quality stream by adding one narrow, deeper inspection surface for selected recordings without changing the generic recording core or replay format.

Stage 25U architecture result:

- The app layer now owns an explicit `RecordingPacketHistogramAnalyzer` seam for on-demand selected-recording analysis.
- That analyzer intentionally stays in `HapticDrive.Asio.App`:
  - it loads a selected recording on demand,
  - it inspects packet payloads only when the recording metadata says `F1 25`,
  - it uses the existing F1 25 packet-header parser/definitions to build a packet-ID histogram,
  - it reports ignored unknown packet IDs and invalid packet headers separately.
- `MainWindow` now caches that analysis by recording path and populates the Telemetry / UDP detail text lazily after selection, instead of pushing more work into the initial library refresh path.
- The generic recording assembly remains game-agnostic:
  - `.hdrec` bytes, summary loading, replay, and the recording-file APIs are unchanged,
  - no F1-specific packet-type concepts were moved into `HapticDrive.Asio.Recording`.
- The result narrows one more recording-library gap:
  - operators can now inspect the packet mix of a selected F1 25 recording directly from the app,
  - large-library list refresh still stays focused on the earlier generic streamed summaries rather than eagerly parsing every recording for game-specific details.

Stage 25U deliberately does not add random-access packet browsing, persistent sidecar indexes, cross-game histogram analyzers, packet-content drill-down views, or a new `.hdrec` format version. It adds one on-demand selected-recording inspection seam first so deeper browse/index work can build on a visible product surface without breaking the generic recording boundary.

## Stage 25V Selected-Recording Packet Preview Baseline

Stage 25V extends the Stage 25U selected-recording analysis seam by adding one first-pass packet preview instead of stopping at aggregate histogram counts only.

Stage 25V architecture result:

- `RecordingPacketHistogramAnalyzer` now produces both:
  - aggregate F1 25 packet-ID histogram text,
  - a short selected-recording packet preview sample.
- The preview intentionally stays narrow and on-demand:
  - it captures only the first few packets from the selected recording,
  - it shows sequence number, relative time, packet kind/ID, and payload size,
  - it reports unknown packet IDs and invalid headers in the same preview stream when encountered.
- The app still computes and caches that analysis only after the user selects a recording, so list refresh behavior remains unchanged.
- The recording/core boundary remains intact:
  - no new `.hdrec` metadata or sidecar format was added,
  - no game-specific preview schema was moved into `HapticDrive.Asio.Recording`,
  - replay and summary-loading behavior remain unchanged.
- The result narrows one more inspection gap:
  - operators can now see both the mix of packet types and a first-pass view of packet order/timing for the selected recording,
  - later packet-browser work can build on an already-visible selected-recording inspection surface instead of appearing from nowhere.

Stage 25V deliberately does not add random-access packet browsing, packet-body decode views, persistent indexes, cross-game preview analyzers, or a new `.hdrec` format version. It adds a small preview rung first so deeper browse/index work can evolve from a proven selected-recording UI path.

## Stage 25W Retained Backup History Baseline

Stage 25W returns to the persistence-hardening stream and adds one more recovery rung without jumping to full transactional rollback.

Stage 25W architecture result:

- `DocumentBackupHistory` now owns a shared retained-history helper for persisted single-document stores:
  - it writes timestamped `.lastgood` snapshots into a sibling history directory,
  - it keeps only a small bounded rolling set,
  - it stays independent from the atomic write path so save semantics remain simple and local.
- `AppSettingsStore`, `HapticProfileStore`, and `PhprEffectProfileStore` now all share the same fallback ladder:
  - try the primary document,
  - try the single `.lastgood` snapshot,
  - then iterate newest-first retained-history snapshots.
- Recovery remains intentionally single-document scoped:
  - no cross-file restore point is claimed,
  - no multi-document transaction or coordinated rollback is introduced,
  - save success for one persisted file still does not imply consistent restore points across every persisted artifact.
- The result materially improves production resilience for corruption scenarios that damage more than one immediate file copy while keeping the persistence ownership model narrow and explicit.

Stage 25W deliberately does not add cross-file rollback orchestration, transactional restore points, background repair of all persisted artifacts, or cloud/remote backup behavior. It adds a small retained-history rung first so broader persistence recovery can build on a proven local fallback chain.

## Stage 25X Selected-Recording Detail Clipboard Baseline

Stage 25X returns to the selected-recording tooling seam and adds one operator-support rung without widening the generic recording boundary.

Stage 25X architecture result:

- `RecordingLibraryDetailFormatter` now owns a second output shape alongside the on-screen detail text:
  - the app can build one deterministic clipboard report for the selected recording,
  - that report includes the selected file name/path, the existing library summary text, and the same detailed analysis text already shown in the UI.
- The Telemetry / UDP page now exposes an explicit copy action for selected recordings:
  - it reuses the existing on-demand/cached analysis path,
  - it can populate analysis before copying when the selected recording has not been analyzed yet,
  - it does not introduce a second packet-analysis implementation just for export/copy.
- The stage stays app-local and intentionally shallow:
  - no new `.hdrec` metadata or sidecar cache is introduced,
  - no raw packet attachment/export flow is added,
  - no packet-body decode view or random-access browse surface is added.
- The result materially improves production supportability because operators can now move selected-recording context into bug reports and notes without manually retyping or screenshotting the detail panel.

Stage 25X deliberately does not add persistent recording indexes, packet-body drill-down, random-access packet browsing, raw capture export, or cross-game detailed analyzers. It adds a copyable inspection artifact first so later browse/support tooling can build on a proven selected-recording detail contract.

## Stage 25Y Support-Bundle Selected-Recording Detail Baseline

Stage 25Y connects the selected-recording inspection lane to the existing local support-bundle lane without introducing raw-capture attachment behavior.

Stage 25Y architecture result:

- `SupportBundleExportInputs` and `SupportBundleExporter` now carry one optional selected-recording detail artifact:
  - when present, the bundle includes `selected-recording-detail.txt`,
  - manifest and summary content now record that optional attachment in a sanitized way,
  - the bundle still remains text-only and local-only.
- The export path reuses the existing selected-recording formatter/analysis contract instead of building a second support-only packet summary:
  - the app can populate selected-recording analysis on demand before export,
  - the support bundle therefore carries the same selected-recording detail shape the UI and clipboard path already use.
- Privacy and hardware-safety boundaries remain unchanged:
  - no raw `.hdrec` file is attached,
  - no packet payload bytes are exported separately,
  - no extra device-path or raw-hardware data is added to the bundle.
- The result improves production supportability because a local support bundle can now carry the operator's currently inspected recording context alongside the diagnostics report, reducing context loss between issue observation and artifact export.

Stage 25Y deliberately does not add raw recording attachment, remote upload, packet-body decode exports, automatic log harvesting, or broader incident bundle packaging. It adds one selected-recording detail attachment first so later support tooling can build on a proven sanitized bundle contract.

## Stage 25Z Selected-Recording Detail Export Baseline

Stage 25Z extends the same selected-recording inspection lane with one standalone local artifact path instead of requiring clipboard or support-bundle workflows.

Stage 25Z architecture result:

- `SelectedRecordingDetailExporter` now owns a narrow local export path for selected-recording inspection text:
  - output lands under `local-validation-results/recording-inspections/`,
  - file names are timestamped and recording-name-based,
  - export remains sanitized text only.
- The Telemetry / UDP page now exposes a direct export action for the selected recording detail:
  - it reuses the existing selected-recording formatter and on-demand analysis cache,
  - it does not build a second recording-analysis/export shape,
  - it gives operators one saved local artifact without needing to generate a full support bundle.
- Privacy and format boundaries remain unchanged:
  - no raw `.hdrec` payload bytes are exported,
  - no new recording sidecar/index schema is introduced,
  - no packet-body decode data is added.
- The result improves production supportability and operator workflow by giving recording inspection a first-class local artifact path that matches the repo's existing `local-validation-results` pattern.

Stage 25Z deliberately does not add raw capture attachment, packet-body export formats, sidecar recording indexes, remote upload, or random-access packet browsing. It adds one standalone local inspection artifact first so later recording/support tooling can build on a proven sanitized export contract.

## Stage 25AA Structured Recording Inspection Seam

Stage 25AA turns the selected-recording inspection path into a more explicit app-side data seam without changing the visible Telemetry / UDP workflow.

Stage 25AA architecture result:

- `RecordingPacketHistogramAnalyzer` now exposes a structured inspection result in addition to the existing formatted-text path:
  - histogram entries are explicit typed items,
  - preview rows are explicit typed items,
  - unsupported/unavailable states are explicit result statuses instead of only ad hoc strings.
- `RecordingPacketInspectionFormatter` now owns the current text rendering contract:
  - the UI, clipboard export, support-bundle attachment, and standalone local export can continue to consume stable text,
  - later browse/index features can consume the structured inspection result directly without reverse-parsing strings.
- The stage stays deliberately app-local:
  - no new `.hdrec` format metadata is added,
  - no game-specific structured inspection model is moved into the generic recording assembly,
  - no packet-browser UI is introduced yet.
- The result improves future extensibility because richer selected-recording workflows can now evolve from one typed inspection seam instead of continuing to grow around one formatted string blob.

Stage 25AA deliberately does not add packet-body decode views, persistent recording indexes, raw capture exports, cross-game analyzers, or random-access packet browsing. It establishes the structured inspection seam first so those deeper features can build on a cleaner app-side contract.

## Stage 25AB Structured BST-1 Effect Summary Seam

Stage 25AB returns to the broader effect-extensibility stream and removes one more app-side fixed-list seam without changing shipped haptic behavior.

Stage 25AB architecture result:

- BST-1 presenter/report effect summaries now have one shared app-side typed contract:
  - `Bst1EffectSummarySnapshot` carries the diagnostics-oriented effect state,
  - `Bst1EffectSummaryItem` carries per-effect key, display name, enable state, and active state.
- `Bst1EffectSummaryFormatter` now owns the current summary text rendering contract:
  - diagnostics text still renders in the existing order and style,
  - routing / mixer effect text still renders in the existing order and style,
  - later effect additions can target one shared summary seam instead of multiple presenter-local hardcoded strings.
- `MainWindow` now builds that summary snapshot once from the `HapticEffectEngineSnapshot` path and passes it into both diagnostics and routing presenters, reducing repeated fixed-list assembly in the shell layer.
- The stage stays deliberately narrow:
  - no tuning UI rewrite,
  - no profile-schema change,
  - no dynamic effect registration in the WPF layer,
  - no change to the runtime effect-engine ownership introduced earlier in Stage 25F and Stage 25P.
- The result improves production maintainability because future BST-1 effect growth now has one cleaner presenter/report seam instead of multiple drift-prone string-building call sites.

Stage 25AB deliberately does not add data-driven effect cards, plugin-style effect metadata, dynamic profile editors, or broader effect-schema generalization across persisted settings. It removes one more presenter/report coupling point first so those later steps can build on a stronger app-side contract.

## Stage 25AC Effects-Page Status Summary Seam

Stage 25AC continues the same effect-extensibility stream by removing one more app-side fixed-list effect summary without changing the visible WPF card layout or runtime haptic behavior.

Stage 25AC architecture result:

- The Effects page now has a typed fallback summary seam in addition to the generic activity list added earlier:
  - `EffectStatusSummaryItem` carries per-effect page-summary text,
  - `EffectsPageStatusSummaryFormatter` owns the ordered fallback summary rendering.
- `EffectsStatusSnapshot` can now carry both:
  - generic `ActivityItems` for later dynamic/new-effect summaries when the engine provides them,
  - typed fallback `SummaryItems` so the shipped BST-1 set no longer depends on one presenter-local hardcoded string.
- `MainWindow` now builds the page-summary items once from `HapticEffectEngineSnapshot` before handing them to `EffectsStatusPresenter`, reducing yet another place where future effect additions would otherwise require bespoke summary wiring.
- The stage stays intentionally narrow:
  - no WPF card-generation rewrite,
  - no tuning/profile schema changes,
  - no change to active-effect counting or effect-engine runtime sequencing,
  - no new plugin/metadata surface for effect registration.
- The result improves maintainability because app-side effect summary/report surfaces now converge on typed contracts instead of drifting across presenters.

Stage 25AC deliberately does not add data-driven effect cards, dynamic effect registration in the WPF layer, plugin-style effect metadata, or broader effect-schema generalization across persisted settings and tuning UI. It removes one more presenter-local status seam first so those larger changes can build on a cleaner app-side baseline.

## Stage 25AD Audio-Profile BST-1 Effect Control Seam

Stage 25AD keeps working down the same effect-extensibility backlog by reducing one of the remaining flat app-side effect-control contracts instead of jumping straight to a full dynamic UI/schema rewrite.

Stage 25AD architecture result:

- The audio-profile control path now has a typed BST-1 effect-control contract:
  - `Bst1AudioProfileEffectControlValues` groups effect-side slider/toggle values,
  - `Bst1AudioProfileEffectControlTextValues` groups effect-side display text,
  - `Bst1AudioProfileEffectControlApplicationSnapshot` gives the app one effect-focused profile-hydration seam.
- `Bst1AudioProfileEffectControlSnapshotBuilder` now owns the BST-1 effect portion of profile mapping:
  - profile-to-controls hydration,
  - effect-side display-text shaping,
  - control-input-to-profile-effects application.
- `AudioProfileControlSnapshotBuilder` now composes that effect-specific seam with the still-separate profile name, mixer, and safety values instead of directly owning every effect field mapping itself.
- `MainWindow` still renders the same WPF controls and persists the same JSON schema, but it now consumes grouped effect-control values/text instead of a completely flat effect contract.
- The stage stays intentionally narrow:
  - no persisted schema change,
  - no WPF layout rewrite,
  - no dynamic control generation,
  - no runtime haptic-behavior change.
- The result improves maintainability because future BST-1 effect additions now have one cleaner profile-control seam to extend before any larger UI/schema generalization happens.

Stage 25AD deliberately does not add data-driven effect editors, plugin-style effect metadata, dynamic WPF control generation, or broader profile-schema generalization across every persisted/settings surface. It narrows the audio-profile control seam first so those larger changes can build on a less brittle app-side contract.

## Stage 25AE Audio-Profile BST-1 Effect Input Seam

Stage 25AE continues the same profile-control cleanup by reducing the remaining flat effect-input contract, not by changing the current control layout or persisted schema.

Stage 25AE architecture result:

- The audio-profile control path now has a typed BST-1 effect-input record:
  - `Bst1AudioProfileEffectControlInputs` groups the effect-side slider/toggle input values captured from WPF,
  - `AudioProfileControlInputs` now composes that grouped effect input with the still-separate profile name, mixer, and safety fields.
- `Bst1AudioProfileEffectControlSnapshotBuilder.BuildProfileEffects(...)` now consumes the grouped effect-input contract instead of the broader audio-profile input bag.
- `MainWindow.BuildCurrentAudioProfileControlInputs()` still reads the same WPF controls, but it now hands the builder one explicit grouped effect-input object instead of one giant flat effect list.
- The stage stays intentionally narrow:
  - no persisted profile schema change,
  - no WPF layout rewrite,
  - no dynamic control generation,
  - no runtime haptic-behavior change.
- The result improves maintainability because the profile-control seam now converges around grouped effect-side input, value, and text contracts instead of leaving one last large flat effect-input path behind.

Stage 25AE deliberately does not add data-driven effect editors, plugin-style effect metadata, dynamic WPF control generation, or broader persisted-profile/settings schema generalization. It removes one more brittle app-side input contract first so later UI/schema work can build on a cleaner baseline.

## Stage 25AF Effects-Status Snapshot Seam

Stage 25AF returns to the live Effects page and removes one more effect-growth hotspot by moving the status snapshot assembly out of `MainWindow` and behind a dedicated app-side builder.

Stage 25AF architecture result:

- The full runtime/options-to-status mapping for the Effects page now lives in `EffectsStatusSnapshotBuilder` instead of a long `MainWindow` block.
- The builder owns:
  - typed `EffectsStatusSnapshot` construction from `HapticEffectEngineSnapshot` plus `HapticEffectEngineOptions`,
  - slip-telemetry significance classification for the existing presenter contract,
  - ordered structured fallback summary items for the existing Effects-page status text.
- `MainWindow` now keeps the same visible boundary:
  - it still pulls the live pipeline snapshot,
  - it still passes the result to `EffectsStatusPresenter`,
  - it still applies the final presentation through `EffectsViewControl`.
- The stage stays intentionally narrow:
  - no WPF layout rewrite,
  - no persisted profile or settings schema change,
  - no effect tuning/default change,
  - no runtime haptic-behavior change.
- The result improves maintainability because future BST-1 effect additions now have one cleaner app-side status-assembly seam instead of requiring another long `MainWindow` mapping edit just to reach the existing presenter/view path.

Stage 25AF deliberately does not add data-driven effect cards, plugin-style effect metadata, dynamic WPF control generation, or broader effect-schema generalization across profiles/settings/diagnostics. It narrows one more app-side assembly seam first so those later changes can build on a less tangled status path.

## Stage 25AG Routing/Mixer Status Snapshot Seam

Stage 25AG continues the same effect-extensibility cleanup by removing the Routing / Mixer page's remaining large status-assembly block from `MainWindow` and by centralizing the shared BST-1 effect-summary snapshot build step.

Stage 25AG architecture result:

- The Routing / Mixer page now gets its typed `RoutingMixerStatusSnapshot` through `RoutingMixerStatusSnapshotBuilder` instead of a long `MainWindow` mapping block.
- The new builder owns:
  - BST-1 routing/effect enabled-active mapping from `HapticEffectEngineSnapshot`,
  - active-effect count and generic activity-item handoff,
  - routing-summary input shaping for the existing presenter contract.
- BST-1 effect-summary snapshot creation now also lives in one shared app-side builder:
  - `Bst1EffectSummarySnapshotBuilder` replaces the former `MainWindow`-local fixed-list summary assembly,
  - both routing and diagnostics now consume the same summary snapshot construction seam.
- `MainWindow` still keeps the same visible boundary:
  - it still gathers the live runtime/device state,
  - it still passes the resulting snapshot to `RoutingMixerStatusPresenter`,
  - it still applies the final presentation through `RoutingMixerViewControl`.
- The stage stays intentionally narrow:
  - no WPF layout rewrite,
  - no persisted profile or settings schema change,
  - no routing rule or priority change,
  - no runtime haptic-behavior change.
- The result improves maintainability because future BST-1 effect additions now have one cleaner routing-status seam and one shared BST-1 effect-summary seam instead of requiring additional `MainWindow`-local fixed-list mapping edits.

Stage 25AG deliberately does not add data-driven effect cards, plugin-style effect metadata, dynamic WPF control generation, or broader effect-schema generalization across profiles/settings/diagnostics. It removes one more app-side mapping hotspot first so those later changes can build on a cleaner routing/reporting baseline.

## Stage 25AH BST-1 Diagnostics Section Seam

Stage 25AH continues the same effect-extensibility cleanup by removing the remaining inline BST-1 diagnostics-section assembly from `MainWindow`.

Stage 25AH architecture result:

- The Advanced / Diagnostics BST-1-specific section now gets one focused app-side seam through `Bst1DiagnosticsSectionBuilder`.
- The new builder owns:
  - shared BST-1 effect-summary snapshot reuse,
  - BST-1 slip/lock diagnostics text assembly,
  - mixer/safety summary text assembly for the diagnostics presenter contract.
- `MainWindow` still keeps the same visible boundary:
  - it still gathers runtime snapshots and road-diagnostics lines,
  - it still builds the broader diagnostics snapshot through `DiagnosticsStatusSnapshotBuilder`,
  - it still applies the final presentation through `AdvancedDiagnosticsViewControl`.
- The stage stays intentionally narrow:
  - no WPF layout rewrite,
  - no persisted schema change,
  - no diagnostics report format change beyond equivalent existing content ownership,
  - no runtime haptic-behavior change.
- The result improves maintainability because future BST-1 effect additions now have one more explicit diagnostics seam instead of needing additional inline effect-diagnostics string work inside `MainWindow`.

Stage 25AH deliberately does not add data-driven effect cards, plugin-style effect metadata, dynamic WPF control generation, or broader effect-schema generalization across profiles/settings/diagnostics. It removes one more app-side effect-diagnostics hotspot first so later metadata/schema work can build on a less tangled diagnostics path.

## Stage 25AI Shared BST-1 Effect Catalog Seam

Stage 25AI continues the same effect-extensibility work by centralizing the shipped BST-1 effect metadata that was still duplicated across multiple summary builders and formatters.

Stage 25AI architecture result:

- The app now has one shared BST-1 effect catalog:
  - `Bst1EffectCatalog` defines the current shipped effect keys,
  - carries display labels,
  - owns per-surface ordering for diagnostics, routing, and Effects-page fallback summary flows.
- The catalog is now consumed by the existing app-side effect seams:
  - `Bst1EffectSummarySnapshotBuilder` now gets effect keys/labels from the catalog instead of embedding a fixed list locally,
  - `Bst1EffectSummaryFormatter` now uses catalog-owned diagnostics/routing ordering,
  - `EffectsPageStatusSummaryFormatter` now uses catalog-owned ordering for the Effects-page fallback summary path,
  - `EffectsStatusSnapshotBuilder` now aligns its structured summary-item keys with the shared catalog instead of carrying another duplicate key set.
- The stage stays intentionally narrow:
  - no runtime haptic-behavior change,
  - no WPF layout rewrite,
  - no persisted profile or settings schema change,
  - no dynamic effect registration yet.
- The result improves maintainability because adding or renaming a shipped BST-1 effect now has one clearer metadata seam instead of multiple scattered key/order lists across the app layer.

Stage 25AI deliberately does not turn the full effect surface into a data-driven registry, dynamic WPF control system, or plugin-style schema. It centralizes the remaining duplicated shipped-effect metadata first so those later changes can build on a more coherent baseline.

## Stage 25AJ Audio-Profile View Application Seam

Stage 25AJ continues the same effect-extensibility cleanup by removing the remaining large audio-profile control-application block from `MainWindow` and by letting the extracted page views own their own direct control assignment surface.

Stage 25AJ architecture result:

- Audio-profile hydration now applies grouped control values through the extracted page seams:
  - `ProfilesView.ApplyAudioProfileControlValues(...)` owns profile-name control application,
  - `EffectsView.ApplyAudioProfileEffectControlValues(...)` owns BST-1 effect control application,
  - `EffectsView.ApplyAudioProfileEffectControlText(...)` owns BST-1 effect display-text application,
  - `RoutingMixerView.ApplyAudioProfileMixerControlValues(...)` owns mixer/safety control application,
  - `RoutingMixerView.ApplyAudioProfileMixerControlText(...)` owns mixer/safety display-text application.
- `MainWindow` still keeps the same visible ownership boundary:
  - it still builds the profile application plan through `AudioProfileControlSnapshotBuilder`,
  - it still owns `_updatingTuningUi` sequencing and the broader profile load/apply workflow,
  - it still owns runtime application, persistence execution, and event handling.
- The stage stays intentionally narrow:
  - no persisted profile schema change,
  - no WPF layout rewrite,
  - no dynamic control generation,
  - no runtime haptic-behavior change.
- The result improves maintainability because future BST-1 effect/control growth now extends one of the extracted view seams instead of reopening another long `MainWindow` control-assignment block during profile hydration.

Stage 25AJ deliberately does not add data-driven effect editors, plugin-style effect metadata, dynamic WPF control generation, or broader profile-schema generalization across persisted settings and tuning UI. It removes one more shell-level hydration hotspot first so later UI/schema work can build on a cleaner baseline.

## Stage 25AK Audio-Profile View Input Capture Seam

Stage 25AK continues the same cleanup by removing the matching audio-profile input-capture block from `MainWindow` and by letting the extracted page views own their own direct control-read surface.

Stage 25AK architecture result:

- Audio-profile input capture now reads grouped control state through the extracted page seams:
  - `ProfilesView.BuildAudioProfileNameInput()` owns profile-name capture,
  - `EffectsView.BuildAudioProfileEffectControlInputs()` owns BST-1 effect control capture,
  - `RoutingMixerView.BuildAudioProfileMixerControlInputs()` owns mixer/safety control capture.
- `MainWindow` still keeps the same visible ownership boundary:
  - it still composes `AudioProfileControlInputs`,
  - it still calls `AudioProfileControlSnapshotBuilder.BuildProfile(...)`,
  - it still owns tuning-change sequencing, runtime application, and persistence execution.
- The stage stays intentionally narrow:
  - no persisted profile schema change,
  - no WPF layout rewrite,
  - no runtime haptic-behavior change,
  - no dynamic control generation.
- The result improves maintainability because future BST-1 effect/control growth now extends the extracted view seams for both profile hydration and profile capture instead of reopening another large `MainWindow` control-read block.

Stage 25AK deliberately does not add data-driven effect editors, plugin-style effect metadata, dynamic WPF control generation, or broader profile-schema generalization across persisted settings and tuning UI. It removes the matching input-capture hotspot first so later UI/schema work can build on a more consistent shell boundary.

## Stage 25AL MainWindow Audio-Profile Control Accessor Cleanup Seam

Stage 25AL closes the immediate audio-profile shell cleanup stream by removing the leftover dead profile-related control accessors from `MainWindow` after both hydration and input capture already moved onto the extracted views.

Stage 25AL architecture result:

- `MainWindow` no longer keeps stale direct accessors for:
  - profile-name/profile-status controls,
  - BST-1 effect tuning/profile controls used only by the audio-profile seam,
  - mixer/safety controls used only by the audio-profile seam.
- The extracted view seams remain the live boundary for that profile workflow:
  - `ProfilesView` owns profile-name hydration/capture,
  - `EffectsView` owns BST-1 effect hydration/capture,
  - `RoutingMixerView` owns mixer/safety hydration/capture.
- Guardrail coverage now also asserts that `MainWindow` does not regain those old profile-control accessor declarations.
- The stage stays intentionally narrow:
  - no runtime haptic-behavior change,
  - no persisted profile schema change,
  - no WPF layout rewrite,
  - no dynamic control generation.
- The result improves maintainability because the shell boundary now matches the real ownership split instead of leaving obsolete direct-access escape hatches behind.

Stage 25AL deliberately does not add data-driven effect editors, plugin-style effect metadata, dynamic WPF control generation, or broader profile-schema generalization across persisted settings and tuning UI. It finishes this local shell cleanup first so later effect-surface work starts from a cleaner composition root.

## Stage 25AM Audio-Profile Workflow Feedback Planner Seam

Stage 25AM continues the same profile-workflow cleanup by removing repeated user-feedback branching from `MainWindow` and centralizing it behind one pure planner.

Stage 25AM architecture result:

- Audio-profile workflow feedback now has one shared app-side seam:
  - tuning-change save feedback,
  - profile-name commit feedback,
  - combined audio plus P-HPR save feedback,
  - combined audio plus P-HPR load feedback,
  - reset-to-default feedback.
- `AudioProfileWorkflowFeedbackPlanner` owns:
  - footer-status text selection,
  - whether profile-status text should be refreshed,
  - profile-status message selection,
  - validation-message handoff for those workflow paths.
- `MainWindow` still keeps the same visible ownership boundary:
  - it still executes runtime changes, persistence, and control application,
  - it still decides when each workflow runs,
  - it now delegates the repeated message/feedback branching to the planner instead of hardcoding it inline.
- The stage stays intentionally narrow:
  - no runtime haptic-behavior change,
  - no persisted profile schema change,
  - no WPF layout rewrite,
  - no dynamic control generation.
- The result improves maintainability because later profile-workflow changes can evolve one feedback seam instead of re-editing repeated `FooterStatusText` and `UpdateProfileStatus(...)` branches across multiple shell handlers.

Stage 25AM deliberately does not move persistence execution, runtime application, or control capture/application ownership out of `MainWindow`. It isolates workflow feedback first so later workflow/orchestration cleanup can build on a more consistent message contract.

## Stage 25AN Audio-Profile View Sync Coordinator Seam

Stage 25AN continues the same profile-control cleanup by removing the remaining cross-view audio-profile call choreography from `MainWindow` and centralizing it behind one coordinator with narrow interfaces.

Stage 25AN architecture result:

- Audio-profile view synchronization now has one shared app-side seam:
  - current profile control-input capture across Profiles/Effects/Routing views,
  - control-value application across those views,
  - control-text application across Effects/Routing views.
- `AudioProfileViewSyncCoordinator` owns that cross-view choreography and depends only on narrow interfaces:
  - `IAudioProfileProfilesViewSync`,
  - `IAudioProfileEffectsViewSync`,
  - `IAudioProfileRoutingMixerViewSync`.
- The extracted views now implement those sync interfaces explicitly while keeping their existing internal methods and event-forwarding behavior.
- `MainWindow` still keeps the same visible ownership boundary:
  - it still owns `_updatingTuningUi`,
  - it still builds/validates profiles through the existing snapshot builders,
  - it still owns runtime application and workflow execution,
  - it no longer manually stitches together every profile-control capture/application call across the three views.
- The stage stays intentionally narrow:
  - no runtime haptic-behavior change,
  - no persisted profile schema change,
  - no WPF layout rewrite,
  - no dynamic control generation.
- The result improves maintainability because later profile-control surface changes can extend one coordinator seam instead of reopening `MainWindow` for every cross-view wiring edit.

Stage 25AN deliberately does not move runtime execution or persistence orchestration out of `MainWindow`, and it does not make the effect surface data-driven. It isolates the cross-view synchronization seam first so later workflow/orchestration cleanup can build on a cleaner shell boundary.

## Stage 26B Session-Aware Telemetry Freshness

Stage 26B hardens the telemetry-to-runtime safety boundary so freshness follows the actual signal sample that output depends on instead of whichever packet happened to arrive last.

Stage 26B architecture result:

- Telemetry timing is now carried end-to-end:
  - `UdpTelemetryPacket` includes `ReceivedAtUtc` and `ReceivedAtTimestamp`,
  - `VehicleStateStamp` carries those same receive-time fields into every applied signal sample.
- The F1 25 adapter is now session-aware at the `VehicleState` boundary:
  - source identity is `F1 25|<remote-ip>`,
  - source-IP, `SessionUid`, and player-car changes reset accumulated state before applying the new packet,
  - older same-session `OverallFrameIdentifier` packets are ignored,
  - equal-frame packets can still merge complementary packet types into the current `VehicleState`.
- Freshness rules are centralized in `HapticDrive.Asio.Core.Vehicle.Freshness.VehicleStateFreshness`:
  - one evaluator exists for telemetry, motion, session, lap, car status, damage, motion ex, and event samples,
  - freshness now checks presence, same-session identity, no future-frame regression, frame-lag tolerance, and age threshold together.
- Runtime freshness is now signal-specific:
  - `HapticPipelineCoordinator` evaluates telemetry freshness from `VehicleState.Telemetry.Stamp`,
  - fresh session/lap/event traffic no longer makes stale car telemetry appear fresh,
  - pipeline snapshots now surface separate freshness snapshots for telemetry, motion, session, lap, car status, damage, motion ex, and event samples.
- Duplicate freshness drift is reduced across the codebase:
  - BST-1 effect guards,
  - `RoadTextureEvaluator`,
  - `SlipLockEvaluationInput`,
  - and mock `PHprPedalEffectsRouter`
  now route their session/frame-validity checks through the centralized freshness model instead of carrying divergent local logic.
- Safety integration is now tighter:
  - stale telemetry still mutes telemetry-driven rendering immediately,
  - when critical driving telemetry stays stale past the hardening threshold, the same global `OutputInterlock` can latch with `TelemetryStale`.

Stage 26B deliberately does not yet change the repo's live packet-ingress topology. UDP receive/forward/record handling still needs the next bounded-worker/backpressure stage so packet flow, forwarding, and recording can be hardened under sustained load.
