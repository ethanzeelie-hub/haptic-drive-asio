# Known Issues

This file tracks active issues only. Historical stage-by-stage issue notes are archived in [docs/archive/KNOWN_ISSUES_STAGE_HISTORY.md](/C:/Users/ethan/OneDrive/Documents/ASIO%20Haptic%20Engine%20Program/docs/archive/KNOWN_ISSUES_STAGE_HISTORY.md).

## Active blockers

- Public redistribution remains blocked until the owner selects and approves license terms. Packaging and release artifacts are readying the repo for release, but they do not grant redistribution rights.

## Hardware-later validation

- Physical shaker feel, safe gain, physical latency, and final frequency tuning are still unvalidated in this codebase and must not be claimed complete until local hardware testing is finished.
- Real Simagic P-HPR USB writes remain unauthorized unless the owner provides the exact approval phrase required by the project rules.

## Production-hardening gaps still being closed

- `MainWindow.xaml.cs` is below the Stage 8 guardrail but still above the final Stage 14 target; the remaining shell reduction and final guardrail tightening are still pending.
- The WPF tuning surface is partially descriptor-driven. The generated registry-backed effect settings exist, but some legacy fixed controls remain for the currently shipped effects.
- WASAPI debug output remains manual/experimental only and must not be treated as a production streaming path.

## Future enhancements

- F1 25 is still the only production game integration. The registry/normalizer seam is ready for future games, but a second game is not implemented yet.
- Additional effect categories, deeper tuning UI, and richer routing/editor workflows remain future work after the production-hardening pass.

## Documentation note

- `ROADMAP.md` and `DEVELOPMENT_LOG.md` preserve planning and historical implementation notes. This file should stay limited to current issues that still matter to an engineer using or hardening the repo today.
