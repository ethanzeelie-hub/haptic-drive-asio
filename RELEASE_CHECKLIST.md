# Release Checklist

Use this checklist before treating a local package as release-ready.

## Preconditions

- The solution restores in locked mode.
- High and critical package vulnerabilities are absent.
- Release build, Release test, format verification, vulnerable-package audit, and Release launch preflight all pass.
- `LICENSE.md` still accurately states redistribution status.
- `THIRD_PARTY_NOTICES.md` is current for shipped dependencies.

## Artifact checks

- `.\Publish-HapticDrive.ps1 -Configuration Release -Runtime win-x64` succeeds.
- `.\Test-ReleaseArtifact.ps1 -Configuration Release -Runtime win-x64` succeeds.
- The default zip includes:
  - app binaries,
  - `README.md`,
  - `QUICK_START.md`,
  - `LICENSE.md`,
  - `RELEASE_STATUS.md`,
  - `THIRD_PARTY_NOTICES.md`.
- The default zip excludes portable PDBs.
- The release manifest and package manifest contain no absolute workspace paths.
- The release manifest includes commit hash, configuration, runtime identifier, and package SHA-256.
- SHA-256 checksum file matches the produced zip.

## Policy checks

- README does not imply production WASAPI streaming support.
- No release script disables NuGet audit.
- No known unresolved high or critical dependency vulnerabilities remain.
- Public redistribution is still blocked until the owner chooses a license.

## Manual reminder

- Packaging readiness does not claim physical shaker feel, safe gain, latency, or final tuning.
