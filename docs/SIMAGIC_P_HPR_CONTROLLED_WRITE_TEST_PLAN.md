# Simagic P-HPR Controlled Write Test Plan

## Stage 2P Purpose

Stage 2P creates the complete controlled write test plan before any direct real-write code is enabled.

This stage is no-write:

- no real P-HPR output adapter,
- no HID writer,
- no USB output report,
- no HID feature report,
- no write-capable UI,
- no real vibration,
- and no automated hardware test.

The extended Phase 2 / Phase 3 master prompt authorizes implementing the later gated Stage 2Q real-write code path. It does not authorize unattended hardware vibration, automatic startup pulses, persisted arming, automated real writes, or claims of physical validation.

## Evidence Map

The local `C:\Users\ethan\Downloads\Complete Files Required` evidence folder was available for Stage 2P. Only sanitized Markdown, CSV, TXT, and derived summaries were reviewed. Raw `.pcap`, `.pcapng`, private USB captures, serial numbers, and unsanitized device paths are not committed.

| Evidence package | Stage 2P use | Safety boundary |
| --- | --- | --- |
| P700 throttle input | Confirms throttle input is `u16_le@5`, mirror diagnostic is `u16_le@15`, raw range is `0..4095`. | Input only, not P-HPR output. |
| P700 brake input | Confirms brake input is `u16_le@3`, mirror diagnostic is `u16_le@13`, observed HID input payload length is 17 bytes. | Input only, not P-HPR output. |
| GT Neo shift paddles | Confirms 41-byte observed input report, left paddle `report[14] & 0x02`, right paddle `report[14] & 0x01`. | Input only; captured USB address is session-only. |
| SimHub P-HPR output | Supports SimHub `F1 EC` start/stop hypothesis, module `01` brake, module `02` throttle, direct frequency and strength bytes, software-timed duration. | Candidate for later controlled test only, not executed in Stage 2P. |
| SimPro Manager output | Confirms a separate `80 1E 89` family with module/strength/frequency observations, but more unresolved behavior. | Do not merge with SimHub; not the first direct path. |
| Device inventory / interface notes | Still missing exact VID/PID, report ID, interface, endpoint, descriptor, and open/access behavior. | Direct control cannot proceed without explicit selection and manual validation. |

## Preconditions

Before the first real write test in a later stage:

1. User is physically present and supervising.
2. SimPro Manager is closed unless explicitly testing coexistence.
3. SimHub is closed unless explicitly testing coexistence.
4. P700 is connected.
5. Brake and throttle P-HPR modules are installed.
6. Haptic Drive ASIO is running.
7. Emergency stop is visible.
8. Brake and throttle module mapping is known.
9. Correct device/interface/report is selected.
10. Real writes are disabled by default.
11. Direct-control enabled state is explicit and runtime-only.
12. Direct-control armed state is explicit, runtime-only, and not persisted.
13. Safety limits are active.
14. Coexistence status is `Clear` for the first test.

## First Write Test

The first real write test must be deliberately small:

- one module only,
- brake first recommended,
- low strength, default maximum `10%`,
- short duration, default maximum `100 ms`,
- conservative known frequency such as `50 Hz`,
- no loop,
- no continuous vibration,
- immediate stop available,
- user observes whether the selected module vibrates,
- and if anything unexpected happens, use emergency stop and stop testing.

## Manual Test Sequence

1. Confirm read-only device inventory.
2. Confirm SimPro/SimHub coexistence status is clear.
3. Start Haptic Drive ASIO.
4. Confirm direct-control mode is disabled.
5. Confirm no direct-control arming is persisted from previous app launch.
6. Enable direct-control mode manually.
7. Select target device/interface/report manually.
8. Arm direct control manually.
9. Send one low-strength brake pulse.
10. Verify a stop command is sent and vibration stops.
11. Send one low-strength throttle pulse.
12. Verify a stop command is sent and vibration stops.
13. Test emergency stop.
14. Test telemetry stale gate.
15. Test emergency mute gate.
16. Test `DrivingArmed` gate.
17. Test SimPro/SimHub conflict gate.
18. Record results.

## Pass Criteria

- Pulse occurs only on the selected pedal.
- No continuous runaway occurs.
- Stop works.
- Emergency stop works.
- App remains responsive.
- Direct control cannot start when SimPro/SimHub conflict is active.
- No writes occur unless direct control is explicitly enabled and armed.
- No writes occur on app startup.
- No armed state is persisted.

## Abort Criteria

Stop immediately if any of these occur:

- wrong pedal vibrates,
- both pedals vibrate unexpectedly,
- vibration continues after stop,
- app freezes,
- device disconnects,
- SimPro/SimHub conflict appears,
- report/interface identity is unknown,
- output feels stronger than expected,
- unexpected motion/noise/heat occurs,
- stop command fails,
- or emergency stop fails.

## Readiness Model

Stage 2P adds a no-write readiness model:

- `PHprControlledWriteChecklist`
- `PHprControlledWriteReadiness`
- `PHprControlledWriteReadinessIssue`
- `PHprControlledWriteReadinessIssueCode`
- `PHprControlledWriteTestPlan`
- `PHprManualTestResultTemplate`

The model always reports Stage 2P as blocked for real output, even when future manual checklist inputs are all true. This is intentional.

The WPF Devices page exposes a disabled "P-HPR Direct Write Readiness" diagnostic state. It does not add buttons, adapters, device writers, or write-capable controls.

## Stage 2Q Preparation Boundary

Stage 2Q may implement a minimal gated real-write path, but automated verification must still use fake writers only. Stage 2Q must keep real output default-off, arming runtime-only, emergency stop visible, and physical validation manual.

## Stage 2Q Implementation Status

Stage 2Q implements the minimal gated path described above:

- Windows HID report writer boundary with fake-writer tests,
- SimHub F1 EC start/stop encoder,
- runtime-only WPF direct-control enable/arm/device selection,
- one-pulse brake and throttle buttons guarded by enable, arm, selected device, clear coexistence, and emergency stop state,
- accepted-paddle direct gear-pulse route that remains disabled/unarmed by default,
- emergency stop stop-report attempt for brake and throttle when a device is selected.

Stage 2Q does not execute this test plan and does not validate physical P-HPR behavior. Controlled real validation moves to Stage 2R.
