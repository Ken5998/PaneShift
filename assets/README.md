# PaneShift icon

Place the approved original artwork at `assets/paneshift.ico`, then rebuild PaneShift.
No placeholder branded artwork is included. Without this file, the application uses the existing Windows application icon gracefully.

Use a valid multi-resolution Windows ICO (for example 16, 20, 24, 32, 40, 48, 64 and 256 pixel frames) for clear rendering at different DPI settings. Do not copy artwork from Rectangle or other applications.

`PaneShift.App.csproj` conditionally uses this file for the executable icon and embeds the same file as `PaneShift.Icon`. `ApplicationIcon` centralizes runtime loading, owns/disposes the tray icon, and exposes a cached `WindowIcon` for future WPF settings windows. Adding the file requires no source-code changes. Invalid ICO files can fail the executable build and should be corrected; runtime loading failures use the fallback and are reported in tray status.

A future installer should reference this same source asset. There is no installer project in this milestone. Rebuild/restart to apply artwork changes; Windows may cache executable icons.
