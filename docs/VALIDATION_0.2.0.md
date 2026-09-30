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
