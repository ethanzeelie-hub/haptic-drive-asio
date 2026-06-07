# Simagic Protocol Hypotheses

## Stage 2J Purpose

Stage 2J converts the Stage 2I capture-analysis results and related sanitized input evidence into formal P-HPR protocol hypotheses.

This document prepares Stage 2K mock protocol/output work only. It does not approve, implement, or execute direct P-HPR control.

## Safety Boundary

- Hypotheses only.
- No USB writes.
- No HID output reports.
- No HID feature reports.
- No production protocol adapter.
- No live hardware protocol encoder.
- No live hardware protocol decoder that creates `PHprCommand` values.
- No `IPHprOutputDevice` calls.
- No `MockPhprOutputDevice` calls in Stage 2J.
- No route from `ShiftIntentEvent` to haptic output.
- No ASIO/BST-1 audio path change.

Nothing in this document authorises real USB writes.

## Evidence Sources Reviewed

- `AGENTS.md`
- `docs/SIMAGIC_CAPTURE_ANALYSIS.md`
- `docs/SIMAGIC_CAPTURE_GUIDE.md`
- `docs/SIMAGIC_P_HPR_PHASE_2_RESEARCH.md`
- `docs/SIMAGIC_P_HPR_SAFETY_PLAN.md`
- `docs/SIMAGIC_SHIFT_INTENT_DESIGN.md`
- `docs/SIMAGIC_USB_DEVICE_INVENTORY.md`
- `docs/SIMAGIC_USER_DATA_REQUEST.md`
- `docs/SIMAGIC_WHEEL_INPUT_RESEARCH.md`
- `docs/research/simagic/P700_PEDAL_INPUT_EVIDENCE.md`
- `docs/research/simagic/GT_NEO_SHIFT_PADDLE_EVIDENCE.md`
- `docs/research/simagic/PHPR_OUTPUT_CAPTURE_EVIDENCE.md`
- Sanitized P-HPR evidence bundle files:
  - `evidence/simhub/simhub_f1ec_active_stop_records.csv`
  - `evidence/simhub/simhub_f1ec_duration_summary.csv`
  - `evidence/simhub/simhub_f1ec_validation_report.txt`
  - `evidence/simpro/phpr_setreport_summary.txt`
  - SimPro compare-summary files
- Sanitized P700 throttle/brake and GT Neo paddle evidence bundles.

Raw captures, private paths, serial numbers, and unsanitized inventories remain uncommitted.

## Confidence Scale

| Level | Meaning |
| --- | --- |
| Unknown | Not enough evidence to assign meaning. |
| Low | Possible interpretation with limited or unresolved evidence. |
| Medium | Repeated observation, but still missing key validation. |
| High | Repeated observation matching controlled scenario changes. |
| ConfirmedObservation | Bytes or input bits were directly observed in sanitized captures. |

`ConfirmedObservation` means the bytes were observed. It does not mean safe command status, write-ready status, or approval for real hardware output.

## Confirmed Non-Output Input Mappings

These are read-only input mappings. They are not P-HPR output commands and must not be used to infer motor-control bytes.

| Device | Mapping | Bytes | Scale | Confidence | Boundary |
| --- | --- | --- | --- | --- | --- |
| P700 throttle primary | `u16_le@5` | 5-6 | `raw / 4095 * 100` | ConfirmedObservation | input only |
| P700 throttle mirror | `u16_le@15` | 15-16 | diagnostic only | ConfirmedObservation | input only |
| P700 brake primary | `u16_le@3` | 3-4 | `raw / 4095 * 100` unless later calibration says otherwise | ConfirmedObservation | input only |
| P700 brake mirror | `u16_le@13` | 13-14 | diagnostic only | ConfirmedObservation | input only |
| GT Neo left paddle | `report[14] & 0x02` | byte 14 bit 1 | active-high | ConfirmedObservation | input only |
| GT Neo right paddle | `report[14] & 0x01` | byte 14 bit 0 | active-high | ConfirmedObservation | input only |

USB capture addresses such as wheelbase address `8` and one P700 address observed in private input evidence are session-only and must not be used at runtime.

## SimHub F1 EC Protocol Family Hypothesis

