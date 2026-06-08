# Simagic SimPro / SimHub Coexistence Detection

## Stage 2O Purpose

Stage 2O adds safe, read-only process detection for SimPro Manager and SimHub so Haptic Drive ASIO can warn about coexistence risk before any future direct P-HPR control is enabled.

This stage does not send USB writes and does not implement real P-HPR output.

## Safety Boundary

Stage 2O is read-only process observation only.

Allowed:

- enumerate process names,
- match conservative SimPro Manager and SimHub process-name patterns,
- report coexistence status,
- surface WPF diagnostics and warnings,
- pass status into `PHprSafetyContext`.

Forbidden:

- killing SimPro Manager or SimHub,
- hooking either process,
- injecting into either process,
- patching either process,
- memory inspection,
- IPC control,
- changing files or settings for either application,
- sending USB writes,
- sending HID output reports,
- sending HID feature reports,
- vibrating real P-HPR hardware.

## Implementation

The read-only model lives in `HapticDrive.Simagic.PHPR.Abstractions.Coexistence`:

- `IPHprSoftwareCoexistenceDetector`
- `PHprSoftwareCoexistenceDetector`
- `IPHprSoftwareProcessProvider`
- `WindowsProcessSnapshotProvider`
- `PHprSoftwareCoexistenceSnapshot`
- `PHprSoftwareProcessSnapshot`
- `PHprDetectedSoftwareProcess`
- `PHprCoexistenceOptions`

`WindowsProcessSnapshotProvider` uses normal .NET process enumeration to read process names. It has a non-Windows safe fallback and does not control processes.

The detector is fakeable in tests through `IPHprSoftwareProcessProvider`.

## Status Mapping

The detector reports:

| Status | Meaning |
| --- | --- |
| `Unknown` | Detection has not run, is unsupported, or failed safely. |
| `Clear` | No matching SimPro Manager or SimHub process was detected. |
| `SimProRunning` | SimPro Manager appears to be running. |
| `SimHubRunning` | SimHub appears to be running. |
| `ActiveConflict` | SimPro Manager and SimHub both appear to be running. |

Process access errors are handled safely as `Unknown`.

## Safety Integration

The WPF app stores the latest coexistence snapshot and passes its `Status` into `PHprSafetyContext.SoftwareConflictStatus` when building mock P-HPR safety contexts.

`PHprSafetyLimiter` already rejects start commands when `SoftwareConflictStatus` is `ActiveConflict`, using violation `SimProConflict`.

Stage 2O remains conservative: an active SimPro+SimHub conflict blocks mock P-HPR starts as well as any future direct-control starts. Single-process observations warn but do not block mock starts.

## WPF Diagnostics

The Devices page shows:

- SimPro Manager running: yes/no/unknown,
- SimHub running: yes/no/unknown,
- coexistence status,
- last scan time,
- direct-control block status,
- detected matching process names,
- read-only detection statement,
- and any safe detection error.

The Diagnostics page includes the same coexistence status in the copyable diagnostics report.

## Tests

Stage 2O adds hardware-free tests for:

- no matching processes -> `Clear`,
- SimPro only -> `SimProRunning`,
- SimHub only -> `SimHubRunning`,
- both -> `ActiveConflict`,
- provider errors -> safe `Unknown`,
- non-Windows fallback -> safe `Unknown`,
- active conflict rejection through `PHprSafetyLimiter`,
- and no control/hook/kill/write API surface in coexistence types.

## Final Statement

Stage 2O detects process presence only. It does not validate real coexistence behavior with hardware and does not approve direct P-HPR writes.
