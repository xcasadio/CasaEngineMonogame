# ADR-0102: PBR scope extends to bloom, transparency, skinning and local probes

- **Status**: Accepted
- **Date**: 2026-11-14
- **Source**: this chantier: `ai-agent/tasks/pbr-rendering-tasks.md` (decisions D8 to D12), from the author's answers of 2026-11-14
- Renumbered from ADR-0094 on 2026-11-15 when origin/main was merged (ADR-0095 to ADR-0099 now carry the renumbered lighting decisions)

## Context

ADR-0013 locked the scope of the PBR work on 2026-08-09. Its plan was never executed and went stale after ADR-0086, ADR-0087 and ADR-0097 to ADR-0101 (frozen frame, buffered views, ambient term, detail texture, mipmaps, shadow settings). A first consumer, the RacingGame port, needs glass, a glow and reflective props, which the 2026-08-09 scope left out. Skinned meshes are drawn by `SkinnedMeshRendererComponent`, outside `ForwardRenderPipeline`.

## Decision

- A bloom pass on the HDR scene target is part of the PBR work.
- Transparent PBR materials are part of the PBR work.
- PBR on skinned meshes is part of the PBR work. This replaces the "out of scope for V1" point of ADR-0013. Skinned PBR stays forward and is drawn in the existing skinned draw path, outside `ForwardRenderPipeline`: the "forward only, inside `ForwardRenderPipeline`" point of ADR-0013 is amended for skinned meshes only.
- Prefiltering of local reflection probes is part of the PBR work.
- The plan stops at the engine and is validated by an engine demo. Switching a game to PBR is a separate plan in that game's repository.
- The 2026-08-09 plan is archived and replaced by `ai-agent/tasks/pbr-rendering-tasks.md`.

Every other point of ADR-0013 stands: PBR per material next to `lit-diffuse`, metallic-roughness, forward only, unchanged light ceilings, pixel lighting only, a per-world colour pipeline whose default is `Legacy`, existing macro and technique conventions.

## Consequences

- The work is larger: eleven phases instead of seven. Each phase is reviewed before execution and verified after.
- Bloom requires the UI to be composed after tonemapping, which changes where the view pipeline composes it in the linear mode only.
- Skinned PBR adds techniques to `skinEffect.fx` instead of moving skinned meshes into `ForwardRenderPipeline`; a skinned normal map stays out of scope.
- Transmission, refraction, clear coat and the other glTF material extensions stay out of scope.
