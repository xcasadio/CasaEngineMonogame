# Animation IK Demo

`AnimationIkDemo` is the dedicated runtime showcase for the new two-bone IK solver.

What it demonstrates:
- real skinned character content driven by the modern animation runtime
- automatic selection of a valid two-bone chain from the loaded skeleton
- runtime IK target driving with live weight changes
- debug visualization of the chain, target, and pole vector

Controls:
- `Space`: toggle automatic target orbit
- `I`: enable or disable IK
- `V`: toggle the skeleton/pose debug overlay
- `Left/Right/Up/Down`: move the target when orbit is disabled
- `PageUp/PageDown`: move the target vertically when orbit is disabled
- `O` / `P`: decrease or increase IK weight
- `Backspace`: reset the target from the current pose
- `R`: reset actor transform and target

Implementation notes:
- the demo uses `SkinnedMeshComponent.SetTwoBoneIkConstraint(...)`
- constraints are applied during `RiggedModel` pose post-processing, after animation evaluation and before the final skinned pose is pushed to nodes
- the current sample uses `Content\\SkinnedMesh\\kid_idle.model` so the IK solver is demonstrated on real content without procedural placeholder geometry