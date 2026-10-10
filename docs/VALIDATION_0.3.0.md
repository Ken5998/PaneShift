# 0.3.0 validation

Release preparation and local acceptance, 2026-10-10. The maintainer authorized v0.3.0 publication after reviewing the local MSI/ZIP scans. Final Actions-built artifact verification is recorded separately below. The v0.2.0 draft/tag/assets remain unchanged.

## Implementation

Core owns backward-compatible update settings, release validation and cadence decisions. Windows owns HTTP downloads, cache persistence, installed-copy detection and a relocated installer helper. App owns the async Updates view model, draft checkbox, one due-time dispatcher timer and graceful shutdown. Existing hotkey/translation architecture is unchanged.

Production MSI uses pinned WiX 6.0.2, a separate stable UpgradeCode and per-user directory, managed custom actions using built-in .NET Framework 4.8, commit-phase Inno cleanup, conditional Run cleanup and rollback restoration. Custom actions resolve the installing user's explicit loaded registry hive. Native AppSearch provides validated legacy-record values from the interactive client when the service cannot read them. A global mutex scoped by user SID lets the installer service detect the running 0.3.0 app across sessions. The app retains its old local mutex for compatibility; older Inno copies are checked once by exact executable path at preflight. Shared MSI authoring is also used by the isolated trial script; historical artifacts are not rebuilt.

## Build and package verification

- Initial `Build-Release.ps1 -Version 0.3.0` completed restore, Release build, full tests, self-contained publish, ZIP/MSI packaging and final verification with 655 passing tests. After the startup correction, Release build and the full suite passed again: **662 tests passed, none failed/skipped** (573 Core, 89 Windows/App), zero warnings/errors. `Build-Msi.ps1` rebuilt only the installer and `Verify-Release.ps1` reverified the existing unchanged publish/ZIP and new MSI.
- Release-script checks passed syntax, 16 semantic-version cases, cleanup boundaries, historical EXE/current MSI naming and VirusTotal multipart payload tests. Actionlint reported no workflow errors. The static site build passed.
- A separate compiled probe used the real public GitHub endpoint and offered no newer stable release to version 0.3.0. This verifies a live check, not a future-release installation.
- Self-contained payload verification covered all 478 files, x64 metadata, included runtime and the existing asInvoker/uiAccess=false/PerMonitorV2 manifest. MSI verification checked version, per-user/no-elevation metadata, production UpgradeCode, payload count, HKCU registry ownership and user-impersonated rollback/commit actions.
- Local Defender scanned the exact final MSI below and reported no threats, exit 0: product `4.18.26080.4`, engine `1.1.26080.3`, signatures `1.459.649.0`. No exclusions/protection settings were changed. This is not a VirusTotal result.

| Local unsigned artifact | SHA-256 | Bytes |
| --- | --- | --- |
| PaneShift-0.3.0-Setup-x64.msi | `d203e5407945116c49f5bd6369a2ca1085d0c811ab990084c5ad8fa12956513a` | 58,639,064 |
| PaneShift-0.3.0-win-x64.zip | `b23e3b3406c924be1b9a0287f08e761c3a4a118e1fee1436e32be76b7f71998f` | 76,144,795 |

These are local review packages, not published/tagged Actions artifacts. `SHA256SUMS.txt` is under ignored `artifacts/`, with local evidence under `artifacts/validation-0.3.0`.

## Native installation and Settings checks

An initial automatic approval review rejected a live migration attempt; that command did not execute. The maintainer subsequently explicitly authorized migration and closed PaneShift. The authorized native interactive MSI install then completed with exit 0. Its client AppSearch captured the default Inno record, commit cleanup ran the fixed original uninstaller successfully, old executable/uninstaller files disappeared and the MSI Start Menu shortcut was restored.

Repair restored a deliberately removed installer-owned `LICENSE.txt`. Uninstall removed the app while retaining a deliberately created unowned file and settings. These checks used intermediate builds of the same production authoring/custom actions, before the final cross-session mutex adjustment; they do not establish that every intermediate hash equals the final artifact.

The pre-startup-correction MSI (`706fc8c13274ac0a0370a1a0b02c0ee6d35f8d3493901e03f910db863aaa0b32`) refused installation with exit 1603 while its global per-user mutex was held, before payload mutation. After normal lock release, installation completed with exit 0. All 478 installed payload files matched the publish directory, and Windows Installer reported per-user assignment. Its last automatically inspected ProductCode was `{8E4F458A-4C63-41B6-99C5-FEC5FC68A9D7}`. The rebuilt MSI above has ProductCode `{846C972F-3FF7-4C55-80AF-6A03C2420A56}`; its subsequent manual migration acceptance is recorded below. App payload and ZIP are unchanged.

Settings remained byte-for-byte unchanged through migration, repair, uninstall and final installation (`DFD1848655906374C2D71C7D1CCAB4638C3D9AF26665769B15BD953EB1847F26`). On subsequent Settings verification, JSON included the new disabled update section; previous layout, gaps, Mac-style opt-in and hotkeys remained identical. Toggling the Updates draft on and back off did not write the file (`1D32226747AFFD8A2374298B45136948B4555DC392317B31879B736E5332943F`) and returned Apply to disabled.

