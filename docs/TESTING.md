# Manual regression checklist

## Manual acceptance checks

### Settings GUI acceptance

1. Open Settings from the tray; startup must remain tray-only.
2. Double-click the tray icon again; verify the same window activates (including when minimized).
3. Change gap from 0 to 12; verify the draft preview changes without changing live settings.
4. Apply and immediately tile two windows.
5. Verify 12 physical pixels between them without restarting; test screen-edge gaps too.
6. Record a free combination for Left Half, including an Alt chord.
7. Apply and verify quiet success feedback and persisted JSON.
8. Verify the old shortcut no longer performs Left Half.
9. Verify the new shortcut works immediately.
10. Assign the same chord to two actions; verify Apply is disabled with both names shown.
11. Try a chord owned by another application (via JSON if its owner intercepts recording); verify Apply/Reload failure keeps old working shortcuts and active layout. Failed GUI Apply must leave JSON unchanged.
12. Clear a shortcut, Apply, and verify it becomes unbound.
13. Reset shortcuts to defaults; verify this only changes the draft until Apply, then check both existing sixth defaults.
14. Close Settings and confirm tray operation continues. With dirty edits, test X → Yes/No/Cancel and explicit Cancel. Escape should cancel recording without closing the window.
15. Reopen Settings and confirm persisted values. Reload JSON with a clean draft, then a dirty draft; verify refresh vs. preserved edits and explicit reload confirmation. Test pause/resume and administrator restart with dirty edits.
16. Test 100%, 125% and 150% scaling, minimum window size, scrolling, keyboard-only navigation and focus indicators. Verify Restore and all six sixth actions can be bound.
17. Test light/dark Windows application appearance and high contrast, reopening Settings after each theme change.

### Window management regression

1. Start PaneShift, focus a resizable window, and exercise each default shortcut.
2. Check neighboring thirds on an odd-width/ultrawide work area and taskbar clearance.
3. Repeat at 100%, 125%, and 150% scaling, including displays left of and above primary and initially maximized windows.
4. Reserve a shortcut in another application and verify the conflict appears while other shortcuts still work.
5. Pause/resume, launch a second instance, and Exit; verify hotkey release and icon cleanup.
6. Focus Chrome on an ultrawide and press/release **Ctrl+Alt+Left** four times. Verify 1/2 → 2/3 → 1/3 → 1/2 with a fixed left edge. Repeat for Right, Top, and Bottom, checking the corresponding anchor. Invoke a different action, and then return to a half action: it starts at 1/2. Target another window between repeats and verify the same reset. Pause/resume also starts over.
7. Set `gapPixels` to 12 and `applyGapToScreenEdges` to false; save and choose **Reload Settings**. Place **First Two Thirds | Last Third** on two windows: verify exactly 12 physical pixels between visible frames and flush outer edges. Enable screen-edge gaps and reload: verify 12px against each touched work-area edge. Repeat with odd gap 11 and with 0 for the original gapless layout.
8. Repeat **Ctrl+Alt+D** and verify First → Center → Last → First Third. Repeat **Ctrl+Alt+E** and verify the same sequence at two-thirds width. Repeat **Ctrl+Shift+Win+Up** and verify Top Right → Top Center → Top Left → Top Right; repeat **Ctrl+Shift+Win+Down** for the equivalent Bottom sequence. Verify correct placement and gaps. Test all four half cycles with gaps as well.
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

## PowerShell installer checks

1. Build the site and compare `artifacts/site/win` and `artifacts/site/win.ps1` with `site/win.ps1`.
2. Parse the script with Windows PowerShell 5.1 and PowerShell 7. Run it with `-WhatIf` against the latest public release; verify that the installer and checksums download, SHA-256 succeeds, and PaneShift is neither stopped nor installed.
3. Test a real install and update with PaneShift running: the process must close only after verification, the installed version must match the release, and PaneShift must restart unless `-NoLaunch` was supplied.
4. Run it again at the current version and verify it reports the existing installation without stopping or reinstalling PaneShift. Use `-Force -WhatIf` to exercise the reinstall path without changing the installation.
