# Simagic P-HPR Phase 2 Research

Stage 2A starts the Simagic P-HPR and GT Neo paddle-input phase as research, documentation, and safety intake only. Stage 2B adds safe abstraction projects and a mock-only output skeleton. Stage 2C adds cached driving-state evaluation. Stage 2D adds read-only wheel / paddle input discovery and candidate scoring. These stages do not add USB writes, real P-HPR output, protocol control, live paddle listener code, or haptic routing from paddle input.

## Current Repository Baseline

- Phase 1 is complete through Stage 18.
- The WPF app, F1 25 UDP listener/parser, raw forwarding, recording/replay, `VehicleState`, ASIO readiness, native ASIO streaming backend, haptic effects, mixer, safety chain, emergency mute, stale telemetry mute, profiles, diagnostics, and launch wrapper already exist.
- `NullAudioOutputDevice` remains the automated-test and startup default.
- Stage 18 remains the final pre-Dayton-shaker software package.
- Searches of `src/` and `tests/` during Stage 2A found no Simagic, P-HPR, P700, GT Neo, `ShiftIntent`, or `DrivingArmed` implementation code.
- Stage 2B now defines input and P-HPR contracts without adding real hardware access.
- Stage 2C now defines `DrivingArmedStateService` in `HapticDrive.Actuation` without connecting it to paddle input or P-HPR output.
- Stage 2D now defines richer input discovery snapshots and implements read-only Windows Raw Input plus Windows game-controller capability discovery.

## User Hardware Context

- Simagic P700 2-pedal set connected directly to the PC by USB.
- Brake and throttle pedals.
- Two Simagic P-HPR modules, one on brake and one on throttle.
- P-HPR modules connected through the built-in P700 haptic controller.
- Simagic Alpha Evo 12 Nm wheelbase connected separately to the PC by USB.
- Simagic GT Neo wheel connected to or through the Alpha Evo wheelbase.
- SimPro Manager V3 is currently used.
- P-HPR modules are visible/configurable in SimPro Manager V3.
- P-HPR modules are visible/configurable in SimHub, but SimHub P-HPR control feels noticeably laggier.
- Haptic Drive ASIO must not require SimHub at runtime.

## Phase 2 Objective

Phase 2 adds a separate non-audio actuator architecture for Simagic P-HPR pedal modules while preserving the existing ASIO/BST-1 audio path.

The P-HPR modules are not audio devices. They must not be routed through ASIO and must not be forced into `IAudioOutputDevice`.

Planned future boundaries:

```text
F1 25 telemetry
-> VehicleState
-> audio haptic effects
-> mixer/safety
-> ASIO/BST-1 audio output

GT Neo paddle input and VehicleState
-> shift/effect routing
-> actuator safety
-> P-HPR pedal output
```

The two paths may share event timestamps and diagnostics later, but neither path should block the other.

## Highest-Priority Feature

The most important new behavior is an instant pedal gear pulse from GT Neo paddle input:

```text
GT Neo paddle press
-> read-only Raw Input / DirectInput / HID event
-> ShiftIntentEvent
-> cached DrivingArmed gate
-> immediate P-HPR gear pulse
```

Default future mode:

- `InstantPaddleOnly`
- Fire on left or right paddle press immediately when `DrivingArmed` is true.
- Do not wait for F1 25 telemetry at event time.
- Do not fire a second normal telemetry-confirmed pulse by default.

Telemetry is still required for cached menu-safe driving state, diagnostics, optional telemetry-confirmed mode, optional rejected-shift detection, road vibration, slip, and lock effects.

## Stage 2A Scope

Implemented in Stage 2A:

- Confirmed Stage 18 completion from project docs and development log.
- Confirmed no current Simagic/P-HPR implementation exists in `src/` or `tests/`.
- Created the Phase 2 research and safety documentation set.
- Updated durable project guidance for no real P-HPR writes before explicit approval.
- Updated ignore rules for raw/private capture artifacts.

Not implemented in Stage 2A:

- No input listener.
- No DirectInput, Raw Input, or HID code.
- No P-HPR output abstractions.
- No mock P-HPR output.
- No protocol encoder/decoder.
- No real USB write path.
- No Simagic device output reports.
- No feature reports that can write or change device state.

## Stage 2B Scope

Implemented in Stage 2B:

