# Profiles and Diagnostics

Stage 14 adds practical tuning profiles and runtime diagnostics for the existing Stage 10 through Stage 13 pipeline.

## Profile Scope

Profiles store software tuning values for existing haptic effects, the mixer, and the safety chain.

Profiles include:

- Format version.
- Profile name.
- Per-effect enabled state and conservative gain.
- Selected existing effect parameters exposed by the UI, such as engine frequency bounds, gear/impact pulse duration, kerb base frequency, road texture speed gate, and slip ratio threshold.
- Mixer master gain and normal mute.
- Safety output gain, normalized output ceiling, and limiter enabled state.

Profiles do not include:

- Emergency mute. Emergency mute is runtime-only.
- Output device selection.
- ASIO or WASAPI hardware settings.
- UDP forwarding destinations.
- Recording files or replay files.
- Physical shaker gain, calibration, safe physical output, latency, or frequency-response data.

Stage 18 app settings are separate from profiles. App settings persist theme, UDP forwarding destinations, and last ASIO driver/channel selections. They do not persist ASIO armed state, haptic running state, emergency mute, or physical calibration.

## JSON Format

Profiles are stored as human-readable JSON with `Version` set to `1`.

The default file name used by the shell is:

```text
default.hdprofile.json
```

The default directory is under the current user's local application data:

```text
%LOCALAPPDATA%\HapticDrive.Asio\Profiles
```

Generated profile files are user data and should not be committed.

## Validation

Profile loading and saving run through the same validator.

- Unsupported profile versions fail safely and are not applied.
- Missing profile files fail safely.
- Corrupt JSON fails safely.
- Missing profile sections are repaired to conservative defaults.
- Invalid finite values, NaN, infinity, and out-of-range values are repaired or clamped to conservative software ranges.
- Effect gains remain normalized and bounded.
- Safety output gain remains at or below `0.5`.
- The normalized output ceiling remains at or below the Stage 10 default ceiling of `0.75`.

These are software safety bounds only. They are not physical shaker calibration and must not be treated as final safe gain.

## Diagnostics

Stage 14 diagnostics are read-only status snapshots from existing services and lightweight profile/runtime state.

Diagnostics include:

- UDP listener running state, packet count, packet rate, and last packet age.
- UDP forwarding destination counts, forwarded datagrams/bytes, and error count.
- Configured UDP forwarding destination summary.
- Parser valid, ignored, and failed counts.
- Packet-ID observation counts for known F1 25 packet IDs.
- VehicleState update count and last adapter message.
- Recording active/inactive state, file name, and packet count.
- Replay active/inactive state, source file name, packet count, and status message.
- Effect enabled state, active count, and peak level.
- Mixer peak, safety output peak, limiter count, clipping count, mute, and emergency mute state.
- Test bench active/inactive state, selected signal, peak level, limiter count, and output mode.
- Output mode, hardware-required flag, manual-debug flag, and hardware-absent Null output state.
- ASIO readiness, callback, drop, underrun, jitter, and selected driver/channel/armed state.
- Runtime prerequisite and app-settings path.

Stage 18 adds a copyable diagnostics report. Diagnostics do not add heavy charting, long-term logs, live graphs, routing matrices, physical hardware calibration, or real WASAPI output.
