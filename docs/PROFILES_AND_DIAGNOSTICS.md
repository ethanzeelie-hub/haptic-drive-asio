# Profiles and Diagnostics

Stage 14 adds practical tuning profiles and runtime diagnostics for the existing Stage 10 through Stage 13 pipeline.

## Profile Scope

Profiles store software tuning values for existing haptic effects, the mixer, and the normal-user-facing audio safety chain.

Profiles include:

- Format version.
- Profile name.
- Per-effect enabled state and per-output gain.
- Selected existing effect parameters exposed by the UI, such as engine frequency bounds, gear/impact pulse duration, kerb base frequency, road texture speed gate, and slip ratio threshold.
- Mixer master gain and normal mute.
- Safety output gain. The persisted ceiling/limiter values are normalized back to the protected Stage 18r-B internal settings when profiles load.

Profiles do not include:

- Emergency mute. Emergency mute is runtime-only.
- Output device selection or replay mode.
- ASIO or WASAPI hardware running state.
- UDP forwarding destinations.
- Recording files or replay files.
- Physical shaker gain, calibration, safe physical output, latency, or frequency-response data.

Stage 18r-B app settings are separate from profiles. App settings now persist:

- Theme and Advanced / Diagnostics visibility.
- Safe output selections: preferred output mode, last ASIO driver, and last ASIO channel.
- Replay timing mode.
- UDP forwarding destinations.
- Paddle input device, left/right mapping, and debounce.
- BST-1 local paddle gear pulse enable/strength/frequency/duration mode.
- Shift-intent settings.
- Safe mock and normal P-HPR effect preferences already covered by the existing app-settings model.

App settings intentionally do not persist:

- Haptics running state.
- Emergency mute or emergency-stop state as a desired startup setting.
- ASIO armed state.
- Manual ASIO hardware test active state.
- Direct P-HPR enable/arm/private device state.
- Active pulses, pending stops, or bench-active state.
- Flight-recorder history, mock history, or diagnostic counters.
- Physical calibration or physical validation results.

## JSON Format

Profiles are stored as human-readable JSON with `Version` set to `1`.

The default audio profile file name used by the shell is:

```text
default.hdprofile.json
```

The default directory is under the current user's local application data:

```text
%LOCALAPPDATA%\HapticDrive.Asio\Profiles
```

Generated profile files are user data and should not be committed.

The shell now auto-saves normal audio tuning changes back to `default.hdprofile.json`. Manual profile save/load still exists for explicit snapshots. The separate P-HPR profile remains a manual effect-preferences snapshot and does not re-arm direct output on launch.

## Validation

Profile loading and saving run through the same validator.

- Unsupported profile versions fail safely and are not applied.
- Missing profile files fail safely.
- Corrupt JSON fails safely.
- Missing profile sections are repaired to the Stage 18r-B current-rig defaults.
- Invalid finite values, NaN, infinity, and out-of-range values are repaired or clamped to software-safe ranges.
- Effect gains remain normalized and bounded.
- BST-1 effect gain sliders and stored gain values can now use the full `0.0-1.0` range.
- Safety output gain remains normalized and bounded to `0.0-1.0`.
- The normal-user-facing output ceiling control is removed; profile loads normalize the internal ceiling to `1.0` and keep the limiter enabled.

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
