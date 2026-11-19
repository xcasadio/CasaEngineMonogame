# Effect File Inventory

This inventory distinguishes material-facing shaders from debug and utility shaders and records the current refactor risk for each file.

## Effect files (`.fx`)

| File | Type | Current consumers | Role | Refactor risk |
| --- | --- | --- | --- | --- |
| `LitForward.fx` | material-facing | `StaticMeshRendererComponent` | main lit static-mesh effect with many technique permutations | high |
| `LitPbr.fx` | material-facing | `StaticMeshRendererComponent` (loaded, registered in the shader manager, selector and variant library with `ShaderVariantLibrary.BuildLitPbrAliases()`, hot-reloadable; drawn by no material until the `lit-pbr` material of phase 4) | static-mesh PBR effect (metallic-roughness, glTF conventions), per-pixel lighting, forward: techniques `LitPbr`, `LitPbr_AlphaTest`, `LitPbr_NormalMap`, `LitPbr_NormalMap_AlphaTest`, `LitPbr_Transparent`, `LitPbr_NormalMap_Transparent` (premultiplied output: only the diffuse parts are scaled by the alpha, the specular and the emissive are not); built on `Lighting.fxh` and `Pbr.fxh`; 9 textures (base colour, normal, metallic-roughness, environment cube, two probes, shadow map, occlusion, emissive), `Has...` flags for the optional ones; unclamped linear output | high |
| `UnlitTexture.fx` | material-facing | `StaticMeshRendererComponent` | unlit material effect for textured or colored draws | medium |
| `skinEffect.fx` | material-facing | `SkinnedMeshRendererComponent` | skinned mesh effect with its own techniques and lighting bindings: Blinn-Phong `RiggedModelDraw` and `RiggedModelDrawDualQuaternion`, PBR `RiggedModelDrawPbr` and `RiggedModelDrawPbrDualQuaternion` (ADR-0111: direct lights, received shadows, flat ambient, emissive; no IBL, no normal map), the shadow-depth, scene-depth (`RiggedModelSceneDepth`, `RiggedModelSceneDepthDualQuaternion`, ADR-0116), normal and debug techniques | high |
| `SpriteBatch.fx` | utility/2D | `SpriteRendererComponent` | sprite batching and textured quad rendering | medium |
| `Tonemap.fx` | utility/post-process | `RenderPipeline` (ADR-0105) | pixel-only effect that tonemaps the HDR scene target into the view destination, drawn through `SpriteBatch`; techniques `Tonemap_Reinhard` and `Tonemap_AcesApprox`, parameter `Exposure` | low |
| `Bloom.fx` | utility/post-process | `RenderPipeline`, `BloomRenderer` (ADR-0108) | pixel-only effect drawn through `SpriteBatch`: soft-threshold bright pass, 2 x 2 downsample, separable 9-tap blur and additive composite on the HDR scene target; techniques `Bloom_BrightPass`, `Bloom_Downsample`, `Bloom_BlurHorizontal`, `Bloom_BlurVertical`, `Bloom_Composite`; parameters `BloomThreshold`, `BloomIntensity`, `BloomTexelSize` | low |
| `Fxaa.fx` | utility/post-process | `RenderPipeline`, `FxaaRenderer` (ADR-0114) | pixel-only effect drawn through `SpriteBatch`: FXAA on the tonemapped, display-encoded scene colour (luminance edge detection, search along the edge, bilinear blend across it, `tex2D` only); technique `Fxaa`; parameter `FxaaTexelSize` | low |
| `SceneDepth.fx` | utility/depth | `StaticMeshRendererComponent` via `SceneDepthPass` (ADR-0116) | static-mesh scene depth pre-pass: writes the view depth in world units (clip-space w, not normalised) into the red channel of a floating-point target; techniques `SceneDepth_Solid` and `SceneDepth_Textured` (alpha cut-out on the texture alpha, like `ShadowDepth.fx`); parameters `WorldViewProj`, `AlphaCutoff`, `Texture`; the skinned meshes use the techniques `RiggedModelSceneDepth` and `RiggedModelSceneDepthDualQuaternion` of `skinEffect.fx` | low |
| `Ssao.fx` | utility/post-process | `RenderPipeline`, `SsaoRenderer` (ADR-0116) | pixel-only effect drawn through `SpriteBatch`: screen-space ambient occlusion from the scene depth pre-pass target (view position rebuilt with `1 / M11` and `1 / M22`, normal from the neighbouring positions, 12 hemisphere samples rotated by a computed noise, `tex2D` only, point filter), a separable depth-aware blur and the composite that is multiplied into the HDR scene target; techniques `Ssao_Occlusion`, `Ssao_BlurHorizontal`, `Ssao_BlurVertical`, `Ssao_Composite`; parameters `SsaoTexelSize`, `SsaoProjectionScale`, `SsaoRadius`, `SsaoBias`, `SsaoIntensity`, `SsaoSkyThreshold`, `SsaoSourceTexture`, `SsaoDepthTexture` | medium |
| `IblConvolution.fx` | utility/post-process | `IblCubeGenerator` via `RenderPipeline.SetIblConvolutionEffect` (ADR-0110) | full-screen quad effect that convolves the environment cube into a diffuse irradiance cube and a 2D roughness atlas of prefiltered specular radiance; run once per source cube, outside the view loop | medium |
| `TexturedPrimitive.fx` | utility/2D | `CasaDrawTransaction` (CasaEngine MGUI backend) | unlit textured vertex-color triangle lists, for UI textured paints mapped onto rounded box geometry | low |
| `DebugPrimitiveColor.fx` | debug utility | `Line3dRendererComponent`, `DebugGridComponent`, `DebugAxisComponent`, editor gizmo line/selection path | shared vertex-color debug primitive shader | medium |
| `DebugSolidColor.fx` | debug utility | editor gizmo solid meshes/quads | shared solid-color debug shader | medium |

