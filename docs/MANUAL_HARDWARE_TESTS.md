# Manual Hardware Tests

Manual hardware tests are opt-in checks for real output devices. They now run as readiness checks instead of xUnit skips, so the normal suite can report zero skipped tests without silently energizing hardware.

These tests must not require physical output for automated validation until the matching hardware path is explicitly enabled through local flags or a controlled manual command.

Current Stage 18 follow-up hardware status: the M-Audio M-Track Solo is connected to the user's Windows PC, the driver is installed, the Fosi amplifier is connected, and the Dayton BST-1 chain has been proven working through SimHub. Haptic Drive ASIO app-driven BST-1 output remains pending the new manual ASIO hardware test. Diagnostics may report ASIO driver visibility, render callbacks, backend callbacks, drops, underruns, jitter, telemetry age, forwarding destinations, recording library state, packet-ID counts, manual ASIO hardware test status, and blocked reasons, but automated tests still use fake ASIO catalogs/backends and Null output.

## Manual Test Markers

- `HapticDrive.Asio.Audio.Tests.OutputDeviceTests.Manual_AsioOutputDevice_ReportsPendingOrOpensRealDriverWhenExplicitlyEnabled`
- `HapticDrive.Asio.Audio.Tests.AsioOutputReadinessTests.Manual_MAudioAsioDriverDiscovery_ReportsPendingOrVisibleWhenExplicitlyEnabled`
- `HapticDrive.Asio.Audio.Tests.AsioOutputReadinessTests.Manual_DaytonBst1PhysicalOutput_ReportsPendingUntilExplicitlyValidated`

These are no longer skipped. Without local environment flags they verify that readiness checks complete safely and report physical validation as pending. With local flags, they become stricter ASIO/BST-1 checks:

```powershell
$env:HAPTICDRIVE_RUN_ASIO_HARDWARE_TESTS = "1"
$env:HAPTICDRIVE_BST1_ARRIVED = "1"
$env:HAPTICDRIVE_BST1_PHYSICAL_OUTPUT_VALIDATED = "1"
```

Do not set the BST-1 validation flag until the shaker output check has actually been completed locally.

## Before Running Any Manual Hardware Test

- Confirm the M-Audio interface is connected by USB.
- Confirm the M-Audio ASIO driver is installed.
- Confirm the app launches through `Run-HapticDrive.cmd` or that .NET 8 Desktop Runtime x64 is available for direct executable launch.
- Confirm Windows sound settings can see the M-Audio endpoint, but do not treat that as proof of ASIO usage.
- Confirm the app ASIO driver list can see the M-Audio / M-Track ASIO driver.
- Confirm the amplifier is at minimum volume if connected.
- Confirm the Dayton BST-1 is connected correctly before any manual ASIO hardware pulse.
- Confirm Windows default audio/WASAPI debug output is not being mistaken for ASIO.
- Confirm the test is expected to make sound or haptic movement before enabling it.
- Confirm the app is not using `NullAudioOutputDevice` if the goal is a future physical-output test.
- Confirm whether the test path is ASIO/BST-1 or Simagic P-HPR. P-HPR modules are not ASIO audio devices and use the separate controlled P-HPR write command.

## Controlled P-HPR Write Smoke Test

Ethan has approved controlled Phase 2 P-HPR write testing with the exact phrase:

```text
I approve Phase 2 controlled P-HPR write testing
```

Before any real pulse, use the app picker or `direct-output-dry-run` / `direct-output-open-check` to confirm the selected candidate is a HID device-interface path, not Raw Input metadata only, and that open-check succeeds without sending an output report.

The command below is the only CLI path that can send real P-HPR HID reports. It defaults to dry-run unless `--execute` is present, hides the private HID path from console output, blocks non-clear SimPro/SimHub coexistence, sends a low-strength pulse plan, and requests emergency stop at the end.

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- controlled-write-test --approval "I approve Phase 2 controlled P-HPR write testing" --device-path "<private-hid-path>" --target sequence --strength-percent 10 --frequency-hz 50 --duration-ms 50
```

Add `--execute` only when physically present, the selected private HID path has passed no-report open-check, SimPro/SimHub are closed or clear, and emergency stop is visible:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- controlled-write-test --approval "I approve Phase 2 controlled P-HPR write testing" --device-path "<private-hid-path>" --target sequence --strength-percent 10 --frequency-hz 50 --duration-ms 50 --execute
```

## Stage 16 Manual Readiness Checklist

Use `docs/STAGE_16_ASIO_READINESS.md` for the full checklist.

Short version:

