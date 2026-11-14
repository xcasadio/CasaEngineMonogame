# Demos: what to check by eye

Notes for the demos whose result is judged by looking at them. Each demo's controls and the things to look at are also
on its sheet, on the main screen of `CasaEngine.Demos` and in the in-demo help panel (F1): see
[demos-main-menu.md](demos-main-menu.md). These notes replace the `.md` files that used to sit beside the demos.

Start a demo directly with `CASAENGINE_START_DEMO=<title>`, from `CasaEngine.Demos/`.

## Cutscene MoveTo demo

The red cube leaves the blue marker and reaches the yellow one, driven by the asset
`Content/Cutscenes/move_to_direct.cutscene`.

- Space restarts the cutscene from the start, S stops it, R puts the actor back on the start marker.
- Check: the cube visibly moves; a manual stop leaves no stale state; the control mode is restored; a restart works.

## Cutscene NavigateTo demo

Same scene, with the asset `Content/Cutscenes/navigate_to_grid.cutscene`. The cube holds a real `NavigationGrid2D` in
its `NavigationAgentComponent`, so the movement goes through the cutscene action `NavigateTo`, the navigation agent
and the `CharacterControllerNavigationDriverComponent`.

- Same keys as the MoveTo demo.
- Check: the cube follows the path; a manual stop works; control returns to the player; a restart leaves nothing
  behind.

## Environment showcase

One world uses a global environment cubemap for its ambient and specular light while the visible background of the
view is switched independently.

- B switches the background between the cubemap and a plain colour, E cycles the environment cubemap, L turns the
  environment lighting on or off.
- Check: with B the background changes while the reflections and the ambient tint still come from the current
  cubemap; with L the environment contribution changes while the background stays.

Capture used for the check:

```powershell
$env:CASAENGINE_START_DEMO = 'Environment showcase'
$env:CASAENGINE_CAPTURE_SCREENSHOT_PATH = 'artifacts/validation/environment-showcase-demo.png'
$env:CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS = '1500'
dotnet run --project CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug --no-build
```

## Skinned mesh demo

Animated skinned characters with forward shadows, in three columns (`SkinnedMeshDemo.cs`). Every character uses
linear blend skinning.

- Left: the character casts its shadow on the receiver. Its material is a per-component override, tinted orange
  (`SkinnedMeshComponent.Material`).
- Centre: same caster, `ReceiveShadows` off on the receiver.
- Right: `CastShadows` off on the caster.

Start it with `CASAENGINE_START_DEMO=Skinned mesh demo`; add `CASAENGINE_CAPTURE_SCREENSHOT_PATH` to capture a frame
and exit.
