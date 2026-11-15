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

### The import writes `lit-pbr` materials on request (T9.2)

- **An option, not a new default.** `EditorAssetImportOptions` (`CasaEngine.EditorServices`) carries `MaterialTemplate`, an `ImportedMaterialTemplate` (`LitDiffuse`, the default, or `LitPbr`). `EditorAssetImportService.ImportFile` gets an additive overload `ImportFile(string, string, ILegacyMaterialImportProfile?, EditorAssetImportOptions)`; the existing signature is unchanged and delegates to it with default options. The editor (`FileOperationService`) is not changed and keeps importing `lit-diffuse`; letting the user choose the template in the editor is a follow-up (open point of the plan). The option only concerns the materials of static models.
- **The default path stays bit for bit.** With `LitDiffuse` the material documents, the texture wrappers and the static model are the ones the import wrote before the option existed, a test compares them to a reference captured from the code of the previous commit (identifiers normalised). Only the `LitPbr` path imports the three new textures, so the default import copies the same files as before.
- **Colours are encoded to display colours.** glTF gives `baseColorFactor` and `emissiveFactor` in linear space, while `lit-pbr` stores display colours (it decodes them with the 2.2 power, `ColorSpace`). In `LitPbr` the import encodes the RGB of these two factors with `c ^ (1 / 2.2)`, clamped to 0..1; the alpha of the base colour is unchanged. Rendered in `LinearHdr`, the material then finds the glTF factor again. A factor of 0.5 becomes about 186 / 255. Textures are not converted (the base colour and emissive images are display colours, as `lit-pbr` expects).
- **The mapping is written explicitly, no key is left to the definition default.** `metallic`, `roughness`, `normal_scale`, `occlusion_strength`, `alpha_cutoff`, `base_color`, `emissive_color`, plus the textures `base_color_texture`, `normal_texture`, `metallic_roughness_texture`, `occlusion_texture` and `emissive_texture`. A texture the glTF material does not have produces no texture key. Alpha mode: OPAQUE gives the `Opaque` queue, MASK the `AlphaTest` queue (the cutoff is `alpha_cutoff`), BLEND the `Transparent` queue (the material render state resolver then derives the blend and depth states). `doubleSided` gives the rasterizer state `CullNone`, otherwise the definition default `CullCounterClockwise`. The sampler state is `AnisotropicWrap`, like `lit-diffuse`.
- **Out of scope.** Skinned and rigged models, the ADR-0094 converted glb and the Assimp conversion path are untouched; so are the images embedded in `.glb` files (arbitration P12).

## Consequences

- The reader reads three more channels of each material and allocates nothing new beyond the strings of the paths; it runs at import time only.
- Tests build a glTF probe with external images for the five textures, non-default factors and every alpha mode, with and without `doubleSided`, and check each field.
- Task T9.2 completes this ADR (section above) with the `lit-pbr` material template option of the import API, the encoding of the linear colour factors to display colours and the mapping from these fields to material keys.
