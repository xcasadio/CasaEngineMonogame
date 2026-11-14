# ADR-0096: The linear HDR pipeline renders the scene into a pooled HDR target and tonemaps before the interface

- **Status**: Proposed
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/pbr-rendering-tasks.md` (arbitrations P1, P2 and P6, tasks T1.3 and T1.4)

## Context

ADR-0095 lets a world select `LinearHdr`, but nothing reads the setting. `RenderPipeline.Render` draws every view straight into the destination of the view (the back buffer or the editor's host texture, the scene target `_sceneRt` when shadows are active on a cleared back-buffer view, the retained target of the frozen frame, or the render target of the view's own surface), and `DefaultViewPipeline` draws the interface into the same target as the scene. A `Color` target clips every value above 1 and cannot hold linear light, and the interface must not be tonemapped.

Verified on the code and by measurement (MonoGame DesktopGL 3.8.5.1, ADR-0086, task T0.3):

- Binding the back buffer from another render target clears it when the presentation parameters' `RenderTargetUsage` is `DiscardContents` (the default), at each binding. Two back-buffer views that each draw through an intermediate target (split-screen) erase each other unless this is handled.
- `SpriteBatch.Begin(effect:)` accepts a pixel-shader-only effect: the sprite batch keeps its own vertex shader, binds the drawn texture as sampler 0, and the effect declares no vertex shader.
- `Tonemap.fx` (`Tonemap_Reinhard`, `Tonemap_AcesApprox`, parameter `Exposure`) is compiled by the content pipeline (`CasaEngine/Content/Content.mgcb`), registered in `BuiltInShaderCatalog` and hot reloadable.

## Decision

- **An HDR scene target per view.** In `LinearHdr`, `RenderPipeline` renders the scene of a view into a target taken from `RenderTargetPool` (`HalfVector4` with `Depth24Stencil8`, the size of the view rectangle after the resolution scale) and gives it back at the end of the view. The pool key holds the format, so a target is created once per view size and then reused; each creation is logged with its size, format and render index. If the device refuses a format, the pipeline falls back to `HdrBlendable`, then `Color`, logged once.
- **Tonemapping into the destination of the legacy path.** A pure, unit-tested `ColorPipelinePlanner` decides, from the mode, the frozen frame plan and the surface type, whether a view uses an HDR target and which destination the tonemapping writes to: the initial target (back buffer or editor host texture), the scene target, the retained target, or the view's own surface target. The tonemapping is a full-rectangle opaque `SpriteBatch` draw with `Tonemap.fx`, technique and exposure taken from the resolved environment. The depth of the destination is cleared the way the legacy path clears it. The existing copy of the scene target or retained target to the back buffer, the retention bookkeeping, the debug overlay and the presenter run after, unchanged. `FrameCapturePlanner` is not modified.
- **The interface after the tonemapping.** A new additive interface `IScenePhaseViewPipeline` (`RenderScene`, which is `RenderView` without the interface phase; `SupportsScenePhase`, true by default) is implemented by `DefaultViewPipeline` and by `SkyBackgroundViewPipeline` (which draws its background, then delegates to its inner pipeline, and reports `SupportsScenePhase` only if the inner pipeline does). In `LinearHdr` the pipeline renders the scene, tonemaps, then composes the view's UI service itself. A view pipeline that does not implement the interface (or cannot, once wrapped) is rendered whole, interface included, into the HDR target and tonemapped with it; a warning is logged once per pipeline type.
- **Views the tonemapping cannot serve stay on the legacy path.** A view that keeps the previous content of its colour buffer (`ClearColorBuffer` false), an empty view rectangle, or a missing `Tonemap` effect (logged once) is drawn by the legacy path, because the tonemapping overwrites its whole rectangle.
- **The back buffer survives the binding.** When the HDR path binds the initial target after drawing elsewhere, the presentation parameters' `RenderTargetUsage` is set to `PreserveContents` for that binding only and restored at once, so earlier views of a split-screen are not cleared. The legacy path never does this.
- **Legacy is unchanged.** With the mode `Legacy` (the default) the planner answers "no HDR target" and no line of the existing path runs differently. `RenderTargetPool.Acquire` now counts a created target once it exists, so a format that fails to build leaves no phantom entry.
- This decision does not decode colours: in `LinearHdr` the image is still wrong in colour until the decoding of the next decision. The tonemapper operators are `Reinhard` and `AcesApprox` (Narkowicz), with a scalar exposure and no automatic exposure (P6).

## Consequences

- A world or a view can render in HDR and be tonemapped before its interface; a `Legacy` image is bit-identical to before.
- Every `LinearHdr` view costs one pooled HDR target of its size (8 bytes per pixel plus depth) and one full-screen tonemapping draw; a view in the editor that does not implement `IScenePhaseViewPipeline` has its interface tonemapped.
- Screen sprites and text drawn by `Renderer2DComponent` are still flushed with the scene, so they are tonemapped until a later decision moves them after the tonemapping.
- A view with `ClearColorBuffer` false is not rendered in HDR.
- The format of the retained and captured frozen-frame targets stays `Color`: they receive the tonemapped image.
- The `RenderTargetUsage` toggle depends on MonoGame reading that setting at each binding of the back buffer (true in DesktopGL 3.8.5.1); another backend needs the same check.
