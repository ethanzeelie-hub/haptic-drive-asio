# Road Texture Guide

Stage 18o-B consolidates road texture around one shared software `RoadTextureSignal`.

## Shared Signal

The Core evaluator reads the shared `VehicleState` and produces one immutable signal for the current frame. The signal records:

- telemetry identity fields such as session UID, session time, frame ID, and overall frame ID;
- runtime gates for road effect enabled state, telemetry freshness, haptics running, and cached `DrivingArmed`;
- speed, per-wheel F1 25 surface IDs, suspension acceleration, wheel vertical force, and vertical G;
- dominant surface class/name, surface mix, roughness, raw intensity, smoothed intensity, output intensity, and frequency hints;
- output-specific frequency hints for BST-1 and P-HPR;
- gear-ducking state and the reason a signal was suppressed.

The evaluator does not parse UDP packets directly and does not change F1 25 packet layouts, byte offsets, enum values, raw recording, replay, or forwarding.

## Inputs

The road signal uses F1 25 `m_surfaceType[4]` as the surface source of truth through `VehicleState`. Motion Ex suspension acceleration and wheel vertical-force deltas can increase roughness after conservative thresholds. Vertical G is a supporting roughness input only.

If telemetry is stale, haptics are stopped, road texture is disabled, or cached `DrivingArmed` is false, live road texture is suppressed. Local/manual contexts can opt into evaluation without `DrivingArmed` only where the runtime explicitly supplies that context.

## Outputs

BST-1 road texture renders the shared signal through `RoadTextureEffect`, then the normal mixer, safety processor, limiter, output trim, and selected audio output. P-HPR road vibration consumes the same signal through `PHprRoadVibrationRouter`, then the P-HPR safety limiter and mock or gated real P-HPR output.

The P-HPR path remains separate from `IAudioOutputDevice`; the shared signal is a decision contract, not a shared hardware transport.

## Gear Priority

Accepted local gear pulses notify the road evaluator/router with the accepted pulse timestamp. During the short ducking window, the shared signal marks gear ducking active. BST-1 renders the ducked road intensity, and P-HPR road suppresses road commands so the gear pulse remains dominant.

This is software arbitration only. Final mixed-output priority, safe gain, physical latency, road feel, and frequency tuning still require Ethan-local hardware validation.

## Stage 18q-B Diagnostics

Stage 18q-B adds road diagnostics and a local flight recorder only. It does not tune BST-1 road gain, raise UI gain caps, change P-HPR road cadence, redesign P-HPR road into a continuous model, or change gear pulse logic.

Advanced / Diagnostics now reports the shared road signal with telemetry freshness, cached driving state, speed scale, speed reference, surface IDs/class/name/mix, roughness contribution components, raw intensity, smoothed intensity, output intensity, BST-1/P-HPR frequency hints, BST-1 grain/noise amount, gear-ducking state, ducking gain, and suppression reason.

The same diagnostics report includes BST-1 road proof:

- road enabled state and road gain;
- road peak/RMS before the mixer;
- estimated road peak/RMS after mixer and safety gain when limiter/clipping are idle;
- total mixer peak and total output peak;
- safety output gain, conservative ceiling, limiter state, limited samples, and clipped samples;
- a clear note that total output peak is total output, not road-only output.

P-HPR road diagnostics now expose route attempts, attempts per second, routed commands, routed commands per second, ignored reason, interval suppression, safety rejection, stale telemetry suppression, gear-ducking suppression, higher-priority suppression, in-flight suppression, command-rate suppression, last command target/strength/frequency/duration/intensity, last stop reason/age, and whether a previous "last road routed" result is stale/historical while road is currently disabled.

When explicitly enabled from Advanced / Diagnostics, the local road flight recorder writes JSONL to:

```text
local-validation-results/road-texture-flight-recorder.jsonl
```

The recorder is disabled by default, writes from the low-frequency diagnostics path rather than the ASIO callback, includes a session ID, wall-clock timestamp, monotonic timestamp, replay/source and telemetry freshness, haptics/emergency state, road signal fields, BST-1 proof fields, and P-HPR route/command fields. Files under `local-validation-results/` are local validation evidence and must not be committed.

Known physical findings remain open after Stage 18q-B: BST-1 road is active but too weak at current caps, and P-HPR road still uses sparse pulse-style commands. The next physical road validation should enable the road flight recorder before replay testing and attach the resulting JSONL with the diagnostics export.

## Stage 18q-C/D/E/F Road Behavior Changes

Stage 18q-C widens BST-1 / ASIO road output gain to support local hardware tuning. The previous 25% setting remains available and conservative; the new 100% maximum is not a universal safe setting and must be approached gradually through the normal mixer, safety gain, limiter, emergency mute, and selected ASIO output chain.

Stage 18q-D separates the road switches:

- shared road signal enabled;
- BST-1 road output enabled;
- brake P-HPR road output enabled;
- throttle P-HPR road output enabled.

P-HPR road can now run from the shared road signal even when BST-1 road output is disabled. If the shared signal is disabled, no road output should route anywhere.

Stage 18q-E changes P-HPR road from sparse pulse-style routing to a bounded cadence model. The app owns a background road cadence task instead of relying on the 500 ms UI/status timer. P-HPR road uses overlapping road-duration commands, default 100 ms cadence, 350 ms hold timeout, explicit stops on inactive/stale/haptics-stopped/disabled/gear-ducking conditions, and a watchdog stop if updates stop arriving.

P-HPR road remains lower priority than gear, wheel slip, and wheel lock. Gear ducking can stop/suppress road so gear pulses keep command budget and tactile priority. Stop, Stop All, Emergency Stop, emergency mute, command-rate safety, direct-control readiness, coexistence gates, stale telemetry gates, and app shutdown cleanup remain safety boundaries.

The road flight recorder now includes shared/output switch state, P-HPR runtime state, cadence, hold timeout, active modules, last road start/update/stop age, road stop reason, stop command count, watchdog stops, and command-rate suppression counters. Use these fields during local validation; they are not physical proof by themselves.

## Stage 18r-C BST-1 Road Tuning

Stage 18r-C keeps the shared evaluator/signal architecture and extends only the BST-1 road tuning side:

- low-speed BST-1 frequency;
- high-speed BST-1 frequency;
- speed reference up to the F1 range around `330 km/h`;
- speed-frequency influence;
- grain / texture amount;
- existing BST-1 / ASIO road output gain.

The shared minimum-speed gate remains in Shared / Global Effect Settings. The new BST-1 road controls live in the BST-1 Road Texture card and auto-save through the normal audio profile.

Road speed no longer needs to feel "finished" by `160 km/h`. The evaluator keeps intensity bounded and gear ducking intact, while the BST-1 texture changes more through frequency and grain than through raw amplitude alone. P-HPR road still consumes the same shared signal and remains on its separate routing/runtime path.

These values are starting points only. They do not claim final physical asphalt feel, safe physical gain, physical latency, or exact high-speed tuning until Ethan validates the real hardware chain locally.
