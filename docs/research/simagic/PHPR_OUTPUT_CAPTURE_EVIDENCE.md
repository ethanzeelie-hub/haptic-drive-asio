# P-HPR Output Capture Evidence

## Purpose

This sanitized note records Stage 2J P-HPR output observations without approving or implementing direct hardware writes.

## Safety Boundary

- Evidence and hypotheses only.
- No USB writes.
- No output reports sent by Haptic Drive.
- No feature reports sent by Haptic Drive.
- No protocol encoder for live hardware.
- No protocol decoder producing live `PHprCommand` values.
- No P-HPR routing.

Nothing in this document authorises real USB writes.

## Transport Observation

The reviewed P-HPR output evidence points to host-to-device USBHID SET_REPORT payloads, usually represented in sanitized Wireshark exports as `usb.data_fragment` with 64-byte payloads.

## SimHub F1 EC Observation

Observed active/start family:

```text
F1 EC [MODULE] 01 [FREQ] [STRENGTH] 00 ...
```

Observed stop/idle family:

```text
F1 EC [MODULE] 00 0A 00 00 00 ...
```

Observed module bytes:

| Byte | Interpretation | Confidence |
| --- | --- | --- |
| `01` | brake | High |
| `02` | throttle | High |
| `00` | all/neutral/init/baseline candidate | Low |

Observed active fields:

| Field | Byte | Observation | Confidence |
| --- | ---: | --- | --- |
| state | 3 | `01` active/on | ConfirmedObservation |
| frequency | 4 | direct Hz, examples `0A`, `14`, `1E`, `28`, `32` | High |
| strength | 5 | direct percent, examples `0A`, `14`, `28`, `3C`, `50`, `64` | High |

Duration appears software-timed for tested SimHub captures: active/start then stop/idle after the requested interval.

## SimPro 80 1E 89 Observation

SimPro Manager appears to use a separate 64-byte SET_REPORT family beginning with:

```text
80 1E 89 ...
```

Sanitized compare summaries contain candidate byte changes for module, strength, and frequency, but Stage 2J does not promote SimPro byte meanings into a mock-ready or write-ready command surface.

Current Stage 2J status:

- family prefix: ConfirmedObservation,
- field meanings: Low/Unknown,
- Stage 2K surface: `SimProUnknownMock` only unless a later mock-only stage deliberately scopes more.

## Evidence References

- `evidence/simhub/simhub_f1ec_active_stop_records.csv`
- `evidence/simhub/simhub_f1ec_duration_summary.csv`
- `evidence/simhub/simhub_f1ec_validation_report.txt`
- `evidence/simpro/phpr_setreport_summary.txt`
- SimPro compare-summary files from the sanitized local bundle.

Raw `.pcapng` files remain private and uncommitted.
