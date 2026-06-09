# Manual Hardware Tests

Manual hardware tests are opt-in checks for real output devices. They now run as readiness checks instead of xUnit skips, so the normal suite can report zero skipped tests without silently energizing hardware.

These tests must not require physical output for automated validation until the matching hardware path is explicitly enabled through local flags or a controlled manual command.

Current Stage 18 hardware status: the M-Audio M-Track Solo is connected to the user's Windows PC and the driver is installed. The Fosi amplifier has been received. The Dayton BST-1 shaker has not arrived, so physical shaker output testing is deferred. Stage 18 diagnostics may report ASIO driver visibility, render callbacks, backend callbacks, drops, underruns, jitter, telemetry age, forwarding destinations, recording library state, and packet-ID counts, but automated tests still use fake ASIO catalogs/backends and Null output.

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
- Confirm the Dayton BST-1 is connected correctly only after it arrives. If it has not arrived, do not run physical output tests.
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
- Do not connect or energize the Dayton BST-1 until it arrives and the manual physical test is intentionally run.

## Stage 18 Final Pre-Shaker Checklist

Before the BT-1 arrives, the safe software package should be checked through:

- Launch wrapper startup.
- Null output startup.
- ASIO driver visibility refresh.
- Explicit ASIO driver/channel selection without auto-arming.
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
