# PBR rendering

How CasaEngine renders metallic-roughness (PBR) surfaces next to the historical Blinn-Phong path: the `lit-pbr` material, the colour pipeline it relies on, bloom, transparency, image-based lighting (IBL), local reflection probes, skinned meshes and the glTF import. PBR is chosen per material; `lit-diffuse` is untouched and the default `Legacy` colour pipeline renders every existing image bit for bit as before.

Decisions: ADR-0103 (scope), ADR-0104 to ADR-0107 (colour pipeline, see [color-pipeline.md](color-pipeline.md)), ADR-0108 (bloom), ADR-0109 (`lit-pbr`), ADR-0110 (IBL), ADR-0111 (skinned PBR), ADR-0112 (glTF PBR import), ADR-0113 (scope extended to clear coat, SSAO and FXAA), ADR-0114 (FXAA), ADR-0115 (clear coat), ADR-0116 (scene depth pre-pass and SSAO), ADR-0117 (detail texture). They live in [docs/decisions](../decisions/README.md).

## Workflow

1. **Pick the colour pipeline of the world.** PBR is meant for `LinearHdr` (linear light, half-float target, tonemapping, bloom); it also runs in `Legacy`, where it is shaded in display space. See [color-pipeline.md](color-pipeline.md) for the `color_pipeline` key.
2. **Give the world an environment cube** if surfaces should reflect something (`WorldEnvironmentSettings.EnvironmentAssetId`, or a cube supplied at run time with `SpecularEnvironmentCubemap`). The IBL is generated from it, once, and shared by every `lit-pbr` surface.
3. **Author a `lit-pbr` material** (a `.material` file whose `definition_id` is `lit-pbr`, see the keys below). The editor opens, edits and previews such a file; creating one from the editor is not offered for any definition today, so write the file by hand, import a glTF (below) or build a `LitPbrMaterial` in code.
4. **Add a directional light with shadows** as for any lit material; point and spot lights are evaluated by the same BRDF.
5. **Optional**: turn bloom on, add local reflection probes, put a `PbrMaterial` on a skinned mesh, give a material a `clear_coat`, turn on FXAA and SSAO (below).

`CasaEngine.Demos` has `PBR material demo` (a metal and roughness grid, a glass sphere, an emissive sphere with bloom, a skinned character, IBL, and with `CASAENGINE_PBR_DEMO_CLEARCOAT=1` a row of five clear-coated spheres, shown by default outside the validation configuration; the `F` key toggles FXAA and `O` the SSAO) and `Skinned mesh demo` (Blinn-Phong or PBR with the `P` key). Their bench variables are listed in [demos-check-notes.md](demos-check-notes.md) and [demos-main-menu.md](demos-main-menu.md).

## The `lit-pbr` material

`lit-pbr` is a built-in material definition (`BuiltInMaterialDefinitions.CreateLitPbrDefinition`). Its runtime class is `LitPbrMaterial`; it exists only compiled from a definition (no JSON of its own). A material names its own shader technique, `LitPbr[_NormalMap][_AlphaTest|_Transparent]`, because the technique cannot be derived from the family alone (ADR-0109).

| Key | Type | Default | Meaning |
|---|---|---|---|
| `base_color_texture` | texture | none | Base colour, sRGB display colour. |
| `normal_texture` | texture | none | Tangent-space normal. Selects the `NormalMap` technique as soon as it is set, with or without a base texture. Needs tangents on the mesh. |
| `metallic_roughness_texture` | texture | none | Roughness in G, metallic in B (glTF layout), multiplied by the two factors. |
| `occlusion_texture` | texture | none | Ambient occlusion in R, scaled by `occlusion_strength`; applies to the ambient and IBL terms only. |
| `emissive_texture` | texture | none | Emitted colour. |
| `detail_texture` | texture | none | Detail texture (ADR-0117): the base texture is multiplied by `2 * detail(texCoord * detail_tiling)` before the colour is decoded, as in `lit-diffuse` (ADR-0100); mid grey (128/255) leaves it unchanged. Read only together with `base_color_texture`; ignored by the skinned path. |
| `detail_tiling` | at least 0 | 1 | Repeats of the detail texture over the base texture coordinates. A value that is not finite or not above 0 is read as 1. |
| `base_color` | colour | white | Display colour; its alpha is the opacity. An alpha below 255 makes the material transparent. |
| `metallic` | 0 to 1 | 0 | Dielectric by default (not the 1 of glTF). |
| `roughness` | 0 to 1 | 0.5 | Perceptual roughness; `alpha = roughness^2`, floored at 0.045 in the shader. |
| `emissive_color` | RGB | 0 | Emitted colour; also turns the emissive feature on. |
| `occlusion_strength` | 0 to 1 | 1 | |
| `normal_scale` | at least 0 | 1 | |
| `clear_coat` | 0 to 1 | 0 | Weight of a clear varnish layer (car paint), ADR-0115. 0 renders exactly like a material without the layer. |
| `clear_coat_roughness` | 0 to 1 | 0.1 | Perceptual roughness of the varnish, floored at 0.045 in the shader. |
| `alpha_cutoff` | 0 to 1 | 0.5 | Used by the `AlphaTest` queue only (it is bound as 0 elsewhere). |

