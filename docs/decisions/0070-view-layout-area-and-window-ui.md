# ADR-0070: View layout area and window-level UI

- **Status**: Accepted
- **Date**: 2026-10-06 (strategy and engine additions chosen by the author on 2026-10-06, plan approved the same day)
- **Source**: this chantier: `ai-agent/tasks/demo-browser-split-tasks.md` (decision D1, points P6 and P9, tasks T1.1 to T2.1) and the
  discussion with the author of 2026-10-06 (the demos window split into a demo browser and the scene, strategy A: the scene stays
  rendered into the back-buffer by its views). Keeps ADR-0048 (virtual resolution) and ADR-0054 (view-local UI clip rectangles);
  replaces no ADR.

## Context

- The demos app showed its navigation in an MGUI window drawn over the scene (`CasaEngine.Demos/Content/Screens/demo-info.xaml`,
  300x440, top right). The author decided to split the window in two resizable parts: a demo browser on the left and the scene on the
  right.
- Back-buffer views render into a rectangle of the back buffer (`BackBufferSurface.ViewportRect`). The automatic layout
  (`ViewManager.ApplyBackBufferLayout`) divided the whole screen with `SplitScreenLayout.Compute(width, height, ...)`, and both the
  default view (`DefaultRuntimeViewBootstrapper.CreateDefaultView`) and the single view resized by `CasaEngineGame.OnScreenResized`
  (`VirtualResolutionRuntime.ResizeSingleBackBufferView`) covered the whole window, or the integer-fit image of a virtual resolution
  (ADR-0048).
- Every UI runtime belonged to a view (`RenderView.UIView`). A view without a camera is not possible: the render pipeline reads
  `view.Camera` unconditionally (`RenderPipeline.cs`, `RenderView`). `InputRouter.IsMouseHandledByUI` and `IsKeyboardCapturedByUI` read
  only the view's UI; gameplay consumers such as `ScriptArcBallCamera` use them to ignore input aimed at the UI.
- In MGUI, a desktop gives keyboard focus to its first focusable element on keyboard activity when nothing has focus
  (`UIFocusNavigationService.QueueAutoFocusIfNeeded`), the focus setter is private, and the only public way to drop focus is to make
  the focused element non-focusable (`MGDesktop.SanitizeKeyboardFocusState`).

## Decision

- **Layout area.** `ViewManager.LayoutInsets` (a `ViewLayoutInsets` record struct, `Zero` by default) keeps margins of the screen
  free of back-buffer views; `ViewManager.GetLayoutArea(width, height)` returns the screen minus the margins (negative margins count
  as zero, at least one pixel stays inside the screen). The automatic layout divides that area through the new overload
  `SplitScreenLayout.Compute(Rectangle area, viewCount, mode)` (the full-screen overload delegates to it with the screen at the
  origin); the default view and the single resized view take the area, their camera sized to it.
- **No virtual resolution with an area.** A virtual resolution places its image in the whole window: a layout area that is not the
  whole window, combined with a virtual resolution, throws `InvalidOperationException` when the default view is created or the single
  view resized.
- **Window-level UI.** `CasaEngineGame.SetWindowUI(IUIViewRuntime, IRenderSurface)`, `ClearWindowUI()` and `WindowUI` install one UI
  that belongs to no view. It is updated right after the per-view UIs, with metrics computed from its surface as for a view
  (`UIScaler` with a 1920x1080 reference), and drawn last, after the third draw phase, on its surface (the viewport is then restored to
  the whole back buffer).
- **Input arbitration.** `InputRouter.WindowUI`, set by `SetWindowUI` and `ClearWindowUI`, makes the window UI's pointer and keyboard
  state count for every view in `IsMouseHandledByUI` and `IsKeyboardCapturedByUI`. The rest of the routing (dispatch, modal view,
  pointer capture) ignores it.
- **Ownership.** The caller owns the window UI (the game never disposes it) and keeps its keyboard focus consistent: nothing in the
  engine takes focus away from it. The demos browser makes its focusable elements focusable only while the pointer is over it
  (plan point P9).

## Consequences

- With zero insets and no window UI, nothing changes: the existing tests pass unchanged and the seven probe demos give the same
  readouts as before the change.
- Code that lays out its own back-buffer views from the screen size must use `GetLayoutArea` to stay out of the margins
  (`SplitScreenDemo`, `ViewManagerSandbox` and `RenderToTextureDemo` are adapted in this chantier).
- A narrower area means a smaller aspect ratio: `Camera3dComponent` keeps its horizontal framing by widening its vertical field of view
  ((π/4 × 16/9) / aspect), so a 3D scene looks smaller beside a panel (60° at 1024x768, about 83° at 744x768).
- Layout insets and a virtual resolution cannot be combined; supporting both (an integer-fit image inside the area) is deferred.
- One window UI at a time. It draws over everything in its rectangle, including `AfterRenderPipeline` overlays, and it is not part of
  the modal resolution of the router.
- The arbitration queries read one more UI state per call (`UIRoot.IsPointerOverUI` enumerates the desktop windows).
