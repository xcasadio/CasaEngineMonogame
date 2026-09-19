# ADR-0033: Screen effect above-UI layer

- **Status**: Accepted
- **Date**: 2026-09-19
- **Source**: `ai-agent/tasks/screen-effect-above-ui-tasks.md` (this chantier: plan approved by the
  author on 2026-09-19, decisions D1-D5, Phase 0-1 notes for T0.1 and T1.1)

## Context

The chantier originates in the Alundra port (`docs/plan-e13-hud.md` of the parent repository,
decision D-E13-11, §6 point 6): in the original PSX game, a warp fade captures the last frame,
health gauge included, and darkens that whole capture; the port instead draws its health gauge in
MGUI and its fade through `ScreenEffectComponent`, which submits its full-viewport quad at
`RenderPass2D.ScreenEffects = 750` (`CasaEngine/Framework/Rendering/Depth/RenderPass2D.cs:12-17`),
structurally below the UI composition step of every view pipeline. Left unchanged, a warp fade would
leave the MGUI gauge floating over the black overlay instead of darkening with the scene.

The plan's verified state of the repository establishes the facts the decision rests on:

- MGUI composition happens inside each view pipeline, not after it:
  `DefaultViewPipeline.RenderView` enqueues world draws, flushes the renderers, then calls
  `(view.UICompositionService ?? DefaultUICompositionService.Instance).Compose(...)`
  (`CasaEngine/Framework/Rendering/DefaultViewPipeline.cs:26-57`, pre-chantier text at `:7-19`).
- Three implementations of `IViewRenderPipeline` exist (`IViewRenderPipeline.cs:31`):
  `DefaultViewPipeline` (game/runtime), `OverlayViewPipeline`
  (`CasaEngine.Editor/Runtime/Rendering/OverlayViewPipeline.cs:21`, editor and Play-in-Editor, its
  own renderer flush and its own composition call at `:148`, without delegating to the default
  pipeline), and `SkyBackgroundViewPipeline` (delegates to an inner pipeline). A mechanism placed in
  the default pipeline alone would miss the editor and Play-in-Editor.
- `OverlayViewPipeline.cs:144-149` calls the exact same expression as
  `DefaultViewPipeline.cs:53-54` (`view.UICompositionService ?? DefaultUICompositionService.Instance`)
  to compose the UI, so a single hook placed inside `DefaultUICompositionService.Compose` covers all
  three pipelines by construction.
- A prior pattern of an out-of-pipeline flush already exists: `TileMapSurfaceComponent.cs:206` calls
  `spriteRenderer?.Flush(in frame)` directly to draw into its own target.
- `RenderPassDepthOffset.DeriveDepthOffset` is an explicit `switch`, not a rank-based derivation
  (`RenderPassDepthOffset.cs:17-28`), with tests enumerating the values in a hard-coded table and
  requiring monotonicity (`RenderPassDepthOffsetTests.cs:16-26, 35-45, 47-60`); this chantier does not
  touch it (D3).
- `ScreenEffectComponent` submits its overlay quad during `Update`, not `Draw`
  (`CasaEngine/Framework/Application/Components/ScreenEffectComponent.cs:18-23, 40-41, 47-74`), so it
  is flushed at the renderer-flush step, before the UI is composed.
- The overlay quad's placement formula is only valid behind a `Camera2dComponent`
  (`ScreenEffectComponent.cs:53-67, 123-129`); with a 3D camera it falls back to
  `cameraPosition = Vector3.Zero`, which does not reliably cover the viewport.

## Decision

- D1 — A layer setting on the service, not on the component: `ScreenEffectLayer { BelowUI, AboveUI }`
  (`CasaEngine/Framework/Rendering/ScreenEffects/ScreenEffectLayer.cs`), read through a new `Layer`
  property of `ScreenEffectService`, defaulting to `BelowUI`. No existing consumer changes behaviour
  until something sets `Layer = AboveUI`.
