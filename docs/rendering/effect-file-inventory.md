# Effect File Inventory

This inventory distinguishes material-facing shaders from debug and utility shaders and records the current refactor risk for each file.

## Effect files (`.fx`)

| File | Type | Current consumers | Role | Refactor risk |
| --- | --- | --- | --- | --- |
| `basicEffect.fx` | material-facing | `StaticMeshRendererComponent` | main lit static-mesh effect with many technique permutations | high |
| `UnlitTexture.fx` | material-facing | `StaticMeshRendererComponent` | unlit material effect for textured or colored draws | medium |
| `skinEffect.fx` | material-facing | `SkinnedMeshRendererComponent` | skinned mesh effect with its own techniques and lighting bindings | high |
| `spritebatch.fx` | utility/2D | `SpriteRendererComponent` | sprite batching and textured quad rendering | medium |
| `DebugPrimitiveColor.fx` | debug utility | `Line3dRendererComponent`, `DebugGridComponent`, `DebugAxisComponent`, editor gizmo line/selection path | shared vertex-color debug primitive shader | medium |
| `DebugSolidColor.fx` | debug utility | editor gizmo solid meshes/quads | shared solid-color debug shader | medium |
| `axisComponent.fx` | legacy debug utility | no direct runtime consumer found after `DebugPrimitiveColor.fx` migration | former axis/debug line shader | low |
| `simple.fx` | legacy utility | no direct runtime consumer found in current repo | old textured utility shader | low |

## Include files (`.fxh`)

| File | Included by | Role | Refactor risk |
| --- | --- | --- | --- |
| `Macros.fxh` | `basicEffect.fx`, `UnlitTexture.fx`, `skinEffect.fx` | shared macros, texture declarations, technique helpers | high |
| `Structures.fxh` | `basicEffect.fx` | shared vertex/pixel structs used by the lit static effect | medium |
| `Lighting.fxh` | `basicEffect.fx`, `skinEffect.fx` | shared forward-lighting helpers and directional-light evaluation | high |

## Consumer notes

- `basicEffect.fx` remains the critical static-rendering shader and still carries the naming ambiguity with MonoGame `BasicEffect`.
- `skinEffect.fx` is still isolated from the main material/shader policy: it is loaded directly by the skinned renderer instead of being resolved through the same path as static materials.
- `DebugPrimitiveColor.fx` is now the shared replacement for former MonoGame `BasicEffect` usages in debug/runtime overlays.
- `DebugSolidColor.fx` is currently used by the editor gizmo for solid meshes and translucent quads.
- `axisComponent.fx` is no longer loaded by `DebugAxisComponent`; it is a strong candidate for removal or archival after the naming-convention pass.
- `simple.fx` currently has no direct C# consumer in the repository and should be reviewed before keeping it in the shipping content set.

## Refactor guidance

1. Treat `basicEffect.fx`, `skinEffect.fx`, and `Lighting.fxh` as architecture-critical files.
2. Treat `spritebatch.fx`, `DebugPrimitiveColor.fx`, and `DebugSolidColor.fx` as utility shaders that should keep clear, explicit names.
3. Treat `axisComponent.fx` and `simple.fx` as cleanup candidates unless a hidden external/content consumer is reintroduced.