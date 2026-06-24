# Production Readiness Checklist

This checklist is the final manual smoke pass for the software architecture and packaging baseline. It does not replace local physical hardware validation.

## Launch and safe startup

- [ ] Launch the app with no ASIO hardware required.
- [ ] Confirm `NullAudioOutputDevice` is the default selected output.
- [ ] Confirm the global safety interlock is visible and starts latched.
- [ ] Confirm the telemetry listener can remain idle without crashing or blocking startup.

## Safety controls

- [ ] Confirm the top-bar safety state is always visible.
- [ ] Press emergency mute and confirm the global output interlock remains latched.
- [ ] Use `Ctrl+Shift+M` and confirm emergency mute trips from the keyboard.
- [ ] Use `Ctrl+Shift+R` only when safe and confirm reset succeeds only after readiness checks pass.

## Telemetry, recording, and replay

- [ ] Confirm F1 telemetry absence does not fail the app.
- [ ] Confirm loopback remains the default telemetry mode.
- [ ] Confirm enabling LAN telemetry surfaces the warning/allowlist status.
- [ ] Start a sample recording and confirm status updates remain non-blocking.
- [ ] Replay a sample recording and confirm the app can run the replay path with no hardware attached.

## Diagnostics and privacy

- [ ] Export a safe-mode support bundle.
- [ ] Confirm private paths, serial-like values, hostnames, private IPs, and raw USB payloads are redacted in the safe bundle.
- [ ] Confirm the support bundle does not include the P-HPR authorization phrase or session-authorization state.

## Packaging

- [ ] Run `Prepare-ReleaseArtifact.ps1 -Configuration Release`.
- [ ] Confirm the release zip, checksum, manifests, and documentation payload are created from the Release artifact.
- [ ] Run `Test-ReleaseArtifact.ps1 -Configuration Release -Runtime win-x64`.
- [ ] Confirm the release manifest records commit hash, configuration, runtime identifier, and package SHA-256.
- [ ] Confirm the default public artifact excludes portable PDBs.

## Final non-claims

- [ ] Do not claim final shaker feel, safe physical gain, or physical latency until local hardware validation is complete.
- [ ] Do not claim real P-HPR safety validation, stop behavior, or coexistence validation without supervised manual evidence.
- [ ] Do not treat Direct mode selection, arm state, dry-run, or open-check as write authorization.
- [ ] Do not claim public redistribution rights until the owner selects license terms.
