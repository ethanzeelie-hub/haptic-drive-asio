# How to Add a Haptic Effect

Use this checklist for every new audio haptic effect.

1. Add a descriptor to `BuiltInHapticEffectRegistry` with:
   - stable `Key`,
   - `DisplayName`,
   - `Category`,
   - `RequiredSignals`,
   - `Parameters`,
   - `CreateDefaultSettings()`,
   - `Validate(...)`,
   - `CreateRuntime(...)`.

2. Implement or extend the effect runtime/DSP class in `HapticDrive.Asio.Audio`.

   The runtime must render from canonical `HapticRenderFrame`.

3. Add the effect's default settings to descriptor coverage only.

   Do not add a new profile schema field.

4. Add tests for:
   - registry presence,
   - descriptor runtime creation,
   - default-settings validation,
   - out-of-range validation behavior,
   - persistence/migration behavior if the effect adds new parameters,
   - runtime behavior as appropriate.

5. If the effect needs presentation text, add that through descriptor-driven or presenter-level seams.

   Do not add new `MainWindow.xaml.cs` switch branches or hardcoded profile persistence branches.

6. Keep the runtime strongly typed and real-time safe.

   Descriptor metadata is for construction, validation, persistence, and UI generation. It is not a script host.

7. Preserve compatibility rules:
   - no real P-HPR writes in automated tests,
   - no hardware dependency in normal unit tests,
   - no blocking/logging/string allocation in the real-time render path,
   - no render-path locking or post-warmup allocation.

Quick acceptance checklist:

- descriptor registered,
- settings validate,
- schema-v2 save/load round-trips,
- unknown effect keys still preserve safely,
- no `MainWindow` effect-specific switch added,
- no new profile JSON top-level field added for the effect.
