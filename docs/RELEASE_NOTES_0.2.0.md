# PaneShift v0.2.0 — Mac-style shortcuts

## Highlights

- Optional **Mac-style shortcuts**, disabled by default for new and existing users. Enable them in **Settings → Keyboard**, then Apply.
- Win+C/V/X/A/Z/S/F translates to Ctrl+C/V/X/A/Z/S/F. Win+Shift+Z sends Ctrl+Shift+Z.
- Win+T opens a tab; Win+Shift+T reopens a closed tab in supporting applications.
- **Win+W → Ctrl+W** closes the current tab/document instead of opening Windows Widgets.
- **Win+Q → Alt+F4** closes the foreground window gracefully, preserving save prompts and cleanup. It never forcibly terminates a process.
- Only the explicit whitelist is translated. Win+V, Win+A, Win+S, Win+W and the other listed native shortcuts are replaced while enabled; native behavior returns immediately on disable + Apply or tray pause. Unrelated Windows shortcuts remain unchanged.
- Existing JSON, window shortcuts, gaps and startup preference remain compatible. Apply, Cancel, Restore defaults and Reload Settings support the new feature; failed activation retains the previous configuration.
- Window management continues using RegisterHotKey. The optional translation layer needs a narrowly scoped low-level hook because Windows reserves the source combinations. No typed text or keyboard activity is logged, stored or transmitted; injected input is ignored.
- Standard-user operation is preserved. Use the existing explicit administrator restart for elevated targets. Target apps decide the meaning of standard chords; games, protected apps and competing keyboard utilities may require pausing shortcuts.

## Downloads

- `PaneShift-0.2.0-Setup-x64.exe` — per-user Windows 11 x64 installer.
- `PaneShift-0.2.0-win-x64.zip` — extract all files and run `PaneShift.App.exe`.
- `SHA256SUMS.txt` — hashes of the final installer and ZIP.

The .NET runtime is included. Exit PaneShift before updating. Installation preserves settings and does not enable Mac-style shortcuts or startup automatically. Signing and VirusTotal results are reported by the release workflow for the actual final assets; this source document makes no scan or signing claim.
