# SampleProject

A project with no gameplay DLL. Its menu opens, wires its buttons and changes world from data alone (ADR-0078).

## Run

```bash
dotnet run --project CasaEngine.Launcher -- Projects/SampleProject/SampleProject.json
```

## Controls

- Mouse: click a button.
- Keyboard: the arrow keys move the focus, Enter activates the focused button.

## What to look at

- `SampleProject.json`: `FirstScreenName` names the screen opened after the first world.
- `Screens/main-menu.uiscreen`: `default_focus` and the commands of `Play`, `Options` and `Quit`.
- `Screens/menu-popup.uiscreen`: a modal screen that closes itself.
- `NextWorld.world`: the empty world `Play` loads, with a solid background. No screen opens there.

The format is described in `docs/engine/data-driven-screens.md`.
