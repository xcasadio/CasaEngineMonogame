# ADR-0010: Shader naming convention and applied renames

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `docs/engine/shader-naming-convention.md:17`, `docs/engine/shader-naming-convention.md:18`, `docs/engine/shader-naming-convention.md:19`, `docs/engine/shader-naming-convention.md:20`

## Context

The repository mixes material shaders, debug/utility shaders, 2D/blit shaders and shared includes, previously without a consistent naming rule. `docs/engine/shader-naming-convention.md` fixes the naming rule per category (material-facing PascalCase semantic names, `Debug`-prefixed utility shaders, PascalCase 2D/blit utility shaders, `.fxh` shared includes) and records four renames/removals already applied to bring existing shaders into line with it.

## Decision

- `basicEffect.fx` was renamed to `LitForward.fx` to avoid confusion with MonoGame's own `BasicEffect` (source: `docs/engine/shader-naming-convention.md:17`).
- Dead legacy utility shaders `axisComponent.fx` and `simple.fx` were removed from shipping content once their consumers disappeared (source: `docs/engine/shader-naming-convention.md:18`).
- `spritebatch.fx` was renamed to `SpriteBatch.fx` so the active 2D utility shader follows the same explicit naming convention as the other retained utility effects (source: `docs/engine/shader-naming-convention.md:19`).
- `TexturedPrimitive.fx` was introduced under the 2D/blit rule rather than the `Debug` prefix, because it is a shipping UI rendering path, not a tooling helper (source: `docs/engine/shader-naming-convention.md:20`).

## Consequences

- New material shaders must use a semantic PascalCase name tied to the render family or pass; new debug shaders use the `Debug` prefix only when they are renderer/tooling helpers rather than materials (source: `docs/engine/shader-naming-convention.md:23-25`).
- Implementation status verified in code: `CasaEngine/Framework/Rendering/Shaders/BuiltInShaderCatalog.cs` registers `Shaders/LitForward.fx` (line 22), `Shaders/SpriteBatch.fx` (line 27) and exposes `TexturedPrimitiveContentName = "Shaders\\TexturedPrimitive"` (line 18), consistent with the three renames/additions above.
- `axisComponent.fx` and `simple.fx` are no longer part of `CasaEngine`'s shipping shader content; a search of the repository found no reference to either name in any `.mgcb`, `.cs` or `.json` file. Files with these names still exist under `Projects/RPGDemo/Shaders/` (a sample project, not engine shipping content) — unverified whether that project's own build wires them in; this is outside the scope of the cited decision, which concerns the engine's shipping content.
