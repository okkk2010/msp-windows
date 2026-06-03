# Task Summary

Expanded the Windows control window for login and visual library selection.

# Scope

Updated the remote control form so the window is no longer designed as a tiny fixed utility and added Google login, token restoration, library refresh, and preview-card overlay selection.

# Changed Files

- `msp-windows/RemoteControlForm.cs`
- `msp-windows/Settings/AppSettingsService.cs`
- `docs/worklogs/_index.md`
- `docs/worklogs/2026-06-02_msp-windows-login-library-preview-ui.md`

# Verification Result

- `dotnet build msp-windows.slnx -p:SignManifests=false` succeeded with 0 warnings and 0 errors.

# Decisions Made

- Replaced the dropdown-based loaded overlay selection with a responsive preview card grid.
- Added Google login through the documented browser + localhost callback flow.
- Stored and restored the access token through `settings.json` using `AppSettingsService`.
- Loaded My Library through `/api/library` and used `thumbnailUrl` for visual selection previews.
- Kept code-based overlay loading and local cache entries available as cards.

# Issues

- Default signed build remains dependent on the existing ClickOnce/PFX signing setup.
- Login and thumbnail loading require the backend OAuth callback and image URLs to be reachable at runtime.
- No live WinForms screenshot verification was performed in this environment.

# Next Steps

- Run the app locally and verify the OAuth redirect URL, library thumbnails, and selection behavior against the production API.
