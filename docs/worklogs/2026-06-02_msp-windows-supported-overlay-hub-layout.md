# Task Summary

Rebuilt the Windows remote control screen to match the supplied Figma overlay hub concept and current button-role specification.

# Scope

Focused on `RemoteControlForm` UI structure and button behavior mapping. Unsupported runtime controls such as opacity, scale, module toggles, and in-game open/settings panels were not added.

# Changed Files

- `msp-windows/RemoteControlForm.cs`
- `docs/worklogs/_index.md`
- `docs/worklogs/2026-06-02_msp-windows-supported-overlay-hub-layout.md`

# Verification Result

- `dotnet build msp-windows.slnx -p:SignManifests=false -p:OutDir=.codex\build-check\` succeeded with 0 warnings and 0 errors.
- Removed the temporary `.codex\build-check` output after verification.

# Decisions Made

- Reorganized the screen as an overlay launcher: left navigation rail, header/account area, detected game status, active overlay preview, code loading, and compact library selection.
- Kept button roles aligned with current app support:
  - `Google Login`: starts OAuth callback flow and loads user/library.
  - `Logout`: clears token/user/library state.
  - `Load`: loads a 6-character overlay code, caches JSON, and applies it.
  - `Refresh Library`: refreshes saved overlays when signed in.
  - `Start Overlay`: starts overlay for the selected game window.
  - `Stop Overlay`: closes the current overlay form.
  - `Choose`: focuses the library chooser instead of implying unsupported editor behavior.
- Used a large active overlay preview and compact library rows instead of the previous full-card grid.

# Issues

- Live screenshot verification was not performed.
- Default signed build still depends on the existing ClickOnce/PFX setup.

# Next Steps

- Run the app and compare the actual WinForms layout against the Figma frame at desktop size.
