# GT Neo Shift Paddle Evidence

## Purpose

This sanitized note records confirmed GT Neo left/right paddle HID input bits used by Stage 2J only to keep paddle input separate from P-HPR output hypotheses.

## Safety Boundary

- Input evidence only.
- No USB writes.
- No output reports.
- No feature reports.
- No P-HPR command generation.
- No haptic output route.

## Confirmed Report Characteristics

| Observation | Value |
| --- | --- |
| Most common input report length | 41 bytes |
| Left paddle | `report[14] & 0x02` |
| Right paddle | `report[14] & 0x01` |
| Active state | bit set / pressed |

## Confidence

Confidence is high for normal GT Neo left/right shift paddle detection from the reviewed sanitized capture bundle.

Movement-only/no-paddle evidence did not trigger byte 14 bit 0 or byte 14 bit 1.

## Runtime Warning

The USB capture address observed for one wheelbase session was session-only. Runtime code must use a proper Windows input path and stable device selection, not Wireshark or USBPcap addresses.

These input mappings do not imply P-HPR write capability.
