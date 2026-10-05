# ADR-0054: MGUI clip rectangles are local to the view, the device scissor is absolute

- **Status**: Accepted
- **Date**: 2026-10-05 (plan E19.s2 read until READY, review 2, on 2026-10-05; work in the engine authorized by the author on 2026-10-03)
- **Source**: this chantier: `ai-agent/tasks/e19s2-ui-clip-view-space-tasks.md` (rules S2-R1 to S2-R3). Parent repository:
  `docs/plan-e19-opcodes.md`, section 1.2q.1 (E19.s2), decision D-E19-71 and open point O-E19-60: the author saw the HUD and the
  MGUI boxes cut or missing in an enlarged window. A defect of the port, fixed in the engine. Amends the UI part of ADR-0048 (the
  view rectangle of a virtual resolution); replaces no ADR.

## Context

- MGUI computes every clip rectangle in pixels of the view it draws (`MGElement` target bounds; the desktop is `(0, 0, width, height)`
  of the view, `ViewRenderHost.GetBounds`). `CasaDrawTransaction.SetClipTarget` wrote that rectangle as it was into
  `GraphicsDevice.ScissorRectangle`, intersected it with the value the device held, and `CurrentClipBounds` read it back as it was.
- MonoGame 3.8.5.1 DesktopGL applies the scissor in absolute pixels of the render target (`GL.Scissor` without any viewport term),
  while `SpriteBatch` draws relative to the origin of the viewport. A view that does not start at the corner of its target (the bands
  of a virtual resolution, ADR-0048; a split screen) therefore drew each element at `rect + origin` and clipped it at `rect`.
- The device keeps the scissor of its last reset when the window is resized by the user (only `Window.ClientSizeChanged` is raised);
  after an enlargement it still held the start-up size, and the first clip of the UI intersected with it. The MGUI host
  (`RenderHost`) and the editor host (`CasaGameRenderHost`) already refresh it on `ClientSizeChanged`; the runtime did not.
- The world is not touched: its sprites draw without a clip. `CasaEngine.Tests` had no real-GPU harness.

## Decision

- **Clip space.** `CasaDrawTransaction` shifts each clip rectangle by the origin of the current viewport
  (`GraphicsDevice.Viewport.X/Y`) when it writes it to the device, before the intersection and the comparison, for the fallback to
  the whole view and for a clip popped to none; it brings the device scissor back by the same origin when it reads it
  (`CurrentClipBounds`, compared with view-local rectangles by MGUI). The raw values saved and restored around a scope are not
  changed. A view at the origin (render target, editor) is unchanged.
- **Fresh scissor.** `CasaEngineGame.OnWindowClientSizeChanged` first calls `UiDeviceScissor.ResetToBackBuffer`, an internal static
  utility that sets, unconditionally, the device scissor to `(0, 0, BackBufferWidth, BackBufferHeight)`; before the early return of a
  game without a virtual resolution. Unconditionally, because between two frames no caller of the runtime holds a scissor of its own
  (the per-view graphics state snapshot takes it at the start of each view and gives it back at the end).
- **Proof.** A real-GPU harness (`GpuDeviceHost`, `[GpuFact]`, skipped without a GPU) ported from `MGUI.Tests` to `CasaEngine.Tests`;
  the split-screen demo carries a UI element in its offset view and reads it back from the back buffer in process.

## Consequences

- The whole MGUI UI drawn in a banded or offset view (inventory, sub-inventory, HUD, save screen, the engine dialogue box) is
  clipped where it is drawn. Callers that compare `CurrentClipBounds` with view-local rectangles keep working unchanged.
- A game that sets a scissor on purpose before a UI draw and expects it to survive a window resize loses it; none was found in the
  runtime.
- Not covered: the rendering of a UI into an off-screen render target inside an offset view (the mask clip strategy is never
  selected with the default capabilities); the one-frame lag of a window reopened right after a resize is cosmetic and unconfirmed.
- The `ClientSizeChanged` subscription itself needs a running game; the recipe of the parent plan (S2-5) checks it.
