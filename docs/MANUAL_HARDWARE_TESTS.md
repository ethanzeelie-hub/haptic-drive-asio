# Manual Hardware Tests

Manual hardware tests are opt-in checks for real output devices. They must stay skipped by default until the user confirms the full hardware chain is available and a manual test is intended.

These tests must not be required for automated validation until the user confirms hardware is available.

Current Stage 16 hardware status: the M-Audio M-Track Solo is connected to the user's Windows PC and the driver is installed. The Fosi amplifier has been received. The Dayton BST-1 shaker has not arrived, so physical shaker output testing is deferred. Stage 16 diagnostics may report ASIO driver visibility and readiness state, but automated tests still use fake ASIO catalogs/backends and Null output.

## Stage 02 Manual Test Marker

`HapticDrive.Asio.Audio.Tests.OutputDeviceTests.Manual_AsioOutputDevice_OpensRealDriverWhenHardwareIsAvailable` is skipped by default.

It remains skipped by default. Stage 16 adds readiness diagnostics and fake-backend coverage, but native streaming validation is still manual/local work.

## Before Running Any Manual Hardware Test

- Confirm the M-Audio interface is connected by USB.
- Confirm the M-Audio ASIO driver is installed.
- Confirm Windows sound settings can see the M-Audio endpoint, but do not treat that as proof of ASIO usage.
- Confirm the app ASIO driver list can see the M-Audio / M-Track ASIO driver.
- Confirm the amplifier is at minimum volume if connected.
- Confirm the Dayton BST-1 is connected correctly only after it arrives. If it has not arrived, do not run physical output tests.
- Confirm Windows default audio/WASAPI debug output is not being mistaken for ASIO.
- Confirm the test is expected to make sound or haptic movement before enabling it.
- Confirm the app is not using `NullAudioOutputDevice` if the goal is a future physical-output test.

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

## Stage 02 Expected Result

- With no ASIO driver available, ASIO output returns a failure result instead of crashing.
- With a matching fake driver catalog and fake backend in tests, ASIO output can validate explicit driver selection, arming, channel routing, lifecycle, stop, dispose, and safety-processed buffer submission.
- No automated test requires real ASIO, WASAPI, M-Audio, Fosi, Dayton BST-1, F1 25, or live telemetry.
