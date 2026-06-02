# Haptic Effects

Stage 12 adds the first generated driving haptic effects: engine vibration and gear shift.

## Source Data

The official F1 25 v3 PDF was checked before implementation. The following fields are direct F1 25 telemetry outputs used by Stage 12:

- Car Telemetry: `m_speed`, `m_throttle`, `m_gear`, `m_engineRPM`, and `m_suggestedGear`.
- Car Status: `m_maxRPM`, `m_idleRPM`, and `m_networkPaused`.
- Session and Lap Data: `m_gamePaused`, `m_pitStatus`, `m_driverStatus`, and `m_resultStatus`.

The PDF does not provide a direct engine-vibration signal or dedicated gear-shift haptic event. Stage 12 therefore synthesizes these effects from shared `VehicleState` values populated by the F1 25 adapter.

## Engine Vibration

Engine vibration is a continuous deterministic source buffer.

Default assumptions:

- Enabled by default with conservative gain `0.08`.
- RPM maps linearly from idle/max RPM to a base haptic frequency range of `34-50 Hz`.
- If F1 25 car-status idle/max RPM is missing or invalid, the effect falls back to `3000-12000 RPM`.
- Throttle scales intensity while preserving a quiet idle component.
- A SimHub-style high-frequency component is available at `50 Hz` with conservative gain.
- Deterministic frequency jitter is configurable but disabled by default.
- Missing telemetry, zero RPM, unrealistic RPM, paused/network-paused, garage, invalid, or inactive driving states produce silence.
- Pit states reduce output instead of treating pit-lane movement as full driving load.

These values are not physical shaker calibration and must not be treated as final safe gain, final feel, final latency, or final frequency tuning.

## Gear Shift

Gear shift is a short deterministic transient source buffer.

Default assumptions:

- Enabled by default with conservative gain `0.18`.
- Valid forward gear changes trigger a `15 Hz`, `80 ms` decaying pulse.
- Initial telemetry, unchanged gear, missing gear, neutral, and reverse do not trigger a pulse.
- A `100 ms` engaging debounce prevents repeated kicks from rapid gear bounce.
- Optional RPM-based gain modulation exists but is disabled by default.

This is a simple safe transient, not a drivetrain shock simulation.

## Integration

The `HapticEffectEngine` renders active effect buffers as `AudioMixerInput` sources. The existing Stage 10 mixer and safety processor still handle master gain, normal mute, emergency mute, invalid sample sanitisation, limiting, clipping, and final submission to `NullAudioOutputDevice` in automated tests.

Stage 12 does not implement kerb, impact, road texture, slip, traction loss, ABS, suspension, surface-specific effects, real WASAPI output, real ASIO streaming, Simagic P-HPR output, profile editing, or physical calibration.
