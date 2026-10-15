# Swiss keyboard: Right Alt + G

This feature is in development, not yet a published release. In Settings →
Keyboard, enable **Write @ with Right Alt + G (AltGr)** and Apply. The JSON
setting is `keyboard.rightAltGAtSign`, default false. Win-key Mac-style shortcuts
remain independently configurable. Existing files load without rewriting.

## Why Ctrl+Alt needs selective routing

The default right-third action uses Ctrl+Alt+G. Windows can represent AltGr as
Left Ctrl+Right Alt, while [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
uses either Alt key and either Ctrl key. Ignoring the resulting WM_HOTKEY would
stop window placement but would not return the original text input to the app.

`KeyboardShortcutConfiguration` splits the existing, validated window bindings
only while the new flag is enabled. Ctrl+Alt chords use the existing dedicated
keyboard hook, matching the exact configured chord and excluding Right Alt.
All other bindings keep the transactional RegisterHotKey implementation. This
preserves native AltGr input, including dead keys and combinations such as
AltGr+E that overlap a default window chord. Left Alt with either Ctrl retains
the configured window action. Right Alt+G is the sole additional text mapping.

The hook posts window commands to the existing App message window; it never
performs window placement or WPF work in the keyboard callback. Each posted
command carries a configuration generation so queued commands from a previous
Apply/pause/session cannot invoke a newly configured action. An unassigned menu
mask prevents an intercepted window chord from opening Alt/Win menus.

Activation prepares the hook before saving. Native registrations and runtime
state change through the existing transaction. Hook preparation, registration
or disk failures retain the old active snapshot and file. Disable restores
native registrations; pause/exit removes the hook when no feature needs it.
If hook creation fails at startup, legacy native window registrations are used
and a warning is shown. Hook-only chords do not provide exclusive native
reservations or reliable conflict discovery against other keyboard utilities.

## Unicode input and AltGr state

The at-sign sender uses [KEYEVENTF_UNICODE / VK_PACKET](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput)
with a balanced character down/up pair and menu masking. It does **not** release,
repress, snapshot or restore Ctrl/Alt/Shift/Win for this text mapping. Windows
retains ownership of the AltGr companion Ctrl and its physical release.
Partial-send recovery releases only accepted Unicode/mask key-down events and
never retries text or manufactures modifier transitions. Existing Win-key
translation retains its original Win/Shift release/restore behavior.

An earlier test build released and restored AltGr/Ctrl around Unicode insertion.
The maintainer reported that @ appeared but Ctrl-like behavior remained after
release (Esc opened Start). Treating the AltGr companion Ctrl as a physical key
to restore was unsafe. The corrected sender never emits those modifier events;
tests now enforce that invariant, including every partial-send prefix. The
previous mock's independent modifier states did not expose Windows' coupling.

Injected input is ignored before shortcut matching. Consumed source keys retain
only an outstanding-key bit until release, preventing auto-repeat and loops.
There is no typed-text buffer, keyboard log, clipboard change, network traffic,
layout installation, DLL injection, service or background polling. SendInput
retains the normal UIPI/elevation limitations of the Win-key translation feature.

## Validation, 2026-10-16

- Debug and Release: all **693** tests pass (579 Core, 114 Windows); zero skipped.
- Tests cover old/malformed settings, independent flags, GUI draft/reset/reload,
  native/hook routing, exact left/right Alt behavior, source repeats/releases,
  injected events, unchanged unrelated AltGr keys, Unicode input, partial sends
  without modifier transitions, and activation rollback after failures.
- After replacing the faulty build, the maintainer confirmed the requested physical-key checks: **works, no key remains active**. This is user-reported
  acceptance on Italian (Switzerland) with a Swiss French physical keyboard.
- Comparing the local pre-test backup with the accepted configuration confirms
  that only the new keyboard option changed; all other JSON settings and the
  exact startup Run value remained unchanged. The local settings backup is
  retained under ignored `artifacts/altgr-preview/`.
- Visual automation could not attach to the development Settings window:
  the helper associated its window with the installed executable. No screenshot
  or automated theme/DPI acceptance is claimed. Broader app/game/elevation tests
  remain in the manual regression checklist.
