# ADR-0072: The demos start on a menu world and load a fresh world per demo

- **Status**: Accepted
- **Date**: 2026-10-08
- **Source**: this chantier: `ai-agent/tasks/demos-main-menu-tasks.md` (decisions D1 to D8, taken with the author on
  2026-10-08), branch `chantier/demos-main-menu` from `main` (`07e354d0`).

## Context

- `CasaEngine.Demos` split its window into a demo browser and the scene (ADR-0070). The author asked for a main screen
  that presents the demos and lets the player choose one, a world loaded for the chosen demo, and Esc or the gamepad
  Select button to return to the main screen.
- All demos shared one `World`, created at startup and emptied with `ClearEntities` at every change; Esc and the gamepad
  Back button quit the application (`CasaEngine.Demos/DemosGame.cs` before this chantier).
- `GameManager.SetWorldToLoad(World)` replaces the current world without clearing it; only the path overload clears
  the old world (`CasaEngine/Framework/Application/GameManager.cs`, `UpdateWorld`). The editor relies on that: it keeps
  its edit world aside during a play session.
- A world must be populated before its `LoadContent`: `World.AddEntity` only queues the entity, the physics context
  reads the space policy when `LoadContent` creates it, and the view bootstrapper picks its camera among the world's
  entities and creates a default one otherwise.
- Screens pushed in a view disappear with the views at every world change; the RPG title screen is pushed when its world
  begins play and removed when it ends.

## Decision

- **Menu world.** The main screen lives in a world of its own, rebuilt on every return. That world has a fixed camera
  and no scene. Its screen (`MainMenuScreen`, list and sheet, MGUI built-in Dark theme) is pushed on the active view when
  the world is loaded, and removed and disposed before the world is left, like the RPG title screen.
- **One world per demo.** Each launch creates a new `World`. The demos app clears the world it leaves before
  `SetWorldToLoad(World)`, then lets the demo clean up. The next demo builds its scene on the new world before it loads,
  and only its camera initialization and UI wait for `WorldLoaded`. The engine is not changed.
- **Inputs.** During a demo, Esc or Back (Select) returns to the main screen. On the main screen, Enter, A, a double
  click or Launch starts the selected demo after one "Loading ..." frame; neither Esc nor Back quits, only Quit or the
  window close does.
- **Automation.** `CASAENGINE_START_DEMO` starts a demo directly, without the main screen. `CASAENGINE_DEMO_CYCLE` goes
  through every demo from the main screen and checks each world change.
- The side browser, F1, its layout insets and `CASAENGINE_DEMO_BROWSER` are removed from the demos. The engine API of
  ADR-0070 (view layout area, window-level UI) is unchanged and no longer used by the demos.

## Consequences

- No demo state crosses a demo change: physics context, entities, voices and UI belong to a world, and the engine frees
  the assets no one holds at each change. A demo that kept an asset in a field across runs without holding it would see
  it freed; the cycle runs every demo twice to catch that, and found none.
- Every change goes through a world load, so a heavy demo freezes the frame it loads in; there is still no asynchronous
  load in the engine.
- The demos must build their scene in `Initialize`, on the new world before it loads; setting up a demo after
  `WorldLoaded` would silently lose its camera and physics settings (the cycle detects it).
- Gamepad navigation of the main screen relies on MGUI focus navigation (D-pad, A) and on the game reading A and Back; it
  was not checked with a real gamepad in this chantier.
