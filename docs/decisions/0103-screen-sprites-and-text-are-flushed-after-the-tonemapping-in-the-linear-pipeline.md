# ADR-0103: Screen sprites and text are flushed after the tonemapping in the linear pipeline

- **Status**: Proposed
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/pbr-rendering-tasks.md` (arbitration P5, task T1.6)
- Renumbered from ADR-0098 on 2026-11-15 when origin/main was merged (ADR-0094 to ADR-0098 now carry the renumbered lighting decisions)

## Context

ADR-0101 tonemaps the HDR scene target, then composes the interface; ADR-0102 decodes the display colours the scene reads. The renderers of the view do not all produce scene colours (verified on the code):

- `ParticleRendererComponent` draws `TexturedPrimitive.fx` (its own clone, shared with the MGUI backend which keeps its own instance), `SpriteRendererComponent` draws `SpriteBatch.fx` and `PsxQuad.fx`, `Line3dRendererComponent` draws `DebugPrimitiveColor.fx`: all three draw in the scene, with depth.
- `Renderer2DComponent` draws screen sprites, text and 2D lines through an XNA `SpriteBatch` without an effect. Those are display colours already (a HUD, a font): decoding them in the scene and tonemapping them would change them. It is flushed last by `DefaultViewPipeline`, with `DepthStencilState.Default` (or the one of `GraphicsSettings`) set by its own `Flush`.
- `SkyBackgroundViewPipeline` draws a gradient texture (8-bit colours) and a sun (an alpha-only falloff texture tinted by `SkySettings.SunColor`) through a `SpriteBatch` without an effect, into the scene target.
- `DebugSolidColor.fx` is the editor transform gizmo drawn over the view by the editor, not a scene renderer.

## Decision

- **Particles, 3D sprites and lines decode in their shaders**, like the lit shaders: `ColorPipelineIsLinear` (default 0) declared in each, `DecodeColor4` wrapping the sampled colour times the vertex colour (or the colour constant). Each renderer binds the constant from `frame.Environment.LinearColorActive` on its own effect, on every flush (the sprite renderer also on the static batch, which has a frame; its full-screen display has none and binds 0). The MGUI backend never binds it, so the UI keeps its display colours on the shared `TexturedPrimitive.fx`.
- **A marker for display-ready renderers: `IPostTonemapRenderer`.** It extends `IViewFlushableRenderer` with `FlushPostTonemap`, an additive interface. `Renderer2DComponent` implements it. In a view that really draws through the HDR target, `DefaultViewPipeline.RenderScene` skips the implementers, and `RenderPipeline.RenderHdrView` flushes them between the tonemapping and the composition of the interface, after setting `DepthStencilState.None`. The plan called it a pure marker; a member was needed because `Renderer2DComponent.Flush` sets its own depth state, which the pipeline could not override from outside, so the implementation draws without depth test when asked to flush after the tonemapping (`FlushQueues(depthTest: false)`). `DefaultViewPipeline.RenderView` (the whole view) and every legacy path still call `Flush` in the usual order with the usual states.
- **Limit (open point O3).** In `LinearHdr` the screen sprites and text no longer test the depth of the scene: the depth lives in the HDR target, not in the destination, and the 2D renderer draws over the tonemapped image like the interface.
- **The sky background decodes on the CPU.** The gradient is drawn from a second texture, built on first use in `HalfVector4` from the same colours decoded to linear light (decoding into an 8-bit texture would crush the dark end of the gradient); the sun colour is decoded before its 0.65 factor; the white core and the alpha-only falloff texture need nothing. The legacy path draws the unchanged texture and colour.
- **What is not covered.** Lines are proved by a text test only (no demo draws 3D lines in the measurement scene). `LegacySkyCube.fx` is bound by games, not by the engine. `DebugSolidColor.fx`, the editor gizmo, is not decoded.

## Consequences

- A `LinearHdr` view keeps the interface, the screen text and the screen sprites at their legacy colours (measured within 2 levels on the measurement demo, back buffer and buffered view), while particles, 3D sprites and the sky are decoded then tonemapped like the lit scene.
- A view pipeline that does not implement `IScenePhaseViewPipeline` (ADR-0101) is rendered whole: its `Renderer2DComponent` content is still flushed inside the HDR target and tonemapped with the scene, as the interface of such a pipeline is.
- A new renderer that draws display-ready colours implements `IPostTonemapRenderer`; one that draws scene colours binds `ColorPipelineIsLinear` from the frame.
