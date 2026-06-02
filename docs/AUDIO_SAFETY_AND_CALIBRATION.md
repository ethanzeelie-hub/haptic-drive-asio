# Audio Safety and Calibration

Stage 10 adds the first internal audio safety chain. It is deterministic infrastructure for later haptic effects and real output devices; it is not physical shaker calibration.

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

## Boundaries

- No generated haptic effects are implemented in Stage 10.
- No Stage 11 test bench signals are implemented in Stage 10.
- No real ASIO or WASAPI streaming is implemented in Stage 10.
- `NullAudioOutputDevice` remains the default automated-test output.
- These normalized defaults are conservative software limits only. No final physical safety, safe gain, shaker feel, latency, or frequency tuning claims may be made until the real hardware chain is tested.
