# Release checks and MSI updates

## Settings and scheduling

`updates.checkAutomatically` defaults to `false`, including existing JSON. General edits it in the normal Apply/Cancel draft; Restore defaults disables it after Apply. A null/non-object section or non-Boolean flag fails validation without activating a partial configuration. Reload reschedules only after a valid configuration has committed.

When enabled, check on launch if at least 24 hours have elapsed, then use a single dispatcher timer for the next due check. No task/service runs while PaneShift is closed. Manual checks ignore this interval. Overlapping checks are coalesced. Disabling cancels an automatic request; exit cancels pending network operations. Attempts, including failures, persist in `update-check.json` beside settings, so restarting the app does not repeatedly hit GitHub. Clock rollback triggers one new attempt with the current timestamp. Automatic notification is shown once per release tag.

The fixed endpoint is `https://api.github.com/repos/Ken5998/PaneShift/releases/latest`. Only newer three-part stable tags from that repository are accepted. Drafts and prereleases are ignored. The UI displays network/format/rate-limit failures without modal alerts; manual retry remains available. Latest-version metadata requests time out after 30 seconds.

## Verified downloads and installation

Release assets must have exact versioned filenames, exact repository download URLs, a positive bounded size and a GitHub `sha256:` digest. MSI: `PaneShift-<version>-Setup-x64.msi`; ZIP: `PaneShift-<version>-win-x64.zip`. Downloading is an explicit action, independent of automatic checks. HTTPS redirects are limited to the exact GitHub/API/release-asset hosts, with no credentials or arbitrary domains. Streamed downloads enforce size limits and verify SHA-256 before renaming the partial file. A missing digest or hash mismatch is an error; there is no unverified execution fallback. SHA-256 is an integrity check, not code signing.

Installed copies are recognized by their per-user MSI marker or the historical Inno installation record. An elevated copy must be closed and reopened normally before update. After download and any unsaved Settings confirmation, a copy of the current application/runtime is staged in a GUID directory under `%LOCALAPPDATA%\PaneShift\Updates`. Its helper invocation runs without acquiring the app mutex or registering shortcuts, waits for the original process identity to exit normally, rechecks the MSI hash, opens the standard Windows Installer wizard, verifies the new executable version and launches it. MSI cancellation/failure restarts the original executable where still available. There is no forced process termination, service, task, UAC bypass or automatic installation.

The staging directory contains the installer and `install.log` for diagnosis. It is retained; automatic cache deletion is not implemented. Close the helper before manually removing a completed staging directory. Portable copies download a verified ZIP and open its folder, leaving extraction and folder switching to the user. They never install over a portable folder.

## MSI migration

Production MSI uses its own stable UpgradeCode and `%LOCALAPPDATA%\Programs\PaneShift-MSI`, distinct from the trial and Inno directories. It is per-user, requires Windows 11 x64 and includes the .NET 10 runtime. Its managed custom actions use the .NET Framework 4.8 already included in supported Windows. No additional runtime download is performed by the installer.

Close any running PaneShift copy first. Install, repair and uninstall check a global mutex scoped to the current user SID and refuse to proceed while it is owned; older Inno copies are checked once by their exact default executable path. Restart Manager automatic closure is disabled. Custom `INSTALLFOLDER` values are rejected to avoid overlapping another installer's file ownership.

When the known HKCU Inno record points to the default `%LOCALAPPDATA%\Programs\PaneShift`, the new payload commits before its fixed `unins000.exe` is invoked. A startup command pointing exactly to the old executable is redirected to the new executable; other commands are untouched. Inno's uninstaller removes its own files/registration; the MSI-owned Start Menu shortcut is restored if that removal deleted the shared shortcut name. Settings JSON is never written or removed by either migration action. The installer never enables startup for users who had it off. A custom-location, missing or redirected legacy uninstaller requires manual removal first.

MSI and Inno do not share an atomic transaction. If legacy cleanup fails after commit, the new MSI remains installed and a migration warning is recorded/displayed; review Installed apps and startup before retrying legacy cleanup. Do not silently treat this as complete migration. Existing legacy files are not recursively deleted by custom actions.

The interactive install captures the startup command alongside the legacy record. If the service cannot see that command, the captured value supplies the original preference; any visible service value takes precedence. Redirection is conditional on exact ownership and is checked before and after Inno cleanup, with a read-back after writing. A flag enabled manually after installation does not establish that migration preserved it. Normal uninstall followed by reinstall deliberately clears the installed copy's opt-in and requires re-enabling it; this is distinct from an in-place upgrade.

If a quiet install cannot read the old installation record while its uninstaller still exists, it fails before changing application files. Open the MSI interactively so native AppSearch can supply the validated record, or uninstall the old copy first; settings are retained. The installer never guesses an uninstaller command from an unverified record.

MSI major upgrades use the same identity and directory, reject downgrades and preserve startup during removal of the older MSI. Normal uninstall removes only a Run command pointing exactly to this copy and has a rollback action to restore that command if uninstall rolls back. Other portable-copy startup registrations and user-created files are retained. Trial MSI installations are not production upgrades; remove the trial separately after closing it.

## Privacy

With automatic checks off and no manual check/download, this feature sends no network requests. Checks/downloads contact GitHub using its normal HTTPS API and release CDN; those services see standard request metadata such as source IP and the generic PaneShift User-Agent. No settings, typed keys, window titles, identifiers or usage telemetry are uploaded. The local cache stores only the last attempt timestamp and notified release tag. Release notes are plain text, with no embedded scripts or HTML execution.

## Acceptance

- Old JSON defaults off; Apply/Cancel/reset/reload retain their established behavior.
- Manual check with the flag off, offline/rate-limited errors, no newer public release, new stable release and missing/invalid digest.
- Confirm no automatic download/install; check cancellation and no duplicate requests/notifications after restart.
- Verify downloaded bytes against the published SHA256SUMS, and rejection of mismatched/truncated files or untrusted redirects.
- Installed update: normal shutdown, unsaved Settings handling, native installer cancellation/failure, successful version verification/relaunch.
- Portable download: correct ZIP and explicit folder switching; startup paths are not silently moved.
- Default Inno migration with startup on/off and a foreign portable startup entry; settings and one final MSI uninstall entry.
- MSI repair, upgrade, downgrade refusal, running-app refusal and uninstall with matching/foreign startup values; settings and unowned files preserved.
- Fresh standard-user laptop/VM, no UAC or .NET 10 prompt, themes/DPI and all existing shortcut regressions.
