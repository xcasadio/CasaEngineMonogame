# ADR-0048: A project virtual resolution fitted at an integer factor with black bands

- **Status**: Accepted. Amended by ADR-0054 (the clip rectangles of the UI are local to the view; the device scissor is refreshed on every resize; the rest stands)
- **Date**: 2026-10-03 (decisions taken with the author on 2026-10-03)
- **Source**: this chantier: `ai-agent/tasks/e19s-virtual-resolution-tasks.md` (rules S-R2 to S-R5). Parent repository:
  `docs/plan-e19-opcodes.md`, step E19.s, decisions D-E19-47 and D-E19-60 (the image keeps the 320 x 240 of the original,
  enlarged without distortion, followed in real time, at a whole factor only). Complements ADR-0011 (2D render spaces and
  pixel-perfect checklist); replaces no ADR.

## Context

- A game that wants a fixed logical picture (the Alundra port: 320 x 240) had no engine support. The runtime puts one view
  over the whole window and its 2D camera shows `viewport / Zoom` world units, so the visible area changes with the window
  and the game had to set the zoom itself, once per world.
- On DesktopGL a user resize of the window only changes the back buffer and raises `Window.ClientSizeChanged`; it never
  resets the device. `CasaEngineGame.OnScreenResized` was only reached from `DeviceReset` and `ApplyDisplaySettings`, so the
  view, its camera and the UI bounds kept the size they had when the world loaded.
- `BackBufferPresenter` and `PresentMode.Fit` exist but nothing builds them; the band fill is an empty branch, the sampler
  is linear and the destination rectangle is fixed at construction.
- The projection of a 2D camera is computed from the camera's own viewport (`Camera2dComponent.ComputeProjectionMatrix`),
  which `CameraComponent.OnScreenResized` sets to the size it is given, and which `World.OnScreenResized` sets to the
  window size for every camera.
- A XAML screen had one hook, `OnWindowLoaded`, called once; a screen that places its window from the desktop bounds had no
  way to learn that the bounds changed.

## Decision

- **Project setting.** `ProjectSettings.VirtualResolution` (`VirtualResolutionSettings`: `Width`, `Height`, `Mode`) is
  optional. Absent, the runtime behaves as before (full-window view; the editor, the demos and other projects are not
  affected). The only mode is `IntegerFit`; the bands are black. An invalid declaration fails at load, naming the key.
- **Layout.** A pure function, `VirtualResolutionLayout.Compute`: `k = max(1, floor(min(W / vw, H / vh)))`, image
  `vw*k` by `vh*k`, centered with offsets `floor((W - vw*k) / 2)` and `floor((H - vh*k) / 2)`, then cropped by the window
  (a window smaller than the image shows its center at factor 1).
- **Application.** Outside external view management (the editor), the single back-buffer view takes the image rectangle as
  its viewport, and its camera takes a viewport of the size of that cropped rectangle and, for a `Camera2dComponent`,
  `Zoom = k` (so it frames exactly `vw` by `vh`). Done when the default view is created
  (`DefaultRuntimeViewBootstrapper`) and in `CasaEngineGame.OnScreenResized` after `World.OnScreenResized`. The engine
  subscribes to `Window.ClientSizeChanged` to follow a resize in real time (ignored while the window is minimized).
- **Bands.** When the image does not cover the window, the whole back buffer is cleared to black before the views, each frame
  (viewport saved and restored around the clear).
- **Screens.** `XamlUIScreenBase.OnScreenBoundsChanged(Rectangle)` is called when the bounds of the screen's desktop change;
  `UIRoot.Update` feeds `ScreenStack.NotifyScreenBounds`, which reaches every XAML screen, including those frozen under a
  modal one.
- The view is rendered straight into the letterboxed back-buffer viewport; no intermediate render target and no
  `BackBufferPresenter`.

## Consequences

- The game no longer sets the 2D camera zoom: the engine owns it when a virtual resolution is declared. A game that
  declares one and also sets the zoom will be overridden at the next resize.
- Every pixel of the image has the same size (whole factor); the cost is wider bands than an exact fit (1920 x 1080 gives
  x4, a 1280 x 960 image, 320 pixel bands left and right and 60 pixel bands above and below).
- World-space content (backdrops, cellular layers, fades) is sized by the camera view and needs no change; UI is view-local
  and follows the view rectangle.
- Mouse input is mapped by the view rectangle already (`ViewRenderHost`); a XAML screen that wants the scale recomputes it
  in `OnScreenBoundsChanged`.
- Not covered by an automated test: the `ClientSizeChanged` subscription and the black clear need a graphics device; they
  are checked in a running game by the consumer's acceptance.
- Deferred: a non-integer (exact fit) mode, `Fill`/`Stretch` modes, a configurable band colour, and wiring
  `BackBufferPresenter`. A window that starts or ends minimized keeps the last layout.
