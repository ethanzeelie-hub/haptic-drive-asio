# Haptic Effects

Stage 13 implements the first six generated driving haptic effects: engine vibration, gear shift, kerb, impact, road texture, and slip / brake-lock. Stage 14 adds practical UI tuning and profile persistence for those existing effects. Stage 15 feeds live and replayed telemetry through those same effects, mixer, safety chain, and `NullAudioOutputDevice` in the first playable mock pipeline. Stage 17 moves live rendering into an output-owned path and adds stale telemetry mute before hardware validation. Stage 18o-B consolidates BST-1 and P-HPR road texture around one shared software road signal, and Stage 18r-E/F gives real P-HPR wheel slip / wheel lock their own bounded continuous cadence path.

## Source Data

The official F1 25 v3 PDF was checked during the parser stages and summarized in `docs/F1_25_PACKET_SPEC_IMPLEMENTATION.md`. Stage 13 does not add or change packet layouts, offsets, enum values, packet lengths, or packet versions.

Direct F1 25 telemetry fields consumed through shared `VehicleState` include:

- Car Telemetry: `m_speed`, `m_throttle`, `m_brake`, `m_gear`, `m_engineRPM`, `m_suggestedGear`, and `m_surfaceType[4]`.
- Car Status: `m_maxRPM`, `m_idleRPM`, `m_tractionControl`, `m_antiLockBrakes`, and `m_networkPaused`.
- Motion: `m_gForceVertical`.
- Motion Ex: suspension position / velocity / acceleration, wheel speed, wheel slip ratio, wheel slip angle, wheel lateral / longitudinal force, wheel vertical force, local velocity, angular velocity, and angular acceleration.
- Event: collision event code `COLL` and player involvement.
- Session and Lap Data: `m_gamePaused`, `m_pitStatus`, `m_driverStatus`, and `m_resultStatus`.

Where F1 25 does not output a dedicated haptic signal, effects synthesize conservative deterministic buffers from these fields.

## Engine Vibration

Engine vibration is a continuous deterministic source buffer from Stage 12.

Default assumptions:

- Enabled by default with conservative gain `0.08`.
- RPM maps linearly from idle/max RPM to a base haptic frequency range of `34-50 Hz`.
- If idle/max RPM is missing or invalid, the effect falls back to `3000-12000 RPM`.
- Throttle scales intensity while preserving a quiet idle component.
- A SimHub-style high-frequency component is available at `50 Hz` with conservative gain.
- Missing telemetry, zero RPM, unrealistic RPM, paused/network-paused, garage, invalid, or inactive driving states produce silence.

## Gear Shift

Gear shift is a short deterministic transient source buffer from Stage 12.

Default assumptions:

- Enabled by default with conservative gain `0.18`.
- Valid forward gear changes trigger a `15 Hz`, `80 ms` decaying pulse.
- Initial telemetry, unchanged gear, missing gear, neutral, and reverse do not trigger a pulse.
- A `100 ms` engaging debounce prevents repeated kicks from rapid gear bounce.

## Kerb

Kerb vibration is a continuous deterministic rumble source.

Default assumptions:

- Enabled by default with conservative gain `0.12`.
- Rumble strip surface ID `1` and ridged surface ID `11` activate the effect.
- Missing surface telemetry, unknown surface IDs, very low speed, paused/network-paused, garage, invalid, or inactive driving states produce silence.
- Intensity scales with speed from `5-120 km/h`, active wheel count, and optional Motion Ex contact / suspension movement data.
- Output uses a low `20 Hz` rumble with an optional `44 Hz` component and small deterministic roughness.

## Impact

Impact is a short deterministic transient source.

Default assumptions:

- Enabled by default with conservative gain `0.20`.
- Player-involved collision events can trigger a pulse.
- Abrupt vertical-G, wheel vertical force, or suspension acceleration spikes can trigger a pulse after an initial baseline has been observed.
- Initial state does not trigger an impact.
- Repeated impacts are bounded by a `120 ms` cooldown and a small frame gap.
- Invalid, missing, stale, NaN, infinity, negative, or unrealistic values are ignored safely.
- Output uses a `44 Hz`, `90 ms` decaying pulse.

This is not crash physics, damage modelling, or final physical impact tuning.

## Road Texture

Road texture is a low-level continuous deterministic source. Stage 18o-B evaluates one shared `RoadTextureSignal` in Core, then lets the BST-1 audio effect and P-HPR road router consume that same signal with output-specific rendering and safety behavior.