## Include files (`.fxh`)

| File | Included by | Role | Refactor risk |
| --- | --- | --- | --- |
| `Macros.fxh` | `LitForward.fx`, `LitPbr.fx`, `UnlitTexture.fx`, `skinEffect.fx`, `TexturedPrimitive.fx` | shared macros, texture declarations, technique helpers | high |
| `Structures.fxh` | `LitForward.fx`, `LitPbr.fx` | shared vertex/pixel structs used by the lit static effect | medium |
| `Lighting.fxh` | `LitForward.fx`, `LitPbr.fx`, `skinEffect.fx` | shared forward-lighting helpers and directional-light evaluation | high |
| `ColorPipeline.fxh` | `LitForward.fx`, `LitPbr.fx`, `UnlitTexture.fx`, `skinEffect.fx`, `SkyCubemap.fx`, `LegacySkyCube.fx`, `TexturedPrimitive.fx`, `SpriteBatch.fx`, `PsxQuad.fx`, `DebugPrimitiveColor.fx` | `DecodeColor` / `DecodeColor4`: display-to-linear decoding of the linear colour pipeline, a select on `ColorPipelineIsLinear` that returns the input untouched when 0 (ADR-0106, ADR-0107); see `color-pipeline.md` | medium |
| `Pbr.fxh` | `LitPbr.fx`, `skinEffect.fx` | GGX BRDF (distribution, height-correlated Smith visibility, Schlick Fresnel, Lambert diffuse) and `EvaluatePbrLights`: walks the directional, point and spot lights of `Lighting.fxh` with the same attenuation formulas and slot lists restated (pinned by `PbrShaderCoverageTests`), both lobes lit by the diffuse colour of the light, diffuse and specular returned apart | medium |

## Consumer notes

- `LitPbr.fx` is the PBR counterpart of `LitForward.fx` for static meshes. Its light, environment and shadow constants keep the slots of `LitForward.fx` (a text test pins it), so the same binders fill both. A PBR material on a skinned mesh (`SkinnedMeshComponent.PbrMaterial`) is drawn by the PBR techniques of `skinEffect.fx` (ADR-0111).
- `LitForward.fx` is the critical static-rendering shader and now carries a semantic name distinct from MonoGame `BasicEffect`.
- `skinEffect.fx` is still isolated from the main material/shader policy: it is loaded directly by the skinned renderer instead of being resolved through the same path as static materials.
- `DebugPrimitiveColor.fx` is now the shared replacement for former MonoGame `BasicEffect` usages in debug/runtime overlays.
- `DebugSolidColor.fx` is currently used by the editor gizmo for solid meshes and translucent quads.
- `TexturedPrimitive.fx` backs `IUIDrawContext.DrawTexturedTriangleList` in the CasaEngine MGUI backend. Its texture is declared with a bare `DECLARE_TEXTURE(Texture, 0)` on purpose: no sampler state is baked into the effect, so the caller's `GraphicsDevice.SamplerStates[0]` still governs and a Wrap sampler keeps tiling the texture.
- `axisComponent.fx` and `simple.fx` no longer have direct C# consumers and have been removed from the shipping MGCB content list.

## Refactor guidance

1. Treat `LitForward.fx`, `skinEffect.fx`, and `Lighting.fxh` as architecture-critical files.
2. Treat `SpriteBatch.fx`, `TexturedPrimitive.fx`, `DebugPrimitiveColor.fx`, and `DebugSolidColor.fx` as utility shaders that should keep clear, explicit names.
3. Treat any reintroduction of `axisComponent.fx` or `simple.fx` as an explicit compatibility decision rather than default shipping content.