# Stage 18p-A Product UI Architecture And Replay Timing Report

Date: 2026-06-12

Scope: diagnostic and architecture report only. This stage does not perform the product UI rewrite, does not change ASIO/BST-1 output, does not change P-HPR HID/report behavior, and does not change F1 25 parser layouts.

## Executive Summary

The current WPF app can support a modern dark dashboard, left sidebar, top status/action bar, and card-based workflow, but the implementation should be staged. The app already has a dark resource foundation in `src/HapticDrive.Asio.App/App.xaml`, a left `NavigationList`, top action buttons, and named page panels in `src/HapticDrive.Asio.App/MainWindow.xaml`. The limiting factor is maintainability: `MainWindow.xaml` and `MainWindow.xaml.cs` are very large, and normal controls, runtime workflow, and research diagnostics are interleaved.

The safest UI direction is a hybrid Effects page:

- Hardware sections for `BST-1 Seat Shaker`, `Brake P-HPR`, and `Throttle P-HPR`.
- Effect cards inside each hardware section for gear, road, engine, kerb, impact, slip, and lock where supported.
- Shared/global controls for concepts that truly are shared, such as gear duration and road signal/ducking.
- Advanced-only controls for low-level P-HPR min/max Hz, min/max strength, raw HID details, report shape, validation harnesses, and internal counters.

Replay files already store packet relative timing. The apparent "instant replay" root cause is not the `.hdrec` format; it is the WPF replay call using `TelemetryReplayOptions.Fast` for both replay-latest and replay-selected. The replay service also defaults to fast mode when options are omitted. Stage 18p-B should switch normal UI replay to time-preserving mode, make fast replay explicit, and add delete-selected recording behavior.

## Architecture Findings

1. Current WPF structure can support the target layout, but only cleanly after resource extraction and component splitting.
   - Existing support: `App.xaml` already defines dark brushes, card border style, button styles, text styles, and default `Button`/`TextBlock`/`CheckBox` styles.
   - Existing support: `MainWindow.xaml` already has a top app/status bar, left navigation list, and named panels including `EffectsPanel`, `MixerPanel`, `DevicesPanel`, `RecordingsPanel`, `ForwardingPanel`, `ProfilesPanel`, `AdvancedPhprDiagnosticsPanel`, `SettingsPanel`, and `DiagnosticsPanel`.
   - Current risk: one giant XAML file plus one giant code-behind makes broad visual work risky because moving a visual block can disturb event handlers and runtime state.

2. Shared theme/style layer should be extracted from `App.xaml`.
   - Keep `App.xaml` as the resource dictionary merger and startup entry point.
   - Add `src/HapticDrive.Asio.App/Resources/Theme.xaml` for colors, brushes, spacing, radii, typography, and accent definitions.
   - Add `src/HapticDrive.Asio.App/Resources/Styles.xaml` for reusable WPF control styles: sidebar item, top bar button, status badge, card border, section heading, form row, compact metric, danger button, primary button, field input.
   - Later add `src/HapticDrive.Asio.App/Resources/Templates.xaml` only if repeated card/data templates become stable.

3. A new `Theme.xaml` and `Styles.xaml` should be added before visual redesign.
   - This keeps the red-accent dark product look maintainable.
   - It also avoids burying product theme decisions in `MainWindow.xaml`.
   - Start with a red accent token, but keep it as a resource so later brand/contrast changes are cheap.

4. Current pages should be restyled in place first, then split into smaller components.
   - First pass: keep existing event handlers and named controls while applying shell/card styles.
   - Second pass: split stable regions into WPF `UserControl`s such as `DashboardPage`, `DevicesPage`, `EffectsPage`, `RoutingMixerPage`, `TelemetryUdpPage`, `AdvancedDiagnosticsPage`, `StatusCard`, `DeviceCard`, `EffectCard`, and `MetricBadge`.
   - Do not start with full MVVM extraction; introduce view models only where a page split needs a stable data contract.

5. UI changes safe to do first without touching haptic runtime logic:
   - Move colors/styles into resource dictionaries.
   - Restyle the app shell, sidebar, top bar, cards, typography, spacing, badges, and buttons.
   - Reduce normal-page explanatory text and move detailed diagnostic copy into Advanced.
   - Add visual status widgets based on existing snapshot text values.
   - Add a delete-selected recording UI backed by file deletion, because it affects the recording library only and not parser/audio/HID behavior.
   - Change normal replay UI to call time-preserving replay; this touches replay scheduling but not ASIO, P-HPR protocol bytes, parser offsets, or effect math.

