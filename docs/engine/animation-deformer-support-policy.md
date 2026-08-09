# Animation Deformer Support Policy

This note defines which deformation paths the current animation pipeline supports, which ones are converted at import time, and which ones are intentionally ignored.

## Supported deformers

- Bone skinning authored as mesh bone weights is supported end to end.
- The runtime supports both linear blend skinning and dual quaternion skinning on the GPU.
- Blend-shape style morph targets are supported when Assimp exposes them through mesh animation attachments plus morph animation channels.

## Import conversions

- Assimp mesh bones are converted into the existing rigged-model bone palette and runtime skeleton data.
- Assimp mesh animation attachments are converted into MorphTarget runtime objects.
- Assimp mesh morph animation channels are converted into MorphClip, MorphChannel, and MorphKeyframe runtime objects.
- Empty Assimp UV and color channels are filtered during import so fixed-size Assimp channel arrays do not inflate runtime data.

## Runtime application order

- Morph deltas are applied on the CPU in mesh local space first.
- The morphed vertices are then sent through the existing GPU skinning path.
- This order is identical for linear blend skinning and dual quaternion skinning.

## Explicit limitations

- Runtime morph application only targets the single UV set and single vertex-color channel exposed by VertexPositionTextureNormalTangentWeights. Additional UV or color channels are preserved in MorphTarget data but are not applied by the current renderer.
- Morph sampling currently follows direct clip playback and cross-fade transitions only. Animation graphs, layer masks, additive layers, and IK do not contribute extra morph blending yet.
- Runtime morphing uses per-instance dynamic vertex buffers. Shared RiggedModel asset vertices are never mutated at draw time.
- Mesh bounds are not expanded dynamically from morph weights; culling and editor bounds still rely on imported mesh extents.

## Unsupported deformers

- Deformer families that do not lower to bone skinning or Assimp mesh attachments are not supported.
- This includes lattice-style deformers, wire deformers, muscle systems, cloth or physics-driven deformers, and cache-driven geometry deformation paths.
- The importer keeps the base mesh, skeleton, and supported animation data, but does not build runtime objects for those unsupported deformers.

## Fallback behavior

- If a morph animation channel cannot resolve a target mesh index, the runtime attempts a mesh-name fallback.
- If that fallback also fails, the channel is skipped instead of mutating the wrong mesh.
- Files that only rely on unsupported deformers still import, but the unsupported deformation result is absent at runtime.

## Future extension points

- Add graph-aware morph blending so blend trees and layered animation can drive morph weights alongside skeletal pose blending.
- Expand the vertex format and renderer if multiple UV sets or multiple color channels must be morphed at runtime.
- Revisit bounds inflation when morph-heavy content becomes important for culling or editor framing.