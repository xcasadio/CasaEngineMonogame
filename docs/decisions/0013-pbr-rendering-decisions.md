# ADR-0013: PBR rendering decisions

- **Status**: Accepted
- **Date**: 2026-08-09
- **Source**: `ai-agent/tasks/pbr-rendering-implementation-plan.md:25-32`

## Context

CasaEngine's only lighting model is Blinn-Phong (`SpecularPower` and `pow(dot(H,N), SpecularPower)` in `CasaEngine/Content/Shaders/Lighting.fxh`, shared by `LitForward.fx` and `skinEffect.fx`); there is no linear/sRGB/HDR/tonemapping workflow. Two built-in material definitions exist today, `lit-diffuse` and `unlit-texture`, in an extensible `MaterialDefinitionRegistry`. glTF import already reads the `MetallicRoughness` channel but degrades it to Blinn-Phong via `ConvertRoughnessToSpecularPower` (source: `ai-agent/tasks/pbr-rendering-implementation-plan.md:11-19`). A plan for adding a metallic-roughness PBR lighting model was written and its scope decisions were locked with the author on 2026-08-09.

## Decision

- PBR is chosen per material, not per game or per level: `lit-pbr` is an additional definition alongside the existing `lit-diffuse`, which stays intact, and existing content must show no visual regression (source: `ai-agent/tasks/pbr-rendering-implementation-plan.md:25`).
- The workflow is metallic-roughness (glTF-aligned), not specular-glossiness (source: `ai-agent/tasks/pbr-rendering-implementation-plan.md:26`).
- Forward only: PBR stays inside `ForwardRenderPipeline` (Opaque/Transparent passes); no deferred, no clustered (source: `ai-agent/tasks/pbr-rendering-implementation-plan.md:27`).
- The existing light ceilings (8 directional + 8 point + 8 spot, an mgfxc constraint) do not change (source: `ai-agent/tasks/pbr-rendering-implementation-plan.md:28`).
- `LitPbr.fx` is pixel lighting only; no vertex-lighting variant (source: `ai-agent/tasks/pbr-rendering-implementation-plan.md:29`).
- The color pipeline (linear/HDR/tonemap) is a per-World/per-view setting via `WorldEnvironmentSettings`, not a per-material one; its default is `Legacy` (current behavior unchanged) (source: `ai-agent/tasks/pbr-rendering-implementation-plan.md:30`).
- The existing technique/macro conventions (`Macros.fxh`) and the compilation targets already used by `LitForward.fx` (mgfxc compatibility) must be respected (source: `ai-agent/tasks/pbr-rendering-implementation-plan.md:31`).
- PBR skinning (`skinEffect` in GGX) is out of scope for V1, noted as a future extension (source: `ai-agent/tasks/pbr-rendering-implementation-plan.md:32`).

## Consequences

- Adding `lit-pbr` must not regress `lit-diffuse` or any existing content; both definitions coexist in `MaterialDefinitionRegistry` (source: `ai-agent/tasks/pbr-rendering-implementation-plan.md:25`).
- Keeping the color pipeline default at `Legacy` means turning on linear/HDR/tonemapping is an explicit per-World opt-in, not a global behavior change (source: `ai-agent/tasks/pbr-rendering-implementation-plan.md:30`).
- Skinned meshes will not get a PBR lighting path in V1; they keep the Blinn-Phong `skinEffect` until a future extension (source: `ai-agent/tasks/pbr-rendering-implementation-plan.md:32`).
- Implementation pending: plan `ai-agent/tasks/pbr-rendering-implementation-plan.md`, no task started. Verified in code: a repository-wide search found no `lit-pbr`, `LitPbr` or `ColorPipelineSettings` symbol in any `.cs` or `.fx` file; the plan's own task list (`ai-agent/tasks/pbr-rendering-implementation-plan.md:61-302`) marks every PBR task `⏳` (pending), with no task marked done.
