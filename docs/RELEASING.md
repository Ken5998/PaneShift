# Building and publishing a release

## Version and build tools

`Directory.Build.props` is the single local version source (initially `0.1.0`). A tagged release uses its validated `v<SemVer>` tag to pass `-p:Version=<SemVer>` through build/publish. Assembly/FileVersion use the three numeric components plus `.0`; ProductVersion and Settings show the complete semantic version. Numeric components must fit Windows metadata (0–65534). The manifest's fixed assembly identity is a loader identity, not the product version; its asInvoker, uiAccess and Per-Monitor V2 settings remain unchanged.

Use Windows 11 x64, PowerShell 7, .NET 10 SDK, and Inno Setup 6.7 or newer. GitHub-hosted `windows-2025` runners build CI and releases. Action revisions are pinned; review dependency updates deliberately. The runtime patch comes from the installed .NET SDK, so rebuild releases when runtime security servicing requires it.

No Inno compiler installed? Run:

```powershell
$iscc = ./scripts/Install-InnoSetup.ps1
./scripts/Build-Release.ps1 -IsccPath $iscc
```

The optional helper downloads Inno Setup 6.7.3 from the official GitHub release, verifies its pinned SHA-256 and trusted Pyrsys B.V. Authenticode publisher, then installs the build tool per-user under ignored `artifacts/tools/InnoSetup`. It does not elevate. The hash is taken from the upstream release's asset digest. Follow [upstream verification guidance](https://jrsoftware.org/isdl-verify.php) when updating it. Inno Setup has separate licensing terms; consult those terms for commercial use.

With your own compiler:

```powershell
./scripts/Build-Release.ps1 -IsccPath 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

`Build-Release.ps1` restores, builds, runs all tests, publishes, packages, verifies and hashes. Each native command is checked for failure. Close the running application before rebuilding its output files or testing another copy.

## Publish strategy and outputs

`src/PaneShift.App/Properties/PublishProfiles/Release-win-x64.pubxml` selects Release, win-x64, self-contained, directory deployment, no trimming, no single-file bundling and no ReadyToRun. The apphost retains the established `PaneShift.App.exe` filename because the existing administrator restart uses it.

WPF and native runtime files are shipped beside the executable, including required localization resources. Single-file can work for some WPF deployments, but native bundling/extraction adds another path to verify and offers little benefit when ZIP and installer are already the distribution formats. Trimming is inappropriate for this un-audited WPF/reflection workload. See [.NET single-file guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).

```text
artifacts/
  portable/
    PaneShift-0.1.0-win-x64/          # complete publish directory
    PaneShift-0.1.0-win-x64.zip
  installer/
    PaneShift-0.1.0-Setup-x64.exe
  SHA256SUMS.txt
