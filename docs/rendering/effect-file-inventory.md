# Effect File Inventory

This inventory distinguishes material-facing shaders from debug and utility shaders and records the current refactor risk for each file.

## Effect files (`.fx`)

| File | Type | Current consumers | Role | Refactor risk |
| --- | --- | --- | --- | --- |
| `LitForward.fx` | material-facing | `StaticMeshRendererComponent` | main lit static-mesh effect with many technique permutations | high |
| `UnlitTexture.fx` | material-facing | `StaticMeshRendererComponent` | unlit material effect for textured or colored draws | medium |
| `skinEffect.fx` | material-facing | `SkinnedMeshRendererComponent` | skinned mesh effect with its own techniques and lighting bindings | high |
| `spritebatch.fx` | utility/2D | `SpriteRendererComponent` | sprite batching and textured quad rendering | medium |
| `DebugPrimitiveColor.fx` | debug utility | `Line3dRendererComponent`, `DebugGridComponent`, `DebugAxisComponent`, editor gizmo line/selection path | shared vertex-color debug primitive shader | medium |
| `DebugSolidColor.fx` | debug utility | editor gizmo solid meshes/quads | shared solid-color debug shader | medium |

## Include files (`.fxh`)

| File | Included by | Role | Refactor risk |
| --- | --- | --- | --- |
| `Macros.fxh` | `LitForward.fx`, `UnlitTexture.fx`, `skinEffect.fx` | shared macros, texture declarations, technique helpers | high |
| `Structures.fxh` | `LitForward.fx` | shared vertex/pixel structs used by the lit static effect | medium |
| `Lighting.fxh` | `LitForward.fx`, `skinEffect.fx` | shared forward-lighting helpers and directional-light evaluation | high |

## Consumer notes

- `LitForward.fx` is the critical static-rendering shader and now carries a semantic name distinct from MonoGame `BasicEffect`.
- `skinEffect.fx` is still isolated from the main material/shader policy: it is loaded directly by the skinned renderer instead of being resolved through the same path as static materials.
- `DebugPrimitiveColor.fx` is now the shared replacement for former MonoGame `BasicEffect` usages in debug/runtime overlays.
- `DebugSolidColor.fx` is currently used by the editor gizmo for solid meshes and translucent quads.
- `axisComponent.fx` and `simple.fx` no longer have direct C# consumers and have been removed from the shipping MGCB content list.

## Refactor guidance

1. Treat `LitForward.fx`, `skinEffect.fx`, and `Lighting.fxh` as architecture-critical files.
2. Treat `spritebatch.fx`, `DebugPrimitiveColor.fx`, and `DebugSolidColor.fx` as utility shaders that should keep clear, explicit names.
3. Treat any reintroduction of `axisComponent.fx` or `simple.fx` as an explicit compatibility decision rather than default shipping content.