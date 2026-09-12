# PaneShift

A keyboard-first Windows window manager inspired by Rectangle's positioning workflow on macOS. An independent MIT-licensed implementation; no Rectangle branding, code, or assets are reused.

## Build and run

Requires Windows 11 and the .NET 10 SDK with Windows desktop targeting support.

```powershell
dotnet restore PaneShift.sln
dotnet build PaneShift.sln -c Release
dotnet test PaneShift.sln -c Release --no-build
dotnet run --project src/PaneShift.App
```

The app starts in the notification area (possibly in its overflow menu), without a main window. Right-click the icon for **Shortcuts and status**, **Pause shortcuts**, or **Exit**. Pause releases registrations; resume retries them. A second instance exits with a message. Exit unregisters hotkeys and removes the icon.

## Default shortcuts

All shortcuts use **Ctrl+Alt**.

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

Registration conflicts appear in a tray notification and the status dialog. Successful registrations continue to work. Other applications or graphics drivers may reserve these combinations.

## Architecture

- `PaneShift.Core` (`net10.0`): physical-pixel rectangles, pure geometry, action identifiers, configuration and hotkey models, original window geometry history. No platform API calls.
- `PaneShift.Windows` (`net10.0-windows`): Win32/DWM P/Invoke, foreground-window and monitor work-area retrieval, window placement, visible-frame compensation, and global hotkey lifecycle.
- `PaneShift.App` (`net10.0-windows`): WPF lifecycle and message-only HWND. Windows Forms supplies only the notification icon and its menu. No external window-management dependencies.
- `PaneShift.Core.Tests`: xUnit geometry, partition coverage, history, and configuration tests.

Layouts use rational boundaries relative to the active window's monitor **work area**, preserving taskbar space. Each integer boundary is `size * numerator / denominator`, with 64-bit intermediate arithmetic. Adjacent regions share boundaries, avoiding gaps at odd widths. Center Two Thirds spans 1/6 to 5/6. Rounding differences are at most one pixel. Degenerate zero-sized tiles are rejected.

The executable declares Per-Monitor V2 DPI awareness. All native geometry uses physical desktop pixels, including negative origins; WPF device-independent units never enter the geometry engine. DWM visible frame bounds compensate for invisible resize borders, falling back to the outer rectangle when unavailable. The manifest establishes DPI awareness before WPF startup. The Forms-only analyzer advice WFO0003 is explicitly suppressed with an explanation in the project because Forms does not own application startup.

Before the first modification, the service saves original geometry and native placement per HWND. Subsequent tiles preserve that original state. `Restore` is implemented in the service using the original placement, including maximized state, but has no default shortcut. History lives only for the current session. Closed windows and handles reused by another process/thread are pruned on the next action; reuse within the same process/thread remains a limitation until lifecycle tracking is added.

## Milestone scope

Implemented: halves, corners, thirds, two thirds, six sixths, Maximize, Center, tray operation, and default global shortcuts. Sixths can be mapped through `PaneShiftConfiguration`; no default bindings were specified. Configuration is currently supplied in code, independently of action execution. A settings UI and persisted configuration are future work.

Almost Maximize, Maximize Height, Make Smaller/Larger, and Next/Previous Display have reserved action identifiers. Unsupported actions fail explicitly before window modification. Display transitions, lifecycle-aware history, and a custom tray icon are deferred.

PaneShift uses `RegisterHotKey` with `MOD_NOREPEAT`. It does not inject DLLs, access process memory, install keyboard hooks, or request elevation. This cannot guarantee compatibility with every game or anti-cheat system; use Pause when needed. Applications may enforce minimum sizes or reject placement, and elevated applications may be inaccessible. Native failures produce tray notifications. A successful positioning call does not guarantee that a target application accepted the exact size.

API references: [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey), [DWM window attributes](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute).

## Manual acceptance checks

1. Start PaneShift, focus a resizable window, and exercise each default shortcut.
2. Check neighboring thirds on an odd-width/ultrawide work area and taskbar clearance.
3. Repeat at 100%, 125%, and 150% scaling, including displays left of and above primary and initially maximized windows.
4. Reserve a shortcut in another application and verify the conflict appears while other shortcuts still work.
5. Pause/resume, launch a second instance, and Exit; verify hotkey release and icon cleanup.

Automated tests validate geometry without controlling other desktop applications. Mixed-DPI, fullscreen/game compatibility, and real multi-monitor behavior require manual device testing.
