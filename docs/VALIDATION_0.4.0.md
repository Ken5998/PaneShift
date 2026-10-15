# 0.4.0 validation

Release preparation, 2026-10-16. This record distinguishes development acceptance, local packaging and final GitHub Actions artifacts.

## Feature acceptance

The optional Swiss Right Alt+G feature extends the existing configuration transaction and dedicated keyboard hook rather than adding another input subsystem. Default window bindings remain registered natively while the feature is off. Details and the modifier-state fix are recorded in [KEYBOARD_ALTGR.md](KEYBOARD_ALTGR.md).

Debug and Release suites passed all **693 tests** (579 Core, 114 Windows/App), with no failures or skips. Coverage includes backward-compatible settings, independent flags, view-model draft/reset/reload, exact Alt-side routing, unrelated AltGr characters, repeats/injected input, balanced Unicode sends including partial-send recovery, and activation rollback.

The maintainer confirmed the corrected development build with physical keys on Italian (Switzerland) and a Swiss French keyboard: repeated AltGr+G, release then letters/space/Esc/arrows/Ctrl+C/V, G re-pressed while holding AltGr, AltGr+E and Ctrl+Left Alt+G worked without a modifier remaining active. Only the new keyboard flag changed in the saved JSON; other settings and the exact startup command matched their pre-test backup.

Visual automation could not attach to the development Settings window. No new screenshot, light-theme/DPI matrix, clean-machine runtime test, reboot acceptance or full app/game/elevated-foreground matrix is claimed for this version. The [manual checklist](TESTING.md) retains those checks. Word replacement is not part of this release.

## Packaging and publication

`Build-Release.ps1 -Version 0.4.0` completed dependency restore, Release build, all 693 tests, self-contained win-x64 publish, ZIP/MSI packaging and checksum verification. Application and installer builds had zero warnings/errors. The payload contains 478 files and .NET/Windows Desktop runtime 10.0.13. MSI metadata verifies x64, per-user/no-elevation scope, the unchanged production UpgradeCode and startup/migration actions.

Release-script syntax, 16 semantic-version cases, cleanup guards, MSI/EXE naming and VirusTotal multipart tests passed. Actionlint found no workflow errors. The static site build and `git diff --check` passed.

| Local unsigned review artifact | SHA-256 | Bytes |
| --- | --- | --- |
| PaneShift-0.4.0-Setup-x64.msi | `5a6be1aed6166196594bd5cfbf0405528a93902e6f481366ed234997b69e7e9a` | 58,663,640 |
| PaneShift-0.4.0-win-x64.zip | `f6aa2b6d5fc4516966325c19acead01e8ccf72cb54ac4af7757620f4f5228083` | 76,182,186 |

These are local review hashes, not final GitHub download hashes. The maintainer explicitly authorized submission of both final Actions-built 0.4.0 packages to VirusTotal before publication. Final remote artifact verification will be recorded separately. Historical releases, including the v0.2.0 draft, remain unchanged.
