# ADR-0113: PBR scope extends to clear coat, screen-space ambient occlusion and FXAA

- **Status**: Proposed
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/pbr-clearcoat-ssao-fxaa-tasks.md` (decisions D1 to D3, arbitrations P1 and P2), from the author's request of 2026-11-15

## Context

ADR-0103 listed "transmission, refraction, clear coat and the other glTF material extensions" as out of scope of the PBR work, and the executed plan `ai-agent/tasks/pbr-rendering-tasks.md` did not add an anti-aliasing pass or an ambient occlusion pass. A car-paint material needs a varnish layer, and the `LinearHdr` image chain has no edge smoothing (no multisampling, `CasaEngineGame.cs:191`) and no contact darkening. The author chose FXAA over TAA and MSAA, and a clear coat on a dielectric for car paint.

## Decision

- The PBR scope extends to three engine features: FXAA after the tonemapping, a clear coat layer on the `lit-pbr` material, and screen-space ambient occlusion (SSAO) fed by a scene depth pre-pass. This amends the clear coat exclusion of ADR-0103; the other exclusions of ADR-0103 stay.
- FXAA and SSAO act only for a view that really renders through the HDR target (`LinearHdr`). In `Legacy` their settings are ignored. The clear coat is a property of the material: it follows `lit-pbr` in both pipelines, `Legacy` included.
- Every feature is off by default. FXAA and SSAO settings are additive keys of `ColorPipelineSettings`; the clear coat is two additive scalar keys of the `lit-pbr` definition. An existing file reads back unchanged. With the three features off (clear coat at 0) the image is bit-identical to the current one in both pipelines.
- The plan stops at the engine and is validated by engine demos. TAA, MSAA, clear coat on skinned meshes and SSAO limited to the ambient term stay out of scope.
- The detailed designs are recorded by the tasks that build them: ADR-0114 (FXAA), ADR-0115 (clear coat), ADR-0116 (scene depth pre-pass and SSAO).

## Consequences

- Reference fingerprints of the plan: the `Legacy` captures, the `LinearHdr` colour pipeline capture, and the PBR demo validation capture in both pipelines, recorded in the plan under task T0.1. A task that touches a shader or the image path must reproduce them with the features off.
- Skinned meshes get no clear coat, and the clear coat does not read the local probes (written limits, ADR-0115).
- Views that render scene and interface in one block (editor views) get no FXAA (ADR-0114).
