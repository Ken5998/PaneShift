# PaneShift

<p align="center">
  <img src="assets/paneshift.png" alt="PaneShift logo" width="160" />
</p>

A keyboard-first Windows window manager inspired by Rectangle's positioning workflow on macOS. An independent MIT-licensed implementation; no Rectangle branding, code, or assets are reused.

## Build and run

Requires Windows 11 and the .NET 10 SDK with Windows desktop targeting support.

```powershell
dotnet restore PaneShift.sln
dotnet build PaneShift.sln -c Release
dotnet test PaneShift.sln -c Release --no-build
dotnet run --project src/PaneShift.App
```

The app starts in the notification area (possibly in its overflow menu), without a main window. Right-click the icon for **Shortcuts and status**, **Open Settings File**, **Reload Settings**, **Pause shortcuts**, **Restart as administrator...** (when standard), or **Exit**. Pause releases registrations and resets command repetition; resume retries registrations. A second instance exits with a message. Exit unregisters hotkeys and removes the icon. Exit an older running copy before rebuilding or starting a new application version.

## Default shortcuts

The following shortcuts use **Ctrl+Alt**.

| Key | Action |
| --- | --- |
| Left / Right | Left / Right Half |
| Up / Down | Top / Bottom Half |
| U / I | Top Left / Top Right |
| J / K | Bottom Left / Bottom Right |
| D / F / G | First / Center / Last Third |
| E / R / T | First / Center / Last Two Thirds |
| Enter | Maximize |
| C | Center, preserving size and clamping to the work area |

Two additional shortcuts use **Ctrl+Shift+Win**:

| Key | Action |
| --- | --- |
| Up | Top Right Sixth |
| Down | Bottom Right Sixth |

The four left/center sixth actions remain available through action identifiers and the code-supplied hotkey configuration, without default bindings. There are 18 default shortcuts in total.

Repeated half commands on the same target window cycle **1/2 → 2/3 → 1/3 → 1/2**, anchored to the requested left, right, top, or bottom edge. Each press must be released before repeating (`MOD_NOREPEAT`). There is no timing threshold. A different action or target HWND, Restore, an invalid target, a failed command, or pause resets the sequence. Target changes and validity are checked only when a command arrives; switching away and back without invoking a PaneShift command is not tracked.

Registration conflicts appear in a tray notification and the status dialog. Successful registrations continue to work. Other applications or graphics drivers may reserve these combinations.

## Settings and gaps

Settings are read at startup and on explicit **Reload Settings** from `%LOCALAPPDATA%\PaneShift\settings.json`. At startup, the file is created with defaults if missing:

```json
{
  "gapPixels": 0,
  "applyGapToScreenEdges": false,
  "repeatedCommands": {
    "halfActions": "cycleSizes"
  }
}
```

To apply changes without restarting:

1. Choose **Open Settings File** from the tray to reveal the JSON in Explorer.
2. Edit `settings.json` in a text editor.
3. Save the file.
4. Choose **Reload Settings** from the tray.

The next positioning command uses the new configuration. Existing windows are not moved by reload. A successful reload resets the half-action cycle to 1/2 and preserves original Restore history, pause state, and hotkey registrations. The success notification and status dialog show active gap, screen-edge, and repetition settings. Reload is available while paused.

There is no file watcher, polling, background timer, or settings window. Hotkey editing remains in `PaneShiftConfiguration`; the JSON currently controls gaps and the repetition strategy only. `cycleSizes` is the only implemented repetition strategy.

Missing properties inherit defaults and unknown properties are ignored. On **runtime reload**, invalid JSON, negative gaps, unsupported repetition values, missing files, and file-access errors keep the current known-good configuration and repetition state completely unchanged. A concise failure notification appears; details remain in **Shortcuts and status** until a successful reload clears them. Reload never creates or rewrites the file. At **startup**, when no active configuration exists yet, load failures still use safe defaults and report a warning. Existing files, including malformed files, are never overwritten automatically.

`gapPixels` is a nonnegative count of **physical pixels**, applied to the visible window frame after the ideal tile is calculated. Zero preserves the original ideal geometry. An internal leading edge receives `floor(gap/2)` inset and an internal trailing edge receives `ceil(gap/2)`: adjacent windows have exactly the configured gap, including odd values. There is no cumulative inset across commands. With `applyGapToScreenEdges: false`, outer edges stay flush with the work area; with `true`, outer edges receive the full gap. Taskbar space stays excluded.

