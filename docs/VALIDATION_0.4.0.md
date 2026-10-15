# 0.4.0 validation

Release preparation, 2026-10-16. This record distinguishes development acceptance, local packaging and final GitHub Actions artifacts.

## Feature acceptance

The optional Swiss Right Alt+G feature extends the existing configuration transaction and dedicated keyboard hook rather than adding another input subsystem. Default window bindings remain registered natively while the feature is off. Details and the modifier-state fix are recorded in [KEYBOARD_ALTGR.md](KEYBOARD_ALTGR.md).

Debug and Release suites passed all **693 tests** (579 Core, 114 Windows/App), with no failures or skips. Coverage includes backward-compatible settings, independent flags, view-model draft/reset/reload, exact Alt-side routing, unrelated AltGr characters, repeats/injected input, balanced Unicode sends including partial-send recovery, and activation rollback.

The maintainer confirmed the corrected development build with physical keys on Italian (Switzerland) and a Swiss French keyboard: repeated AltGr+G, release then letters/space/Esc/arrows/Ctrl+C/V, G re-pressed while holding AltGr, AltGr+E and Ctrl+Left Alt+G worked without a modifier remaining active. Only the new keyboard flag changed in the saved JSON; other settings and the exact startup command matched their pre-test backup.

Visual automation could not attach to the development Settings window. No new screenshot, light-theme/DPI matrix, clean-machine runtime test, reboot acceptance or full app/game/elevated-foreground matrix is claimed for this version. The [manual checklist](TESTING.md) retains those checks. Word replacement is not part of this release.

## Packaging and publication

Local release packaging and final Actions-built asset verification will be recorded here after completion. Historical release evidence and packages remain unchanged.