- Start with Null output.
- Refresh ASIO diagnostics.
- Select ASIO deliberately.
- Select the M-Audio / M-Track driver deliberately.
- Select one output channel deliberately.
- Arm ASIO deliberately.
- Start Haptics deliberately.
- Verify Emergency Mute and Stop Haptics.
- Use test bench first, then replay, then live UDP only after the lower-risk checks are safe.
- Keep physical gain changes manual and gradual after the Dayton BST-1 arrives.

## Stage 17 Manual Streaming Checklist

Use `docs/STAGE_17_NATIVE_ASIO_STREAMING.md` after the Stage 16 readiness checklist.

Short version:

- Keep Null output as the startup/default path.
- Select ASIO, driver, channel, and arming deliberately.
- Start Haptics deliberately.
- Watch render callbacks, backend callbacks, dropped buffers, underruns, jitter, and telemetry age.
- Verify stale telemetry mute by stopping replay or live telemetry and confirming output returns to silence.
- Verify Emergency Mute and Stop Haptics.
- Do not energize the Dayton BST-1 from Haptic Drive ASIO unless the manual physical test is intentionally being run.

## Manual ASIO Bass Shaker Test

Use this only when you deliberately want Haptic Drive ASIO to energize the connected BST-1 for a short pulse.

Required gates:

- Output mode is `ASIO Output`.
- Driver is the M-Audio / M-Track ASIO driver.
- Output channel 0 or 1 is selected deliberately.
- ASIO is armed.
- Start Haptics is not required for `Test BST-1 Pulse` or the local BST-1 paddle gear pulse path.
- Emergency mute is clear.
- Normal mute is off.
- The selected output channel is within the reported ASIO output-channel count.

UI workflow:

- Open Devices.
- Confirm Bass Shaker / ASIO status shows ASIO Output, the M-Audio / M-Track driver, armed true, and a valid channel. `ASIO READY` is sufficient for the standalone pulse path; `ASIO ACTIVE` means a stream/callback is currently running.
- Use `Select channel 0` or `Select channel 1` to choose the intended zero-based ASIO channel. Channel selection must not vibrate.
- Use `Test BST-1 Pulse` to run the selected frequency/strength/duration. Start Haptics, UDP, replay, F1 telemetry, `VehicleState`, and `DrivingArmed` are not required for this local pulse.
- Use a short duration first; runtime requests are capped at 1 second.
- `Mono / both` is diagnostic-only in the current single-selected-channel architecture and does not start output.

The manual ASIO test signal is routed through the Stage 10 mixer, safety chain, limiter, and selected ASIO output channel. It is separate from the Null synthetic benchmark, and it never routes to Simagic P-HPR.

Stage 18n-B diagnostics for manual and local paddle BST-1 pulses are written locally to:

```text
local-validation-results/bst1-asio-pulse-flight-recorder.jsonl
```

Rotated files such as `local-validation-results/bst1-asio-pulse-flight-recorder.jsonl.1` are local validation evidence only and must not be committed.

The recorder includes queue capacity/count, callback activity, expected frame count, accepted frame count, rendered frame count, completion reason, accepted/dropped buffer counts, limiter peak, pulse source ID, renderer instance ID, transport path, haptics-running-at-start state, pulse-owned pre/post limiter frame and energy proof, global callback delta, and latest-press-wins replacement status. A non-zero pulse may be recorded as `completed-full` only after the expected pulse-owned frame count has rendered and non-zero post-limiter peak/RMS energy exists. Global callback movement alone is not enough.

Manual and local paddle BST-1 pulses use the same generator, mixer, safety chain, limiter, output trim, and selected ASIO channel whether Start Haptics is stopped or running. If haptics are already running, the transport path is `live-haptics-callback`; if haptics are stopped, the transport path is `local-persistent-callback` and the output-owned callback is lazy-started by the explicit pulse.

Fresh app startup may open the selected ASIO driver for readiness/capability hydration without starting output. This is allowed to cache the M-Audio output-channel count for channel validation, but it must not emit startup output.

The disabled `Minimize to tray on close` placeholder is removed until a real tray mode exists. Closing the window should dispose ASIO, local pulse, paddle listener, UDP listener, timers, and related resources, write shutdown diagnostics, and terminate the WPF process.

## Stage 18q-B Road Texture Evidence Capture

Stage 18q-B is diagnostics-only. It does not fix weak BST-1 road gain and does not change the current P-HPR road pulse/cadence model.

Before the next physical road validation run:

- Open Advanced / Diagnostics.
- Check `Record road texture flight recorder`.
- Confirm the displayed path is `local-validation-results/road-texture-flight-recorder.jsonl`.
- Start the same replay or live telemetry scenario being physically evaluated.
- Capture a normal diagnostics export after the run.
- Keep the JSONL file local unless a sanitized copy is intentionally attached for review.

The road recorder is disabled by default and writes from the diagnostics/status path, not from the ASIO callback. It records the shared road signal, BST-1 pre/post gain proof, P-HPR road route cadence and suppression counters, haptics running state, emergency mute/stop state, replay source, telemetry age, and stale/historical last-road markers.

