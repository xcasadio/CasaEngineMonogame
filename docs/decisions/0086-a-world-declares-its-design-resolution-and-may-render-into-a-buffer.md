# ADR-0086: A world declares its design resolution and may render into a buffer

- **Status**: Accepted
- **Date**: 2026-11-08
- **Source**: this chantier: `ai-agent/tasks/world-render-buffer-tasks.md` (decisions D1 to D6, settled with the author on 2026-11-08), branch `chantier/world-render-buffer`. Amends ADR-0048 and ADR-0079.

## Context

- A project has a single `VirtualResolution` (`ProjectSettings.VirtualResolution`), and its only mode is `IntegerFit` (`VirtualResolutionSettings.cs`, `ProjectSettingsHelper.ReadVirtualResolution`). ADR-0079 records the consequence: every screen of a project shares one design resolution.
- `IntegerFit` draws the view straight into a letterboxed area of the back buffer. `VirtualResolutionLayout.Apply` sets the viewport of a `BackBufferSurface` to the image rectangle and the 2D camera zoom to the integer factor k. There is no intermediate render target and no `BackBufferPresenter` (ADR-0048).
- ADR-0048 deferred a non-integer fit and the wiring of `BackBufferPresenter`. That presenter exists but is never built: it samples with `LinearClamp`, fixes its rectangle at construction, leaves its band branch empty and has no integer factor.
- A project can be made of screens drawn at different resolutions, for example a high-resolution logo followed by low-resolution scenes of different sizes, each shown in the same window. Modern engines offer two ways to show a low fixed resolution: drawing at window resolution with a scale, or rendering at the base size into a buffer that is then enlarged. Godot calls them the `canvas_items` and `viewport` stretch modes.
- The pipeline already renders render-target views before back-buffer views and calls each view's presenter after the view (`RenderPipeline.Render`). MGUI draws into the bound target, and its bounds come from the view surface's `ViewportRect`.

## Decision

- **Two paths coexist.** `IntegerFit` stays the direct drawing of ADR-0048, unchanged. Two modes are added that render the world and its UI screens into a render target of the design size:
  - `BufferedIntegerFit` presents the target at the integer factor k of `VirtualResolutionLayout.Compute`, sampled with `PointClamp`, with black bands;
  - `BufferedFit` presents it at the largest non-integer factor `min(windowWidth / width, windowHeight / height)` that keeps the aspect ratio, sampled with `LinearClamp`, with black bands. The presented size is the scaled size rounded down to whole pixels, centred with the same floor-half offsets as `Compute`.
- **Per world.** A `.world` may declare an optional `virtual_resolution` object with `width`, `height` and `mode`. Without it, the project's `VirtualResolution` applies. The project's `Mode` also accepts the two new modes. A world block takes its `ScaleUI` from the project, or false when the project has no `VirtualResolution`.
- **At world load.** The effective resolution is set when a world is loaded and its views are rebuilt. The window never changes size.
- **Presentation and input.** The buffered view's presented rectangle, in window pixels, is the single source used to choose the view under the pointer and to convert a window position to a design pixel: `(window - origin) * design / presented size`. Points in the bands belong to no view.
- **Ownership.** The default view owns the render target it creates and releases it when the view is removed, for example on a world change.
- **Additive format.** An existing project or `.world` loads and renders exactly as before.

## Consequences

- ADR-0048 still holds for `IntegerFit`. Its deferred non-integer fit and presenter wiring are resolved for the buffered modes only. Fill and Stretch modes and a configurable band colour stay deferred.
- ADR-0079: a screen follows the design resolution of the world that is loaded, not only the project's. In a buffered mode a screen receives the target's bounds, so its scale is 1 whether `ScaleUI` is set or not.
- ADR-0068: in a buffered mode, free quads are drawn at design pixels and the target is then enlarged. Their rule per screen pixel at factor k applies to `IntegerFit` only.
- ADR-0070: a view with layout insets is still refused with a virtual resolution, buffered modes included.
- The editor manages its own views and has no active virtual resolution, so it ignores the world block, as it ignores the project setting today. The block is read and written, but not yet shown in the inspector.
- A buffered frame costs one extra full-target draw into the back buffer and one band clear. `BufferedFit` is smoothed, so it is not pixel-exact. `BufferedIntegerFit` at k is expected to match `IntegerFit` at k pixel for pixel for content on whole design pixels, and a GPU test checks it.
- With shadows enabled, the `_sceneRt` detour of `RenderPipeline` serves back-buffer views only. A buffered view renders shadows without it. Whether that changes their result is to be checked and written here.