- `HapticDrive.Input.Abstractions`
- `HapticDrive.Input.Windows` placeholder project for later read-only Windows input work.
- `HapticDrive.Simagic.PHPR.Abstractions`
- `IShiftIntentSource`
- `IWheelPaddleInputSource`
- `IInputDeviceDiscovery`
- `ShiftIntentEvent`
- `PaddleSide`
- `DrivingArmedState`
- `IDrivingArmedStateProvider`
- `IPHprOutputDevice`
- `PHprCommand`
- `PHprModuleId`
- `PHprCommandSource`
- `PHprSafetyLimits`
- `PHprOutputSnapshot`
- `MockPhprOutputDevice` skeleton.

Not implemented in Stage 2B:

- No Windows Raw Input implementation.
- No DirectInput implementation.
- No HID input-report reader.
- No P700/P-HPR device discovery implementation.
- No shift-intent router.
- No telemetry-backed `DrivingArmed` service.
- No protocol encoder/decoder.
- No real USB writes.
- No real P-HPR output.

## Stage 2C Scope

Implemented in Stage 2C:

- `HapticDrive.Actuation`
- `DrivingArmedStateService`
- `DrivingArmedStateServiceOptions`
- `DrivingArmedEvaluationContext`
- `DrivingArmedSuppressionReason`
- `DrivingArmedStateServiceSnapshot`
- Update from existing `VehicleState`
- Update from existing `HapticPipelineSnapshot`
- Default false until recent valid telemetry is observed
- Menu-safe mode enabled by default
- Require recent telemetry enabled by default
- Telemetry freshness threshold
- Allow zero-speed active driving enabled by default
- Diagnostics-only unsafe override option
- Suppression reasons for no telemetry, stale telemetry, paused, network-paused, garage/menu/result state, invalid state, not moving/inactive, emergency mute, and haptics stopped

Not implemented in Stage 2C:

- No paddle input listener.
- No shift intent router.
- No P-HPR routing.
- No UI wiring.
- No real USB writes.
- No real P-HPR output.

## Stage 2D Scope

Implemented in Stage 2D:

- `InputDeviceInfo`
- `InputDeviceKind`
- `InputDiscoveryMethod`
- `InputControlInfo`
- `InputDeviceDiscoverySnapshot`
- `IWheelInputCandidateProvider`
- Raw Input metadata discovery
- Windows game-controller capability discovery
- Safe redaction for normal device path display
- Likely-device scoring for Simagic wheelbase, GT Neo / wheel input path, P700 pedals, and unknown HID/game-controller devices
- WPF Devices page Refresh Input Devices diagnostics
- Hardware-free tests for models, zero devices, exceptions, scoring, deterministic fake discovery, safe empty snapshots, and no write-like discovery interface methods

Not implemented in Stage 2D:

- No live paddle input listener.
- No rising-edge detection.
- No left/right paddle mapping.
- No `ShiftIntentEvent` routing from actual hardware input.
- No P-HPR routing.
- No DirectInput-specific dependency.
- No HID input-report reader.
- No USB output reports or write-capable feature reports.
- No real P-HPR output.

## Required Follow-Up Data

Stage 2A requests the hardware/software data listed in `docs/SIMAGIC_USER_DATA_REQUEST.md`.

The highest-value first items after Stage 2D are:

1. SimPro Manager V3 P700/P-HPR screenshots.
2. SimHub P-HPR detection and mapping screenshots.
3. Windows Device Manager hardware IDs for the P700 and Alpha Evo / GT Neo-visible devices.
4. USBView or USB Device Tree Viewer exports for descriptors and HID report descriptors.
5. Windows game controller / DirectInput button numbers for the GT Neo left and right paddles.
6. Haptic Drive ASIO Refresh Input Devices candidate output, especially device display names and discovery errors.

USBPcap/Wireshark captures are useful later, but they are not required before the Stage 2B abstraction work.

## Write Safety Gate

No real P-HPR USB writes may be implemented or executed until the user says exactly:

```text
I approve Phase 2 controlled P-HPR write testing
```

That approval phrase has not been provided as of Stage 2A.

Before that phrase, work is limited to read-only discovery, input observation, documentation, mock output, protocol hypotheses, tests, and diagnostics.

## Legal and Coexistence Notes

- Do not copy SimHub code or UI assets unless license compatibility is checked and documented.
- Do not depend on hidden SimHub internals.
- Do not copy, hook, patch, or inject into SimPro Manager.
- Do not modify SimPro Manager settings automatically.
- Do not kill SimPro Manager.
- Detecting whether SimPro Manager or SimHub is running is allowed in a later stage.
- Default future direct P-HPR control must remain disabled when a conflict risk exists.