Use the new evidence to decide later whether the BST-1 issue is evaluator scaling, renderer gain, mixer/safety gain, or hardware gain staging, and whether the P-HPR issue is route cadence, gate suppression, command drop, or pulse semantics. Gear pulse priority remains protected and must be revalidated before any future tuning stage is accepted.

## Stage 18q-C/D/E/F Road Texture Validation Checklist

Stage 18q-C/D/E changes road behavior and must be validated locally before treating the new tuning as accepted. Codex verification is software/fake-backed only.

Recommended starting settings:

- Shared road signal enabled.
- Limiter enabled.
- Emergency mute off.
- Master gain and safety output gain set deliberately in Routing / Mixer.
- BST-1 road output enabled only for BST-1 tests. Start at the previous equivalent 25% BST-1 / ASIO road output gain, then increase in small steps toward 100% only if the shaker remains controlled.
- P-HPR road output starts low. Use brake/throttle road output scale around 10-15% for the first cadence test, then increase gradually.
- Keep P-HPR gear disabled until road-only cadence is proven, then enable gear to validate priority.

Run in this exact order:

1. BST-1 road only.
   - Start real-time replay from `f1-25-20260612-063003.hdrec`.
   - Enable shared road signal and BST-1 road output.
   - Disable P-HPR road.
   - Increase BST-1 / ASIO road output gain gradually from 25%.
   - Confirm road feels continuous and road-like, not clipped or limiter-driven.
   - Export diagnostics and keep the road flight recorder JSONL.

2. P-HPR road only.
   - Enable shared road signal.
   - Disable BST-1 road output.
   - Enable real P-HPR road plus brake/throttle road outputs at low scale.
   - Confirm road is no longer sparse 3-5 second isolated thumps.
   - Watch diagnostics for runtime `Active`, cadence near 100 ms, duration around 180-220 ms or higher, no command-rate suppression, and clear stop reasons.

3. BST-1 + P-HPR road together.
   - Enable shared road signal, BST-1 road output, and both P-HPR road outputs.
   - Keep levels conservative at first.
   - Confirm the combined feel is road-like and not a thump generator.

4. Paddle gear while road is active.
   - Enable gear after road-only behavior is proven.
   - Trigger accepted paddle/local gear pulses.
   - Confirm gear clearly cuts through road.
   - Confirm road ducks/stops and resumes after the gear window without starving gear commands.

5. Emergency Stop.
   - While P-HPR road is active, press Emergency Stop.
   - Confirm P-HPR output stops and the emergency state is visible.
   - Confirm no road output resumes until the emergency state is intentionally cleared and normal gates are safe again.

6. Stop Haptics.
   - Run road briefly.
   - Press Stop Haptics.
   - Confirm BST-1 audio road stops and P-HPR road records a stop/idle state.

7. App close.
   - Run road briefly.
   - Close the app normally.
   - Confirm shutdown performs safe cleanup and a later launch does not persist runtime-only direct-control active state, emergency state, haptics running state, or active test state.

Stop immediately and export diagnostics plus `local-validation-results/road-texture-flight-recorder.jsonl` if P-HPR road feels like sparse thumps, sticks on, blocks gear pulses, triggers command-rate suppression, or fails to stop on Emergency Stop / Stop Haptics / app close.

## Stage 18 Final Pre-Shaker Checklist

Before app-driven BST-1 validation, the safe software package should be checked through:

- Launch wrapper startup.
- Null output startup.
- ASIO driver visibility refresh.
- Explicit ASIO driver/channel selection without auto-arming.
- Manual ASIO Bass Shaker Test blocked-reason display while still on Null output.
- Deliberate manual ASIO 40/50 Hz pulse only after the connected BST-1 chain is ready.
- UDP forwarding destination add/edit/remove with loopback protection.
- Recording start/stop and recordings library refresh.
- Replay latest and replay selected recording.
- Diagnostics refresh and copy report.
- Emergency Mute and Stop Haptics.

## Stage 02 Expected Result

- With no ASIO driver available, ASIO output returns a failure result instead of crashing.
- With a matching fake driver catalog and fake backend in tests, ASIO output can validate explicit driver selection, arming, channel routing, lifecycle, stop, dispose, and safety-processed buffer submission.
- Stage 17 fake-backend tests also validate output-owned callback cadence, dropped-buffer diagnostics, stale telemetry mute, emergency mute, and stop/dispose behavior.
- No automated test requires real ASIO, WASAPI, M-Audio, Fosi, Dayton BST-1, Simagic P700/P-HPR hardware, F1 25, or live telemetry.
- Normal test runs should report zero skipped tests. A zero-skip test run is not the same as physical validation.
