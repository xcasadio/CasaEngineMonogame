# ADR-0113: FXAA filters the tonemapped scene before the post-tonemap renderers and the interface

- **Status**: Proposed
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/pbr-clearcoat-ssao-fxaa-tasks.md` (decision D1, arbitrations P1 and P3, tasks T1.1 and T1.2)

## Context

ADR-0104 tonemaps the HDR scene target of a `LinearHdr` view straight into the destination of the view (`RenderPipeline.RenderHdrView`), then ADR-0106 flushes the screen sprites and text and composes the interface on top. The engine uses no multisampling (`CasaEngineGame.cs`, `PreferMultiSampling = false`; every render target is created with 0 samples), so scene edges are aliased. The author chose FXAA over TAA and MSAA (D1). ADR-0112 extends the PBR scope to it, and the setting `ColorPipelineSettings.AntiAliasing` (`anti_aliasing`, `None` by default) exists since T1.1.

An FXAA filter works on a display-encoded, low-dynamic-range image and must not blur text or interface edges. The destination of the tonemapping can be the back buffer, a view surface, the scene target or the retained frozen frame: none of them can be sampled by the pass that follows (the back buffer cannot, and the others would couple the pass to each destination).

Measured on the code (MonoGame DesktopGL 3.8.5.1, `ps_3_0`): `tex2D` with a linear clamped sampler works through `SpriteBatch` with a pixel-only effect (as `Tonemap.fx` and `Bloom.fx` do); the OpenGL chain ignores a requested texture level (ADR-0109), so the pass reads with `tex2D` only.

## Decision

- **Place of the pass.** When `AntiAliasing == Fxaa`, the tonemapping is drawn into a pooled `SurfaceFormat.Color` target without depth, of the size of the view rectangle; then the destination is bound as before (`BindHdrDestination`) and `FxaaRenderer` draws that target 1:1 into the view rectangle with `Fxaa.fx`. This happens after the tonemapping and **before** `FlushPostTonemapRenderers` and the interface composition, so text, screen sprites and the interface are never filtered. Without FXAA (or without the shader) the path is the unchanged one: no intermediate target, no extra draw.
- **Only in a view that composes its interface afterwards (P3).** The pass runs only in the branch of `RenderHdrView` where `composeInterfaceAfter` is true (the view pipeline implements `IScenePhaseViewPipeline`). A view whose pipeline renders everything in one block, interface included (the editor views, `OverlayViewPipeline`), is not filtered: filtering would blur its interface. The limit is written in `docs/engine/color-pipeline.md`.
- **`LinearHdr` only (P1).** `Legacy` has no tonemapping step and no HDR target; its views are not filtered and the setting is ignored there.
- **Algorithm.** `Fxaa.fx` is a pixel-only effect with one technique, `Fxaa`, and one parameter, `FxaaTexelSize`. It is written from the published idea of FXAA (luminance edge detection, edge orientation, search along the edge, blend by a bilinear read displaced across the edge, plus a sub-pixel term); no third-party code is used. Luminance is computed from the encoded colour with the Rec. 601 weights. Nine reads for the contrast test and the edge direction, then up to twelve reads in each direction along the edge; the shader has no early exit, so every `tex2D` stays in uniform control flow. A pixel whose local contrast is below `max(0.0312, 0.125 x brightest neighbour)` reads back its own value, which the linear filter returns exactly at a texel centre. Alpha is 1.
- **Pooled target.** The target comes from `RenderTargetPool.Shared`; its key `(width, height, Color, DepthFormat.None)` is stamped in `_hdrLastUsedRender` with `_renderIndex` like the bloom does, so `ReleaseUnusedHdrTargets` frees it after a size change. The HDR target never has the format `Color` without depth and the bloom skips a `Color` HDR target, so a `Color` key without depth is the FXAA one; the pipeline has its own counters `FxaaTargetCreationCount` and `FxaaTargetDisposalCount`, which the bloom counters do not include.
- **Device states.** `FxaaRenderer` puts back the blend, depth, rasterizer and sampler states it sets, so the code that follows meets the states the unfiltered path leaves.
- **Proof of execution.** Each view counts its FXAA passes in `RenderStats.FxaaPasses` (reset with the other counters, 1 when the view was filtered, 0 otherwise); `FxaaRenderer.DrawCount` counts the draws since the start. The demo harness writes the per-view count once at the moment of a capture.
- **Hot reload and loading.** `Fxaa.fx` is in `Content.mgcb` and in `BuiltInShaderCatalog` (`FxaaContentName`); the game loads it with the tonemap and bloom effects and reloads it like them. A missing shader logs one warning and skips the pass.

## Consequences

- One extra full-rectangle `SpriteBatch` draw (the FXAA one, at most 34 texture reads per pixel: 9 for the contrast test and the edge direction, 24 for the search along the edge, 1 for the final blend) and one pooled 4-byte-per-pixel target per view size, only for a view that asks for FXAA. The tonemapping itself is unchanged, drawn into the pooled target instead of the destination.
- The editor views (`OverlayViewPipeline`) and any custom pipeline without `IScenePhaseViewPipeline` get no FXAA. The retained frozen-frame target and the editor host texture, as destinations, are not exercised by the T1.2 measurements.
- No TAA and no MSAA: the filter does not recover sub-pixel detail, it blurs luminance edges (thin bright lines on a dark background can still shimmer when the camera moves).
- The scene depth is not in the pass: FXAA needs none. SSAO (ADR-0115) is independent and acts before the bloom, on the HDR target.