6. UI changes to delay because they risk proven hardware paths:
   - Rebinding P-HPR direct enable/arm/candidate/open-check/report-shape controls before the direct-control safety gates are split from research diagnostics.
   - Changing manual BST-1 pulse, ASIO arm/channel, output trim, or local gear-pulse handler semantics while visually moving controls.
   - Merging P-HPR road/slip/lock settings into a new model before compatibility tests prove the same safe settings are persisted and loaded.
   - Any change that persists direct-control enablement, arming, selected private HID path, emergency-stop latch, active pulse state, write history, or crash/fault state.

## Current UI Map

| Current section/control area | Current location | Target location | Action |
| --- | --- | --- | --- |
| Top app title, telemetry status, Start/Stop, Start Recording, Emergency Mute, theme button | Main shell top bar | Top status/action bar | Stay, restyle and shorten labels. |
| `NavigationList` | Left shell | Sidebar navigation | Stay, restyle as product sidebar. |
| Dashboard status cards: output mode, haptics, UDP, packets, forwarding, parser, VehicleState, recording | Dashboard/default page | Dashboard | Stay, restyle as compact status widgets. |
| Audio effect tuning: engine, gear, kerb, impact, road, slip | `EffectsPanel` | Effects | Stay logically, split into BST-1 effect cards. |
| Effect state/default diagnostic cards | `EffectsPanel` | Effects plus Advanced | Keep compact state in Effects; move long/default diagnostic text to Advanced. |
| Synthetic test bench | `TestBenchPanel` on Advanced when enabled | Advanced / Diagnostics | Stay advanced/debug-only. |
| Master gain, mute, safety output gain, ceiling, limiter | `MixerPanel` | Routing / Mixer | Stay, restyle as routing/safety card. |
| Bass Shaker / ASIO setup | `DevicesPanel` | Devices | Stay; no normal effect tuning except manual test and channel test. |
| Manual BST-1 pulse | `DevicesPanel` | Devices | Stay as manual hardware test. |
| BST-1 Paddle Gear Pulse settings | `DevicesPanel` | Effects, with minimal readiness summary in Devices | Move effect tuning to Effects; keep enable/readiness/manual local test entry points in Devices if needed. |
| Simagic P-HPR Pedals master enable/mode/status/Stop All | `DevicesPanel` | Devices | Stay. |
| Brake/Throttle P-HPR normal gear pulse values and test buttons | `DevicesPanel` | Devices for manual test; Effects for normal gear tuning | Split: test buttons/readiness in Devices, gear tuning in Effects. |
| Wheel / shift paddle selector, refresh, listener, mapping, debounce | `DevicesPanel` | Devices | Stay. |
| Shift intent enable/mode/diagnostics | `DevicesPanel` | Devices plus Advanced | Keep normal mode in Devices; detailed counters in Advanced. |
| Local Gear Test and Direct Paddle Gear Bench validation | `DevicesPanel` | Advanced / Diagnostics, with any production paddle gear source summarized in Devices/Effects | Move validation/bench details to Advanced; do not hide required production paddle listener controls. |
| Advanced diagnostics gate | `AdvancedPhprDiagnosticsPanel` | Advanced / Diagnostics | Stay. |
| P-HPR workflow, live F1 validation, coexistence, controlled write readiness | Advanced | Advanced / Diagnostics | Stay. |
| P-HPR Real Direct Control candidate/report/approval internals | Advanced | Advanced / Diagnostics | Stay; keep runtime-only. |
| P-HPR real road settings | Advanced | Effects for normal strength/sensitivity/output scale; low-level min/max in Advanced | Split carefully. |
| P-HPR real slip/lock settings | Advanced | Effects for normal controls; low-level min/max in Advanced | Split carefully. |
| P-HPR controlled validation harness | Advanced | Advanced / Diagnostics | Stay. |
| Mock P-HPR gear and pedal effects routing | Advanced | Advanced / Diagnostics, with summary in Routing / Mixer | Stay mostly advanced; normal user should not need it. |
| Recording and replay library | `RecordingsPanel` under Telemetry / UDP | Telemetry / UDP | Stay; add delete selected and real-time/fast replay choice. |
| UDP forwarding destinations | `ForwardingPanel` under Telemetry / UDP | Telemetry / UDP | Stay. |
| Profiles | `ProfilesPanel` | Profiles or compact profile actions within Effects/Routing later | Keep for now; future product shell can decide whether Profiles remains top-level. |
| Settings and copyable diagnostics | `SettingsPanel`, `DiagnosticsPanel` | Advanced / Diagnostics | Stay advanced. |