- D2 — A "post-UI" hook lives in the UI composition step, not in the view pipelines.
  `DefaultUICompositionService.Compose` calls, right after `view.UIView?.Draw()`, the draw of any
  post-UI overlays registered on the view through a new `IPostUIOverlay` contract and a small
  preallocated list on `RenderView` (`RegisterPostUIOverlay`/`UnregisterPostUIOverlay`/
  `PostUIOverlays`). All three pipelines flow through this single composition service, editor and
  Play-in-Editor included, because `OverlayViewPipeline` delegates to the same
  `DefaultUICompositionService` instance as `DefaultViewPipeline`.
- D3 — No second queue, no new render pass, `RenderPassDepthOffset` stays untouched. In `AboveUI`
  mode, `ScreenEffectComponent` no longer submits its quad during `Update`; from the D2 hook it
  submits to the sprite renderer at the unchanged `ScreenEffects` pass, then immediately calls
  `Flush(in frame, stats)`, following the `TileMapSurfaceComponent.cs:206` pattern. Nothing is ever
  left queued from one frame to the next, in any pipeline.
- D4 — GPU state: the immediate flush runs after `Desktop.Draw()`, which leaves the device in an
  unspecified state. `SpriteRendererComponent.Flush` now saves the `DepthStencilState`,
  `RasterizerState`, `SamplerStates[0]` and `BlendState` it is about to change and restores them on
  exit, per the repository's GPU-state-restoration rule (`AGENTS.md` §9.4).
- D5 — The decision is recorded as this ADR; the texts that said the screen-effects pass is "below
  the UI" unconditionally are corrected to state that this is the default behaviour, not an absolute
  one; and a visible smoke in an engine demo (`TileMapDemo`, the only demo on a `Camera2dComponent`)
  demonstrates the `AboveUI` layer darkening an MGUI screen along with the scene, per the sample
  requirement of `AGENTS.md` §6 for any non-trivial visible feature.

Two other designs were considered and rejected:

- **A second sprite queue flushed by a fourth step of the view pipelines, routed through a new
  render pass above `UI`.** Rejected: `OverlayViewPipeline` does not delegate render-flush to the
  default pipeline, so a queue never flushed there would grow unbounded past the sprite renderer's
  preallocated vertex buffer (`SpriteRendererComponent.cs:31-34`, `NbSprites = 10000`); and the new
  pass would have fallen into `RenderPassDepthOffset.DeriveDepthOffset`'s default `_ => 0f` arm,
  breaking the monotonicity its tests require (`RenderPassDepthOffsetTests.cs`).
- **An `IUICompositionService` decorator redrawing the effect itself.** Rejected: it would duplicate
  the overlay submission and blend-mode logic already implemented in
  `ScreenEffectComponent.SubmitOverlay` (`ScreenEffectComponent.cs:130-163`). The D2 hook reuses the
  existing sprite renderer instead of reimplementing submission.

## Consequences

- Adding a post-UI overlay to a view is now a two-step contract (`IPostUIOverlay` +
  `RenderView.RegisterPostUIOverlay`/`UnregisterPostUIOverlay`) usable by any future full-viewport
  effect that must draw after MGUI, not only by `ScreenEffectComponent`.
- The hook is walked with an explicit `for` loop over a preallocated list inside
  `DefaultUICompositionService.Compose`, with no allocation and no closure, consistent with
  `AGENTS.md` §9.3.
- `RenderPassDepthOffset`, the render passes and the three `IViewRenderPipeline` implementations are
  unchanged; only `DefaultUICompositionService`, `RenderView`, and `ScreenEffectComponent`/
  `SpriteRendererComponent` were touched.
- `ScreenEffectService.Clear()` deliberately leaves `Layer` untouched: it is a rendering setting, not
  fade state, exactly as it already leaves `R`/`G`/`B` untouched.
- Known limit, unchanged by this chantier: the overlay quad's placement is only valid behind a
  `Camera2dComponent` (pre-existing limit of `ScreenEffectComponent`, not introduced here); a 3D-only
  view does not get a correctly placed overlay in either layer.
- GPU-state restoration in `SpriteRendererComponent.Flush` (D4) could not be pinned by a unit test:
  the test suite builds no real `GraphicsDevice` (confirmed absent repository-wide); it is covered
  instead by the `TileMapDemo` smoke (T2.2) and by manual observation.
- Reproducing the PSX's screen-capture-then-fade behaviour itself stays out of scope: only the draw
  order is addressed, the scene keeps rendering live under the fade.
