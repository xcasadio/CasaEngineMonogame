# ADR-0098: A world may use a flat ambient term while keeping a reflection cube

- **Status**: Accepted
- **Date**: 2026-11-14
- **Source**: this chantier: `ai-agent/tasks/lighting-ambient-shadow-tasks.md` (decision D8, task E2), approved with the RacingGame plan `ai-agent/tasks/rgce-3d-lighting-shadows-tasks.md`
- Renumbered from ADR-0090 on 2026-11-15 when origin/main was merged (its ADR-0090 is another decision)

## Context

When a world has a specular environment cube, `EnvironmentShaderBinder` sets `HasEnvironmentCubeTexture = 1` and the lit shaders compute the ambient term from the cube: `cube(normal) * EnvironmentAmbientColor * material ambient` (`LitForward.fx` `ComputeBaseAmbientTerm`, `skinEffect.fx` `ComputeSkinnedAmbientContribution`); without a cube it is `AmbientColor * material ambient`. The same flag also drives the reflections (`SampleEnvironmentReflection`). A world that needs reflections therefore could not have a uniform, controllable ambient level: the ambient followed the cube's colours. The RacingGame conversion binds a reflection cube in its race world and must reproduce the original game's flat ambient term (`texture * (ambient + diffuse * N.L)`).

## Decision

- Add `WorldEnvironmentSettings.AmbientSource` (new enum `AmbientSource`): `Auto` (default, today's behaviour) or `Flat` (always `AmbientColor * AmbientIntensity * material ambient`, reflections from the cube unchanged).
- The field is optional in the world file: key `ambient_source`, read case-insensitively, absent means `Auto`, written only when it is not `Auto` so existing worlds keep their format. `Clone`, `CopyFrom`, `ResetToDefaults`, `Load` and `Save` handle it.
- `EnvironmentResolver` copies it to `ResolvedEnvironmentSettings.AmbientSource` (`UsesFlatAmbient`); `EnvironmentShaderBinder` binds it as the new uniform `UseFlatAmbientSource`, distinct from `HasEnvironmentCubeTexture`, declared in both `LitForward.fx` and `skinEffect.fx` at the same slot (`_ps(c36)`, `_cb(c32.z)`). The register annotations of the shaders are documentation only (empty macros in `Macros.fxh`); the slot was checked free in both files.
- The editor's world details panel gets an "Ambient Source" combo, undoable like the neighbouring environment fields.

## Consequences

- No change for existing worlds and assets: `Auto` is the default and the key is absent from saved files.
- A game can keep a reflection cube and still control its ambient exactly (RacingGame sets `Flat`).
- The choice is world-wide (and per view through `EnvironmentOverride`); there is no per-material switch.
- Older engine versions reading a world saved with `Flat` ignore the unknown key and fall back to the cube ambient.
- See `docs/engine/lit-ambient-and-ambient-source.md`.
