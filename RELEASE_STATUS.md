# Release Status

- Packaging status: software hardening complete; manual hardware validation and owner/legal decisions remain.
- Redistribution status: blocked until the project owner selects explicit license terms.
- Supported release path: Windows, .NET 8 Desktop Runtime, framework-dependent `win-x64` package.
- Default safe runtime path: `NullAudioOutputDevice`.
- ASIO remains the intended low-latency production output path.
- WASAPI remains experimental/manual-debug-only and must not be represented as the production output path.
- Real P-HPR non-stop writes require session-only authorization, direct enable/arm, clear coexistence, and a clear global interlock at the physical write boundary.
- Physical shaker feel, safe gain, physical latency, and final tuning remain local hardware-validation items and are not claimed by software packaging alone.
- Real P-HPR stop/off behavior and direct coexistence behavior remain manual local validation items.
