# Simagic P-HPR Output Adapter

## Phase 3A Status

Phase 3A hardens the Stage 2Q direct P-HPR backend into a production-quality adapter boundary while preserving every direct-control safety gate.

The adapter remains disabled and unarmed by default. Automated verification uses fake HID writers only and does not send hardware output.

## Adapter Boundary

The real direct-control backend lives in `HapticDrive.Simagic.PHPR.Output.Windows`:

- `SimagicPhprOutputDevice`: gated `IPHprOutputDevice` implementation.
- `IPhprHidReportWriter`: fakeable writer lifecycle boundary.
- `WindowsHidReportWriter`: Windows selected-HID-path writer.
- `SimHubF1EcRealReportEncoder`: 64-byte SimHub `F1 EC` start/stop encoder.
- `PHprRealOutputDiagnostics`: command, report, connection, lifecycle, and failure diagnostics.
- `PHprDirectGearPulseRouter`: accepted shift-intent to brake/throttle command routing.

The adapter is separate from ASIO, `IAudioOutputDevice`, the audio mixer, and the BST-1 output path.

## Lifecycle

The writer boundary now has explicit operations:

```text
OpenAsync
WriteReportAsync
CloseAsync
```

`SimagicPhprOutputDevice` opens the writer lazily only during an explicit start, stop, or emergency-stop operation. App startup and plain configuration do not open a device handle and do not write reports.

Normal start commands require:

- direct control enabled,
- direct control armed,
- selected device path/interface/report,
- report length matching the 64-byte SimHub F1 EC payload,
- selected writer interface matching the configured selector,
- SimPro/SimHub coexistence `Clear`,
- emergency stop clear,
- `PHprSafetyLimiter` acceptance.

Stop and emergency-stop paths may attempt stop reports with a selected valid interface so the app can try to quiet the modules. Dispose attempts emergency-stop-style brake/throttle stop reports only when direct control is armed or a stop is pending, then closes the writer where possible.

## Failure Handling

The adapter records and surfaces:

- connection state,
- writer-open state,
- open attempt/success counts,
- close attempt/success counts,
- successful report count,
- failed report count,
- stop report count,
- timeout count,
- disconnect count,
- invalid-report count,
- last open/write/stop/close status,
- last target/report state/report length,
- last error.

Write operations are timeout-wrapped with a normalized default of `250 ms`. Timeouts, disconnects, invalid report length/ID, not-selected state, and generic failures are classified separately for diagnostics.

If a write reports disconnect, later start commands are blocked through the safety context until the user reconfigures the selected output path.

## WPF Diagnostics

The Devices page `P-HPR Real Direct Control` panel now shows:

- connection state,
- writer-open state,
- lifecycle counters,
- write timeout,
- last write/stop/open/close status,
- disconnect/timeout/invalid-report counters.

No new automatic output trigger is added in Phase 3A.

## Tests

Phase 3A fake-writer tests cover:

- explicit open/close without reports,
- open blocked without enabled/armed state,
- write failure,
- stop failure,
- disconnect handling,
- write timeout handling,
- report length validation before writer open,
- dispose stop plus close behavior,
- emergency stop,
- no persisted armed state,
- safety-bypass prevention.

No test opens the real Windows HID writer against hardware.

## Non-Claims

Phase 3A does not prove:

- the user's exact P700/P-HPR HID path,
- physical brake/throttle mapping,
- physical stop behavior,
- safe gain,
- physical latency,
- sustained-vibration safety,
- SimPro/SimHub real-device coexistence.

Those remain local supervised validation tasks.
