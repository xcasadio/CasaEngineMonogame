# Motion Matching Scope In CasaEngine

## Overview

Motion matching should stay a research layer on top of the modern animation runtime. The current production path already covers clip playback, blend spaces, layered blending, root motion, advanced cross-fades, events, and IK. A motion matching prototype must reuse those foundations without turning `AnimationController` into a monolith or blocking the shipping locomotion stack.

## Current Foundations

- Pose data is already structured around `SkeletonDefinition`, `SkeletonPoseLocal`, `SkeletonPoseModel`, and `BoneTransform`.
- Clip sampling is deterministic through `AnimationClipSampler`, `AnimationClip`, and typed joint tracks.
- Runtime playback already exposes reusable transition pieces through `AnimationController`, `AnimationPoseBlender`, `BlendSpace1DNode`, `BlendSpace2DNode`, and `AnimationCrossFadeSettings`.
- Root motion is already observed and consumed through `RootMotionDelta` and `RootMotionMode`.
- Layering, masks, events, and two-bone IK are already available and should remain orthogonal to any motion matching query system.
- Import-time clip preparation already exists through `RiggedModelLoader`, `EditorAssetImportService`, and `AnimationClipCompressor`.
- Existing validation patterns already exist in `CasaEngine.Tests/Animation/AnimationControllerTests.cs` and `CasaEngine.Tests/Animation/AnimationClipSamplerTests.cs`.
- Visual validation can reuse `CasaEngine.Demos/Demos/AnimationBlendDemo.cs` or a later dedicated demo.

## Missing Prerequisites

The repo is not ready for production motion matching yet. These prerequisites are still missing:

1. A reusable trajectory representation for past and future root samples.
2. Per-joint derivative extraction, not only root motion deltas.
3. A pose and trajectory distance metric with explicit bone weighting.
4. A frame-level database that indexes candidate poses across clips.
5. Lightweight metadata for clip families, tags, and optional phase hints.
6. A query API that stays separate from `AnimationController` and can evolve independently.

## Non-Blocking R&D Boundary

The initial motion matching phase should stay deliberately narrow:

- Keep the existing controller, blend spaces, and advanced cross-fades as the production path.
- Build the prototype as an opt-in query layer that returns candidate frames instead of rewriting playback.
- Start with brute-force search over a small database. Do not introduce acceleration structures in phase 1.
- Reuse the current `CrossFade()` path to transition into matched frames.
- Treat metadata authoring, phase detection, symmetry handling, learned similarity, and crowd-scale search as later phases.

This keeps motion matching outside the critical path while still letting the team validate whether the feature is worth pursuing.

## Recommended Phase 1 Architecture

Phase 1 should add pure, testable building blocks first:

```text
AnimationClip
  -> AnimationTrajectoryExtractor
  -> AnimationTrajectory[]
  -> MotionMatchingDatabase
  -> MotionMatchingQuery
  -> best candidate frame
  -> existing AnimationController.CrossFade()
```

Recommended new runtime files:

```text
CasaEngine/Framework/Animations/
  AnimationTrajectory.cs
  AnimationTrajectoryExtractor.cs
  AnimationDistanceMetric.cs
  MotionMatchingDatabase.cs
  MotionMatchingClip.cs
  MotionMatchingQuery.cs
```

Recommended first responsibilities:

- `AnimationTrajectory`: compact representation of sampled root positions, velocities, and facing over a short time window.
- `AnimationTrajectoryExtractor`: offline or load-time extraction from `AnimationClipSampler`.
- `AnimationDistanceMetric`: pure cost functions for pose and trajectory comparison.
- `MotionMatchingDatabase`: frame index over clips plus extracted features.
- `MotionMatchingQuery`: caller-provided pose, desired trajectory, and optional clip constraints.

## Integration Rules

To avoid breaking the runtime stack:

- Do not bake motion matching state directly into `AnimationController` in phase 1.
- Do not make motion matching a prerequisite for locomotion, cross-fades, or blend spaces.
- Do not add per-frame allocations in the runtime query path.
- Do not couple the database format to editor-only authoring code.
- Keep the first implementation deterministic and unit-testable.

## Validation Plan

The first real implementation phase should validate three things:

1. Pure tests for distance and ranking logic.
2. A small demo that visualizes the selected candidate and the active trajectory query.
3. Evidence that the prototype remains acceptable with a small clip corpus before any optimization work starts.

## Out Of Scope For Now

These subjects should stay out of the first motion matching milestone:

- Spatial acceleration structures.
- Automatic metadata authoring tools.
- Learned or neural similarity metrics.
- Production retargeting dependencies.
- Crowd or multi-character motion matching.
- Replacing the current controller and blend space pipeline.

## Repo References

Useful files when starting the implementation later:

- `CasaEngine/Framework/Animations/AnimationController.cs`
- `CasaEngine/Framework/Animations/AnimationClipSampler.cs`
- `CasaEngine/Framework/Animations/AnimationPoseBlender.cs`
- `CasaEngine/Framework/Animations/SkeletonPoseLocal.cs`
- `CasaEngine/Framework/Animations/RootMotionDelta.cs`
- `CasaEngine/Framework/Animations/AnimationClipCompressor.cs`
- `CasaEngine.Tests/Animation/AnimationControllerTests.cs`
- `CasaEngine.Tests/Animation/AnimationClipSamplerTests.cs`
- `CasaEngine.Demos/Demos/AnimationBlendDemo.cs`

## Decision

Motion matching is worth keeping on the roadmap, but only as a separate R&D stream. The production upgrade path should continue to rely on the current controller, blend spaces, advanced cross-fades, root motion, and IK until a dedicated motion matching prototype proves its value.