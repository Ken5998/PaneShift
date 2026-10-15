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

These are local review hashes, not final GitHub download hashes. The maintainer explicitly authorized submission of both final Actions-built 0.4.0 packages to VirusTotal before publication. Final remote artifact verification is recorded below. Historical releases, including the v0.2.0 draft, remain unchanged.

## Final Actions-built files

[Release workflow 38817272730](https://github.com/Ken5998/PaneShift/actions/runs/38817272730) succeeded for tag `v0.4.0`, targeting `dbb24839db8818aa90bd03c03eeeac77f4bb839f`. All 693 tests passed again, application and installer builds had zero warnings/errors, and final payload/MSI/ZIP verification passed. Signing was not enabled; these assets are unsigned.

All three downloaded files matched GitHub asset digests, and the MSI/ZIP matched `SHA256SUMS.txt`. The extracted ZIP contains 478 files with product version 0.4.0 and file version 0.4.0.0.

| Final artifact | SHA-256 | Bytes |
| --- | --- | --- |
| PaneShift-0.4.0-Setup-x64.msi | `310e172420d1782e92ea745ed817f05269729749b066c4010c062acfbce21a7d` | 58,634,968 |
| PaneShift-0.4.0-win-x64.zip | `f23d2962bcaca3d61ebc9110ef7754ffbfe95835360c629a74608f724602fa78` | 76,182,165 |
| SHA256SUMS.txt | `7472dc7bc4ed6f54168822423f1ec08a7598b366ab19f8933d095029b14b6e10` | 192 |

## Upgrade acceptance of the final MSI

With PaneShift already closed normally, the exact Actions-built MSI above upgraded the existing 0.3.0 installation and returned exit 0. The MSI log records successful installation of 0.4.0 and removal of the previous product. All 478 installed payload files match the final extracted ZIP.

The settings JSON stayed byte-for-byte unchanged (`58BF886810602F5C2A5A429BF7F8A3076C778FE8520E0D83DB4BFCC394D2750B`), and the exact startup Run command stayed unchanged. The maintainer's intentionally enabled Swiss/Mac-style/update preferences were retained. Backward-compatible defaults for files without the new flag are covered by automated tests; this live upgrade tests preservation of an existing opt-in.

The installed 0.4.0 executable was launched successfully. Its loaded `hostpolicy.dll`, `coreclr.dll` and `PresentationNative_cor3.dll` came from the installed MSI directory. No forceful process shutdown was used. This is a successful MSI major upgrade and installed launch, not a successful upgrade through the updater helper, final portable launch, fresh no-.NET machine test or new reboot test. Local backups, installer log and comparison evidence are under ignored `artifacts/validation-0.4.0/`.

## Scans and publication

Both final files were submitted through the existing release workflow with the maintainer's explicit consent. The initial API responses were pending. Report-only refreshes did not upload the files again. The ZIP public report subsequently exposed a newer analysis ID for the same exact SHA-256; the draft was aligned with that visible analysis before the [final report-only refresh](https://github.com/Ken5998/PaneShift/actions/runs/38818849377).

- [Final MSI report](https://www.virustotal.com/gui/file/310e172420d1782e92ea745ed817f05269729749b066c4010c062acfbce21a7d/detection): completed, **0 malicious / 0 suspicious** engine results.
- [Final ZIP report](https://www.virustotal.com/gui/file/f23d2962bcaca3d61ebc9110ef7754ffbfe95835360c629a74608f724602fa78/detection): completed, **0 malicious / 0 suspicious** engine results. The public page displayed **0/62**, with unsupported-type, timeout and failure entries listed separately.

These are informational results for these exact hashes, not signing, a safety guarantee or proof that every file inside the archive was scanned. The generated GitHub release notes contain both completed results and report links.

[PaneShift v0.4.0 — Swiss keyboard shortcuts](https://github.com/Ken5998/PaneShift/releases/tag/v0.4.0) was published as stable/latest on 2026-10-16 local time (2026-10-15 22:42:53 UTC), with the three verified public assets above. README, version source, configuration/architecture/security documentation, regression checklist, release notes and website copy were updated together. The website continues to direct download buttons to the latest GitHub release page. No existing release/tag/assets were overwritten.
