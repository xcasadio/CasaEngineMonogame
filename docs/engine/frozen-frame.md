# Frozen frame (E19.TR-4a, ADR-0085)

The engine keeps the last composed frame of the active view and can show it frozen, below the screen effect veil. **Engine = mechanism,
game DLL = policy**: the engine decides nothing about when to freeze; the game asks, shows and releases. The option is off by default and the
render path is then the one of before.

```text
FrozenFrameService     state only, no MonoGame type: option, request, image state, visibility
        ^
FrozenFrameComponent   GameComponent + IPostUIOverlay (order -100): registers on the active view, draws the image
        |
FrameRetention         (internal) the retained and captured targets, GPU side
FrameCapturePlanner    (internal) the pure decision: scene target or not, copy or fail
```

Entry point from the game: `game.FrozenFrameComponent.Service`.

## Interface

| Member | Meaning |
|---|---|
| `RetainLastFrame` (get/set, false) | Keep the last frame of the active view. Must be on **before** the tour whose image is wanted. |
| `RequestCapture()` / `CaptureRequested` | Ask to keep the image of the previous tour. Idempotent while it waits. |
| `LastCapture` | `None`, `Captured`, or `Unavailable` (no retained frame: first tour, resize, editor, view without colour clear, option off). One warning is logged per failed request. |
| `CaptureCount`, `HasImage`, `CapturedWidth`, `CapturedHeight` | State of the kept image. `CaptureCount` is monotone. |
| `ShowBase()` / `HideBase()` / `BaseVisible` | Show or hide the image. |
| `IsBaseDrawable` | `BaseVisible && HasImage`: the test of drawing. |
| `ShouldRegisterBase` | `BaseVisible && (HasImage || CaptureRequested)`: the test of registration (see below). |
| `Release()` | Hide, drop the image, forget a pending request. Does not reset `CaptureCount`. |

## The contract: the image of the previous tour

The image kept is the image of the `Draw` of the **previous** tour, user interface and veils included (the gauge is in the image). When the
game asks in an `Update`, the `Draw` of that tour has not happened: what is kept is the tour before. Typical use, in one `Update`:

```csharp
var frozen = world.Game?.FrozenFrameComponent?.Service;
frozen.RetainLastFrame = true;   // earlier, at world installation
...
frozen.RequestCapture();
frozen.ShowBase();               // same Update: the image is shown from this tour's Draw
...
if (frozen.LastCapture == FrozenFrameCaptureState.Unavailable) { /* keep the live scene */ }
...
frozen.Release();                // at the end of the transition
```

The request and the display are posed in the same `Update`. The component registers in its own `Update`, before the capture made in the
`Render` of that tour, when `HasImage` is still false: that is why registration follows `ShouldRegisterBase` (a pending request is enough),
while drawing follows `IsBaseDrawable`.

## Persistence and the duty to release

The state persists until `HideBase` or `Release`, across a change of world. **Forgetting `Release` freezes the screen for good**: releasing is
the caller's duty, the engine never releases by itself. `Release` does not free the retained target (it is needed for the next capture while the option is on); the captured target is freed by the
component's next `Update` once the service holds neither an image nor a request. The retained target is freed when the option goes off, both on `Dispose`.

## Order of the overlays

`RenderView.RegisterPostUIOverlay(overlay, order)` ranks by increasing order, then by registration order. `PostUIOverlayOrder.FrozenFrame` is
-100 and `Default` is 0 (the screen effect veil, [screen-effects.md](screen-effects.md)): the frozen image is always below the veil, whichever
registered first. The image is drawn opaque, point-sampled, without depth, over the whole rectangle of the view, through the game `SpriteBatch`.

## What the game decides

Turning the option on, when to request, when to show, what to do on `Unavailable`, the veil that tints the image (the existing veil, on
the `AboveUI` layer) and the release. None of this is in the engine.

## Limits

- One view: the active view. Nothing is kept for the editor, a view with a target, or a view without colour clear (`Unavailable`).
- Two targets of the size of the view while the option is on (4.7 MiB each at 1 280 x 960); the second exists only while an image or a request does.
- An image captured at another size than the view (a resize in the middle) is stretched to the view, point-sampled.
- No fill colour, slices or iris: a later change adds them to the same service without changing these members.
- Tests: the service, the planner, the registration and the overlay order run without a GPU (`CasaEngine.Tests/Rendering/FrozenFrames`); the
  pixels are checked by the demo "Frozen frame demo", which reads the back buffer in process and compares it with predictions computed from
  the colours it poses (`CASAENGINE_START_DEMO="Frozen frame demo"`, run from `CasaEngine.Demos/`).
