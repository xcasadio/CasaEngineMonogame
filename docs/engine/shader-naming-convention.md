# Shader Naming Convention

This convention keeps material shaders, debug utilities, 2D/blit shaders, and shared includes clearly separated in CasaEngineMonogame.

## Categories

| Category | Naming rule | Current examples | Notes |
| --- | --- | --- | --- |
| Material-facing shaders | PascalCase semantic name | `LitForward.fx`, `UnlitTexture.fx`, `skinEffect.fx` | Use names that describe the rendering role, not legacy engine analogies. |
| Debug/utility shaders | `Debug` prefix + explicit role | `DebugPrimitiveColor.fx`, `DebugSolidColor.fx` | Shared colored-primitive helpers stay outside the material architecture. |
| 2D/blit utility shaders | PascalCase role name | `SpriteBatch.fx`, `TexturedPrimitive.fx` | Name the rendering function directly rather than the historical file casing. |
| Shared includes | `.fxh` + semantic role | `Macros.fxh`, `Lighting.fxh`, `Structures.fxh` | Favor role-driven names; broader include renames can happen separately when churn is justified. |
| Legacy/archived shaders | remove from shipping content when unused | `axisComponent.fx`, `simple.fx` | Do not keep dead utility shaders in MGCB unless a compatibility need is explicit. |

## Applied decisions

- `basicEffect.fx` has already been renamed to `LitForward.fx` to avoid confusion with MonoGame `BasicEffect`.
- Dead legacy utility shaders (`axisComponent.fx`, `simple.fx`) were removed from shipping content once their consumers disappeared.
- `spritebatch.fx` is now `SpriteBatch.fx` so the active 2D utility shader follows the same explicit naming convention as the other retained utility effects.
- `TexturedPrimitive.fx` was introduced under the 2D/blit rule rather than the `Debug` prefix: it is a shipping UI rendering path, not a tooling helper.

## Practical rules

1. Introduce new material shaders with a semantic PascalCase name tied to the render family or pass.
2. Introduce new debug shaders with the `Debug` prefix only when they are renderer/tooling helpers rather than materials.
3. Prefer removing dead shader assets from MGCB over keeping ambiguous legacy names around.
4. Only rename shared include files when the benefit exceeds the churn across all consumers.
