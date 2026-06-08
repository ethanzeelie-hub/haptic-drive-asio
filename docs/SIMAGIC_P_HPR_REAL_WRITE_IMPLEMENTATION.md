# Simagic P-HPR Gated Real Write Implementation

## Stage 2Q Purpose

Stage 2Q adds a minimal write-capable P-HPR output path for later controlled local testing.

This stage implements code only. It does not run a hardware pulse, does not validate physical feel, does not validate safe gain, does not prove pedal mapping, and does not claim real hardware latency or stop behavior.

## Implemented Path

The real direct-control path lives in `HapticDrive.Simagic.PHPR.Output.Windows`:

- `PHprHidDeviceSelector`: runtime-only manual HID device/interface/report selection.
- `IPhprHidReportWriter`: fakeable HID report writer boundary.
- `WindowsHidReportWriter`: Windows file-handle report writer, used only when a command passes every gate.
- `SimHubF1EcRealReportEncoder`: 64-byte SimHub `F1 EC` start/stop report encoder.
- `SimagicPhprOutputDevice`: gated `IPHprOutputDevice` implementation with safety limiting, emergency stop, and software-timed stops.
- `PHprDirectGearPulseRouter`: accepted `ShiftIntentEvent` to brake/throttle command routing for direct output.

The command path is:

```text
manual test pulse or accepted ShiftIntentEvent
-> PHprCommand
-> SimagicPhprOutputDevice gates
-> PHprSafetyLimiter
-> SimHubF1EcRealReportEncoder
-> IPhprHidReportWriter
-> selected Windows HID path
```

The direct path does not use ASIO, `IAudioOutputDevice`, the BST-1 mixer, or the audio render callback.

Phase 3A hardens this same adapter boundary with explicit writer `OpenAsync` / `CloseAsync` lifecycle, write timeout handling, connection-state diagnostics, selected-interface/report validation, disconnect classification, and close-on-dispose behavior. Phase 3B completes instant paddle gear-pulse production integration with independent brake/throttle settings, safe settings persistence, and route latency traces. See `docs/SIMAGIC_P_HPR_OUTPUT_ADAPTER.md` and `docs/SIMAGIC_P_HPR_INSTANT_SHIFT_GUIDE.md`.

## Protocol Surface

Stage 2Q implements only the preferred SimHub F1 EC hypothesis:

```text
start: F1 EC [module] 01 [frequency_hz] [strength_percent] 00 ...
stop:  F1 EC [module] 00 0A 00 00 00 ...
```

Module bytes:

- `01`: brake
- `02`: throttle

Duration is software timed by sending a start report and scheduling a stop report after the configured duration.

SimPro Manager `80 1E 89` detailed writes remain unsupported.

## Gates

Start commands are blocked unless all of these are true:

- direct control enabled,
- direct control armed,
- selected device path/interface/report configured,
- coexistence status is `Clear`,
- emergency stop latch is clear,
- `PHprSafetyLimiter` accepts the command,
- required module is available,
- strength/frequency/duration are inside safety limits,
- command rate and continuous-duration limits are respected.

Stop and emergency-stop reports may be attempted when a device is selected so the app can try to quiet both modules.

Enable, arm, and selected device path are runtime-only and are not persisted.

On WPF app close, the real output device is disposed and attempts emergency-stop style brake/throttle stop reports only when a device is selected and direct control is armed or a stop is already pending.

## WPF Surface

The Devices page now has a `P-HPR Real Direct Control` section showing:

- direct-control enabled state,
- armed state,
- manual device path, interface, optional report ID, and report length,
- per-pedal enabled/strength/frequency/duration settings,
- brake and throttle one-pulse buttons,
- emergency stop and clear emergency stop,
- last command status,
- last write status/error/report length/target/command,
- last gear-pulse latency timestamps,
- coexistence and safety status.

Test pulse buttons are disabled unless enable, arm, selected device, clear coexistence, and clear emergency-stop conditions are all satisfied.

## Tests

Automated tests use fake HID writers only. They cover:

- no write on startup,
- no write without enabled and armed,
- no write without selected device,
- no write when coexistence is `Unknown`, `SimProRunning`, `SimHubRunning`, or `ActiveConflict`,
- no write when safety rejects,
- brake and throttle start/stop report bytes through a fake writer,
- duration-scheduled stop reports,
- emergency stop stop frames,
- accepted paddle routing only when enabled and armed,
- accepted upshift/downshift routing without telemetry gear-confirmation wait,
- suppressed shift intent not writing,
- per-pedal settings and disabled-pedal suppression,
- safe real gear-pulse settings persistence without persisted enable/arm/device state,
- no ASIO audio path reference from the output project,
- existing mock output behavior remaining mock-only.

## Explicit Non-Claims

Stage 2Q does not prove:

- the correct P700/P-HPR Windows HID path,
- report ID or report length correctness for the user's hardware,
- SimPro/SimHub coexistence behavior on the real device,
- brake/throttle physical module mapping,
- stop/off behavior under real hardware ownership,
- safe gain,
- physical latency,
- sustained vibration safety.

Those remain manual validation tasks.
