# Simagic Capture Analysis

Stage 2I adds read-only capture analysis tooling for Simagic P700 / P-HPR research.

The tooling can inspect:

- Wireshark CSV exports that include payload columns such as `payload_spaced`, `usb.data_fragment`, or `usbhid.data`.
- Sanitized Wireshark text summaries that include payload counts and `payload=` lines.
- Sanitized compare-summary text files that list byte differences.
- `.pcap` and `.pcapng` files at the container-summary level.

Raw captures and generated analysis output remain private and ignored by git.

## Safety Boundary

Stage 2I is analysis-only.

Allowed:

- count payload observations,
- fingerprint payloads,
- summarize payload lengths and source columns,
- compare payloads byte-by-byte,
- summarize `.pcap` / `.pcapng` container structure,
- export sanitized JSON reports under ignored paths.

Forbidden:

- USB writes,
- HID output reports,
- HID feature reports,
- vibration commands,
- P-HPR commands,
- protocol hypotheses,
- command encoders or decoders,
- field naming such as module, strength, frequency, duration, report ID, or checksum,
- SimPro Manager / SimHub control,
- ASIO/BST-1 audio path changes.

Stage 2J documents protocol hypotheses separately in `docs/SIMAGIC_PROTOCOL_HYPOTHESES.md`. Stage 2K documents mock-only protocol modelling in `docs/SIMAGIC_P_HPR_MOCK_PROTOCOL.md`.

## Commands

Analyze one file or a folder recursively:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-analysis <capture-or-export-path>
```

Compare two capture/export sources:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-diff <left-capture-or-export-path> <right-capture-or-export-path>
```

Set a custom output directory:

```powershell
.\.dotnet\dotnet.exe run --project src\HapticDrive.Simagic.PHPR.Research\HapticDrive.Simagic.PHPR.Research.csproj -- capture-analysis <path> --output-dir capture-metadata\generated
```

Default outputs:

- `capture-metadata/generated/simagic-capture-analysis-sanitized.json`
- `capture-metadata/generated/simagic-capture-diff-sanitized.json`

## Output Contents

Sanitized reports include:

- source file names only, not absolute private paths,
- payload observation counts,
- payload length counts,
- source column counts,
- payload SHA-256 fingerprints truncated to short IDs,
- short payload previews,
- byte-diff observations,
- `.pcap` / `.pcapng` packet counts, interface counts, link types, and captured-byte totals,
- warnings such as detected USBPcap link type.

Reports intentionally do not serialize raw payload byte arrays.

## Stage 2I Evidence Status

The local `Complete Files Required` bundle contains sanitized Wireshark-derived P-HPR evidence. Stage 2I tooling can analyze those local files, but the bundle itself and generated reports must stay uncommitted unless a future sanitized summary is deliberately reviewed for inclusion.

No real write approval has been provided. Real P-HPR writes remain gated by the exact approval phrase in `docs/SIMAGIC_P_HPR_SAFETY_PLAN.md`.

## Stage 2J Handoff

Stage 2J converts reviewed Stage 2I analysis outputs into formal hypotheses:

- confirmed non-output input mappings,
- SimHub `F1 EC` start/stop/duration hypotheses,
- separate SimPro `80 1E 89` family notes,
- unknowns and missing evidence,
- Stage 2K mock-only surface,
- and real-write blockers.

Stage 2J still does not send USB writes, issue HID output/feature reports, create production encoders/decoders, create live `PHprCommand` values, or route haptics.

## Stage 2K Handoff

Stage 2K uses Stage 2J's SimHub `F1 EC` readiness to create mock-only frames, mock encode/decode tests, deterministic duration schedules, mock output diagnostics, and safe CLI examples.

Stage 2K keeps SimPro `80 1E 89` as `SimProUnknownMock` / `NeedsMoreCaptures`. It still does not send USB writes, issue HID output/feature reports, create production protocol adapters, access hardware, control SimPro Manager or SimHub, create live haptic routing, or validate real P-HPR behavior.
