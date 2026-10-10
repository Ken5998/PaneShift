# MSI packaging trial

This is a local packaging experiment, not a replacement release or an accepted Inno-to-MSI migration. The application, ZIP, published releases and v0.2.0 draft assets are unchanged.

## Build

```powershell
./scripts/Build-MsiTrial.ps1
```

The default input is the downloaded official v0.2.0 ZIP under `artifacts/github-v0.2.0`, with the verified SHA-256 `c79397d3215b9fd31a52243acb6b0da1db1584739d93b50e917487774ee2f306`. The script rejects a hash mismatch and unsafe archive paths. It does not recompile or change application files.

WiX 6.0.2 and its matching UI extension are pinned and installed from NuGet into ignored `artifacts/tools/wix`. [WiX terms](https://docs.firegiant.com/wix/osmf/) refer to the [OSMF policy](https://opensourcemaintenancefee.org/consumers/faq/): revenue-generating individuals/organizations meeting the threshold pay; an open-source project without sufficient revenue does not. The maintainer described PaneShift as an individual, noncommercial project.

Outputs under `artifacts/msi-trial/0.2.0`:

- `PaneShift-0.2.0-MSI-Trial-x64.msi`: unsigned, embedded cabinet, no prerequisite downloader.
- `SHA256SUMS.txt`: hash of this trial MSI, separate from release checksums.
- `provenance.json`: input ZIP hash, tool version and file count.
- `build/`: generated authoring, original payload and intermediate files.

## Trial behavior

The product is intentionally named **PaneShift (MSI trial)** with its own upgrade identity. It installs per-user into `%LOCALAPPDATA%\Programs\PaneShift-MSI-Trial`, with a separately named Start Menu shortcut. The wizard uses Windows Installer's UI; there is no EXE bootstrapper. Installation does not launch the app, add startup entries, or write settings. Normal Windows Installer repair/uninstall applies only to trial-owned files and registry component markers. Empty payload directories are removed; user-added files are not recursively deleted.

The application still uses the shared `%LOCALAPPDATA%\PaneShift\settings.json` and single-instance mutex. Close the existing copy before launching the trial. Editing Settings in either copy changes the same preferences.

Do **not** override `INSTALLFOLDER` to the existing Inno installation or a directory used by another copy. These installers have different ownership records.

For this packaging trial, leave Start at login pointing at the original installation. If testing startup from the trial copy, disable it in that copy before uninstalling it and restore the original copy's setting afterward. Conditional removal of the app-managed Run value is not implemented in the prototype yet. Existing foreign-copy Run values must remain unchanged during trial install/uninstall.

## Before adopting MSI for public releases

- Implement and test an explicit Inno migration strategy. MSI major upgrades only recognize related MSI products; they do not automatically uninstall Inno.
- Preserve settings and the user's existing startup choice through migration; ensure there is one final uninstall entry and no obsolete uninstaller/file ownership.
- Add conditional startup cleanup for this installation, including foreign portable-copy preservation and MSI upgrade behavior.
- Verify running-app handling and graceful close behavior; this prototype disables Restart Manager automatic closure.
- Test fresh standard-user installation without UAC, repair, reinstall, MSI upgrades/downgrades, uninstall, a no-.NET VM and the complete application acceptance matrix.
- Review the exact MSI hash with antivirus vendors. A different format or zero detections is not a safety guarantee or a determination that the original installer was a false positive.
- Production migration/startup and release integration are being implemented separately for 0.3.0; see [Updates](UPDATES.md). Public adoption still requires production acceptance. Do not replace the existing v0.2.0 tag/assets silently.

## Validation results

Local validation on 2026-10-10, using the exact official v0.2.0 ZIP payload:

- WiX 6.0.2 built the x64 MSI with normal Windows Installer validation enabled, without validation suppressions. The MSI contains 478 application/runtime files and is unsigned.
- Trial MSI SHA-256: `d6a24ae17eecbaa6c67ba1c201a5539289c078b2872fb1db2e68c2bbb98ed9df` (58,258,004 bytes). Rebuilding generates a new package identity; recheck the resulting hash before comparing scans.
- Actual silent install, repair after removal of a trial-owned file, and uninstall succeeded. All 478 installed files matched the input payload hashes. The Start Menu shortcut targeted the trial executable and Windows Installer reported per-user assignment (`AssignmentType=0`).
- Uninstall preserved an unowned test file. The original installed executable, existing settings JSON and foreign-copy startup registration remained byte-for-byte unchanged. The trial was uninstalled after the test; the original installation remains in place.
- Microsoft Defender completed a custom scan of this MSI with no threats reported (exit 0), using security intelligence `1.459.649.0`, engine `1.1.26080.3`, product `4.18.26080.4`. This is a local scan result, not a VirusTotal result.
- `Test-ReleaseScripts.ps1` passed syntax, version, cleanup-boundary and VirusTotal multipart checks. The trial build script also rejected a prerelease MSI version and a mismatched input ZIP hash without changing the MSI. The application was not rebuilt and its full test suite was not rerun for this packaging-only experiment.

Ignored evidence is under `artifacts/msi-trial/0.2.0`: `provenance.json`, `install-validation.json`, install/repair/uninstall logs, `defender.log` and `SHA256SUMS.txt`.

A fresh standard-user account, startup cleanup, Inno migration and MSI upgrade behavior still require acceptance. A successful silent install does not imply those checks have passed. Interactive laptop acceptance is recorded below.

## Maintainer laptop acceptance — 2026-10-10

The laptop already had PaneShift v0.1.1 installed through the public PowerShell installation command. The maintainer confirmed the isolated MSI trial worked successfully:

- The interactive installer did not request UAC and no installation/security block was observed.
- The trial application launched and retained the existing settings.
- Mac-style shortcuts worked when enabled; disabling the feature restored normal shortcut behavior.
- Returning to the original v0.1.1 installation restored normal operation with the existing configuration.

This confirms the side-by-side packaging experiment on that device, not an executed in-place Inno-to-MSI upgrade. Startup from the trial copy, cleanup of its startup entry, a separate fresh standard-user account and a freshly inventoried no-.NET environment were not individually confirmed. The existing local install/repair/uninstall evidence above remains separate from this manual report.

## Public antivirus review — 2026-10-10

The maintainer authorized public submission of the exact MSI above to VirusTotal and acceptance of the service terms. [The completed report](https://www.virustotal.com/gui/file/d6a24ae17eecbaa6c67ba1c201a5539289c078b2872fb1db2e68c2bbb98ed9df) showed **0/59 detections**, with 59 engines reporting `Undetected`, two engines timing out (Rising and Trellix ENS), and 14 engines reporting `Unable to process file type`. Microsoft reported `Undetected`.

**DeepInstinct reported `Unable to process file type`, not `Undetected`.** This trial therefore does not show that DeepInstinct has cleared PaneShift or resolved its earlier Inno installer detection. The result supports further MSI acceptance testing but does not establish that Inno was the cause. The format and installer metadata differ, and unsupported engines did not produce a clean verdict.

The analysis ID was `YzM2ZTQ4ZjAwMTgyMDZjMDI4YzY5ZDNlZTllMzdjMjg6MTc5MTYzNDMzMQ==`; the report's last analysis date was 2026-10-10 14:12:11 Europe/Zurich. The local `virustotal-result.txt` and `virustotal-result.jpg` record the completed report. The v0.2.0 release remains a draft; this experiment does not authorize publication or replacement of its assets.
