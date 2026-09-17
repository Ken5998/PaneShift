# Configuration reference

## Start at sign-in

The tray and General share **Start PaneShift when I sign in**. This immediate per-user Windows preference is stored in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (`PaneShift` value), not in settings.json. Apply, Cancel, Restore defaults and explicit JSON reload do not change it. The command is the quoted current executable without arguments. Debug builds and elevated instances cannot edit it. Keep the folder stable and re-enable from the new copy after moving a portable build. Windows Startup apps can override a registered entry. No entry is created automatically during app launch or installation.

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

The next positioning command uses the new configuration. Existing windows are not moved by Apply or Reload. Success resets all repeated-command cycles to their initial layout, preserves original Restore history and pause state, and activates the new shortcut map. While paused, Apply/Reload briefly probes the complete candidate map, then releases registrations and stays paused; another application can still claim a chord before resume.

The Settings window is the primary editor; the explicit JSON workflow remains supported. There is no file watcher, polling or background timer. `cycleSizes` is the only supported value for `halfActions`. Third, two-thirds and sixth actions always cycle horizontally within their size and row; no additional setting is needed. A successful external reload refreshes an open clean draft. If it has edits, the window preserves them and blocks Apply until you explicitly choose **Reload into this window** (with confirmation) or discard and reopen.

An optional `hotkeys` object maps stable, case-sensitive action IDs to chords. Missing/null sections use existing defaults; missing entries inherit that action's default, while a JSON `null` entry explicitly disables it. For example, add this property to bind Restore and disable Center:

```json
"hotkeys": {
  "restore": "Ctrl+Alt+Backspace",
  "center": null
}
```

Modifier spelling is case-insensitive and normalized to `Ctrl+Alt+Shift+Win+Key` order. Apply writes the complete implemented action map in deterministic ordinal key order. Unknown/unimplemented action IDs, invalid chords and internal duplicates are rejected. Old JSON without `hotkeys` remains compatible; future actions can inherit defaults when their entries are missing.

Missing properties inherit defaults and unknown top-level properties are ignored. On **runtime reload**, invalid JSON, negative gaps, unsupported repetition values, invalid/conflicting shortcuts, missing files and file-access errors keep the known-good configuration and repetition state unchanged. Details remain in **Shortcuts and status** until a successful reload clears them. Reload never creates or rewrites the file. At **startup**, load failures use safe defaults and report a warning. Existing files are never overwritten automatically; explicit GUI Apply replaces the file after validation and successful hotkey preparation.

`gapPixels` is a nonnegative count of **physical pixels**, applied to the visible window frame after the ideal tile is calculated. Zero preserves the original ideal geometry. An internal leading edge receives `floor(gap/2)` inset and an internal trailing edge receives `ceil(gap/2)`: adjacent windows have exactly the configured gap, including odd values. There is no cumulative inset across commands. With `applyGapToScreenEdges: false`, outer edges stay flush with the work area; with `true`, outer edges receive the full gap. Taskbar space stays excluded.

The generic transformation applies to all tiled layouts and repeated half sizes. Maximize, Center, and Restore retain their existing behavior without gap transformation. If a gap would eliminate a tile's width or height, the command is rejected before changing the window, with a tray notification; reduce the gap for that work area. Target applications may still enforce minimum sizes that override the requested geometry.
