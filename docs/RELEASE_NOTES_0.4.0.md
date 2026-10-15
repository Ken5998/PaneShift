# PaneShift v0.4.0 — Swiss keyboard shortcuts

An optional at-sign shortcut for users accustomed to Swiss Mac keyboards, alongside PaneShift's existing window management and Mac-style Win shortcuts.

## Highlights

- New **Swiss keyboard** option in **Settings → Keyboard**: enable **Write @ with Right Alt + G (AltGr)** and **Apply**. It is disabled by default, including when upgrading older configurations.
- Right Alt/AltGr+G inserts `@`. Other AltGr characters and dead keys retain the Windows layout's behavior. Validated with Italian (Switzerland) and a Swiss French physical keyboard.
- Ctrl+Left Alt window-management shortcuts continue to work, including Ctrl+Alt+G for the right third. While the Swiss option is enabled, Ctrl+Alt window bindings share the existing selective hook so Windows does not consume native AltGr text as a window shortcut.
- Balanced Unicode input avoids the stuck Ctrl/AltGr behavior found and corrected during development. It never releases or restores Ctrl/Alt to insert the character, and ignores injected events to prevent loops.
- The Swiss option is independent of the Mac-style Win-key feature. Apply/Cancel, Reset to Defaults, Reload Settings, pause and transactional rollback include both options.
- Existing gaps, window shortcuts, startup preference, update preference and other settings are preserved. Older JSON files remain compatible and do not need manual editing.
- No typed-text buffer, key logging, clipboard access, layout installation or network traffic is added by this feature. The hook is removed when neither optional keyboard feature needs it, and on pause/exit.

Automatic word replacements such as `cmq` → `comunque` are not included in this release.

## Downloads

The existing tag-triggered GitHub Actions workflow produces:

- `PaneShift-0.4.0-Setup-x64.msi`
- `PaneShift-0.4.0-win-x64.zip`
- `SHA256SUMS.txt`

Windows 11 x64; the .NET 10 runtime is included. Exit PaneShift from the tray before installing. The MSI upgrades the previous per-user MSI installation without enabling the new feature automatically. Portable users should extract the entire ZIP into a new folder; both variants share the existing settings path.

The GitHub release records actual signing status, checksums and available scan results for its exact assets. No signing or antivirus result is implied by these source notes.

## Limitations

Applications must accept Windows Unicode input. Standard-user PaneShift cannot send input into elevated applications; use the existing explicit administrator restart when needed. When Swiss support is enabled, hook-routed Ctrl+Alt window shortcuts cannot guarantee exclusive reservation against other keyboard utilities. Disable and Apply to restore the normal RegisterHotKey route. See [implementation and validation](KEYBOARD_ALTGR.md) and the [manual regression checklist](TESTING.md).
