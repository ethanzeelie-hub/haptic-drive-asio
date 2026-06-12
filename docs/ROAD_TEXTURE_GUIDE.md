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

Advanced / Diagnostics now reports the shared road signal with telemetry freshness, cached driving state, speed scale, surface IDs/class/name/mix, roughness contribution components, raw intensity, smoothed intensity, output intensity, BST-1/P-HPR frequency hints, gear-ducking state, ducking gain, and suppression reason.

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
