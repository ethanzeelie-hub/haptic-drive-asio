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

## Phase 2 Planned Actuator Boundary

Phase 2 adds planned Simagic P-HPR pedal support as a separate non-audio actuator path. Stage 2Q includes read-only paddle/input diagnostics, cached `DrivingArmed` shift-intent diagnostics, read-only P700 / P-HPR inventory tooling, capture metadata workflow tooling, read-only capture analysis tooling, analysis-only protocol hypotheses, mock-only protocol/output diagnostics, a reusable P-HPR safety limiter, mock-only gear pulse routing, mock-only road/slip/lock pedal-effect routing, read-only SimPro / SimHub coexistence detection, a controlled-write readiness model/runbook, and a gated minimal Windows HID real-output adapter. Stage 2R adds a controlled validation harness. Phase 3A hardens the real-output adapter lifecycle and diagnostics. Phase 3B completes instant paddle gear-pulse production integration with safe per-pedal settings persistence and latency trace diagnostics. Phase 3C completes real road-vibration production integration with safe per-pedal road scaling and route-interval suppression. The real adapter is disabled/unarmed by default and is not physically validated.

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

Devices-page controls expose mock routing enabled/disabled, target, strength, frequency, duration, clear diagnostics, mock emergency stop, and clear mock emergency stop. Persisted settings are limited to mock routing preferences. Emergency-stop state and mock histories are not persisted.

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
- wheel slip: target throttle, strength `0.03` to `0.08`, frequency `45` to `75 Hz`, duration `50 ms`, source `WheelSlip`;
- wheel lock: target brake, strength `0.04` to `0.10`, frequency `60` to `90 Hz`, duration `50 ms`, source `WheelLock`.

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

Road-vibration priority remains below gear pulse, wheel slip, and wheel lock. Phase 3C does not route wheel slip or wheel lock through the real direct-output backend; those remain Phase 3D. The ASIO/BST-1 road texture effect remains a separate audio effect path and is unchanged.

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
