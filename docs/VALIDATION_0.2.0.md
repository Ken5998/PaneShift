# v0.2.0 validation record

Date: 2026-09-30. This records observed results; it does not imply every manual matrix combination was exercised.

## Architecture decision

A native RegisterHotKey probe failed with error 1409 for 11 of 12 requested combinations (only Win+Shift+Z succeeded). Window management keeps RegisterHotKey. The optional application whitelist uses WH_KEYBOARD_LL on a dedicated message thread, ignores injected events, and sends balanced SendInput sequences. See [architecture details](ARCHITECTURE.md#mac-style-shortcut-translation) and the [exact mappings](../README.md#mac-style-shortcuts).

## Automated checks

- Existing and new suites: **623 passed, 0 failed, 0 skipped** (564 Core; 59 Windows/App).
- Release restore/build: **0 warnings, 0 errors**, .NET SDK 10.0.401.
- Coverage includes backward-compatible defaults, persisted opt-in, invalid JSON section, internal conflicts, transactional failures, enable/disable/re-enable/pause, draft/reset/reload, every mapping, injected-event exclusion, source auto-repeat and key-up handling, modifier restoration and every partial input prefix.
- Existing release-script tests passed (syntax, 16 version cases, cleanup boundaries, VirusTotal multipart binary payload). Actionlint passed for all workflows.
- Static site assembled with the existing script. The new section links to all mappings; download buttons and PowerShell installer continue resolving the latest published release.
- Build-Release produced the self-contained win-x64 ZIP, Inno Setup installer and SHA256SUMS.txt. Verify-Release checked all 478 ZIP files against the publish directory, version/architecture, included runtime and asInvoker/uiAccess=false/PerMonitorV2 metadata. Local builds are unsigned.

## Interactive acceptance observed

The maintainer confirmed the requested physical-key smoke checks: browser Win+T/Shift+T/W, editor Win+A/C/V/Z/S/F, normal close/save-confirmation behavior for Win+Q, native Win+E/R, and native Win+V/A/W restored after disabling + Apply. No unwanted Start opening or stuck modifiers was reported.

The ZIP was extracted into a new directory and launched with an unrelated working directory. Version 0.2.0 and 478 extracted files were verified. Loaded coreclr, hostpolicy and WPF native modules came from that extracted directory. The maintainer also confirmed opening Keyboard and the held-letter-before-Win case on this build.

Automatic visual capture could not attach to the Settings HWND because the computer-use helper reported an inconsistent window-owner error, including after refreshing the window reference. No successful automated screenshot/theme/DPI verification is claimed.

## Upgrade and remaining scope

AppId, per-user install path, settings path and startup implementation are unchanged. The 0.1.1 installer has the same upgrade/preservation rules. Old JSON defaults the new feature to off; automated round-trip tests preserve prior hotkeys and gaps. Installation never enables the feature or startup.

The expanded [manual checklist](TESTING.md#mac-style-shortcuts-v020) covers Explorer, alternate browsers/editors, all modifier combinations, elevated targets, lock/task switching, themes/DPI and games. Those broader combinations and a fresh no-.NET machine have not all been re-tested for 0.2.0. Earlier device/UAC/runtime acceptance is historical evidence, not a new test of the optional layer.

Elevated/protected apps, raw-input games and competing hooks remain subject to Windows/application behavior. Ctrl+Shift+Z only performs redo where the target supports it; Alt+F4 may close one window or preserve a session according to the target's own policy. SendInput failure recovery is best-effort under UIPI. No forceful termination or privilege workaround is used.

## Local installer upgrade

The installed executable was actually version 0.1.0. The 0.2.0 installer completed an in-place per-user upgrade with exit code 0. The settings file SHA-256 and exact startup Run value were identical before and after installation. Installed EXE bytes matched the packaged executable, version 0.2.0; the installed copy was launched afterward. The 0.1.1 → 0.2.0 path was assessed through unchanged installer/AppId/path logic and backward-compatible JSON tests, not falsely recorded as an executed install.

## Official Actions artifacts

Tag `v0.2.0` points to implementation commit `0b3cf5a`. [CI](https://github.com/Ken5998/PaneShift/actions/runs/36733208149), [release build](https://github.com/Ken5998/PaneShift/actions/runs/36733207752) and [website deployment](https://github.com/Ken5998/PaneShift/actions/runs/36733208168) succeeded. The public HTTPS website was checked for the new feature section.

All three draft assets were downloaded through GitHub and matched their GitHub SHA-256 digests. Both package hashes also matched the downloaded SHA256SUMS.txt:

| Official artifact | SHA-256 |
| --- | --- |
| PaneShift-0.2.0-Setup-x64.exe | `035e0d08e954126a98359794bbfc7ac5421ef71a2ad129b6356141efab489567` |
| PaneShift-0.2.0-win-x64.zip | `c79397d3215b9fd31a52243acb6b0da1db1584739d93b50e917487774ee2f306` |

The official ZIP was extracted and checked: 478 files, version 0.2.0 / file version 0.2.0.0, bundled .NET and Windows Desktop runtimes 10.0.12, expected manifest and no source/test/PDB/private-key files. Authenticode reports NotSigned for both application and installer. These official hashes differ from local builds; only the Actions artifacts are distributed.

## Changed files in the release implementation

- Core: `MacStyleShortcuts.cs`, `PaneShiftSettings.cs`, `HotkeySettings.cs`.
- Windows: `IMacStyleShortcutService.cs`, `MacStyleShortcutService.cs`, `ShortcutInput.cs`, `ConfigurationActivation.cs`.
- App: `TrayApplication.cs`, `Settings/SettingsViewModel.cs`, `Settings/SettingsWindow.xaml`.
- Tests: `MacStyleShortcutTests.cs`, `MacStyleActivationTests.cs`, `ShortcutTranslationTests.cs`.
- Version/workflow: `Directory.Build.props`, `.github/workflows/virustotal.yml` (default report version only).
- Website: `site/index.html`.
- Documentation: `README.md`, `docs/ARCHITECTURE.md`, `docs/CODE_SIGNING.md`, `docs/RELEASING.md`, `docs/SETTINGS.md`, `docs/TESTING.md`, `docs/WEBSITE.md`, `docs/RELEASE_NOTES_0.2.0.md`, this validation record.

The existing release mechanism, installer and window geometry were reused without changes.

## Release hold and antivirus investigation

The maintainer explicitly chose to keep v0.2.0 as a draft pending investigation. The latest public release remains v0.1.1; the v0.2.0 tag and verified assets have not been replaced or rebuilt.

The [report-only refresh](https://github.com/Ken5998/PaneShift/actions/runs/36734743485) completed successfully. At review time (2026-09-30), the installer had **2 malicious / 0 suspicious** results and the ZIP **0 malicious / 0 suspicious**. The public installer detection page identified **Microsoft: Trojan:Win32/Wacatac.B!ml** and **DeepInstinct: MALICIOUS**, 2/67 vendors. These are unresolved detections, not confirmed false positives.

Read-only custom scans with local Microsoft Defender returned exit 0 / “found no threats” for the exact official installer, the complete extracted official ZIP and the local installer. Product 4.18.26080.4, engine 1.1.26080.3, signatures 1.459.485.0 (updated 2026-09-30). Scans used `-Scan -ScanType 3 -File <specific path> -DisableRemediation`; no security settings/exclusions were changed. The diagnostic mode scans archives and ignores file exclusions, without remediating the scanned file. This local result does not supersede VirusTotal's different result or establish a false positive.

Comparison against v0.1.1 found no change to `installer/PaneShift.iss`, `scripts/Install-InnoSetup.ps1`, `scripts/Package-Release.ps1` or `.github/workflows/release.yml`. Actions installed pinned Inno Setup 6.7.3 after SHA-256 and publisher validation. The source review found no network transport, remote-thread/process-memory injection or forceful process-termination calls in the application projects. The new optional hook and SendInput calls are the documented whitelist implementation.

VirusTotal's public CAPE behavior view showed the installer launching its extracted Inno Setup `.tmp` component; PaneShift itself was not present in the shown process tree. The view reported no network communications or direct behavior detections, alongside generic MITRE/Sigma labels (including executable creation) and an indication that some sandboxes were still analysing. Those labels alone neither prove malicious behavior nor establish the cause of the AV verdict. In particular, the available evidence does not show that the keyboard hook caused the installer detection. 7-Zip could not unpack this Inno container, so no independent byte-for-byte installer-payload comparison is claimed beyond the trusted packaging workflow and its validated inputs.

Next step: request vendor review of the **unchanged official installer**, hash `035e0d08e954126a98359794bbfc7ac5421ef71a2ad129b6356141efab489567`, using [Microsoft's software-developer submission](https://www.microsoft.com/en-us/wdsi/filesubmission) and DeepInstinct's official support/contact route. No vendor submission or message has been sent. A ready-to-copy submission text and exact sample are retained locally under `artifacts/github-v0.2.0/`. Only the vendors can resolve their classifications. Keep the draft until that evidence is reviewed; do not disable protection or alter binaries merely to change a detection score.