## Effect-Control Model

| Output/effect | User-facing controls | Duration | Frequency | Strength | Shared or per-output | UI target | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| BST-1 Gear Shift | enabled, strength/gain, frequency, shared duration or per-output override, ducking amount/window summary | Yes | Yes | Yes | Duration shared by default; strength/frequency per BST-1 | Effects / BST-1 Gear card | Current, needs cleaner model |
| BST-1 Road Texture | enabled, strength, sensitivity, minimum/full speed scaling, surface influence, smoothing, ducking summary | No normal pulse duration | Frequency range/internal mapping can be advanced | Yes | Shared road signal; per-output BST-1 scale | Effects / BST-1 Road card | Current shared signal |
| BST-1 Engine | enabled, strength, min/max frequency | No | Yes | Yes | Per BST-1 | Effects / BST-1 Engine card | Current |
| BST-1 Kerb | enabled, strength, base/high frequency or simplified frequency, speed gates | Continuous/while active, no user pulse duration by default | Yes | Yes | Per BST-1 | Effects / BST-1 Kerb card | Current |
| BST-1 Impact | enabled, strength, frequency, duration, threshold/cooldown advanced | Yes | Yes | Yes | Per BST-1 | Effects / BST-1 Impact card | Current |
| BST-1 Slip / Lock | enabled, strength, threshold/sensitivity, base frequency, speed gate | No normal pulse duration | Yes | Yes | Per BST-1 | Effects / BST-1 Slip/Lock card | Current combined effect, split later |
| Brake P-HPR Gear Shift | enabled, strength, frequency, shared duration or override | Yes | Yes | Yes | Shared duration by default; per-output strength/frequency | Effects / Brake P-HPR Gear card; manual test in Devices | Current |
| Brake P-HPR Road Texture | enabled, output scale, sensitivity/surface/speed from shared signal; min/max Hz/strength advanced | Not as normal user-facing duration | Advanced min/max Hz | Yes | Shared road signal; per-output brake scale | Effects normal card plus Advanced low-level | Current but buried in Advanced |
| Brake P-HPR Wheel Lock | enabled, strength, sensitivity/threshold, frequency or advanced min/max, priority summary | Command cadence internal; no normal pulse duration unless proven useful | Yes/advanced range | Yes | Per-output/effect, default brake | Effects / Brake P-HPR Lock card | Current |
| Brake P-HPR Slip | hidden or future optional route | No | Advanced | Yes | Per-output if enabled later | Hidden/future or Advanced | Future placeholder |
| Throttle P-HPR Gear Shift | enabled, strength, frequency, shared duration or override | Yes | Yes | Yes | Shared duration by default; per-output strength/frequency | Effects / Throttle P-HPR Gear card; manual test in Devices | Current |
| Throttle P-HPR Road Texture | enabled, output scale, sensitivity/surface/speed from shared signal; min/max Hz/strength advanced | Not as normal user-facing duration | Advanced min/max Hz | Yes | Shared road signal; per-output throttle scale | Effects normal card plus Advanced low-level | Current but buried in Advanced |
| Throttle P-HPR Wheel Slip | enabled, strength, sensitivity/threshold, frequency or advanced min/max, priority summary | Command cadence internal; no normal pulse duration unless proven useful | Yes/advanced range | Yes | Per-output/effect, default throttle | Effects / Throttle P-HPR Slip card | Current |
| Throttle P-HPR Lock | hidden or future optional route | No | Advanced | Yes | Per-output if enabled later | Hidden/future or Advanced | Future placeholder |

Notes:

- Gear shift is pulse-like and should expose duration.
- Road texture is continuous/synthetic. P-HPR road uses command cadence internally, so normal UI should not expose "duration" as if it were a pulse length.
- Road texture should be tuned around shared signal strength, sensitivity, speed scaling, surface influence, smoothing, and per-output scale.
- Gear ducking should be a shared product setting/summary, not buried in diagnostics.