Default assumptions:

- Enabled by default with conservative gain `0.05`.
- Very low speed produces silence.
- Tarmac is intentionally quiet.
- Rumble strip, gravel, grass, sand, concrete, cobblestone, metal, ridged, and other documented surface IDs have conservative per-surface frequency/noise defaults.
- Missing surface telemetry produces silence; missing Motion Ex data uses a safe fallback rather than inventing road feel.
- Suspension acceleration, wheel vertical-force deltas, and vertical G can raise the shared roughness metric when they exceed conservative thresholds.
- Live road texture is gated by telemetry freshness, haptics running state, and cached `DrivingArmed` unless an explicitly local/manual evaluation context is used.
- Accepted local gear pulses briefly duck the shared road signal. BST-1 road renders at the ducked intensity, while P-HPR road commands suppress during the gear-priority window.
- Unknown future surface IDs are preserved in `VehicleState` but do not produce texture by default.
- Deterministic roughness is seeded from the render cursor and surface ID so fixed VehicleState sequences are repeatable.

## Slip / Brake-Lock

Slip is a continuous deterministic source covering Stage 13 slip, traction-loss, and minimal wheel-lock behavior.

Stage 18r-D keeps one shared evaluator/render path but exposes separate BST-1 tuning for wheel slip and wheel lock so each can be enabled, gained, frequency-tuned, and roughness-tuned independently.
Stage 18r-E/F keeps BST-1 unchanged but changes real P-HPR wheel slip / wheel lock from sparse UI-timer pulses to a background bounded continuous cadence model with explicit stop commands, hold-timeout watchdog protection, stale/DrivingArmed suppression, and targeted road-yield plus gear-protection behavior.

Default assumptions:

- Enabled by default at the runtime-options layer with conservative wheel-slip gain `0.09` and wheel-lock gain `0.10`.
- Wheel slip ratio and wheel slip angle determine slip intensity.
- Speed below `8 km/h` suppresses output to avoid low-speed telemetry noise.
- Throttle and brake thresholds are inspired by SimHub-style trigger controls. Stage 18r-D exposes a practical slip ratio threshold for slip and a wheel-speed-ratio sensitivity control for lock; a full tyre/ABS tuning model remains deferred.
- TC and ABS state reduce intensity conservatively when active.
- Brake-lock shaping uses brake input, high slip ratio, and wheel speed much lower than vehicle speed.
- Invalid, missing, stale, NaN, infinity, negative, or unrealistic values are sanitized, bounded, or silenced.
- Output now defaults to `52 Hz` wheel slip with `18%` roughness and `68 Hz` wheel lock with `24%` roughness.
- Real P-HPR wheel slip defaults to the throttle pedal with a `45-50 Hz`, `100 ms` minimum continuous duration window and `100 ms` cadence gate.
- Real P-HPR wheel lock defaults to the brake pedal with a `50 Hz`, `100 ms` minimum continuous duration window and the same cadence/hold model.
- Real P-HPR road yields while slip/lock is actively holding a pedal module, and accepted gear pulses briefly protect both routes without changing gear-pulse timing itself.

Stage 13 does not implement a separate advanced ABS effect, advanced tyre model, or physical lock-up calibration.

## Integration

The `HapticEffectEngine` renders active effect buffers as `AudioMixerInput` sources through an internal registered effect-slot seam. The Stage 10 mixer and safety processor still handle source summing, master gain, normal mute, emergency mute, invalid sample sanitisation, limiting, clipping, and final submission to `NullAudioOutputDevice` in automated tests.

The WPF shell adds Stage 14 controls for per-effect enabled state and gain, selected existing effect parameters, mixer/safety settings, versioned JSON profiles, and read-only diagnostics. The effect engine can retune by replacing immutable option records under a short lock, then continues to feed the same mixer, safety processor, emergency mute, limiter, clipping protection, and `NullAudioOutputDevice` path.

Stage 17 does not add new effect categories, routing matrices, calibration UI, live graphs, real WASAPI output, Simagic P-HPR output, or physical calibration.

The output-owned render path renders effects only from current in-memory `VehicleState`/effect state, then passes buffers through the existing mixer and safety processor. If fresh parsed `VehicleState` has not arrived within the wall-clock timeout, effects are muted and safety silence is rendered until telemetry updates again.

These defaults are not physical shaker calibration and must not be treated as final safe gain, final feel, final latency, or final frequency tuning. Those remain unvalidated until the real hardware chain is tested locally.
