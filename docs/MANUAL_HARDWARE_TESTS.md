# Manual Hardware Tests

Manual hardware tests are opt-in checks for real output devices. They must stay skipped by default until the user confirms the full hardware chain is available and a manual test is intended.

These tests must not be required for automated validation until the user confirms hardware is available.

Current Stage 15 hardware status: the M-Audio M-Track Solo and Fosi amplifier may be present locally, but the Dayton BST-1 shaker has not been validated in this project yet. Stage 15 diagnostics may report ASIO driver visibility only; they do not stream to hardware.

## Stage 02 Manual Test Marker

`HapticDrive.Asio.Audio.Tests.OutputDeviceTests.Manual_AsioOutputDevice_OpensRealDriverWhenHardwareIsAvailable` is skipped by default.

It is only a marker at this stage because real ASIO streaming is not implemented yet.

## Before Running Any Manual Hardware Test

- Confirm the M-Audio interface is connected.
- Confirm the M-Audio ASIO driver is installed.
- Confirm the amplifier is at a safe low level.
- Confirm the Dayton BST-1 is connected correctly. If it has not arrived, do not run physical output tests.
- Confirm Windows default audio/WASAPI debug output is not being mistaken for ASIO.
- Confirm the test is expected to make sound or haptic movement before enabling it.
- Confirm the app is not using `NullAudioOutputDevice` if the goal is a future physical-output test.

## Stage 02 Expected Result

- With no ASIO driver available, ASIO output returns a failure result instead of crashing.
- With a matching driver catalog in tests, ASIO output can select the preferred driver name.
- No real ASIO callback or sample stream is started in this stage.