Per-instance overrides (`MaterialPropertyBlock`, same mechanism as `lit-diffuse`) exist for `base_color`, `metallic`, `roughness`, `emissive_color`, `clear_coat` and `clear_coat_roughness`: one material and one model can draw a whole grid of different surfaces, as `PBR material demo` does. The block wins over the material for the draw, and the next bind restores the material values.

Units: the BRDF is Lambert divided by pi plus GGX (height-correlated Smith visibility, Schlick Fresnel, `F0 = lerp(0.04, base, metallic)`), summed over the same 24 light slots as `LitForward`. Both lobes use the diffuse colour of the light (its specular colour is ignored). At equal light intensity a `lit-pbr` surface is therefore pi times darker in its diffuse part than a `lit-diffuse` one; pick the light intensity accordingly (`PBR material demo` uses 1.4). The GGX denominator takes `1 - NoH^2` from the cross product of the normal and the half vector: computed from `NoH`, it cancels to the last bits of a float on a very smooth surface and two runs of the same scene differed by a few levels.

Colours of the material are display colours, decoded once per pixel to linear light in `LinearHdr` (ADR-0106). An emissive colour is a display colour too, at most 1 per channel.

## Clear coat

`clear_coat` and `clear_coat_roughness` (ADR-0115) add a dielectric varnish over the base: the second specular lobe of a car paint. The varnish follows the **geometric** normal (the interpolated vertex normal): there is no coat normal map, so a normal-mapped base keeps a smooth coat. The full closed form (GGX lobe at `F0 = 0.04`, Kelemen visibility, Schlick Fresnel `F_d` at `VoH`, base multiplied by `1 - clear_coat * F_d`) is the header of `Pbr.fxh`. With image-based lighting the coat reflects the global prefiltered atlas at `clear_coat_roughness`, weighted by `E = 0.04 * A + B` (BRDF table) and `clear_coat`, and the ambient of the base is multiplied by `1 - clear_coat * E`; with the flat ambient there is no coat reflection and the base ambient is multiplied by `1 - clear_coat * Schlick(0.04, NoV)`. A transparent surface keeps the coat lobe whole, as it keeps its other specular parts.

A coat of 0 renders bit-identically to a material without the layer: the coat is a second walk over the light slots that runs only when `clear_coat > 0`, and the base walk has no coat code. The coat is compiled only for `LitPbr.fx` (`#define PBR_CLEAR_COAT`): the skinned effect has none (below).

## FXAA and SSAO

Two post-effects of the `LinearHdr` colour pipeline, both off by default (the settings are the `anti_aliasing`, `ssao_enabled`, `ssao_radius`, `ssao_intensity` and `ssao_bias` keys of `color_pipeline`; the full description, the frame order and the counters are in [color-pipeline.md](color-pipeline.md)).

- **FXAA** (ADR-0114). After the tonemapping and before the post-tonemap renderers and the interface, so the interface is never filtered. It is skipped for a view that renders scene and interface in one block (the editor views, `OverlayViewPipeline`).
- **SSAO** (ADR-0116). A linear depth pre-pass of the opaque static and skinned meshes, then an occlusion and a depth-aware blur that multiply the HDR target between the scene and the bloom. It needs a perspective projection and the same scene/interface separation as FXAA.

Order in a `LinearHdr` frame: scene depth pre-pass, scene colour, SSAO, bloom, tonemapping (into the pooled FXAA input when FXAA is on), FXAA, post-tonemap renderers, interface. In `Legacy` both settings are ignored; the clear coat is a property of the material and follows `lit-pbr` in both pipelines.

