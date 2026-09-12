# PaneShift icon

The approved original artwork is included unchanged:

- `paneshift.ico`: authoritative Windows icon for the executable, tray, future WPF windows and future installer.
- `paneshift.png`: high-resolution project branding, used in the README and available for future UI use. It is not used as a tray icon.

Preserve these approved assets; do not regenerate or destructively resize them.

`PaneShift.App.csproj` uses the ICO for the executable icon and embeds the same file as `PaneShift.Icon`. `ApplicationIcon` centralizes runtime loading, owns/disposes the tray icon, and exposes a cached `WindowIcon` for future WPF settings windows. Built and published output does not require a source assets directory next to it. Runtime loading failures use the defensive fallback and are reported in tray status.

A future installer should reference this same source asset. There is no installer project in this milestone. Rebuild/restart to apply artwork changes; Windows may cache executable icons.
