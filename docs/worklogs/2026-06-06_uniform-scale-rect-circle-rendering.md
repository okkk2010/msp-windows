# Task Summary

Updated Windows overlay rendering so rectangle and circle elements keep their intended aspect ratio across window sizes.

# Scope

- Changed rectangle rendering to use contain-style uniform scale plus centered offset.
- Changed circle rendering to use the same uniform scale and offset.
- Scaled rectangle stroke width, corner radius, and circle stroke width with the uniform scale.
- Left line rendering unchanged because line elements are expected to be removed from the product direction.

# Changed Files

- `msp-windows/Renderer.cs`
- `docs/worklogs/_index.md`
- `docs/worklogs/2026-06-06_uniform-scale-rect-circle-rendering.md`

# Verification Result

- `dotnet build msp-windows/msp-windows.csproj` failed because the .NET SDK build path does not support the project's ClickOnce PFX signing.
- `dotnet build msp-windows/msp-windows.csproj /p:SignManifests=false /p:GenerateManifests=false` passed.

# Decisions Made

- Used `scale = min(targetWidth / baseWidth, targetHeight / baseHeight)` for rectangle and circle elements.
- Used centered offsets for leftover horizontal or vertical space.
- Preserved line behavior for backward compatibility only.

# Issues

- Normal Windows build still requires the existing signing setup or Visual Studio/MSBuild environment that supports the PFX manifest signing flow.

# Next Steps

- Verify visually in fullscreen and windowed overlay modes.
- Remove line rendering and schema support later if the product fully drops line elements.
