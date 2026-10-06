# Demo browser

`CasaEngine.Demos` shows a browser on the left of the window and the running demo on the right. The scene is never
covered: its views are laid out in the area the browser leaves (view layout insets, ADR-0070).

## Layout

- **Browser** (left, MGUI built-in Dark theme): a header (`Demos (30)` and a `<<` collapse button), a tree of the demos by
  theme, a resizable splitter, the description of the selected demo (title, theme, text), and a hint line.
- **Scene** (right): the demo's views, inside `ViewManager.GetLayoutArea`. A demo's own HUD (`Demo.PostDraw` overlays,
  MGUI screens of its view) stays inside the scene.
- **Handle** between them: drag it to resize the browser, from 200 px up to the width that leaves the scene at least
  0.89 × its height (340 px at 1024x768). When the window is too narrow for both, the browser collapses.

## Controls

| Input | Effect |
| --- | --- |
| Click a demo | Loads it (also the demo already selected). |
| Up / Down | Moves the selection; the description follows, nothing loads. |
| Right / Left | Expands a theme or goes down to its first demo / collapses a theme or goes up to it. |
| Home / End | First / last entry. |
| Enter | Loads the selected demo; on a theme, expands or collapses it (as Space does). |
| F1, or `<<` | Collapses / reopens the browser. Collapsed, the scene shows "Press F1 to show the demo browser". |
| Escape | Quits. |

The browser owns the keyboard only while the pointer is over it. Then the demos read an empty keyboard
(`DemoKeyboard.Read`) and the arc-ball camera ignores the keyboard and the mouse (`InputRouter.WindowUI`). Move the pointer
back over the scene to drive the demo.

## Themes

| Theme | Demos |
| --- | --- |
| Rendering | Static model, Material system, Particle system, Environment showcase, Static shadow validation |
| Animation | Skinned mesh, Animation blend, Animation IK, Skeletal animation blending |
| Physics | Collision 3d basic, Collision 2d basic, Top down elevation |
| Scene and views | Scene management, Split-screen (2 views)\*, Render-to-texture, ViewManager v2 Sandbox\* |
| Cutscenes | Cutscene MoveTo, Cutscene NavigateTo |
| 2D and tile maps | Tile map, Tile map 3d, Tile map surface screen |
| UI | World-space UI, MGUI UI Overlay |
| Audio | Audio demo |
| PSX rendering | PSX sprite semi-transparency, Sprite queue capacity, Background layers PSX semi-transparency, Background tint PSX mode 1, Background tint PSX mode 0, PSX free quads\* |

\* Needs the whole window: loading it collapses the browser (F1 reopens it).

## Adding a demo

```csharp
// DemosGame.LoadContentPrivate: the registration order is the index CASAENGINE_START_DEMO uses.
AddDemo(new MyDemo(), ThemeRendering);
AddDemo(new MyMultiViewDemo(), ThemeSceneAndViews, collapsesBrowser: true);
```

- `Title` is the tree entry, `Description` the text below it (plain text, no MGUI markup).
- Read the keyboard with `DemoKeyboard.Read(game)`, not `Keyboard.GetState()`.
- Draw overlays in `PostDraw` from `GraphicsDevice.Viewport`: it is the scene area.
- Lay out extra back-buffer views in `game.GameManager.ViewManager.GetLayoutArea(width, height)`, not the whole back
  buffer.

## Automation

- The browser starts collapsed when `CASAENGINE_CAPTURE_SCREENSHOT_PATH`, `CASAENGINE_DEMO_PIXELS_PATH` or
  `CASAENGINE_PSXQUAD_DUMP_PATH` is set, so captures, probes and dumps show the same image as before the browser.
- `CASAENGINE_DEMO_BROWSER=open` or `collapsed` overrides that, e.g. to capture the browser itself.
- `CASAENGINE_START_DEMO` (index or title) is unchanged.

## Limits

- A narrower scene widens the vertical field of view of the 3D cameras (`Camera3dComponent`): 60° at 1024x768, about 83°
  with the browser at 280 px. F1 gives the whole window back.
- Scenes drawn at one pixel per world unit (PSX demos, tile map) are cropped by the browser, centred on the window centre.
- Layout insets are not supported with a virtual resolution (ADR-0048): combining them throws.
- A view in `OnDemand` mode (ViewManager v2 Sandbox, view 3) keeps its previous image after a resize until it is
  invalidated.

Decisions: see ADR-0070.
