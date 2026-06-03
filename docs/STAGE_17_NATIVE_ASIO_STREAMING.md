# Stage 17 Native ASIO Streaming

Stage 17 adds the first native ASIO streaming backend and moves live haptic rendering into an output-owned low-latency render path while keeping `NullAudioOutputDevice` as the default automated-test and startup output.

## Implemented

- Added `NativeAsioOutputBackend` behind `IAsioOutputBackend` using `NAudio.Asio`.
- Kept ASIO explicit: output mode, driver, output channel, arming, and Start Haptics must all be intentional.
- Removed the WPF haptic render `DispatcherTimer` from the live pipeline.
- Added output-owned render callback support to `IAudioOutputDevice`.
- Kept deterministic manual buffer submission for tests and the test bench.
- Added wall-clock stale telemetry mute so old live telemetry cannot keep driving effects indefinitely.
- Added diagnostics for render callbacks, backend callbacks, submitted buffers, dropped buffers, underruns, render duration, callback jitter, and telemetry age.
- Added fake-backend tests for callback cadence, stale telemetry mute, emergency mute, channel routing, stop/dispose, and dropped-buffer diagnostics.

## Render Path Rules

The render callback is restricted to in-memory audio work:

```text
current VehicleState/effect state
-> haptic effects
-> mixer
-> safety processor
-> output buffer
```

The render callback must not touch UI, disk IO, logging, networking, blocking waits, or async continuations. Live UDP forwarding, telemetry recording, replay scheduling, profile save/load, and UI refresh remain outside the render path.

## Stale Telemetry Mute

The runtime tracks the wall-clock time of the last applied `VehicleState` update. If the timeout is exceeded, the output callback renders safety silence until fresh parsed telemetry updates `VehicleState` again.

This prevents stale live telemetry from continuing to energize effects indefinitely if UDP packets stop, replay ends, or parsing stops producing fresh vehicle state.

## ASIO Streaming Notes

- `NativeAsioOutputBackend` opens the selected driver through NAudio `AsioOut`.
- The app renders mono safety-processed buffers, then routes them to the selected ASIO output channel.
- A small preallocated queue bridges app rendering and the ASIO driver callback.
- Dropped queue submissions and callback underruns are counted.
- Diagnostics are useful for readiness and debugging, but they are not final physical latency measurements.

## Manual Pre-Shaker Checklist

1. Start in Null output and verify the app opens safely.
2. Refresh ASIO diagnostics.
3. Select ASIO deliberately.
4. Select the M-Audio / M-Track ASIO driver deliberately.
5. Select one output channel deliberately.
6. Arm ASIO deliberately.
7. Keep software gains conservative.
8. Start Haptics only after driver, channel, and arming state are intentional.
9. Watch render callbacks, backend callbacks, drops, underruns, jitter, and telemetry age.
10. Verify Emergency Mute.
11. Verify Stop Haptics.
12. Use Null output, fake backend tests, and replay before any future physical shaker test.

Do not claim final shaker feel, safe physical gain, physical latency, or final frequency tuning until the Dayton BST-1 is connected and the full chain has been tested locally.
