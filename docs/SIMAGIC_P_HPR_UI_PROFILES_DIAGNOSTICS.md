# Simagic P-HPR UI, Profiles, And Diagnostics

## Phase 3E Status

Phase 3E polishes the P-HPR workflow around the existing Devices, Profiles, Settings, and Diagnostics pages.

It does not add a new hardware output path. P-HPR remains separate from ASIO, `IAudioOutputDevice`, the audio mixer, and the BST-1 shaker path.

## Devices Workflow

The Devices page now includes a `P-HPR Workflow Summary` section above the detailed P-HPR panels.

The summary reports:

- workflow mode: `Disabled`, `Mock`, or `Real Direct Control`,
- selected real output status without printing private device paths,
- SimPro/SimHub coexistence status,
- direct-control enabled/armed state,
- emergency-stop state,
- validation readiness,
- instant gear-pulse settings,
- road-vibration settings,
- wheel-slip and wheel-lock settings,
- mock routing counters,
- real write/failure counters,
- last real-output error.

The lower Devices sections remain the source of truth for detailed controls:

- read-only SimPro/SimHub coexistence detection,
- direct-write readiness,
- real direct control,
- controlled validation harness,
- live paddle input diagnostics,
- shift-intent diagnostics,
- mock gear routing,
- mock road/slip/lock routing.

## Profiles

The Profiles page now saves and loads two local JSON files:

- the existing audio profile for ASIO/BST-1 effect, mixer, and audio safety tuning,
- a P-HPR effect profile for shift-intent, mock gear, mock pedal effects, real gear pulse, road vibration, wheel slip, and wheel lock preferences.

The P-HPR profile stores safe effect preferences only.

It does not store:

- real direct-control enablement,
- real direct-control arming,
- selected private HID device path,
- emergency-stop latch,
- command history,
- write history,
- validation result data.

Loading a P-HPR profile applies safe effect settings to the current runtime and app settings while preserving runtime-only arm/device state.

## Diagnostics Report

The Diagnostics page and copied diagnostics report include:

- audio and P-HPR profile paths,
- P-HPR workflow mode,
- coexistence state,
- direct-control state,
- selected-output status without raw private path,
- mock status,
- real status,
- gear/road/slip/lock settings,
- validation status,
- last write status,
- last errors,
- persistence boundary notes.

Diagnostics intentionally do not include raw captures, serial numbers, private validation results, or unsanitized device inventories.

## Non-Claims

Phase 3E does not prove physical P-HPR safety, pedal mapping, safe gain, stop behavior, sustained-vibration behavior, physical latency, road feel, slip feel, or lock feel.

Automated verification uses app models, mock output, and fake HID writers only.
