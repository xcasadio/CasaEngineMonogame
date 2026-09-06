# CasaEngine MGUI backend

## Final bootstrap

- Runtime overlay and per-view UI now flow through `UIRoot`, `ViewRenderHost`, `CasaRenderSurfaceAdapter`, and `CasaMonoGameBackendBootstrap` before building `MGDesktop`.
- The editor shell now boots MGUI through `CasaGameRenderHost<GameEditor>` and `CasaMonoGameBackendBootstrap` instead of the upstream MonoGame backend bootstrap.
- World-space and offscreen UI use the same `UIRoot` path, with `CasaRenderSurfaceAdapter` exposing the engine render target to MGUI.

## MainRenderer parity matrix

| MainRenderer responsibility | CasaEngine equivalent | State |
| --- | --- | --- |
| Host / raw input / surface | `ViewRenderHost`, `CasaGameRenderHost`, `CasaRenderSurfaceAdapter`, `CasaBackBufferSurface` | adapted |
| GraphicsDevice / SpriteBatch / PrimitiveBatch | `CasaDesktopRuntime` | ported |
| ContentManager / FontManager / AssetProvider / TextEngine | `CasaDesktopRuntime`, `CasaUIAssetProvider`, `FontStashSharpTextEngine` wiring | adapted |
| RegisterView / UnregisterView / Views / UpdateViews / DrawViews | `CasaDesktopRuntime` | ported |
| ScrollMarker / solid color cache / circle cache | `CasaDesktopRuntime` utility texture caches | ported |
| Draw transaction | `CasaDrawTransaction` | ported |
| Rectangle / stencil / mask clipping | `CasaClipManager`, `CasaRenderTargetPool` | ported |
| Runtime bootstrap | `UIRoot` + `CasaMonoGameBackendBootstrap` | adapted |
| Editor bootstrap | `CasaEngine.Editor/GameEditor.cs` + `CasaGameRenderHost<GameEditor>` | adapted |
| World-space / offscreen surface | `UIRoot`, `WorldUIComponent`, `CasaRenderSurfaceAdapter` | adapted |

## Validation executed

- `dotnet build .\CasaEngine\CasaEngine.csproj -nologo -v:q`
- `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -nologo -v:q`
- `dotnet build .\CasaEngine.Demos\CasaEngine.Demos.csproj -nologo -v:q`
- `dotnet build .\CasaEngine.Tests\CasaEngine.Tests.csproj -nologo`

## Current limits

- Manual visual validation is still pending for `UIOverlayDemo`, `WorldSpaceUIDemo`, and a real editor launch path.
- `MGUI.Core` still transitively references `MGUI.MonoGame.Integration` for compatibility shims, but CasaEngine source paths no longer instantiate the upstream concrete backend types on the nominal runtime, editor, or demo paths.