## Colour pipeline and bloom

See [color-pipeline.md](color-pipeline.md) for the modes, the frame order, the decoding rules and the cost. In short: `LinearHdr` lights in linear light into a half-float target, adds the bloom, tonemaps (`AcesApprox` or `Reinhard`, with an exposure) and draws the interface after the tonemapping.

Bloom (ADR-0108) is four keys of the `color_pipeline` object: `bloom_enabled` (false), `bloom_threshold` (1), `bloom_intensity` (0.5) and `bloom_radius` (1). It runs between the lighting and the tonemapping: a soft-knee bright pass at half size, two further reductions, a separable 9-tap Gaussian at each of the three levels, recombined with equal weight and added to the HDR target. It has no effect in `Legacy`. Its targets are pooled and released with the HDR target.

## Transparency

A `lit-pbr` material whose queue is `Transparent` (a `base_color` alpha below 255, or an explicit queue) uses the `LitPbr_Transparent` or `LitPbr_NormalMap_Transparent` technique with the usual `AlphaBlend` and depth-read states. The output keeps the specular reflection and the emission at full strength and scales only the diffuse parts (direct and ambient) by the alpha, so a glass sphere stays visible and keeps its highlight while the background shows through. `AlphaTest` uses the `_AlphaTest` techniques and a shadow mask built from the material.

## Image-based lighting

The PBR ambient comes from the environment cube of the world (ADR-0110). Nothing is computed per frame:

- **Generation.** The first time a `lit-pbr` shader is bound with a source cube, `IblCache` queues a request; `RenderPipeline.Render` runs it at the very start of the frame, before any view, so no view target is bound. It produces a diffuse irradiance cube (32 texels per face, `E / pi`), a specular atlas and, once for all sources, a 64 by 64 BRDF table (CPU integration, `Vector2`). Replacing the cube of the environment (a new instance) generates again; the entries of a disposed source are released at the next flush. A generation that throws is logged once and leaves the flat ambient.
- **Why an atlas.** The OpenGL chain of MonoGame ignores the level of a cube sample (`texCUBElod` is translated to a plain `textureCube`; a GPU test pins this). The prefiltered specular is therefore a 512 by 1536 `Texture2D`: six latitude-longitude bands for the perceptual roughness 0, 0.2, ..., 1, read with two `tex2D` and a linear interpolation. 12.6 MB per source cube.
- **Colour space.** The generated textures always hold linear light; the shader re-encodes them only in `Legacy`.
- **Counters.** `IblCache.GenerationCount` and `GeneratedOutsideViewLoop` are what the demos and the tests read. `PBR material demo` reports one generation over 600 frames.
- **Without IBL** (no source cube, effect not loaded, generation failed): the flat ambient of the `AmbientSource` setting, see [lit-ambient-and-ambient-source.md](lit-ambient-and-ambient-source.md).
- **Debug.** `LitPbrMaterial.DebugForceIblBaseLevel` forces the unfiltered band, to show what the prefiltering does. It is a static, not serialized.

### Local reflection probes

A `ReflectionProbe` of the world (`World.ReflectionProbes`) refines the specular term inside its influence radius. Its cube goes through the same atlas generation (atlas only: the diffuse ambient stays global). At most two probes are blended by weight, then mixed with the global radiance by `LocalReflectionProbeInfluence`. A cube can be given at run time with `ReflectionProbe.SpecularCubemap`. Probes act only when the global IBL is available, and a world with at least one probe recomputes the blend at every `Resolve` instead of using the resolved-environment cache (the blend depends on the camera).

## Skinned meshes

A skinned mesh draws PBR when its `SkinnedMeshComponent.PbrMaterial` (a `LitPbrMaterial`, additive, not serialized, null by default) is set; nothing else changes for existing callers (ADR-0111). The two techniques `RiggedModelDrawPbr` (linear blend) and `RiggedModelDrawPbrDualQuaternion` live in `skinEffect.fx` and use the same `Pbr.fxh` as the static shader. When the material has no base texture, the texture of the mesh is bound for the draw. **No IBL, no probes, no normal map and no transparency on this path** (see the limitations).

## glTF import

`GltfStaticModelReader` keeps the PBR channels of a glTF material (ADR-0112): metallic and roughness factors, metallic-roughness, occlusion and emissive textures, normal scale, occlusion strength, alpha mode and cutoff, double-sided. A material without a `pbrMetallicRoughness` block gets the glTF defaults (metal 1, roughness 1), except `KHR_materials_unlit` (metal 0).

