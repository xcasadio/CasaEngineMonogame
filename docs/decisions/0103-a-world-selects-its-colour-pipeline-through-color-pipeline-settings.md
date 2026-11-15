# ADR-0103: A world selects its colour pipeline through colour pipeline settings

- **Status**: Proposed
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/pbr-rendering-tasks.md` (decision D6, task T1.1)
- Renumbered from ADR-0095 on 2026-11-15 when origin/main was merged (the renumbered lighting decisions are ADR-0097 to ADR-0101 today)

## Context

Every image is lit and written in display space straight into the view's target: nothing in the engine knows about linear light, high dynamic range or tonemapping (`ai-agent/tasks/pbr-rendering-tasks.md`, section "State of the repository"). The PBR chantier needs a linear HDR path, but ADR-0013 requires that existing content never changes: the new path must be chosen explicitly, per world, and per view through the environment override. World-level rendering settings already follow one pattern: a mutable `WorldEnvironmentSettings`, resolved by `EnvironmentResolver` (view override replaces the world settings as a whole) into the immutable `ResolvedEnvironmentSettings`, cached while `IsDirty` is false and the versions are unchanged (`AmbientSource`, ADR-0098; `ShadowSettings`, ADR-0101).

## Decision

- Add `WorldEnvironmentSettings.ColorPipeline` (read-only property, class `ColorPipelineSettings`, modelled on `ShadowSettings`) with three fields:
  - `Mode`: `ColorPipelineMode.Legacy` (default, today's behaviour) or `LinearHdr`;
  - `Tonemapper`: `TonemapOperator.AcesApprox` (default) or `Reinhard`;
  - `Exposure`: scalar, default 1; a negative or non-finite value becomes 0.
- Serialized under the key `color_pipeline` (object with `mode`, `tonemapper`, `exposure`), read case-insensitively, written only when a field differs from its default and removed otherwise, so existing worlds keep their format. `ResetToDefaults`, `Load`, `Save`, `Clone` and `CopyFrom` handle it.
- `EnvironmentResolver` copies the three values to `ResolvedEnvironmentSettings` (`ColorPipelineMode`, `Tonemapper`, `Exposure`, and the derived `IsLinearHdr`); `CreateLegacy` carries `Legacy` and an exposure of 1. The view override replaces the settings as a whole, like every other environment field; no key is added to `ResolvedEnvironmentCache`, a change followed by `MarkDirty` invalidates it.
- The editor's world details panel gets a mode combo, a tonemapper combo and an exposure field, undoable like the neighbouring environment fields.
- This decision only stores and resolves the setting. No renderer reads it yet: the HDR scene target, the tonemapping and the colour decoding are separate decisions of the same chantier.

## Consequences

- No change for existing worlds and assets: `Legacy` is the default and the key is absent from saved files; no image changes.
- A game or demo can select the linear pipeline per world, or per view through `EnvironmentOverride`.
- Older engine versions reading a world saved with `color_pipeline` ignore the unknown key and render in the legacy pipeline.
- While nothing reads the setting, selecting `LinearHdr` has no visible effect.