## Settings And Profile Persistence

Current persistence:

- `HapticDriveProfile` persists audio effect settings for engine, gear, kerb, impact, road texture, slip, plus mixer master gain/mute and safety output gain/ceiling/limiter.
- `PhprEffectProfile` persists P-HPR effect preferences: shift intent, mock gear routing, mock pedal effects, real P-HPR gear, real road vibration, and real slip/lock.
- `AppSettings` persists theme preference, Advanced enabled preference, last ASIO driver name, last ASIO output channel, forwarding destinations, paddle mapping/debounce, shift intent, mock P-HPR routing, and safe real P-HPR effect settings.
- Recording files persist raw UDP payloads, packet order, sequence numbers, metadata, and relative timing. They do not persist route/profile snapshots.

Settings that should persist after the product restructure:

- User effect preferences: enabled, strength/gain, frequency/range, duration where semantically valid, speed gates, thresholds/sensitivity, smoothing, per-output scale.
- Gear shared duration and per-output override flags.
- Road shared tuning and per-output scale.
- Mixer/routing preferences: master gain, mute if still intentional, limiter enabled, output gain/ceiling, per-output enable/overall strength.
- Device preferences that are safe and non-private: last ASIO driver display name, selected output channel, forwarding destinations, input device selector ID if sanitized, paddle mapping, debounce, shift-intent mode.

Settings that must not persist:

- Emergency stop / emergency mute latch.
- Real direct-control enablement or arming.
- Private HID paths, raw USB inventory, serial numbers, raw report captures, or unsanitized device identifiers.
- Active pulse state, pending scheduled stops, command/write history, live counters, or route decisions.
- Crash/fault state except local private marker files used for recovery.
- Physical validation results unless deliberately exported as private local evidence.

Can existing profiles support the target model cleanly?

- The current stores can support a safe staged move of existing controls because visual relocation can keep the same fields and handlers.
- They do not cleanly model the final hybrid product UI. Audio effects are effect-first in `HapticDriveProfile`, while P-HPR settings are stored separately through `PhprEffectProfile` / `AppSettings`. A future profile version should introduce explicit per-output/per-effect groupings and shared settings such as gear duration and road signal tuning.
- Migration risk is low if 18p-C/18p-D first move visuals without renaming fields. Migration risk becomes moderate when adding per-output overrides or replacing P-HPR road "duration" with an internal cadence concept; that should be a versioned profile migration with tests.

## Replay Timing Root Cause

File format:

- `.hdrec` uses magic `HDREC001`, version `1`, UTC created ticks, source game/profile/app strings, packet count, then packet records.
- Each packet record stores sequence number, non-negative relative ticks, payload length, and raw payload bytes.
- Packet receive timestamps are stored as relative time from recording start, not as per-packet absolute receive timestamps.
- Inter-packet timing is recoverable from successive relative timestamps.

Current replay scheduler:

- `TelemetryReplayService` supports `TelemetryReplayOptions.Fast` and `TelemetryReplayOptions.TimePreserving`.
- Time-preserving replay delays by `recordedPacket.RelativeTime - previousRelativeTime`, scaled by speed.
- If options are omitted, `TelemetryReplayService` currently defaults to fast replay.

Root cause of instant replay in the app:

- `MainWindow.ReplayRecordingAsync` calls `_hapticPipeline.ReplayFileAsync(path, TelemetryReplayOptions.Fast)`.
- Both Replay Latest and Replay Selected call `ReplayRecordingAsync`, so normal UI replay intentionally bypasses recorded timing.
- This explains replay appearing to inject all packets almost instantly even when the recording file has timing.

Replay path and haptic behavior:

- Replay emits `UdpTelemetryPacket` values through `TelemetryReplayService.PacketReplayed`.
- `HapticPipelineCoordinator.ReplayService_PacketReplayed` calls `OfferReplayTelemetryPacket`.
- Replay packets use the same F1 25 parser and `VehicleState` adapter path as live UDP.
- Replay does not forward UDP and does not record itself.
- Haptic output realism depends on preserving packet timing and having the relevant output path running/armed. Fast replay batch-updates state and diagnostics too quickly to represent haptic feel.