SimHub appears to use a simple 64-byte USBHID SET_REPORT payload family beginning with:

```text
F1 EC ...
```

This family is separate from SimPro Manager's `80 1E 89 ...` family.

Status: `ReadyForMockProtocol` for Stage 2K only.

Real write status: `BlockedForRealWrite`.

Safety note: this hypothesis is approved for mock modelling only. It is not approved for real USB writes.

## SimHub Start Packet Hypothesis

Hypothesis:

```text
F1 EC [module] 01 [frequency_hz] [strength_percent] 00 ...
```

| Field | Byte offset | Observed values | Interpretation | Confidence |
| --- | ---: | --- | --- | --- |
| prefix | 0-1 | `F1 EC` | SimHub F1 EC family prefix | ConfirmedObservation |
| module selector | 2 | `01` brake, `02` throttle | target P-HPR module | High |
| module selector | 2 | `00` all/neutral/init/baseline candidate | exact meaning uncertain | Low |
| state | 3 | `01` | active/on | ConfirmedObservation |
| frequency | 4 | `0A` 10 Hz, `14` 20 Hz, `1E` 30 Hz, `28` 40 Hz, `32` 50 Hz | direct Hz | High |
| strength | 5 | `0A` 10%, `14` 20%, `28` 40%, `3C` 60%, `50` 80%, `64` 100% | direct percent | High |
| trailing bytes | 6+ | mostly zero in tested cases | zero/padding/unknown | Low |

One SimHub throttle 10% observation used `09` instead of `0A`; treat that as a documented anomaly, not a different encoding.

Stage 2K may model this as a mock-only SimHub F1 EC start packet. No Stage 2J code sends or prepares it for hardware.

## SimHub Stop Packet Hypothesis

Hypothesis:

```text
F1 EC [module] 00 0A 00 00 00 ...
```

| Field | Byte offset | Observed values | Interpretation | Confidence |
| --- | ---: | --- | --- | --- |
| prefix | 0-1 | `F1 EC` | SimHub F1 EC family prefix | ConfirmedObservation |
| module selector | 2 | `00`, `01`, `02` | target or baseline selector | High for 01/02, Low for 00 meaning |
| state | 3 | `00` | stop/off/idle | ConfirmedObservation |
| byte 4 | 4 | `0A` | default/min frequency, neutral value, or retained baseline; unresolved | Low |
| byte 5 | 5 | `00` | observed zero | Medium |
| remaining bytes | 6+ | zero in tested cases | unknown/padding | Low |

Stop/idle payload is an observation. Real stop command behavior must not be trusted until controlled write validation after explicit approval.

## SimHub Duration Timing Hypothesis

Hypothesis:

SimHub does not encode pulse duration in the active/start payload for tested cases. Duration appears software-timed by sending active/start, then sending stop/idle after the requested interval.

Evidence pattern:

- 100 ms and 500 ms active payloads can be identical.
- Stop packet occurs approximately 0.1 s or 0.5 s later.
- Active-to-stop timing matches requested duration in tested captures.

Confidence:

- High for tested SimHub scenarios.
- Unknown for all possible SimHub effects not captured.

Stage 2K may model duration as mock start plus scheduled mock stop. Real hardware write implementation remains blocked.

## SimPro 80 1E 89 Protocol Family Hypothesis

Hypothesis:

SimPro Manager uses a different 64-byte USBHID SET_REPORT payload family beginning with:

```text
80 1E 89 ...
```

| Field | Byte offset | Observed values | Interpretation | Confidence |
| --- | ---: | --- | --- | --- |
| prefix/family | 0-2 | `80 1E 89` | distinct SimPro Manager family | ConfirmedObservation |
| module selector | unresolved | candidate bytes exist in sanitized compare summaries | not promoted to mock-ready selector | Low |
| strength field | unresolved | candidate strength changes exist in sanitized summaries | not a production control field | Low |
| frequency field | unresolved | candidate frequency changes exist in sanitized summaries | not a production control field | Low |
| duration field | unresolved | none confirmed | may be software-timed, unresolved | Unknown |
| checksum/counter/keepalive | unresolved | not proven | do not assume absent | Unknown |

Status: `NeedsMoreCaptures`.

Real write status: `BlockedForRealWrite`.

