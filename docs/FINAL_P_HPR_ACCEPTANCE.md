# Final P-HPR Acceptance Package

Phase 3H packages the current P-HPR implementation for final review. Phase 3J adds final controlled-write readiness and zero-skip automated reporting. Neither claims physical validation.

## Feature Status

- Instant paddle P-HPR gear pulse: implemented.
- Per-pedal brake/throttle gear settings: implemented.
- Strength/frequency/duration settings: implemented for gear pulse, road, slip, and lock routes.
- Road vibration: implemented through the P-HPR backend and separate from ASIO/BST-1 road texture.
- Wheel slip: implemented through the P-HPR backend and separate from ASIO/BST-1 slip effects.
- Wheel lock: implemented through the P-HPR backend and separate from ASIO/BST-1 brake-lock effects.
- Mock mode: implemented and hardware-safe.
- Real direct mode: implemented but disabled and unarmed by default.
- Emergency stop: implemented with latch and clear behavior.
- SimPro/SimHub detection: implemented as read-only process detection.
- Replay validation: implemented for road/slip/lock software routing.
- Live F1 validation workflow: implemented as a passive manual checklist.
- Controlled P-HPR CLI smoke test: implemented, dry-run by default, real writes gated by exact approval phrase plus `--execute`.
- Direct-output candidate picker: implemented with safe labels, runtime-only private HID paths, HID device-interface selection, HID registry metadata surfacing, OutputReport/FeatureReport transport selection, and no-report/no-feature-report open-check.
- User guide, quick start, troubleshooting, and final acceptance docs: implemented.

## Safety Status

- Real writes default off.
- Real direct arming is not persisted.
- Selected private HID device path is not persisted.
- Safety limiter remains active.
- Emergency stop remains available and latched until cleared.
- SimPro/SimHub non-clear coexistence blocks real direct starts.
- Raw Input metadata-only candidates cannot pass real direct-output gates.
- Real direct `can pulse` requires a successful no-report HID open-check plus known selected HID output-report or feature-report capability, selected transport/report ID/length match, and successful no-command report-shape validation on the selected HID device-interface candidate.
- Feature report ID `0xF1` is surfaced for compatible `VID_3670` candidates and treated as the likely F1 EC command-family shape, but no physical behavior is claimed until local supervised validation.
- `IOException:0x80070057` from HID writes is treated as a report-shape/write-format failure.
- Automated tests and CI do not write to hardware.
- Automated tests now report zero skips by converting prior manual ASIO checks into readiness/pending checks.
- Real P-HPR writes require the controlled CLI command or manually armed app direct mode; they are not part of normal tests.

## Manual Acceptance Checklist

Use this checklist locally before claiming physical validation:

1. App launches with real direct control disabled and unarmed.
2. F1 25 UDP telemetry is active and parser counts increase.
3. `DrivingArmed` becomes true only during active driving.
4. GT Neo paddle presses are mapped and accepted in active driving.
5. Mock gear pulse diagnostics update from accepted paddle presses.
6. Real direct mode is enabled and armed manually only.
7. Brake gear pulse is low strength, short duration, and stops.
8. Throttle gear pulse is low strength, short duration, and stops.
9. Road vibration is tested only after one-pulse behavior is safe.
10. Slip/lock are tested only if safe and controllable.
11. Paddles are suppressed in menus, pause, garage, results, or tabbed-out states.
12. Emergency stop stops both modules and latches.
13. SimPro/SimHub conflict warnings and blocking are visible.
14. Wrong-pedal behavior is absent or documented.
15. Sustained vibration is absent or documented.
16. Private local validation notes are exported outside committed docs.
17. The selected direct-output candidate is a HID device-interface candidate, not Raw Input metadata only.
18. Open-check succeeds for the selected candidate without sending an output report or feature report.
19. Dry-run reports selected transport, report ID, report byte length, expected first bytes, known output/feature capability, and successful report-shape validation before any manual pulse.
20. Optional final CLI smoke test is run with `controlled-write-test --execute` only after the dry run and open-check look correct.

## Verification

Phase 3H final verification must pass:

- restore,
- build,
- full tests,
- format,
- launch preflight,
- safe P-HPR research CLI help,
- mock protocol examples,
- safety examples.
- controlled-write dry-run and fake-writer tests.
- direct-output dry-run and open-check validation.

Normal full-suite verification should report zero skipped tests. Zero skipped tests do not prove physical validation.

## Physical Validation Status

Controlled P-HPR write testing is approved and the command path exists, but physical P-HPR validation is pending Ethan's local supervised run with a selected private HID path. No physical safety, safe gain, pedal mapping, stop behavior, physical latency, sustained-vibration behavior, road feel, slip feel, lock feel, or real SimPro/SimHub coexistence claim is made by automated work.

## Run Command

```powershell
cd "C:\Users\ethan\OneDrive\Documents\ASIO Haptic Engine Program"
.\Run-HapticDrive.cmd
```

## Final Docs

- User guide: `docs\USER_GUIDE.md`
- Quick start: `docs\QUICK_START.md`
- Troubleshooting: `docs\TROUBLESHOOTING.md`
- Final acceptance: `docs\FINAL_P_HPR_ACCEPTANCE.md`