The `KHR_materials_clearcoat` extension is read too (ADR-0115): SharpGLTF 1.0.6 exposes it as the `ClearCoat` channel (factor `ClearCoatFactor`) and the `ClearCoatRoughness` channel (factor `RoughnessFactor`), which become `StaticModelImportedMaterial.ClearCoatFactor` and `ClearCoatRoughnessFactor` (0 for a material without the extension). The `LitPbr` import writes `clear_coat` and `clear_coat_roughness` only when the factor is above 0, so every other material keeps the file it had; the `LitDiffuse` import ignores the extension. The clear coat texture, the clear coat normal texture and the clear coat roughness texture of the extension are not read (the clear coat has no texture).

`EditorAssetImportService.ImportFile(..., EditorAssetImportOptions)` with `MaterialTemplate = ImportedMaterialTemplate.LitPbr` writes `lit-pbr` materials; the default stays `LitDiffuse` and its output is byte-identical to before (pinned by a reference file). In `LitPbr` the RGB of `baseColorFactor` and `emissiveFactor` (linear in glTF) is encoded with `c^(1/2.2)` so that `LinearHdr` gives back the glTF value; OPAQUE, MASK and BLEND become the `Opaque`, `AlphaTest` (with the cutoff) and `Transparent` queues; `doubleSided` becomes `CullNone`.

## Cost

Measured on the development machine in Release, 1024 by 768, no vsync, 600 frames (`CASAENGINE_DEMO_FRAME_STATS=1`, see [demos-main-menu.md](demos-main-menu.md)): on `Legacy` the CPU time of a frame equals the one of the code before the work (Draw 0.645 ms against 0.664 ms) and the wall time is 0.12 ms (5 %) higher, a GPU cost of the decoding (open point O11, accepted). In `PBR material demo`, bloom costs about 1.2 ms of wall time and 0.3 ms of Draw CPU, the IBL about 0.2 ms and no measurable CPU. Details and the table: the validation note of T10.2 in [pbr-rendering-tasks.md](../../ai-agent/tasks/pbr-rendering-tasks.md).

FXAA, SSAO and the clear coat row (plan `pbr-clearcoat-ssao-fxaa-tasks.md`, T4.1): `Release` build made just before the run, 1024 by 768, no vsync, 600 frames (updates 101 to 700), `PBR material demo` in the validation configuration, Intel UHD (the frame time is carried by the GPU: Draw CPU stays under 4 ms). Eight configurations, each run twice; every run was accepted only because the counters of the same log matched its variables (`fxaaDraws` 1 when FXAA is on, `ssaoStaticDraws`, `ssaoSkinnedDraws` and `ssaoPasses` at least 1 when SSAO is on, all 0 when off, and the `[DemoConfig]` line giving the row state). Mean wall time of a frame over the two series:

| FXAA | SSAO | Clear coat row | Wall time (ms) | From the base (ms) |
|---|---|---|---|---|
| Off | Off | Off | 10.61 | base |
| On | Off | Off | 12.74 | +2.13 |
| Off | On | Off | 16.07 | +5.46 |
| On | On | Off | 17.74 | +7.13 |
| Off | Off | On | 11.87 | +1.26 |
| On | Off | On | 14.11 | +3.50 |
| Off | On | On | 16.71 | +6.10 |
| On | On | On | 18.60 | +7.99 |

FXAA costs about 2 ms, SSAO about 5.5 ms and the row (five more spheres, 39 opaque items instead of 34) about 1.3 ms; they add up within about 1 ms. The two series differ by 0.03 to 1.24 ms. These figures are one GPU's; the machine is hybrid Intel and NVIDIA and which GPU ran was not checked.

## Limitations