Safest 18p-B implementation plan:

1. Change normal UI replay to `TelemetryReplayOptions.TimePreserving`.
2. Add an explicit replay mode selector with `Real-time` default and `Fast parser/debug` as a clearly labelled advanced/debug option.
3. Consider changing service default from fast to time-preserving only after checking tests and any deterministic callers; alternatively require the UI to pass an option explicitly.
4. Add fake-clock/unit coverage for time-preserving UI/service calls instead of sleeping in tests.
5. Add delete-selected recording with confirmation, missing-file handling, locked-file failure text, and library refresh.
6. Keep `.hdrec` version 1 compatible because it already stores relative timing.

## Recommended Staged Implementation Plan

### 18p-B - Telemetry / UDP Library Cleanup And Replay Timing Fix

- Default UI replay to real-time/time-preserving mode.
- Add explicit Fast parser/debug replay mode.
- Add Delete Selected recording.
- Add tests for real-time replay, fast replay, missing/locked delete handling, and library refresh.
- Do not change parser layouts, ASIO, P-HPR report bytes, or physical-output behavior.

### 18p-C - App Shell, Dark Theme, Sidebar, And Cards

- Extract `Theme.xaml` and `Styles.xaml`.
- Apply dark dashboard styling with red accent tokens.
- Restyle top status/action bar, sidebar, status cards, and page bands.
- Keep current named controls and event handlers in place.
- Reduce wall-of-text on normal pages by moving long diagnostics behind Advanced.

### 18p-D - Effects Page Hardware/Effect Restructure

- Build the hybrid Effects layout: BST-1, Brake P-HPR, Throttle P-HPR sections with effect cards.
- Move normal P-HPR road/slip/lock settings out of Advanced into Effects.
- Keep low-level P-HPR min/max Hz/strength and raw routing diagnostics in Advanced.
- Introduce or prepare a versioned settings model for shared gear duration and shared road signal tuning.

### 18p-E - Devices And Advanced Cleanup

- Keep Devices focused on hardware discovery, readiness, connection, and manual tests.
- Move validation harnesses, raw direct candidate/report internals, mock routers, and long diagnostics to Advanced.
- Keep Stop All / Emergency Stop highly visible.
- Keep real direct enable/arm/runtime-only safety gates intact and not persisted.

### 18p-F - Routing / Mixer And Final Visual Polish

- Polish Routing / Mixer as output route, gain, limiter, mute, and priority summary.
- Show per-output enable/overall strength and active effects summary.
- Surface priority/ducking summary: gear over road, slip/lock above road, road ducking active/inactive.
- Finalize spacing, contrast, keyboard focus states, and copy.

## Tests To Add Or Preserve

Replay/recording tests:

- Recording stores compatible delta/relative timing for every packet.
- Real-time replay preserves inter-packet delays with a fake clock.
- Fast replay intentionally ignores delays.
- Replay selected feeds parser in original packet order.
- Delete selected recording removes the file and refreshes the library.
- Delete missing file is handled gracefully.
- Delete locked/unauthorized file reports a clear failure and keeps the library stable.
- Old `.hdrec` version 1 recordings remain compatible because relative timing already exists.

UI/model tests:

- Effect settings model supports per-output/per-effect controls.
- Gear duration can be shared across BST-1/P-HPR with per-output override support.
- Road texture model does not require a user-facing pulse duration.
- Advanced-only controls are not required for normal road/gear tuning.
- Emergency stop, direct arming, private device paths, active pulse state, and write history are not persisted.
- Moving controls between pages does not change existing runtime settings until the user edits them.

## Stage 18p-A Outcome

Stage 18p-A should stop here as report-only architecture work. The root cause for replay timing is clear enough that 18p-B can be a small implementation stage. The full UI rewrite should wait until after 18p-B and should proceed in the 18p-C through 18p-F sequence above.

## Stage 18p-B Update

Stage 18p-B implemented the replay/delete follow-up from this report:

- Replay Latest and Replay Selected now use explicit UI-selected replay options.
- `Real-time` replay is the default UI mode and preserves recorded packet timing.
- `Fast debug` replay remains explicit parser/debug mode and is labelled unsuitable for feel/latency testing.
- The replay service default remains fast when options are omitted so deterministic service callers keep their existing behavior; the WPF UI no longer relies on that default.
- Delete Selected recording was added with recordings-folder, `.hdrec`, active-recording, missing-file, and locked/unauthorized-file safeguards.
- The broader dark/sidebar/card product UI rewrite remains staged for 18p-C onward.

