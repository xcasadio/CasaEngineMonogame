# ADR-0086: The engine keeps the last frame of the active view and shows it frozen

- **Status**: Accepted (numbered ADR-0085 until 2026-11-08: renumbered when the branch was merged into main, whose ADR-0085 records that the Tiled JSON map import has a content entry; the decision is unchanged)
- **Date**: 2026-11-08
- **Source**: this chantier: `docs/plan-e19-opcodes.md` of the parent repository, section 1.5.19 (E19.TR-4a and E19.TR-4b (i)), decisions D-TR4A-1 to D-TR4A-14; the measurements are in `docs/plan-e19-theme3-annexe/tr-4a/` of the parent repository

## Context

- A game that wants to cover a transition (a door, a change of map) with "the image of before" needs the engine to keep that image: the
  engine has no run-time capture API, and the content of the back buffer is undefined after the buffer swap
  (`GetBackBufferData` is only used by demos and probes, in `PostDraw`).
- The view of a game is drawn by `RenderPipeline.Render`; the user interface and the post-UI overlays (the screen effect veil, ADR-0034)
  are composed inside that draw, so the finished image of a tour contains the gauges and the veils.
- Post-UI overlays were drawn in registration order only (`DefaultUICompositionService.Compose`); an image below the veil must be drawn first
  whichever of the two registered first.
- The game decides when a transition starts in an `Update`; the `Draw` of the same tour comes after. The image the original game shows is
  the one drawn by the tour **before** the arming `Update`.
- Related decisions, none modified: ADR-0034 (the veil above the user interface ignores depth), ADR-0048 (the virtual resolution, the integer fit and the
  black bands: the retained rectangle is the view rectangle), ADR-0068 and ADR-0071 (free quads and the pattern "the engine draws, the game pushes the state":
  the component follows it, the game DLL pushes the request).

## Decision

- **A service, a component, an option, off by default.** `FrozenFrameService` (`CasaEngine.Framework.Rendering.FrozenFrames`, no MonoGame type)
  is held by `FrozenFrameComponent` (`game.FrozenFrameComponent.Service`). `RetainLastFrame` is false by default: with it off the render path
  is the one of before the change (probe S1: the back buffer is identical octet for octet, on a synthetic bench and on the real game).
  The game DLL turns nothing on in this change.
- **The image of the previous tour comes from a retained target, not from a read of the back buffer.** While the option is on, the active view
  is drawn into a retained scene target (the scene target already used for shadows, generalised, kept for the target view only, so another
  back-buffer view cannot overwrite it) and copied to the back buffer as before. On a capture request, **before the target is cleared**, it
  still holds the previous tour: a GPU copy (opaque, point sampling, the whole rectangle) keeps it in a captured target. No CPU read, no GPU
  stall. The captured image is the image of the `Draw` of the **previous** tour, user interface and veils included.
- **The decision is a pure function.** `FrameCapturePlanner.Plan` (internal) decides, without a GPU, whether the view is drawn through the
  scene target (the old shadow rule, or "target view, option on, cleared back-buffer view of non-empty rectangle"), whether the retained
  frame is copied, or whether the request fails (once). Without retention the old rule holds for the 16 combinations of its four inputs.
- **Outside that path nothing is kept**: the editor (non-null initial target), a view with a target, a view without colour clear, a
  resized view. The request ends `Unavailable` (one warning logged, any earlier image dropped); the game keeps the live scene.
- **The interface.** `RequestCapture()` (idempotent while it waits), `CaptureRequested`, `LastCapture` (`None`, `Captured`, `Unavailable`),
  `CaptureCount`, `HasImage`, `CapturedWidth`, `CapturedHeight`, `ShowBase()`, `HideBase()`, `BaseVisible`, `IsBaseDrawable`
  (`BaseVisible && HasImage`, the drawing test), `ShouldRegisterBase` (`BaseVisible && (HasImage || CaptureRequested)`, the registration
  test), `Release()` (hides, drops the image, forgets a pending request, keeps `CaptureCount`).
- **Request and display are posed in the same `Update`.** The component registers in its own `Update`, when the image does not exist yet
  (it exists after the capture made in the `Render` of the same tour); registering only on `IsBaseDrawable` would show the image one tour
  late. Hence `ShouldRegisterBase` accepts a pending request.
- **Releasing is the caller's duty.** The state persists until `HideBase` or `Release` and survives a change of world; forgetting `Release`
  freezes the screen for good. The engine never releases by itself.
- **Overlays have an order.** `RenderView.RegisterPostUIOverlay(overlay, int order = PostUIOverlayOrder.Default)`: lower first, then
  registration order (stable); a second registration of the same overlay is ignored whatever its order. `PostUIOverlayOrder.FrozenFrame =
  -100` and `Default = 0` (the screen effect veil keeps 0): the frozen image is always below the veil.
- **The image is drawn through the game `SpriteBatch`**, opaque, `PointClamp`, no depth, over the whole rectangle of the view; an image
  captured at another size is stretched to the view (a resize in the middle of a transition).
- **Fallback considered, not taken.** Had the probe S1 shown that the scene-target path alters the image, the capture would have been made at
  the tour of the request (the image of the arming `Update`, one tick later). S1 held, so the fallback is unused.
- **The pixels are proved by a demo, the logic by the suite.** The demo "Frozen frame demo" reads the back buffer in process and compares
  it with predictions computed from the colours it poses. The GPU mutants (copy after the clear, copy after the blit, linear sampling) are
  killed by the demo. The mutant that creates the captured target `DiscardContents` instead of `PreserveContents` survives the demo and is
  declared equivalent: that target is bound once per capture, the copy that follows rewrites every pixel, and MonoGame 3.8.5.1 DesktopGL
  clears a `DiscardContents` target at each binding, before the rewrite.

## Consequences

- The game DLL can, in a later change, turn the option on, ask for a capture and show the image in the same tick, read `LastCapture` on the
  next tour, and release at the end of the transition. This change adds no behaviour to the game.
- Cost when the option is on: two targets of the size of the view (4.7 MiB each at 1 280 x 960), the second created on the first request and
  freed when the service holds neither an image nor a request. The CPU cost of command issue was not measurable in the demo (the difference
  between two runs of the same option is as large as the difference between options); the GPU cost is not measured.
- One view only: the active view. No editor surface. A resize between the request and the display stretches the image.
- Fill colour, slices and iris are not here; a later change adds them to the same service without changing these members.
- Known limit: the planner and the service are tested without a GPU; the two targets, the copy and the draw are covered by the demo only.
