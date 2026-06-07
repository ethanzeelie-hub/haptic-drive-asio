# P700 Pedal Input Evidence

## Purpose

This sanitized note records confirmed P700 brake and throttle HID input mappings used by Stage 2J only to separate input observations from P-HPR output hypotheses.

## Safety Boundary

- Input evidence only.
- No USB writes.
- No output reports.
- No feature reports.
- No P-HPR command generation.
- No haptic output route.

## Confirmed Throttle Mapping

| Field | Offset | Bytes | Encoding | Raw range | Percent |
| --- | ---: | --- | --- | --- | --- |
| Primary throttle | 5 | 5-6 | `u16_le@5` | 0-4095 | `raw / 4095 * 100` |
| Mirror throttle | 15 | 15-16 | `u16_le@15` | diagnostic only | diagnostic only |

Use bytes 5-6 as the canonical throttle value. The mirror field is diagnostic only.

## Confirmed Brake Mapping

| Field | Offset | Bytes | Encoding | Raw range | Percent |
| --- | ---: | --- | --- | --- | --- |
| Primary brake | 3 | 3-4 | `u16_le@3` | expected 0-4095 unless later calibration says otherwise | `raw / 4095 * 100` |
| Mirror brake | 13 | 13-14 | `u16_le@13` | diagnostic only | diagnostic only |

Use bytes 3-4 as the canonical brake value. The mirror field is diagnostic only.

## Separation Rule

These HID IN mappings are device-to-host input data. They are not P-HPR output commands and must never be used to infer haptic motor control.

Capture USB addresses are session-only and must not be hard-coded at runtime. Committed docs intentionally omit raw private paths and serial numbers.
