# Demos main screen

`CasaEngine.Demos` starts on a main screen that presents the demos. Choosing one loads a fresh world for it; during a
demo, Esc or the gamepad Select (Back) button returns to the main screen. Every demo uses the whole window.

## Main screen

- It lives in a world of its own, the menu world, rebuilt every time the player comes back. That world has a fixed
  camera and no scene; the screen covers its whole view (`Content/Screens/main-menu.xaml`, `MainMenuScreen`, MGUI
  built-in Dark theme).
- Left: the themes and their demos, in a tree. Right: the sheet of the selected demo (title, theme, description).
  Bottom: the controls hint, and the Launch and Quit buttons.
- It opens on the first registered demo (index 0), or on the demo the player just left.

## Controls

| Where | Input | Effect |
| --- | --- | --- |
| Main screen | Up / Down, D-pad | Moves the selection; the sheet follows. |
| Main screen | Right / Left, D-pad | Expands a theme or goes down to its first demo / collapses a theme or goes up to it. |
| Main screen | Enter, A, double click on a demo, Launch | Launches the selected demo ("Loading ..." shows for one frame). |
| Main screen | Quit, closing the window | Quits. Esc and Back do not quit from the main screen. |
| Demo | Esc, Select (Back) | Returns to the main screen. |

The demo's own keys are listed in its description and in its HUD.

## Worlds

- Each launch creates a new `World`, which the demo builds in code before the world loads (`Demo.Initialize`,
  `ConfigureSceneLighting`, `CreateCamera`): its entities, physics space policy and camera are in place when
  `World.LoadContent` and the view bootstrapper run. Only `InitializeCamera` and the demo's UI wait for
  `GameManager.WorldLoaded`.
- Leaving a world (to a demo or back to the menu) clears it first: `GameManager.SetWorldToLoad(World)` replaces the
  current world without clearing it. Its entities, physics context, voices and UI are released, then the demo's `Clean`
  runs. The engine then frees the assets no one holds any more (ADR-0036, ADR-0037).

## Themes

| Theme | Demos |
| --- | --- |
| Rendering | Static model, Material system, Particle system, Environment showcase, Static shadow validation |
| Animation | Skinned mesh, Animation blend, Animation IK, Skeletal animation blending |
| Physics | Collision 3d basic, Collision 2d basic, Top down elevation |
| Scene and views | Scene management, Split-screen (2 views), Render-to-texture, ViewManager v2 Sandbox |
| Cutscenes | Cutscene MoveTo, Cutscene NavigateTo |
| 2D and tile maps | Tile map, Tile map 3d, Tile map surface screen |
| UI | World-space UI, MGUI UI Overlay |
| Audio | Audio demo |

## Adding a demo

```csharp
// DemosGame.LoadContentPrivate: the registration order is the index CASAENGINE_START_DEMO uses.
AddDemo(new MyDemo(), ThemeRendering);
```

- `Title` is the tree entry and the sheet title, `Description` the sheet text (plain text, no MGUI markup).
- Build the scene in `Initialize` on `game.GameManager.CurrentWorld`: it is the demo's new world, not yet loaded.
- Read the keyboard with `DemoKeyboard.Read(game)`, not `Keyboard.GetState()`. Esc belongs to the demos app.
- Release in `Clean` what the world does not own; the world itself is already cleared when `Clean` runs.

## Automation

- `CASAENGINE_START_DEMO` (index or title) starts that demo directly, without the main screen.
- `CASAENGINE_CAPTURE_SCREENSHOT_PATH` (and `CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS`) captures the main screen, or the
  demo started by `CASAENGINE_START_DEMO`, then quits. The in-demo reminder "Esc / Select: back to the menu" is hidden
  while the back buffer is captured or probed (`CASAENGINE_DEMO_PIXELS_PATH`).
- `CASAENGINE_DEMO_CYCLE=<frames>` goes from the main screen to every demo and back, twice, `<frames>` frames per world
  (default 60). It checks every world change and exits with code 0, or 1 when a check failed or an error was logged:
  - the new world is a new instance and the world left holds no entity;
  - every view camera belongs to the new world, and the camera the demo created is in a view, so no default camera was
    created;
  - the menu world has one view and one main screen;
  - every world that loads was asked for: during the loading frame of each launch the cycle asks for the same demo
    again, as a second click would, and the game drops that request.

## Limits

- The world change is synchronous: a heavy demo freezes the frame it loads in. "Loading ..." is the frame before it.
- Some captures depend on time: the pace is variable, so a physics demo captured at a given delay is not identical from one
  build to the next. Compare such demos while their scene is still static.
- A view in `OnDemand` mode (ViewManager v2 Sandbox, view 3) keeps its previous image after a resize until it is
  invalidated.

Decisions: see ADR-0072 (main screen, one world per demo). The view layout area and window-level UI of ADR-0070 stay in
the engine; the demos no longer use them.
