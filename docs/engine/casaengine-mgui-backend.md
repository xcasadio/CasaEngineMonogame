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

## Allocation-free text

`CasaDrawTransaction` draws a `ReadOnlySpan<char>` through the text engine without allocating (MGUI ADR-0026), like the `string` overload for the same characters. With `FontStashSharpTextEngine`, the engine the runtime and the editor wire, an `MGTextBlock` whose text changes every frame can therefore be changed, measured and drawn with zero allocation (the block is proven on MGUI's test engines, the engine overloads on a real GPU: there is no single end-to-end test of both).

Use `SetText(ReadOnlySpan<char>)` for a text that changes every frame (HUD counter, value label, telemetry), from a buffer the caller keeps:

```csharp
private readonly char[] _scoreChars = new char[16];

if (score.TryFormat(_scoreChars, out int written))
{
    scoreText.SetText(_scoreChars.AsSpan(0, written));
}
```

- Building a `string` first (`$"{score}"`, `ToString()`) allocates before MGUI is reached: keep `Text = ...` for texts that change rarely.
- Do not read `Text`, `Runs` or `Lines` of such a block in a hot path: they are built on read.
- What is covered, and the cases that still allocate (first sight of a markup value, invalid markup, `TextCharactersPerSecond`, a font change, the rest of the desktop frame), is listed in `MGUI/Docs/text-architecture.md`, section "Texte sans allocation".
- Proof on the engine side: `CasaEngine.Tests/UI/Backend/TextSpanGpuTests.cs` (real GPU; equal pixels, zero bytes per draw).

## Validation executed

- `dotnet build .\CasaEngine\CasaEngine.csproj -nologo -v:q`
- `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -nologo -v:q`
- `dotnet build .\CasaEngine.Demos\CasaEngine.Demos.csproj -nologo -v:q`
- `dotnet build .\CasaEngine.Tests\CasaEngine.Tests.csproj -nologo`

## Current limits

- Manual visual validation is still pending for `UIOverlayDemo`, `WorldSpaceUIDemo`, and a real editor launch path.
- `MGUI.Core` still transitively references `MGUI.MonoGame.Integration` for compatibility shims, but CasaEngine source paths no longer instantiate the upstream concrete backend types on the nominal runtime, editor, or demo paths.