The native General window was visually inspected in the installed 0.3.0 app: dark theme, Updates off, startup checked, standard privileges and active shortcuts. **The maintainer subsequently clarified that startup was off at first launch and they manually re-enabled it before this inspection. That checked state did not establish startup preservation.** Check now displayed the busy state, then returned to enabled with an up-to-date status after a real GitHub request. Startup remains an immediate OS preference; Updates uses Apply. Subsequent migration and reboot acceptance is recorded below.

During diagnosis, shell and MSI service reads of the same user's registry disagreed. A local shell startup-path correction was performed, but that shell view alone is not proof of native startup preservation or cleanup. Repeated native uninstall/reinstall tests could also have removed the matching startup value as designed. The exact cause of the maintainer's initial unchecked state is not established. Quiet migration fails cleanly if an old uninstaller exists but its record cannot be validated, with instructions to use the interactive MSI or uninstall the old copy first.

Following the maintainer's correction, native AppSearch now captures a secure/hidden startup snapshot as well as the legacy record. Migration uses the client snapshot only when the service read is absent, checks exact legacy ownership before and after cleanup, and verifies its own write. Seven regression cases cover missing reads, disabled preferences and foreign/current-copy commands. Package verification checks the secure/hidden property and exact HKCU Run locator. Defender scanned the rebuilt MSI successfully. Automation did not change the maintainer's newly enabled installed preference.

The maintainer then retried the instructed migration with the rebuilt MSI. The installer initially refused to proceed while PaneShift was running; after exiting through the tray's Exit command and retrying, the maintainer confirmed that the startup flag remained enabled. This is user-reported acceptance of startup-on preservation and the running-app guard. The test device, installed ProductCode and artifact hash were not independently rechecked during that manual run. It does not establish startup-off/foreign-copy preservation.

After reboot, the maintainer confirmed that PaneShift launched automatically. They also enabled Mac-style shortcuts and automatic update checks and reported that the application appeared to work correctly. This accepts startup after reboot and basic feature enablement. It does not establish persistence of those newly enabled flags after another restart, the full shortcut regression matrix, a real 24-hour automatic check or a future-version download/install/relaunch.

## VirusTotal review of the local final packages

On 2026-10-10, the maintainer explicitly authorized public submission of both 0.3.0 packages. Local hashes matched `SHA256SUMS.txt` immediately before upload. Both web uploads completed and their analyses finished with the exact expected SHA-256 hashes and filenames displayed.

| Package | Completed web report | Result shown |
| --- | --- | --- |
| MSI | [SHA-256 d203e540…](https://www.virustotal.com/gui/file/d203e5407945116c49f5bd6369a2ca1085d0c811ab990084c5ad8fa12956513a/detection) | 0/57 |
| ZIP | [SHA-256 b23e3b34…](https://www.virustotal.com/gui/file/b23e3b3406c924be1b9a0287f08e761c3a4a118e1fee1436e32be76b7f71998f/detection) | 0/62 |

Both reports stated that no security vendors flagged the file as malicious. MSI had 14 unsupported-type entries and four timeouts; DeepInstinct specifically reported **Unable to process file type**, which is not an affirmative clean verdict or resolution of its old EXE detection. ZIP had seven unsupported-type entries, four timeouts and one failure. The upload UI also warned that this multi-file ZIP exceeds its 3 MB contained-file scanning limit; the outer archive result does not establish that every bundled file was scanned.

These are informational results for the local package hashes above, not a guarantee of safety, signing, SmartScreen reputation or a future Actions-built artifact. Screenshots, page snapshots, analysis URLs and completion evidence are retained under ignored `artifacts/validation-0.3.0/virustotal-*`. The earlier 0.2.0 EXE release/tag/assets were not altered or rescanned.

## Remaining acceptance

An ignored compiled probe called the production `UpdateInstaller.StartHelper` against the installed runtime and the cached MSI of that already-installed product. This avoided installing a distinct same-version ProductCode. The parent exited normally; the relocated helper opened the native Windows Installer maintenance wizard. After the maintainer cancelled it, `install.log` recorded exit 1602, the helper displayed the cancellation message and the original installed executable restarted. The Settings JSON hash and startup command matched their baseline exactly. This validates relocation, handoff, cancellation and relaunch, not a successful future-version upgrade.

After another normal tray exit, the probe called production `RunHelperAsync` with a valid staged job and an intentionally incorrect SHA-256. It returned the verification-failed result, did not create an installer log or execute the MSI, and restarted the installed copy. JSON and startup again matched the baseline. The probe and staged evidence are ignored local validation files, not shipped code.

Remaining checks: Inno migration with startup off/foreign registration on a separate device; native Windows uninstall-entry/startup-list inspection; real MSI major upgrade/downgrade refusal and rollback; successful future-version installation through the helper; final portable launch; light theme/DPI and remaining standard-user device acceptance; verification and antivirus review of any different final Actions-built hashes. The [trial MSI acceptance](MSI_TRIAL.md) does not transfer to the new package/custom actions.

The maintainer lifted the publication hold for v0.3.0 after reviewing these local package results. The release will use the existing tag-triggered workflow and its exact final artifacts; different hashes require their own verification and scan review before publication. Public submission of the local MSI and ZIP occurred only after explicit authorization, as recorded above. Automated checks do not establish successful future-release installation or complete device acceptance.
