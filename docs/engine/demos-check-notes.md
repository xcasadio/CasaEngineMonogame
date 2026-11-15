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

Animated skinned characters in three pairs (`SkinnedMeshDemo.cs`): a large character in front, with linear blend
skinning, and a small one behind, with dual quaternion skinning. Every 2.5 seconds all of them change animation,
alternating a cross-fade and an inertialized transition.

- Left pair: the front character has a per-component material override, tinted orange
  (`SkinnedMeshComponent.Material`).
- The world turns shadows on (`EnvironmentSettings.Shadows.Enabled`) and the key light casts them. Each pair stands
  on its own ground and sets `CastShadows` and `ReceiveShadows` differently:
  - left: the front character casts, the ground and the back character receive. Its shadow shows on both and follows
    the animation;
  - centre: `ReceiveShadows` is off on the ground and on the back character. No shadow shows on them;
  - right: `CastShadows` is off on the front character. It has no shadow.
- Check: the three pairs differ as described; the shadow of the left pair moves with the character.
- Press `P` (action `SkinnedMesh.Pbr`) to switch every character between Blinn-Phong and the PBR techniques of the
  skinning effect (`SkinnedMeshComponent.PbrMaterial`, ADR-0108); the PBR look has no specular tint and darker lit
  sides than the Blinn-Phong one, and the shadows are unchanged.

Start it with `CASAENGINE_START_DEMO=Skinned mesh demo`; add `CASAENGINE_CAPTURE_SCREENSHOT_PATH` to capture a frame
and exit. Measurement switches (all optional): `CASAENGINE_SKINNED_DEMO_FREEZE=1` pauses every character at the same
instant of its first clip, so two captures of one mode are identical; `CASAENGINE_SKINNED_DEMO_PBR=1` starts in the
PBR mode; `CASAENGINE_SKINNED_DEMO_SHADOWS=0` turns the shadows of the world off. The pixel rectangles and thresholds of
the measurements are the constants of `SkinnedMeshDemo.Measurement`.

## PBR material demo

The reference scene of the `lit-pbr` material (`PbrMaterialDemo.cs`, ADR-0106 to ADR-0109), in the `LinearHdr` colour
pipeline with the `AcesApprox` operator, under a procedural grey studio cube (image-based lighting and sky) and one sun
that casts shadows:

- a 5 x 5 grid of spheres that share ONE `LitPbrMaterial` and one model and differ only by the property block of their
  instance (`MaterialPropertyBlock`, the per-instance override path of the material system): roughness 0.1 to 0.9 along a
  row, metal 0 to 1 down a column;
- the specular probe row in front of the grid: the same material with a black base colour and no metal, so the only light
  on a sphere is the sun lobe (the highlight widens with the roughness);
- a coloured smooth metal sphere (it reflects the studio in its own colour), an emissive sphere (the bloom adds its glow),
  a glass sphere in front of a checkerboard (transparent `lit-pbr`), and the skinned character of `SkinnedMeshDemo` drawn
  by the PBR skinning techniques (direct light and shadows, no image-based lighting, ADR-0108).
- Keys: `I` turns the image-based lighting on or off (off: no reflection and a flat ambient colour; the cube still draws
  the sky), `B` the bloom, `T` the tonemapping operator, `+` and `-` the exposure.

Start it with `CASAENGINE_START_DEMO=PBR material demo`. Measurement switches (all optional, everything is on by
default): `CASAENGINE_PBR_DEMO_VALIDATION=1` fixes the camera, freezes the skinned animation and ignores the keys, so a
capture needs no key press and two captures are identical; `CASAENGINE_PBR_DEMO_IBL=0` switches the image-based
lighting off; `CASAENGINE_PBR_DEMO_BLOOM=0` the bloom; `CASAENGINE_PBR_DEMO_GLASS=0` and `CASAENGINE_PBR_DEMO_SKINNED=0`
remove the glass sphere and the skinned character. The measurement pixels, segments and thresholds are the constants of
`PbrMaterialDemo.Measurement`; a capture for them runs past frame 600 (`CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS=12000`),
when the demo logs the generation counter of the IBL cache. `CASAENGINE_THEN_DEMO=<title>` (and
`CASAENGINE_THEN_DEMO_FRAME`, default 120) makes the game switch to another demo after that many updates of the first
one, to check that leaving a demo leaves nothing behind.