Stage 2K may represent this only as `SimProUnknownMock` unless a later stage deliberately scopes a mock-only SimPro model. Do not implement SimPro-compatible write behavior from this hypothesis in Stage 2J.

## SimHub vs SimPro Comparison

| Area | SimHub | SimPro Manager |
| --- | --- | --- |
| Payload family | `F1 EC ...` | `80 1E 89 ...` |
| Transport observation | USBHID SET_REPORT | USBHID SET_REPORT |
| Payload length observed | 64 bytes | 64 bytes |
| Module values | `01` brake, `02` throttle | unresolved for Stage 2K mock surface |
| Frequency | direct Hz, high confidence | candidate evidence exists, still conservative |
| Strength | direct percent, high confidence | candidate evidence exists, still conservative |
| Duration | software-timed start/stop in tested cases | unresolved |
| Stage 2K readiness | mock-ready | unknown/mock placeholder only |
| Real-write status | blocked | blocked |

Do not assume these payload families are interchangeable.

## Unknowns And Missing Evidence

- Exact report ID separate from payload bytes, if any.
- Exact Windows HID interface and endpoint for the P700/P-HPR path.
- HID report descriptor.
- VID/PID and interface confirmation from USBView or USB Device Tree Viewer.
- Whether SimPro Manager holds exclusive access.
- Whether SimHub and SimPro can both see/control P-HPR simultaneously.
- Whether the observed SimHub F1 EC payloads use the same P700/P-HPR hardware path in every relevant setup.
- Real stop/off behavior under direct Haptic Drive ownership.
- Any checksum, sequence, keepalive, or state-sync requirements.
- Behavior when SimPro Manager is running.
- Safe emergency-stop semantics.

## Stage 2K Allowed Mock Surface

Stage 2K should be: Stage 2K - Mock P-HPR Protocol and Output.

Stage 2K may:

- create mock protocol objects from Stage 2J hypotheses,
- create mock start/stop packet representations,
- create mock SimHub F1 EC command structures,
- create mock-only `PHprCommand` mapping,
- feed `MockPhprOutputDevice` in mock tests,
- run tests without hardware,
- validate routing later in mock mode,
- model duration as mock start plus scheduled stop.

Recommended mock-only command model:

| Field | Values |
| --- | --- |
| TargetModule | Brake, Throttle, Both |
| State | Start, Stop, EmergencyStop |
| FrequencyHz | numeric mock field |
| StrengthPercent or Strength01 | numeric mock field |
| DurationMs | app-side timing, mock start plus scheduled stop |
| SourceProtocolFamily | SimHubF1EcMock, SimProUnknownMock |
| EvidenceConfidence | copied from Stage 2J hypothesis |
| MockOnly | always true |

Stage 2K must not:

- write to hardware,
- open Simagic device handles for write,
- send HID output reports,
- send HID feature reports,
- trigger real P-HPR vibration,
- claim direct control is safe.

## Real Write Blockers

- The exact approval phrase has not been provided.
- No controlled write test plan has been executed.
- No real hardware write safety validation exists.
- Stop command behavior has not been validated on real hardware.
- SimPro/SimHub coexistence has not been validated for direct control.
- Device ownership and exclusive access behavior are not validated.
- Report ID, endpoint, and interface selection must be confirmed.
- Any checksum, sequence, or keepalive behavior must be confirmed if present.
- Behavior when SimPro Manager is running must be understood.
- Emergency stop path must exist before real writes.
- `PHprSafetyLimiter` must exist before real writes.
- First real test must be manual, low strength, short duration, one pedal, and no loop.

## Optional User Data That Would Improve Confidence

- Additional SimPro Manager captures/summaries for brake/throttle test vibration.
- SimPro strength/frequency/duration change summaries.
- Exact P700 interface/report IDs from USBView.
- Confirmation whether SimPro must be closed to access the P700.
- Confirmation whether SimHub and SimPro can both see/control P-HPR at the same time.
- Confirmation whether SimHub F1 EC commands were captured against the same P700/P-HPR hardware path.
- Any evidence of report ID separate from payload bytes.
- Endpoint/interface details still missing from committed sanitized docs.

## Final Safety Statement

Nothing in this document authorises real USB writes.
