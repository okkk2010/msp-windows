# Copilot Instructions for msp overlay

## Working Rules
- Before editing, summarize the task scope.
- Do not implement features outside the requested scope.
- Do not invent requirements.
- If requirements are unclear, leave TODO comments instead of making large assumptions.
- After editing, show changed files and verification steps.

## Project Overview

This repository is for msp overlay, a Windows desktop overlay client for reducing motion sickness in 3D games.

The Windows Client is responsible for:

- Loading overlay data by 6-character code
- Google login using localhost callback
- Loading the user's overlay library
- Parsing overlay JSON
- Rendering rect, circle, and line elements
- Managing local settings and cache
- Integrating with the existing Remote Control UI

## Language

- Use Korean for explanations and comments unless existing code uses English.
- Keep implementation practical and aligned with the current MVP scope.

## Development Scope

Do not add features outside the specification unless explicitly requested.

Current MVP includes:

- Code Load
- Google Login
- My Library
- Overlay Apply
- Local Settings / Cache
- Remote Control UI integration

Current MVP excludes:

- Upload from Windows Client
- Library save from Windows Client
- Image element rendering
- Text element rendering
- Complex cache synchronization
- Windows Credential Manager

## Documentation Routing

Before implementing Windows Client features, refer to:

- docs/msp_overlay_windows_client_spec.md

For area-specific implementation, refer to:
- docs/windows-client/01_windows_client_overview.md
- docs/windows-client/02_windows_code_load_spec.md
- docs/windows-client/03_windows_google_login_spec.md
- docs/windows-client/04_windows_library_spec.md
- docs/windows-client/05_windows_overlay_apply_rendering_spec.md
- docs/windows-client/06_windows_local_settings_cache_spec.md
- docs/windows-client/07_windows_remote_control_ui_spec.md
- docs/windows-client/08_windows_api_contract_spec.md
- docs/windows-client/09_windows_internal_structure_spec.md
- docs/windows-client/10_windows_exception_test_completion_spec.md

## Coding Rules

- Keep existing project structure unless a change is necessary.
- Do not rewrite unrelated code.
- Use System.Text.Json for JSON parsing unless the project already uses another JSON library.
- Use async/await for network calls.
- Keep API communication inside MspApiClient.
- Keep authentication logic inside WindowsAuthService.
- Keep settings file logic inside AppSettingsService.
- Keep overlay parsing inside OverlayJsonParser.
- Keep overlay rendering/apply logic inside OverlayApplyService.

## Safety Rules

- Do not hardcode production tokens.
- Do not store Google OAuth client secret in the Windows Client.
- Token storage in settings.json is allowed only for development-stage MVP.
- Prefer localhost callback using 127.0.0.1.

## Response Rules

When modifying code, explain:

1. What files were changed
2. Why they were changed
3. How to test the change
4. Any TODOs or assumptions

## Git Rules
- Work on feature branches.
- Do not commit unless explicitly asked.
- Do not force push.

## Work Log Rules
- Maintain repository-specific worklogs when the repository defines a worklog directory.
- Prefer file-per-task worklogs over one large cumulative log when available.
- Before editing, read `docs/worklogs/_index.md` if it exists.
- If needed, read only the latest 1 to 3 relevant worklog files.
- After editing, create a new worklog file under the repository-defined worklog directory.
- Update `docs/worklogs/_index.md` with the new worklog entry.
- Do not rewrite, delete, or summarize previous worklog files unless explicitly asked.
- Each worklog file should include:
  - Task summary
  - Scope
  - Changed files
  - Verification result
  - Decisions made
  - Issues
  - Next steps
- If no repository-specific worklog path is defined, do not create worklog files automatically. Ask first.

## Repository Worklogs
- Worklog directory: `docs/worklogs/`
- Worklog index: `docs/worklogs/_index.md`
- File naming: `YYYY-MM-DD_task-slug.md`