The generic transformation applies to all tiled layouts and repeated half sizes. Maximize, Center, and Restore retain their existing behavior without gap transformation. If a gap would eliminate a tile's width or height, the command is rejected before changing the window, with a tray notification; reduce the gap for that work area. Target applications may still enforce minimum sizes that override the requested geometry.

## Icon asset

PaneShift includes its finalized original artwork: `assets/paneshift.ico` is the authoritative Windows icon, and `assets/paneshift.png` is the high-resolution branding image used above. The ICO supplies the executable's native icon and is embedded as `PaneShift.Icon` for the central `ApplicationIcon` loader. Both built and published applications use embedded resources, with no dependency on a deployed source `assets` directory. The loader supplies the tray and a cached icon source for future WPF windows; a future installer should reference the same ICO. The defensive runtime fallback remains available if loading fails. See [assets/README.md](assets/README.md).

## Elevated windows and administrator restart

PaneShift runs as a standard-user application by default. Its manifest remains `asInvoker` with `uiAccess="false"`. Windows integrity levels/UIPI can prevent a standard process from controlling elevated windows such as Task Manager. Native error **5 / ERROR_ACCESS_DENIED** produces a friendly access-denied notification. It says the target *may* be elevated because that error alone does not prove the target's privilege level. Other native failures use a generic notification. **Shortcuts and status** retains the last failed action, native error code and diagnostic message, without a stack trace in notifications. Failed commands reset the half-action cycle.

The status dialog shows **Privilege level: Standard** or **Administrator**, queried from the current process token using `OpenProcessToken` and `GetTokenInformation(TokenElevation)`. If that query fails, status shows **Unknown** and its diagnostic; administrator restart is not offered based on a guess. No target process token or memory is inspected.

To control elevated windows, explicitly choose **Restart as administrator...** and accept the Windows UAC prompt. PaneShift uses `ProcessStartInfo` with `UseShellExecute = true` and `Verb = "runas"`; errors never trigger automatic elevation. Cancelling UAC keeps the existing app, registrations, pause state, settings, repetition and Restore history intact. Other launch failures also leave it running and expose details in status.

The new process receives a private handoff argument containing the old PID and its UTC creation timestamp, the exact settings file path, and pause state. Before acquiring the single-instance mutex or registering hotkeys, it waits on the old process's exit with a bounded 30-second OS wait, without polling or a resident helper. The timestamp guards against PID reuse. The old process shuts down only after Shell launch succeeds, releasing hotkeys, tray resources and mutex through its normal exit lifecycle. If it does not exit within the bound, the new process reports the problem and exits without registering hotkeys. Mutex ownership is acquired with a single nonblocking attempt, including recovery from an abandoned mutex; the mere existence of a named mutex is not treated as a running instance. A separately launched third instance is still subject to this single-instance check.

The handoff continues using the original `%LOCALAPPDATA%\PaneShift\settings.json` path, even if the UAC credentials identify another account. Reload and Open Settings File keep using that path for the new session. A real process restart reloads settings from disk and starts a new in-memory Restore history and repetition sequence; unlike Reload Settings, it cannot retain the old process's in-memory history. Pause state is carried over.

When elevated, the tray shows **Running as administrator** and still provides **Exit**. **Restart normally** is intentionally deferred: a child of an elevated process may inherit elevation. Reliably returning to the original desktop user's context requires Explorer-mediated COM launch or additional token handling, which is outside this milestone. Choose **Exit**, then launch PaneShift from a standard Explorer session to run normally again. Launching a standard second copy while the elevated instance owns the mutex shows an explanatory message.

No UAC, Windows security policy, message filter, keyboard hook, DLL injection or process-memory changes are used. Elevation does not guarantee access to every protected window.

