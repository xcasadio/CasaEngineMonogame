# ADR-0079: Data-driven screens follow the design resolution and hold their fonts

- **Status**: Accepted
- **Date**: 2026-10-31
- **Source**: this chantier: `ai-agent/tasks/pixel-ui-screens-tasks.md` (decisions D1 to D10, delegated to and settled by the author on 2026-10-31), branch `chantier/pixel-ui-screens`. Amends ADR-0048 and ADR-0078.

## Context

- ADR-0048 gives a project a virtual resolution: one integer factor `k`, an image centered in the window, black bands. It sizes the view and the 2D camera. A XAML screen is left to do its own scaling (`docs/decisions/0048-virtual-resolution-with-integer-fit-and-black-bands.md`).
- A project without a DLL has no code to do that. In XAML it can only write a fixed `Scale`, which is wrong as soon as the window changes size.
- `MGWindow.Scale` already draws a window through a transform, with its clip and its mouse mapping, and scales from the window's top-left corner (`MGWindow.cs:484`). `Left` and `Top` stay in screen pixels.
- `ApplyScreenPlacement` computes `Left` and `Top` from the window's size in screen pixels and ignores `Scale` (`MGWindow.cs:246-297`).
- `UIRoot.UIScale` and `UIScaler` are a separate mechanism, based on a reference resolution, and do not give an integer factor.
- `UIFontRegistry` holds a bitmap font for every UI text engine (ADR-0036). A family is resolved when the XAML loads, and a family nobody holds silently falls back to the default font (`MGTextBlock.cs:119-122`). `XamlUIScreenBase.OnWindowLoaded` runs after the load, which is too late, and only a code path could call `Acquire`.

## Decision

- **The project opts in with `ScaleUI`.** `VirtualResolution` gains an optional boolean key, read when present and written only when true. A project without it keeps today's behaviour and a byte-identical file:

  ```json
  "VirtualResolution": { "Width": 320, "Height": 180, "Mode": "IntegerFit", "ScaleUI": true }
  ```

- **`k` is the factor of ADR-0048, derived from the window, never fixed in data.** `UIScreenActionContext` gains an optional member, `DesignResolution`, null when `ScaleUI` is off. `GameManager` fills it from `ActiveVirtualResolution`, which is null in the editor.
- **`DataDrivenScreen` scales its own window.** It sets `Window.Scale` at the end of `OnWindowLoaded` and again in `OnScreenBoundsChanged`, from `VirtualResolutionLayout.Compute`. The XAML is written in design pixels: the screen keeps the XAML's `Left` and `Top` and writes `Left * k` and `Top * k` in the same place. A `Scale` written in the XAML is overwritten, as the zoom of a 2D camera is under ADR-0048.
- **A window placed by alignment is refused.** Under this scale, a window with `HasScreenPlacement` throws at load, with the envelope path: the placement would be computed in screen pixels.
- **`UIRoot.UIScale` and `UIScaler` do not change, and neither does MGUI.**
- **The `.uiscreen` envelope gains an optional field, `fonts`**: a list of catalogue names or ids of `.fnt` assets. It is read with the same type check as the fields of ADR-0078, into `CommandFormatErrors`, and is written back only when not empty.

  ```json
  "fonts": [ "pixel-block" ]
  ```

- **`DataDrivenScreen` holds its fonts for its lifetime.** `UIScreenActionContext` gains an optional `AcquireFont`, a `Func<string, IDisposable>`. `GameManager` finds the asset in the catalogue, checks the type `fnt`, and calls `UIFonts.Acquire`. The screen overrides `LoadWindow` to take the holds before `base.LoadWindow`, and gives them back in `Dispose`. A font that cannot be found or loaded fails with the envelope path. The XAML does not change: a text element names the `face` of the `.fnt` in its `FontFamily`.

## Consequences

- A project without a DLL can declare a resolution and get screens that stay pixel-exact when the window changes.
- `XamlUIScreenBase` and its subclasses do not change: only `DataDrivenScreen` reads these fields. A screen with its own class does what it did.
- The format is additive: existing projects and envelopes load and save unchanged.
- Known limits, accepted:
  - A mistyped `FontFamily` in the XAML stays a silent fallback to the default font. Telling it apart needs an MGUI change.
  - A project has one `VirtualResolution`, so all its screens share one design resolution.
  - `k` applies to the window. A window moved by a drag is put back at `Left * k`, `Top * k` by the next change of bounds, so a screen uses `WindowStyle="None"` or leaves its title bar alone.
  - Under a window smaller than the design resolution, `k` is 1 and the view shows the center of the image, while a screen draws from the view's top-left corner.
  - The editor does not read `DesignResolution`: `ActiveVirtualResolution` is null there.
