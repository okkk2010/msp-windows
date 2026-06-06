# Anchor Space Rect Circle Rendering

## Task summary
Render rectangle and circle overlays using anchor metadata so edge-aligned objects stay attached when the display aspect ratio differs from the design canvas.

## Scope
- Windows renderer, parser, and rectangle/circle models.
- Lines remain excluded from this behavior.

## Changed files
- `msp-windows/Overlay/Models/RectElement.cs`
- `msp-windows/Overlay/Models/CircleElement.cs`
- `msp-windows/Overlay/OverlayJsonParser.cs`
- `msp-windows/Renderer.cs`

## Verification result
- `dotnet build msp-windows\msp-windows.csproj /p:SignManifests=false /p:GenerateManifests=false` was blocked because the default executable was locked by a running app process.
- `dotnet build msp-windows\msp-windows.csproj /p:SignManifests=false /p:GenerateManifests=false /p:OutputPath=bin\CodexBuild\` passed.

## Decisions made
- Default anchor is `top-left`.
- Default anchor space is `safeFrame`.
- `screen` anchor space uses the full target bounds; `safeFrame` uses the contain-scaled canvas area.

## Issues
- Default build output may fail while the Windows app is running.

## Next steps
- Close the running Windows app before producing the normal signed/default build output.