- **Skinned PBR has no IBL, no probes, no normal map, no transparency and no clear coat.** `skinEffect.fx` declares no IBL parameter, so a skinned mesh never triggers a generation and gets the flat ambient (ADR-0111). Normal maps on skinned meshes are out of scope.
- **The prefiltered specular is a 2D atlas, not a cube with mips** (ADR-0110). It costs an extra sampler and 12.6 MB per source cube; it is not seamless at the poles of the latitude-longitude mapping; a high-frequency source at high roughness is noisy (64 samples, no filtered importance sampling). If a later MonoGame honours the cube level, a GPU test fails and the choice can be revisited.
- **Sampler budget.** `LitPbr.fx` uses 15 of the 16 pixel samplers of the target (t0 to t8 material, t9 to t11 IBL, t12 and t13 probe atlases, t14 detail texture).
- **Detail texture limits** (ADR-0117). One more texture fetch per pixel when bound, one uniform branch otherwise; no per-instance override of the two keys; the sampler state is the device default (linear, mip filtering, wrap under DesktopGL), so a strongly tiled texture should have mipmaps; `skinEffect.fx` has no detail texture.
- **Clear coat limits** (ADR-0115). The coat reflection reads the global atlas only, never a local probe; there is no coat texture and no coat normal map; the absorption of the base colour under the coat and the scattering between the layers are not modelled; `skinEffect.fx` has no coat (P5). The shipped `LitPbr.xnb` is 1 722 697 bytes (888 091 before the coat). Writing the coat lobe inside the 24 slots of the base walk broke the shading on the OpenGL chain (cause not found), so it is a separate walk: see the ADR before touching it.
- **FXAA and SSAO limits** (ADR-0114, ADR-0116). Both act only in `LinearHdr` and only for a view whose pipeline separates the scene from the interface: the editor views and `OverlayViewPipeline` get neither, and `Legacy` ignores the settings. FXAA does not filter the interface or anything drawn after the tonemapping. SSAO needs a perspective projection. SSAO multiplies the whole opaque colour, direct light included (not only the ambient term), and also the transparent geometry and the other 3D draws (particles, lines, sprites, tile maps) that are already in the HDR target, although these are not in the depth.
- **Deferred P3 defect: IBL generation image and display buffer.** On a frame where an IBL generation runs, the pipeline switches back to the display buffer with `DiscardContents`, which may clear for that one frame the virtual-resolution bars and the phase 1 drawing (inferred from the code, not observed). Not fixed.
- **Deferred P3 defect: probe referenced by an asset id.** The probe blend is recomputed at every `Resolve`; for a probe referenced by an asset id (not by a run-time cube) that reaches a path that allocates (panorama) or logs (failed asset) every frame. No shipped content defines a probe today. Not fixed; use `ReflectionProbe.SpecularCubemap` meanwhile.
- **Open P4 findings.**
  - The cut shadow of an alpha-tested `lit-pbr` is proved by properties and by a mask test, not by an image.
  - A normal texture whose asset is missing leaves the `NormalMap` technique reading an unbound sampler (same behaviour as `lit-diffuse`).
  - Tangents are checked on the uncompiled path only (`RenderFeatureResolver`); a compiled material (`MaterialCompiler` computes the features without the mesh) with a normal texture on a mesh without tangents is not caught (ADR-0109).
  - The "numbered ADR-00NN until" remarks of the ADRs do not mention the second renumbering; ADR-0110 overstates the independence from the sampler address mode.
  - A reloaded `color_pipeline` writes all its fields as soon as one differs from the default; a JSON `null` in one of its fields throws at load (like `exposure`); with the threshold at 1 the soft knee makes colours around 0.5 linear contribute to the bloom.
- **Colour pipeline limits** apply unchanged (view pipelines without `IScenePhaseViewPipeline`, screen sprites losing the scene depth test, mipmaps averaged in encoded space, emissive limited to display white, `Legacy` cost of the select form): see [color-pipeline.md](color-pipeline.md).
- **`LinearHdr` bloom with `AcesApprox` at a light intensity of 16** does not keep three distinct values on the measured sphere (open point O12); the HDR information is kept (244 to 251) and the engine is unchanged.
- **glTF import to `lit-pbr` is available through the API only.** The editor and `FileOperationService` still import as `lit-diffuse` (open point O15). Creating a material from the editor is not offered (O13). Specular-glossiness materials get no special treatment; transmission, refraction and the glTF extensions other than clear coat are out of scope.
- **Not verified by hand.** The keys of the demos (`I`, `B`, `F`, `O`, `T`, `+`, `-`, and `P` in `Skinned mesh demo`), the look of FXAA and SSAO on the clear coat row, the editor preview and hot reload of a `lit-pbr` material (T4.4), and the plausibility of the `LinearHdr` captures by the author's eye (T1.7).
- **Test host.** The full test suite sometimes aborts or hangs with no failing test, most often in `EntityDetailsScriptRowTests` (open point O14, cause unknown).
