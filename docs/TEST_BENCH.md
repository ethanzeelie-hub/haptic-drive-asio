# Test Bench

Stage 11 adds a deterministic internal test bench for validating the software audio path without F1 25, live UDP traffic, ASIO hardware, WASAPI hardware, shaker hardware, or physical output devices.

## Signals

The Stage 11 signal set is intentionally small:

- Silence.
- Fixed-frequency sine tone.
- Linear frequency sweep.
- Pulse / transient signal.
- Constant value / DC signal.

These are synthetic validation signals only. They are not driving haptic effects and must not be treated as engine vibration, gear shift, kerb, road texture, slip, ABS, traction loss, or impact behavior.

## Processing Path

```text
test signal generator
-> AudioSampleBuffer
-> AudioMixerInput
-> AudioRenderPipeline
-> AudioMixer
-> AudioSafetyProcessor
-> IAudioOutputDevice
-> NullAudioOutputDevice by default
```

The test bench respects normal mute and emergency mute. It reports selected signal, active state, sample rate, channel count, buffer size, output mode, output peak, sanitized sample count, limited sample count, clipped sample count, and rendered buffer count.

## Boundaries

- Automated tests use null output only.
- No real WASAPI or ASIO streaming is implemented.
- No real-time callback loop is implemented.
- No graphs, calibration wizard, routing matrix, profile editor, or hardware tuning workflow is implemented.
- No final shaker feel, safe physical gain, physical latency, or frequency tuning claims can be made until the real hardware chain is tested locally.
