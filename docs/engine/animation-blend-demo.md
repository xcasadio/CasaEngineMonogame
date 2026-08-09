# Animation Blend Demo

Objective: demonstrate the modern animation runtime with a real showcase page flow over the existing `kid_idle`, `kid_walk`, and `kid_run` FBX clips plus a few procedural helper clips.

Controls:
- `Tab`: move to the next showcase page.
- `Shift + Tab`: move to the previous showcase page.
- `Backspace`: reset the actor transform and the root-motion trail.
- `Blend space 1D`: `W` walks, `Shift + W` runs.
- `Blend space 2D`: `W`, `A`, `D` explore the triangle and hull clamping.
- `Cross-fade`: `1`, `2`, `3` cross-fade between idle, walk, and run.
- `Upper-body override`: `Space` triggers a masked upper-body action over locomotion, `Q` / `E` adjust layer weight.
- `Additive + root motion`: `Space` triggers a root-motion burst, `R` toggles observe-only vs apply-to-entity, `W` / `Shift + W` still drive the base locomotion graph.
- `F1`: show or hide the demo info overlay.

How to launch:
- Open `CasaEngine.Demos` and select `Animation blend demo` from the demo picker.
- Or, from the `CasaEngine.Demos` directory, start `CasaEngine.Demos` with `CASAENGINE_START_DEMO="Animation blend demo"`.

Implementation notes:
- The demo loads the three raw FBX files directly at runtime.
- The displayed mesh keeps the idle geometry/material setup, then rebinds the walk and run clips onto the same runtime skeleton instance before building the blend graphs.
- The 2D page still uses a technical three-sample triangle because the content set does not currently ship dedicated strafe clips.
- The layered/additive pages add small procedural clips on top of the authored locomotion set so the demo can validate masked override playback, additive blending, animation events, and root-motion extraction without waiting on a larger authored clip library.
- Root-motion apply mode now removes the sampled root transform from the output pose, so consuming and applying the delta to the entity does not double the movement.