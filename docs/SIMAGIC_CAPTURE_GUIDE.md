# Simagic Capture Guide

This guide prepares future SimPro/SimHub capture collection. Stage 2A does not perform capture analysis and does not implement real P-HPR writes.

## Rules

- Do not commit raw captures.
- Keep captures local unless a sanitized summary is created.
- Capture only one changed setting at a time.
- Record tool versions and device firmware versions with every capture.
- Stop immediately if a test produces unexpected strong or continuous vibration.
- Haptic Drive ASIO must not send real P-HPR writes during these captures before explicit approval.

## Suggested Folder Layout

Use a local ignored folder such as:

```text
captures/simagic/YYYY-MM-DD/
```

Suggested filename pattern:

```text
YYYY-MM-DD_tool_device_scenario_attempt.ext
```

Examples:

```text
2026-06-04_usbpcap_p700_simpro-open_attempt-01.pcapng
2026-06-04_usbpcap_p700_brake-test-vibration_attempt-01.pcapng
2026-06-04_usbpcap_p700_throttle-strength-change-only_attempt-01.pcapng
```

## Capture Metadata Template

```text
Scenario:
Date/time:
Tool:
Tool version:
SimPro Manager version:
SimHub version:
P700 firmware:
Alpha Evo firmware:
GT Neo firmware:
Windows version:
Connected devices:
SimPro running:
SimHub running:
Pedal/module selected:
Setting changed:
Old value:
New value:
Observed physical behavior:
Unexpected behavior:
Notes:
```

## Initial Capture Scenarios

Start with simple process/device state captures:

1. SimPro opened with P700 connected.
2. SimPro closed with P700 still connected.
3. SimHub opened with P700 visible, if safe.

Then capture manual test actions from vendor tools:

1. Brake P-HPR test vibration.
2. Throttle P-HPR test vibration.
3. Brake strength changed only.
4. Throttle strength changed only.
5. Brake frequency changed only.
6. Throttle frequency changed only.
7. Brake pulse duration changed only.
8. Throttle pulse duration changed only.
9. SimHub P-HPR gear, lock, or slip test where possible.

## Analysis Readiness

Future Stage 2I analysis should prefer sanitized transfer summaries with:

- Timestamp or sequence index.
- Device/interface/endpoint.
- Direction.
- Transfer type.
- Report ID where known.
- Payload bytes.
- Status/result.
- Scenario metadata.

Synthetic fixtures should be used before importing any real capture summaries into tests.
