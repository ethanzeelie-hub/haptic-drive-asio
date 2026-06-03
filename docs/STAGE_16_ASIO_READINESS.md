# Stage 16 Manual ASIO Readiness

Stage 16 prepares the app for controlled local ASIO readiness checks with the M-Audio M-Track Solo while keeping `NullAudioOutputDevice` as the default automated-test and startup output.

## Hardware State

- M-Audio M-Track Solo: received, plugged into the user's Windows PC, visible in Windows sound settings, and driver installed.
- Fosi Audio BT20A: received.
- Dayton BST-1: not yet received, so physical shaker testing is deferred.

Windows sound output visibility only proves that Windows sees an audio endpoint. It does not prove that Haptic Drive ASIO is using ASIO. ASIO usage must be confirmed through the app ASIO driver catalog, selected output mode, selected driver, channel route, arming state, and output diagnostics.

## Implemented Readiness

- `WindowsRegistryAsioDriverCatalog` lists ASIO driver names from standard Windows ASIO registry locations when available.
- `IAsioDriverCatalog` remains fakeable so automated tests do not require M-Audio or any ASIO driver.
- `AsioReadinessDiagnostics` reports ASIO availability, M-Audio / M-Track driver visibility, selected output mode, selected driver, sample rate, buffer size, selected channel, arming state, running state, buffer counters, and last error.
- `AsioAudioOutputDevice` now requires explicit driver selection, explicit output-channel selection, explicit arming, and explicit Start Haptics before it can run.
- ASIO buffers are accepted only after the existing effect, mixer, and safety processor path has produced the final mono output buffer.
- Stage 16 mono routing maps the safety-processed mono source to one selected ASIO output channel and clears all other routed channels.
- Invalid driver selection, missing driver selection, missing channel selection, invalid channel selection, backend open failure, backend start failure, stop, and dispose are handled safely.

## Stage 17 Follow-Up

Native ASIO streaming is now implemented in Stage 17 behind `IAsioOutputBackend`. This Stage 16 checklist remains the readiness prerequisite: confirm driver visibility, deliberate ASIO selection, channel selection, and arming before using the Stage 17 streaming checklist. Stage 17 still does not validate physical shaker feel, safe physical gain, physical latency, or final frequency tuning.

## Manual M-Audio Checklist

1. Confirm the M-Audio M-Track Solo is connected by USB.
2. Confirm the M-Audio Windows driver is installed.
3. Confirm Windows sound settings can see the M-Audio device, while remembering this does not prove ASIO usage.
4. Start the app and confirm output starts as `NullAudioOutputDevice`.
5. Open Devices and press `Refresh ASIO`.
6. Confirm the app ASIO driver list shows an M-Audio / M-Track ASIO driver.
7. Keep the Fosi BT20A volume at minimum if it is connected.
8. Do not connect or test the Dayton BST-1 until it arrives.
9. Select ASIO deliberately in the app.
10. Select the M-Audio / M-Track ASIO driver deliberately.
11. Select a single output channel deliberately.
12. Arm ASIO deliberately.
13. Keep master/effect/safety gains conservative.
14. Press Start Haptics only after selection, routing, and arming are intentional.
15. Use the synthetic test bench path first at low software level when a real streaming backend exists.
16. Verify Emergency Mute.
17. Verify Stop Haptics.
18. Verify replay-driven pipeline before live F1 25 UDP.
19. Verify live F1 25 UDP only after replay/test-bench checks are safe.
20. Increase physical gain only gradually by the human tester after the Dayton BST-1 arrives.
21. Record observed driver, routing, underrun, or startup issues manually.

Do not claim final shaker feel, safe physical gain, physical latency, or final frequency tuning until the actual Dayton BST-1 chain has been installed and tested locally.