Windows references: [elevated/unelevated launch and the Explorer approach](https://devblogs.microsoft.com/oldnewthing/20131118-00/?p=2643), [TOKEN_ELEVATION](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-token_elevation), [ShellExecuteEx errors](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shellexecuteexw).

## Architecture

- `PaneShift.Core` (`net10.0`): physical-pixel rectangles, pure geometry and gap transform, separate command repetition state and half-size strategy, action identifiers, hotkey/settings models, portable JSON file persistence, original window geometry history. `RuntimeSettings` owns the immutable active snapshot and the repetition state it resets when committing a valid candidate. No platform API calls.
- `PaneShift.Windows` (`net10.0-windows`): Win32/DWM P/Invoke, foreground-window and monitor work-area retrieval, window placement, visible-frame compensation, global hotkey lifecycle, command-failure translation, current-process elevation detection, and explicit restart handoff.
- `PaneShift.App` (`net10.0-windows`): WPF lifecycle, message-only HWND, settings path/loading and Explorer integration, centralized icon loading. Windows Forms supplies only the notification icon and its menu. No external window-management dependencies.
- `PaneShift.Core.Tests`: xUnit geometry, partition coverage, repetition/reset, gap adjacency, history, settings persistence/validation, runtime reload and shortcut tests.
- `PaneShift.Windows.Tests`: deterministic tests for native-error classification, failure/reset behavior, restart arguments, launch configuration and UAC-cancellation mapping; no elevated target is required.

`SettingsStore.LoadCandidate()` reads/deserializes/validates without changing live state. `RuntimeSettings.ReplaceCurrent()` commits a validated snapshot and resets repetition on the same UI thread that handles commands. `WindowService` reads that snapshot once per action and remains alive across reloads, preserving its native placement history. Candidate loading and commit remain separate so future hotkey preparation can be inserted before commit; persisted hotkeys are not implemented here.

Layouts use rational boundaries relative to the active window's monitor **work area**, preserving taskbar space. Each integer boundary is `size * numerator / denominator`, with 64-bit intermediate arithmetic. Adjacent regions share boundaries, avoiding gaps at odd widths. Center Two Thirds spans 1/6 to 5/6. Rounding differences are at most one pixel. Degenerate zero-sized tiles are rejected.

The executable declares Per-Monitor V2 DPI awareness. All native geometry uses physical desktop pixels, including negative origins; WPF device-independent units never enter the geometry engine. DWM visible frame bounds compensate for invisible resize borders, falling back to the outer rectangle when unavailable. The manifest establishes DPI awareness before WPF startup. The Forms-only analyzer advice WFO0003 is explicitly suppressed with an explanation in the project because Forms does not own application startup.

Before the first modification, the service saves original geometry and native placement per HWND. Subsequent tiles preserve that original state. `Restore` is implemented in the service using the original placement, including maximized state, but has no default shortcut. History lives only for the current session. Closed windows and handles reused by another process/thread are pruned on the next action; reuse within the same process/thread remains a limitation until lifecycle tracking is added.

## Milestone scope

Implemented: halves with repeated size cycling, corners, thirds, two thirds, six sixths, generic pixel gaps, persisted JSON settings with explicit runtime reload, Maximize, Center, tray operation, 18 default global shortcuts, and finalized project artwork. Repetition state accepts a sequence length independently of geometry; the half-size strategy supplies the current sequence. Other strategies can be added separately without changing the state tracker.

Almost Maximize, Maximize Height, Make Smaller/Larger, and Next/Previous Display have reserved action identifiers. Unsupported actions fail explicitly before window modification. Display transitions, lifecycle-aware history, configurable sequences, persisted hotkeys, a settings UI, and installer are deferred.

PaneShift uses `RegisterHotKey` with `MOD_NOREPEAT`. It does not inject DLLs, access process memory, install keyboard hooks, or automatically request elevation. This cannot guarantee compatibility with every game or anti-cheat system; use Pause when needed. Applications may enforce minimum sizes or reject placement, and elevated applications may be inaccessible to the default standard instance. Native failures produce tray notifications. A successful positioning call does not guarantee that a target application accepted the exact size.

API references: [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey), [DWM window attributes](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute).

## Manual acceptance checks

1. Start PaneShift, focus a resizable window, and exercise each default shortcut.
2. Check neighboring thirds on an odd-width/ultrawide work area and taskbar clearance.
3. Repeat at 100%, 125%, and 150% scaling, including displays left of and above primary and initially maximized windows.
4. Reserve a shortcut in another application and verify the conflict appears while other shortcuts still work.
5. Pause/resume, launch a second instance, and Exit; verify hotkey release and icon cleanup.
6. Focus Chrome on an ultrawide and press/release **Ctrl+Alt+Left** four times. Verify 1/2 → 2/3 → 1/3 → 1/2 with a fixed left edge. Repeat for Right, Top, and Bottom, checking the corresponding anchor. Invoke a different action, and then return to a half action: it starts at 1/2. Target another window between repeats and verify the same reset. Pause/resume also starts over.
7. Set `gapPixels` to 12 and `applyGapToScreenEdges` to false; save and choose **Reload Settings**. Place **First Two Thirds | Last Third** on two windows: verify exactly 12 physical pixels between visible frames and flush outer edges. Enable screen-edge gaps and reload: verify 12px against each touched work-area edge. Repeat with odd gap 11 and with 0 for the original gapless layout.
8. Stack **Top Right Sixth** and **Bottom Right Sixth** using **Ctrl+Shift+Win+Up/Down** on two windows. Verify correct placement and vertical gap. Test all four half cycles with gaps as well.
9. Repeat these checks at 100%, 125%, and 150% Windows scaling where available, with negative-origin monitors and the taskbar on different edges.
10. With gap 12 active, reload malformed JSON and then gap -1: verify a failure balloon, details in status, and that subsequent commands still use 12. Verify the JSON is not overwritten. Fix and reload; confirm the error clears. A partial valid file should use defaults for missing properties. Confirm **Open Settings File** reveals the correct file.
11. Check the PaneShift artwork on both the tray and built/published executable. Publish with `dotnet publish src/PaneShift.App -c Release --self-contained false`, then run from its `bin/Release/net10.0-windows/publish` directory without copying source assets. Windows may cache Explorer icons. Confirm Maximize, Center, and service-level Restore still behave as before (Restore has no default binding).
12. Progress a half action to 2/3, successfully reload, then invoke it again: it must start at 1/2. Reload must not move windows itself or lose original Restore history. Reload while paused and verify it stays paused; resume and check the new gaps. Normal startup should not produce a reload-success notification.

Automated tests validate geometry without controlling other desktop applications. Mixed-DPI, fullscreen/game compatibility, and real multi-monitor behavior require manual device testing.

## Privilege and restart acceptance checks

1. Run PaneShift normally; check **Privilege level: Standard** in status.
2. Open Task Manager elevated.
3. Invoke a PaneShift positioning shortcut on Task Manager.
4. Verify a friendly Windows-access-denied notification, with error 5 and the failed action in status.
5. Verify normal windows still respond and a failed command restarts the next half sequence at 1/2.
6. Choose **Restart as administrator...**.
7. Accept UAC manually.
8. Verify exactly one PaneShift process/tray icon remains and status says **Administrator**, with no old-instance hotkey conflicts.
9. Verify shortcuts now work on Task Manager where Windows permits them.
10. Verify normal windows, gaps, sixth shortcuts, settings reload and pause/resume still work.
11. Exit the elevated instance, launch normally, then request administrator restart and **cancel UAC**. Verify the same instance remains, shortcuts still work, and settings/Restore history are retained.
12. Restart while paused and verify the new instance is paused. Confirm Open Settings File reveals the original JSON path. Where applicable, repeat with UAC credentials for a different administrator account.
13. While elevated, launch another standard copy: verify a friendly single-instance message, no crash and no extra icon. Exit elevated PaneShift and start from standard Explorer to return to Standard.

UAC acceptance/cancellation, integrity boundaries and the live single-instance handoff require manual Windows testing. The automated tests do not simulate Windows security with sleeps or timing assumptions.

## Validation of the settings reload milestone

The Release build and framework-dependent publish completed without warnings or errors, and all 510 automated tests passed. The embedded ICO matches the approved asset; native icons extracted from both built and published executables were verified against it.

Manual Windows testing confirmed that the PaneShift tray icon is displayed, Reload Settings applies changes successfully, and malformed JSON preserves the working active configuration. These checks do not replace the broader multi-monitor and DPI acceptance checks above.

## Validation of the privilege and restart milestone

The Release build completed without warnings or errors, and all 529 automated tests passed (510 Core tests and 19 Windows tests). The current-process elevation query was also exercised successfully in a standard-user process.

Manual Windows testing of the privilege and restart milestone was confirmed successful by the maintainer. The acceptance checklist above remains available for regression testing; automated error-mapping tests do not substitute for real UAC and integrity-boundary checks.
