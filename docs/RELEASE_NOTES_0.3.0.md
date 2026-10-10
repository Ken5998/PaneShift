# PaneShift v0.3.0 — MSI and optional release checks

Per-user MSI installation, familiar application shortcuts and updates you control.

## Highlights

- Optional automatic stable-release checks every 24 hours, disabled by default; manual Check now in General and the tray.
- Release notes and SHA-256-verified downloads. Installed copies can start an MSI update explicitly; portable copies download a ZIP for manual extraction.
- Per-user Windows Installer packaging, repair and major upgrades, with conditional startup cleanup.
- Migration from the default Inno installation after the new MSI payload commits, preserving settings and startup preference.
- Startup migration captures the original opt-in in the interactive installer and checks it before and after legacy cleanup, including when the installer service cannot read the initial Run value.
- Optional Mac-style shortcuts, disabled by default: Win+C/V/X/A/Z/S/F for copy, paste, cut, select all, undo, save and find; Win+Shift+Z for redo.
- Win+T opens a tab and Win+Shift+T reopens a closed tab. Win+W sends Ctrl+W to close the current tab/document; Win+Q sends Alt+F4 with normal save prompts and cleanup, never forceful process termination.
- While enabled, the whitelist replaces native Windows combinations including Win+V/A/S/W. Disable and Apply to restore them immediately. Unrelated Windows shortcuts remain native.
- Existing settings, window-management bindings and gaps are preserved. Mac-style shortcuts and automatic update checks default to off for older configurations.
- Update requests use GitHub only; no settings, keyboard activity or telemetry are uploaded. No unattended installation or extra service/task is added.
- The PowerShell installation endpoint accepts historical EXE and new MSI releases and waits for normal application exit.

## Downloads

Release assets produced by the existing tag-triggered GitHub Actions workflow:

- `PaneShift-0.3.0-Setup-x64.msi`
- `PaneShift-0.3.0-win-x64.zip`
- `SHA256SUMS.txt`

Windows 11 x64; the .NET 10 runtime is included. Exit PaneShift through the tray before installing. The MSI installs for the current user without an administrator prompt. Portable users should extract the entire ZIP into a new folder.

The GitHub release records signing status, checksums and available antivirus reports for its exact downloadable files. Checksums and scan reports do not establish publisher identity or guarantee safety. The older v0.2.0 draft and its EXE assets are retained unchanged.

Update checks are optional and installation always requires user action. Elevated PaneShift instances must be reopened normally before updating. Standard-user shortcut translation cannot control elevated applications; the existing explicit administrator restart remains available for those windows.
