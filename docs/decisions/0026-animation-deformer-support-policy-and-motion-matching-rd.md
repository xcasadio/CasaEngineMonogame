# ADR-0026: Animation deformer support policy and motion matching as a separate R&D stream

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `docs/engine/animation-deformer-support-policy.md:1-40`; `docs/engine/animation-motion-matching.md:118-125`

## Context

`docs/engine/animation-deformer-support-policy.md` defines which deformation paths the animation pipeline supports, which are converted at import time, and which are intentionally ignored. `docs/engine/animation-motion-matching.md` records a decision on whether to pursue motion matching now or later, after listing the current controller-related runtime and test files it would affect (`docs/engine/animation-motion-matching.md:110-116`).

## Decision

- Supported deformers: bone skinning authored as mesh bone weights, end to end; both linear blend skinning and dual quaternion skinning on the GPU; blend-shape style morph targets when Assimp exposes them through mesh animation attachments plus morph animation channels (source: `docs/engine/animation-deformer-support-policy.md:7-11`).
- Import conversions: Assimp mesh bones become the existing rigged-model bone palette and runtime skeleton data; Assimp mesh animation attachments become `MorphTarget` runtime objects; Assimp mesh morph animation channels become `MorphClip`, `MorphChannel`, and `MorphKeyframe` runtime objects; empty Assimp UV and color channels are filtered at import (source: `docs/engine/animation-deformer-support-policy.md:13-18`).
- Runtime application order: morph deltas are applied on the CPU in mesh local space first, then the morphed vertices go through the existing GPU skinning path; this order is identical for linear blend and dual quaternion skinning (source: `docs/engine/animation-deformer-support-policy.md:20-24`).
- Unsupported deformers: any deformer family that does not lower to bone skinning or Assimp mesh attachments, including lattice-style, wire, muscle, cloth or physics-driven deformers, and cache-driven geometry deformation; the importer keeps the base mesh, skeleton, and supported animation data but does not build runtime objects for these (source: `docs/engine/animation-deformer-support-policy.md:33-37`).
- Motion matching is worth keeping on the roadmap, but only as a separate R&D stream; the production upgrade path continues to rely on the current controller, blend spaces, advanced cross-fades, root motion, and IK until a dedicated motion matching prototype proves its value (source: `docs/engine/animation-motion-matching.md:124-125`).

## Consequences

- Explicit runtime limitations recorded alongside the deformer policy: runtime morph application only targets the single UV set and single vertex-color channel exposed by `VertexPositionTextureNormalTangentWeights`, so additional channels are preserved in `MorphTarget` data but not applied by the current renderer; morph sampling follows only direct clip playback and cross-fade transitions, not animation graphs, layer masks, additive layers, or IK; runtime morphing uses per-instance dynamic vertex buffers and never mutates shared `RiggedModel` asset vertices; mesh bounds are not expanded dynamically from morph weights, so culling and editor bounds still rely on imported mesh extents (source: `docs/engine/animation-deformer-support-policy.md:26-31`).
- Fallback behavior: if a morph animation channel cannot resolve a target mesh index, the runtime attempts a mesh-name fallback; if that also fails, the channel is skipped instead of mutating the wrong mesh (source: `docs/engine/animation-deformer-support-policy.md:38-40`).
- Motion matching work is not scheduled as production work; it stays exploratory until a prototype demonstrates value, so the current controller/blend-space/cross-fade/root-motion/IK path remains the supported production path.
- Implementation status observed in code: `MorphTarget` (`CasaEngine/Framework/Animations/MorphTarget.cs`) and `SkinnedMeshAnimationRuntime` (`CasaEngine/Framework/Animations/SkinnedMeshAnimationRuntime.cs`) exist, along with `SkinnedMesh`, `SkinnedMeshComponent`, and `SkinnedMeshRendererComponent`, matching the supported-deformer decision. No dedicated motion-matching runtime files were searched for beyond the roadmap status; this ADR does not claim to verify the R&D stream's current state (unverified beyond the decision text itself).