```

The ZIP puts `PaneShift.App.exe` at its root. It includes the runtime and LICENSE.txt, but no PDB, test or source files. "Portable" means no installation is needed; settings still use `%LOCALAPPDATA%\PaneShift`, shared with the installed app. Do not copy only the EXE or run both variants at once.

Publish cleans only the selected version directory under artifacts, after path/junction checks. Build from a clean checkout for official releases. `Verify-Release.ps1` checks x64, metadata, self-contained dependencies, unwanted files, installer version and each ZIP entry against the publish directory before writing SHA256SUMS.txt. Hashes cover the final installer and ZIP, not source archives or intermediate artifacts.

## Installer behavior

`installer/PaneShift.iss` defines a stable AppId for upgrades and uses `PrivilegesRequired=lowest` with `%LOCALAPPDATA%\Programs\PaneShift` as default. It requires Windows 11 or later (build 22000+) and x64-compatible Windows. The first supported/tested target is Windows 11 x64; ARM64 emulation has not been accepted as a supported target.

- Start Menu shortcut offered and checked; desktop shortcut offered and unchecked.
- Optional launch after installation; silent installation does not launch the app.
- Per-user uninstall entry with PaneShift icon and version.
- Existing `Local\PaneShift` mutex prompts the user to close a running app before install/uninstall. No forced application shutdown.
- Uninstall removes installer-owned application files and shortcuts, and removes the optional startup entry only if its command matches the installed executable. It does **not** delete `%LOCALAPPDATA%\PaneShift\settings.json`, another copy's startup entry, or user-added files via a wildcard cleanup.
- No service, scheduled task, Run entry or Startup-folder shortcut is installed.

**Start at login is opt-in from the tray or General settings.** It writes only the `PaneShift` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, with a quoted executable path and no restart/elevation arguments. The shared toggle takes effect immediately; JSON Apply/Cancel and Restore defaults do not change it. Debug and elevated instances disable editing. The installer never enables startup automatically; an auto-updater remains out of scope.

## CI and release workflow

`ci.yml` runs on pull requests and pushes to main. It restores, builds and tests Release, validates release scripts, and verifies an unsigned production publish. No signing secrets or public releases are involved.

`release.yml` runs on `v*` tags and rejects invalid SemVer before packaging. The read-only package job builds/tests, publishes, optionally signs the app, builds ZIP/installer, optionally signs the installer, and verifies final files/checksums. Only a dependent release job has repository write permission. It verifies downloaded hashes, creates a **draft** with the three public assets, then optionally submits those files to VirusTotal and updates the draft notes. Failed builds/tests/packaging cannot create a release.

The draft remains unpublished for maintainer acceptance; publish it from GitHub after checking all assets and notes. Pre-release SemVer tags are also marked as GitHub prereleases. A rerun fails safely if the release tag already has a release; inspect/delete an incomplete **draft** before retrying. Do not replace assets of a published version—cut a new tag.

No release/tag is created by local build scripts. To prepare the initial remote release after review and commit:

```powershell
git tag -a v0.1.0 -m 'PaneShift v0.1.0'
git push origin v0.1.0
```

Tag permissions should be restricted to maintainers. Configure SignPath and optional VirusTotal secrets only as described in [CODE_SIGNING.md](CODE_SIGNING.md). No IDs or credentials are pre-filled. Signing requires explicit opt-in; an unsigned draft is permitted and labeled accurately.

## Release acceptance checklist

Use a clean Windows 11 x64 VM without .NET installed for the final no-prerequisites check. Preserve a copy/hash of any existing settings before switching builds.

1. Build/test and run `scripts/Test-ReleaseScripts.ps1`; inspect final hashes and metadata.
2. Extract the ZIP into a new empty directory away from the source/build tree. Launch its EXE; verify one tray icon, branded Settings, version 0.1.0 and no SDK/runtime prompt.
3. Check shortcuts, layout gaps and settings persistence across exit/relaunch; also run the [application regression checklist](TESTING.md).
4. Exit the portable copy. Install using a standard account, verify default per-user directory and no elevation prompt. Check Start Menu selected by default, desktop unselected, and optional launch.
5. Verify the installed tray icon, Settings, shortcuts, persistence and standard privilege status.
6. Start at login: confirm installation alone adds no Run entry. Enable it in General; verify the tray checkbox and the quoted Run value match. Disable it from the tray and verify General refreshes. Repeat enabling/disabling, then sign out/in to confirm standard tray-only launch. If Windows Startup apps disables it, re-enable it there. No Startup shortcut, service or task should be added.
7. Request administrator restart and manually accept/cancel UAC; verify single-instance handoff and settings path. Close the elevated process afterward.
8. Attempt installation/uninstall while running; verify the close-application prompt rather than forced shutdown.
9. Exit PaneShift and uninstall. Verify owned files, uninstall entry and selected shortcuts disappear; confirm the settings file still exists and its content/hash is unchanged. A startup entry pointing at the installed copy must be removed, while one pointing at another portable copy must remain.
10. Reinstall and verify preserved settings are loaded. Review upgrade behavior using a later test version in a disposable VM.
11. If signing is enabled, verify app and installer via `signtool verify /pa /all /v`, check SHA-256/RFC 3161 in signature details, and recompute checksums after signing. Do not use an unsigned result as evidence of signing readiness.
12. Review the draft assets and optional VirusTotal links. The workflow does not equate a successful upload or a low detection count with safety. Publish the draft only after acceptance.

Local artifact tests cannot prove the GitHub-hosted workflow, SignPath credentials/approval policy, VirusTotal account quota, SmartScreen reputation or a clean VM without .NET. Record those checks separately rather than fabricating results.

## Local validation — 2026-09-12

- Release build completed with zero warnings/errors; all 570 application tests passed (523 Core, 47 Windows/App support). Fourteen startup cases cover command quoting/validation, idempotence, foreign-copy registrations, permissions, unavailable modes and shared GUI state.
- PowerShell script parsing, 16 SemVer cases and cleanup boundary checks passed. Actionlint 1.7.12 reported no workflow errors.
- Self-contained publication was verified: 478 files including runtime license notices, x64 apphost, expected metadata and preserved asInvoker / uiAccess=false / PerMonitorV2 manifest values. Every ZIP entry matched its published file hash.
- Inno Setup 6.7.3 compiled the updated installer. Per-user installation completed without administrative privileges, with a Start Menu shortcut and no default desktop shortcut.
- The installed app launched using its own coreclr, hostpolicy and WPF native runtime. Its EXE was verified against the published EXE.
- An actual HKCU test enabled, repeated and disabled startup, then restored the original registration. No permanent startup opt-in was introduced by testing.
- The installed General page was visually checked: the startup checkbox enabled and disabled the actual registration immediately, while Apply remained disabled. The original unchecked state was restored.
- The updated uninstaller removed the installed app and its matching startup Run value; the existing settings file's SHA-256 stayed unchanged. The current build was reinstalled afterward and remains available for interactive acceptance.
- Signing was not performed. The no-key VirusTotal path correctly reported no submission. No remote release, tag, signing request or public sample upload was created during local validation.
- Final readiness recheck repeated restore, Release build, all 570 tests, publication, installer compilation and final checksum verification successfully. The General screenshot is now directly visible in the README.
- GitHub CI completed successfully for commit `a01e951`: https://github.com/Ken5998/PaneShift/actions/runs/34692225988. The tag-triggered release workflow has not yet run.
- Offline VirusTotal fixtures exercised missing-key skip, large-file upload, completed analysis with one informational detection, and network-error redaction. These are simulated responses, not scan results. Actual secret validity/quota and public submissions remain unverified until the tagged workflow runs.
- Initial release highlights are maintained in `docs/RELEASE_NOTES_0.1.0.md`; the draft script incorporates them while retaining the actual build's signing status. Offline checks verified draft-only arguments, notes and final-asset checksum validation without creating a GitHub release.
- The freshly extracted ZIP launched successfully from a new directory containing spaces, with an unrelated working directory. Loaded coreclr, hostpolicy and WPF native modules came from that extracted directory. The test process was stopped and the installed copy restarted afterward.

Still required: actual sign-out/sign-in startup, complete installed/portable GUI and shortcut regression, UAC checks, a clean VM without .NET and the first tagged GitHub Actions run. The portable package requires its extracted folder to remain at the registered path.
