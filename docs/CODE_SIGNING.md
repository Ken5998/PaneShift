# Code signing policy

Official PaneShift release artifacts are built from tagged source in [Ken5998/PaneShift](https://github.com/Ken5998/PaneShift). The [release workflow](../.github/workflows/release.yml) builds and tests the source, publishes the Windows runtime with the app, prepares artifacts and creates a GitHub draft release for maintainer review.

## Current status

**PaneShift code signing is not currently enabled.** The initial locally built v0.1.0 packages are unsigned. Do not describe a release as signed unless its application and installer signatures have actually been verified. Windows may display an unknown-publisher or SmartScreen warning for unsigned or low-reputation downloads. A checksum verifies integrity against the listed file; it does not establish publisher identity by itself.

The planned downloadable files are:

- `PaneShift-<version>-Setup-x64.exe`
- `PaneShift-<version>-win-x64.zip`
- `SHA256SUMS.txt`, computed from the final installer and ZIP after signing (if enabled).

The ZIP is a container, not an Authenticode-signed file. When signing is enabled, it contains the signed `PaneShift.App.exe`. Bundled .NET files retain their original upstream signatures, if present. The pipeline does not claim that every DLL or the generated uninstaller is separately signed.

## SignPath Foundation integration

PaneShift intends to apply for free open-source code signing through [SignPath Foundation](https://signpath.org/). Acceptance, organization settings and certificates are not assumed or fabricated.

The tagged release workflow includes two gated uses of [`signpath/github-action-submit-signing-request`](https://docs.signpath.io/trusted-build-systems/github), pinned to a reviewed commit:

1. Publish the self-contained app.
2. Upload the app executable as a GitHub Actions artifact, submit it to SignPath and verify the returned signed executable.
3. Build the portable ZIP and installer from that signed application.
4. Submit the installer and verify the returned signature.
5. Verify the final ZIP matches its publish directory, calculate hashes, upload release assets, then optionally submit those exact files to VirusTotal.

Signing is disabled unless repository variable `SIGNPATH_ENABLED` is exactly `true`. Once enabled, missing configuration or signing/verification failure **fails packaging**, without silently falling back to unsigned artifacts.

Configure only after acceptance and a reviewed test signing run:

| Name | Storage | Purpose |
| --- | --- | --- |
| `SIGNPATH_API_TOKEN` | GitHub secret | Authorized signing-request token |
| `SIGNPATH_ORGANIZATION_ID` | Repository variable | Assigned organization ID |
| `SIGNPATH_PROJECT_SLUG` | Repository variable | Approved project slug |
| `SIGNPATH_SIGNING_POLICY_SLUG` | Repository variable | Approved release policy |
| `SIGNPATH_APP_ARTIFACT_SLUG` | Repository variable | Artifact configuration for the application request |
| `SIGNPATH_INSTALLER_ARTIFACT_SLUG` | Repository variable | Artifact configuration for the installer request |
| `SIGNPATH_ENABLED` | Repository variable | Explicit opt-in (`true`) after configuration |

The application artifact is a GitHub-generated ZIP containing `PaneShift.App.exe`. The installer artifact is another ZIP containing `PaneShift-<version>-Setup-x64.exe`. SignPath configurations must use a ZIP root and select the corresponding PE executable; the returned extracted files must retain those names. Configure **SHA-256 file digests and RFC 3161 timestamping** in the signing policy. Confirm the returned signature/timestamp algorithms in the pilot release before enabling the production policy. The repository contains no assumed IDs, tokens, certificate files or policy values.

Protect release tags and changes to workflows/scripts. Limit signing-token access to release maintainers and configure SignPath build-origin verification and approval rules. Normal PR and main-branch CI never receives these secrets and always builds unsigned app binaries. GitHub Actions permissions default to read-only; only the release-creation job gets `contents: write`.

## Verification and local certificate signing

Release verification requires a trusted, timestamped Authenticode signature when signing is enabled. For an additional Windows SDK check:

```powershell
signtool verify /pa /all /v path\to\PaneShift.App.exe
signtool verify /pa /all /v path\to\PaneShift-0.1.0-Setup-x64.exe
```

If an authorized local certificate is used instead, keep it in an appropriate certificate store/hardware provider and use `signtool sign /sha1 <certificate-thumbprint> /fd SHA256 /tr <RFC3161-timestamp-URL> /td SHA256 <file>`. Supply real provider values yourself. Sign the application **before** `Package-Release.ps1`, sign the installer afterward, then run `Verify-Release.ps1 -RequireSigned`. Never put a password, private key, PFX or token in the repository, command logs or release assets. Local signing is an alternative maintenance procedure, not a required CI dependency.

## VirusTotal

The optional `VT_API_KEY` GitHub secret enables [VirusTotal API v3](https://docs.virustotal.com/reference/files-scan) submissions for the final installer and ZIP, after checksums and draft asset upload. A missing key is a normal skip. The script spaces requests to respect the public API's four-request-per-minute allowance, backs off on HTTP 429, supports the [large-file upload endpoint](https://docs.virustotal.com/reference/files-upload-url), and polls analysis status a bounded number of times. Account quotas and service availability still apply.

**These are public sample submissions.** Only public release packages should be submitted, never private builds or user configuration. Returned analysis IDs and file hashes produce links in the draft release notes. Reports are informational: one engine detection does not automatically fail a release, and no "0 detections" claim is made without a completed API result. Maintainers should review reports before publishing. Submission/status failures are reported without exposing the key and do not alter the artifacts.

## Retrying a release scan

If submission fails, run **Retry release VirusTotal** from GitHub Actions on `main`, supplying the existing version without `v`. It downloads the existing installer, ZIP and checksums (including from a draft), validates each artifact before submission, and replaces only the VirusTotal section of the notes. It does not rebuild, replace assets, move tags or publish the release. `VT_API_KEY` remains a repository secret.

Uploads use explicit multipart headers (unquoted boundary, quoted field and filename) for compatibility with the large-file endpoint. Release-script tests verify those headers and unchanged binary payload bytes. Diagnostics report the failing stage, HTTP status and recognized API error code without printing credentials or raw responses.

Select **report_only** to refresh the analysis IDs already recorded in the release notes without another upload. IDs are accepted only from notes matching each final artifact's filename and SHA-256 report URL.

## Privacy

PaneShift itself does not transmit user information to network services as part of normal window-management functionality. Source inspection found no HTTP client, telemetry, update checker or network transport in the application projects. Settings remain in `%LOCALAPPDATA%\PaneShift\settings.json`; window placement history remains in memory for the session.

The explicit **GitHub repository** button opens the project URL in the user's browser. Browser requests and Windows certificate/reputation checks are outside PaneShift's window-management functionality. Optional VirusTotal submissions occur in the release workflow, not in the installed app. There is no auto-updater.
