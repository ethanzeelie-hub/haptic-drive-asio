# Haptic Drive ASIO Troubleshooting

## No P-HPR Vibration

Check the path in this order:

1. Confirm P-HPR mock mode updates diagnostics first.
2. Confirm F1 25 UDP telemetry has valid parser counts and fresh telemetry age.
3. Confirm `DrivingArmed` is true during active driving.
4. Confirm the paddle listener is running and mapped left/right paddles show accepted shift intents.
5. Confirm SimPro/SimHub coexistence is `Clear`.
6. Confirm real direct control is enabled and armed for the current session.
7. Confirm a P-HPR device/interface/report is selected.
8. Confirm emergency stop is clear.
9. Confirm the target pedal is enabled and strength/frequency/duration are non-zero and within safety caps.

Automated tests use fake output only. They do not prove real hardware response.

## Wrong Pedal

Stop immediately.

1. Press P-HPR emergency stop.
2. Disable real direct control.
3. Verify brake and throttle module wiring.
4. Re-check selected interface/report.
5. Use one low-strength manual pulse at a time.
6. Record the result in the controlled validation harness.

Do not raise strength or enable road/slip/lock until wrong-pedal behavior is resolved.

## Menu Suppression

If paddles fire in menus, pause, garage, results, or while tabbed out:

1. Check `DrivingArmed` reason in Devices or Diagnostics.
2. Confirm telemetry is fresh and F1 25 session/lap/car status packets are being parsed.
3. Keep Menu Safe Mode enabled.
4. Confirm shift-intent diagnostics show suppressed events when `DrivingArmed` is false.
5. Keep real direct control disabled until the menu state is understood.

Live menu suppression may need local refinement after real F1 25 observations.

## SimPro / SimHub Conflict

Real direct starts require coexistence status `Clear`.

1. Close SimPro Manager and SimHub for first direct tests.
2. Refresh coexistence diagnostics.
3. If status remains non-clear, leave real direct control disabled.
4. Use conflict testing only after safe brake/throttle stop behavior is proven.

The app uses read-only process detection only. It does not kill, hook, inject into, patch, control, or modify either application.

## Device / Interface Selection

If the selected P-HPR output does not open or reports invalid length:

1. Re-check device path, interface name, report ID, and report length.
2. The SimHub F1 EC real adapter expects the configured report length to match the encoder length.
3. Clear emergency stop only after direct control is disabled or the output state is understood.
4. Re-select the device for the current session; selected private paths are not persisted.

Do not commit private device paths, serial numbers, raw USB captures, or private validation exports.

## F1 25 Telemetry Not Updating

1. Confirm F1 25 UDP telemetry is enabled.
2. Confirm the port is `20778`.
3. Check listener running state, packet count, packet rate, parser valid/ignored/failed counts, and VehicleState update count.
4. Check local firewall/network settings if packet count stays zero.
5. Use a known `.hdrec` replay to separate parser/effect issues from live UDP issues.

## Replay Does Not Trigger Gear Pulse

That is expected. Replay drives telemetry-derived road, wheel slip, and wheel lock routing. It does not create GT Neo paddle events or validate instant paddle input unless a future explicit synthetic-input test path is added.

## Emergency Stop Latched

Emergency stop latches safety state.

1. Confirm output stopped.
2. Disable direct control if behavior is unclear.
3. Use Clear Emergency Stop only after the situation is safe.
4. Re-arm direct control manually if another real test is needed.

## Direct Bench Recovery Logs

For Direct Paddle Gear Bench crashes or possible runaway output, collect these local files before changing settings:

1. `local-validation-results/phpr-direct-bench-flight-recorder.jsonl`
2. `local-validation-results/phpr-direct-bench-unclean-shutdown.marker`
3. Windows Event Viewer `Application` entries for `HapticDrive.Asio.App.exe`.
4. The Devices diagnostics text around runtime state, marker state, route service, selected output summary, last stop result, watchdog stop-all, and latency fields.

In the JSONL recorder, read from the last `start-requested` or `start-write-completed` forward. Check whether a matching `scheduled-stop-completed`, `manual-stop-all-completed`, `startup-cleanup-completed`, `paddle input exception recovery`, or `unhandled-exception-stop-all-completed` appears, whether `manualStopAllWriteSucceeded` is true, and whether `errorCategory` is populated.

Stage 18f marshals Direct Paddle Gear Bench paddle-callback UI updates through the WPF dispatcher. A current crash report should not contain `appdomain-unhandled` for `UpdateRealPhprDirectControlStatus` or the WPF message `The calling thread cannot access this object because a different thread owns it`.

If the marker exists, Direct Bench starts are blocked by design. Use `P-HPR Stop All / Clear Device State`; it sends stop-only reports and clears the marker only after the runtime reports success.
