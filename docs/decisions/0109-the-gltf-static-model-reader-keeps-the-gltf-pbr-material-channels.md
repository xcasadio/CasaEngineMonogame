# ADR-0109: The glTF static model reader keeps the glTF PBR material channels

- **Status**: Proposed
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/pbr-rendering-tasks.md` (arbitration P12, task T9.1; completed by task T9.2)

## Context

`GltfStaticModelReader` reads a glTF/GLB file into a `StaticModel` plus one `StaticModelImportedMaterial` per material. Until now it kept only what the Blinn-Phong `lit-diffuse` material needs: the base colour factor and texture, the normal texture, the emissive factor, and a specular power derived from the roughness factor. The metallic factor, the roughness factor itself, the metallic-roughness, occlusion and emissive images, the normal scale, the occlusion strength, the alpha mode and cutoff and the double-sided flag were dropped at load time, so nothing downstream (the editor import, task T9.2) could author a `lit-pbr` material from a glTF file.

## Decision

- **Additive fields on `StaticModelImportedMaterial`.** `MetallicFactor`, `RoughnessFactor`, `MetallicRoughnessTextureFilePath`, `OcclusionTextureFilePath`, `EmissiveTextureFilePath`, `NormalScale`, `OcclusionStrength`, `AlphaMode` (new enum `StaticModelAlphaMode`: `Opaque`, `Mask`, `Blend`), `AlphaCutoff` and `DoubleSided`. No existing member changes; the type is not serialized, so no asset format changes.
- **Values are the glTF values, unconverted.** Factors are kept as authored (metallic and roughness clamped to 0..1), in the linear space glTF defines them in. The colour fields that already existed (`DiffuseColor`, `EmissiveColor`) keep their current meaning and encoding. Converting linear factors to the display colours `lit-pbr` stores is the import's job (T9.2), not the reader's.
- **glTF defaults when a block is absent.** `metallicFactor` 1, `roughnessFactor` 1, `normalTexture.scale` 1, `occlusionTexture.strength` 1, `alphaMode` OPAQUE, `alphaCutoff` 0.5, `doubleSided` false. A material that has no `pbrMetallicRoughness` block reads metallic 1 and roughness 1 as the specification says; a `KHR_materials_unlit` material reads metallic 0, roughness 1 (it does not shade). Specular-glossiness materials get no special handling (they keep the base-colour fallback the reader already had).
- **Texture paths follow the existing rule.** The three new paths use the same resolution as the base colour and normal paths: only external images exist on disk, a path is returned when the file exists next to the model or at its recorded location, otherwise null. Embedded images of `.glb` static models stay out of scope (arbitration P12). `GetTextureFilePaths` is unchanged and does not list the new paths, so the default import copies the same files as before.
- **The specular-power conversion stays.** `SpecularPower` is still derived from the roughness factor with the same formula; the test that pins it is unchanged and green.

## Consequences

- The reader reads three more channels of each material and allocates nothing new beyond the strings of the paths; it runs at import time only.
- Tests build a glTF probe with external images for the five textures, non-default factors and every alpha mode, with and without `doubleSided`, and check each field.
- Task T9.2 will complete this ADR with the `lit-pbr` material template option of the import API, the encoding of the linear colour factors to display colours and the mapping from these fields to material keys.
