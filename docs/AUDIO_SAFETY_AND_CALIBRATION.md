# Audio Safety and Calibration

Stage 10 adds the first internal audio safety chain. Stage 11 adds deterministic test-bench signals that exercise that chain through null output. Stage 12 adds the first generated haptic effect sources and keeps them on the same mixer/safety/null-output path. This is not physical shaker calibration.

## Stage 10 Safety Chain

The current processing order is:

```text
source buffers
-> mixer source gains and master gain
-> invalid sample sanitisation
-> conservative output gain
-> peak limiter
-> hard clipping protection
-> emergency mute
-> output buffer
```

Defaults:

- Internal sample format: interleaved `float` samples.
- Default sample rate: 48000 Hz through `AudioOutputConfiguration.Default`.
- Default channel count: 1 mono channel.
- Default buffer size: 128 frames.
- Default safety output gain: `0.25`.
- Default normalized output ceiling: `0.75`.
- Limiter enabled by default.

The limiter is a simple deterministic peak limiter. If the buffer peak exceeds the configured ceiling after output gain, the whole buffer is scaled so the peak reaches the ceiling. Hard clipping then clamps any remaining overflow to the same ceiling. NaN and infinity inputs are converted to silence.

Emergency mute forces the final output buffer to silence regardless of input.

## Stage 11 Test Bench

The current test bench can generate:

- Silence.
- Fixed-frequency sine tone.
- Linear frequency sweep.
- Pulse / transient signal.
- Constant-value signal for DC and limiter checks.

Each signal fills an `AudioSampleBuffer`, enters the existing `AudioMixer` as a named source, passes through `AudioSafetyProcessor`, and is submitted to `NullAudioOutputDevice` by default.

The test bench reports active state, selected signal, sample format, output mode, output peak, sanitized samples, limited samples, clipped samples, and rendered buffer counts.

## Stage 12 Effects

Engine vibration and gear shift effects render deterministic source buffers from `VehicleState`.

- Engine vibration uses conservative gain, RPM-derived frequency, throttle scaling, status gating, and optional high-frequency shaping.
- Gear shift uses a conservative decaying pulse triggered by valid forward gear changes.
- Both effects enter the same `AudioMixer`, `AudioSafetyProcessor`, emergency mute, limiter, clipping, and `NullAudioOutputDevice` path used by the test bench.
- F1 25 does not output direct engine-vibration or gear-shift haptic signals; these are synthesized effects.

## Boundaries

- Stage 11 signals are synthetic validation tools only, not engine, gear shift, kerb, slip, road texture, ABS, traction loss, or impact effects.
- Stage 12 effects are software defaults only, not final physical calibration.
- No real ASIO or WASAPI streaming is implemented in Stage 12.
- `NullAudioOutputDevice` remains the default automated-test output.
- These normalized defaults are conservative software limits only. No final physical safety, safe gain, shaker feel, latency, or frequency tuning claims may be made until the real hardware chain is tested.