## Stage 18p-C Update

Stage 18p-C implemented the shell/theme/card foundation from this report:

- `App.xaml` now merges `Resources/Theme.xaml` and `Resources/Styles.xaml` instead of keeping the visual system inline.
- The default WPF shell is dark-first with red accent tokens, sidebar navigation, top status/action controls, shared card borders, and reusable button/input/navigation styles.
- `MainWindow.xaml` was visually restyled in place so current named controls, event handlers, page visibility, settings/profile binding, replay controls, and diagnostics update paths remain stable.
- `MainWindow.xaml.cs` only updates theme palette resource values and the top-bar page context; no haptic runtime behavior, parser layout, ASIO backend, P-HPR HID/report bytes, effect math, routing logic, recording format, or replay scheduler behavior changed.
- The final Effects hybrid hardware/effect layout, Devices cleanup, Advanced diagnostics cleanup, Routing / Mixer polish, and final contrast/spacing pass remain staged for 18p-D through 18p-F.

## Stage 18p-D Update

Stage 18p-D implemented the hybrid Effects page layout from this report:

- Effects now starts with Shared / Global Effect Settings, including the shared gear shift master duration used by brake P-HPR, throttle P-HPR, Direct Paddle Gear Bench, and BST-1 sync mode.
- Effects are grouped by hardware output: BST-1 Seat Shaker, Brake P-HPR, and Throttle P-HPR.
- BST-1 cards now cover gear shift, road texture, engine vibration, kerb, impact, and wheel slip / wheel lock while keeping the existing audio effect settings and handlers.
- Brake P-HPR cards now cover gear shift, road texture, and wheel lock; throttle-only slip controls are not shown in the brake section.
- Throttle P-HPR cards now cover gear shift, road texture, and wheel slip; brake-only lock controls are not shown in the throttle section.
- Road texture is presented as continuous/synthetic and linked to the shared `RoadTextureSignal`; normal Effects road cards do not expose a pulse duration.
- Low-level P-HPR min/max Hz, minimum strength, internal command duration, target overrides, raw direct candidate/report controls, validation harnesses, mock routers, and raw diagnostics remain in Advanced.
- Devices keeps hardware discovery/readiness, ASIO manual BST-1 pulse testing, P-HPR mode/readiness/Stop All, manual brake/throttle pulse tests, and wheel/paddle listener/mapping.
- No haptic runtime behavior, parser layouts, ASIO backend behavior, P-HPR HID/report bytes, Direct Paddle Gear Bench runtime, command-rate limiter logic, replay/delete behavior, or physical-output behavior changed.
- Devices cleanup and Advanced diagnostics cleanup remain staged for 18p-E; Routing / Mixer polish remains staged for 18p-F.

## Stage 18p-E Update

Stage 18p-E implemented the Devices / Advanced cleanup from this report:

- Devices now keeps the operational hardware surface: ASIO output mode/driver/channel/arm/readiness, manual BST-1 pulse testing, P-HPR enable/mode/readiness/emergency recovery/Stop All, manual brake/throttle pulse tests, and wheel/paddle input refresh/listener/mapping/debounce/status.
- Detailed Local Gear Test and Paddle Gear Bench validation controls moved out of Devices and into Advanced Diagnostics.
- Advanced Diagnostics now owns the bench validation surface next to real direct-control candidate/report/open-check/dry-run controls, controlled validation harness, mock gear routing, mock pedal effects routing, and low-level P-HPR diagnostic ranges.
- The moved controls kept their existing names and handlers so settings hydration, runtime status updates, listener start logic, bench counters, and shared duration synchronization continue to use the existing code paths.
- Source-XAML tests now guard the Devices boundary and Advanced ownership of bench/direct/mock/low-level diagnostic controls.
- No haptic runtime behavior, parser layouts, ASIO backend behavior, P-HPR HID/report bytes, Direct Paddle Gear Bench routing behavior, command-rate limiter logic, replay/delete behavior, or physical-output behavior changed.
- Routing / Mixer polish and final visual review remain staged for 18p-F.
