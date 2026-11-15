# ADR-0105: Display colours are decoded to linear light for a view that really renders through the HDR target

- **Status**: Proposed
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/pbr-rendering-tasks.md` (arbitrations P3 and P4, task T1.5)
- Renumbered from ADR-0097 on 2026-11-15 when origin/main was merged (ADR-0095 to ADR-0099 now carry the renumbered lighting decisions)

## Context

ADR-0104 renders a `LinearHdr` view into an HDR target and tonemaps it, but nothing decodes the colours yet: every colour the engine knows (textures, material colours, light colours, the ambient, the clear colour, cubes) is a display colour, and lighting it as if it were linear light then encoding the result again is wrong. Verified on the code:

- The shaders read material colours from constants (`DiffuseColor`, `EmissiveColor`, `SpecularColor`, `MaterialAmbientColor`), samples (`Texture` times the optional detail texture, cubes, probes) and vertex colours.
- Light colours and the ambient reach the shader already multiplied by their intensity (`ForwardLightBinder`, `ResolvedEnvironmentSettings.EffectiveAmbientColor`); the shadow colour is a factor.
- A view whose mode resolves to `LinearHdr` can still be drawn by the legacy path (ADR-0104: `ClearColorBuffer` false, an empty rectangle, a missing tonemap shader). Decoding such a view without tonemapping it would be wrong.
- `RenderPipeline` is the only place that knows whether a view really draws through the HDR target; the resolved environment is cached per view and cannot carry that decision.
- An 8-bit `Color` cannot hold a decoded dark value (the clear colour 20/255 decodes to 0.0036, below half a level).

## Decision

- **The power law, decoded where the value is read.** `ColorSpace.Decode(c) = c^2.2` on the CPU and `DecodeColor(c)` in `ColorPipeline.fxh` in the shaders, the inverse of the `pow(c, 1 / 2.2)` of `Tonemap.fx`. Shaders declare `float ColorPipelineIsLinear` before including `ColorPipeline.fxh`; with the constant at 0 `DecodeColor` is a select that returns its argument untouched (the legacy image stays bit-identical).
- **What a shader decodes.** The base colour after its modulation by the detail texture (never the detail sample alone: mid-grey is neutral in display space only), vertex colours (in the vertex shader), `DiffuseColor`, `EmissiveColor`, `SpecularColor`, `MaterialAmbientColor`, the material reflection cube, the environment cube and the local probes, the unlit texture and tint, the sky cubes and their tint. Never the normal map, the shadow map or any non-colour data. The decoded material diffuse and specular colours are computed once per pixel (static variables of `Lighting.fxh`), not in every light slot: repeating the decoding in the 24 unrolled light slots produced a wrong image on the OpenGL backend even with the constant at 0.
- **What the CPU decodes.** Because they arrive multiplied by an intensity: light colours are decoded BEFORE the multiply by intensity (`ForwardLightBinder`), the ambient is `Decode(AmbientColor) x AmbientIntensity` (`ResolvedEnvironmentSettings.EffectiveAmbientColor`), the shadow colour is decoded, and the HDR clear colour is decoded in floating point (the `Vector4` overload of `Clear`).
- **Which views decode: the ones that really use the HDR target.** `ResolvedEnvironmentSettings.LinearColorActive` is set by `RenderPipeline.Render` on its copy of the resolved environment only when the planner chose the HDR target and the tonemap shader is loaded (`useHdr`). Everything built from that copy (light collection, `RenderFrame.Environment`, `RenderContext.Environment`) carries it: `EnvironmentShaderBinder` binds `ColorPipelineIsLinear` from it, `ShaderBindCache` hands it to `ForwardLightBinder`, `SkyCubemapRenderer` binds it. The resolved mode (`IsLinearHdr`) is never used to decode. A copy of the environment made elsewhere (editor previews, tests) keeps the legacy value false.
- **Origin of the environment cubes (open point O2).** Only a cube built from a `.hdr` panorama holds linear light; the resolver sets `BackgroundCubemapIsLinear` and `SpecularEnvironmentCubemapIsLinear` when the cube is the panorama cube and the panorama file is `.hdr` (`PanoramaEnvironmentGenerator.IsLinearPanorama`, by extension, like the loader). The procedural sky and the physical atmosphere are built from authored `Color` values, a DDS or authored cube and the probes are display images: all are decoded. The shaders read `EnvironmentCubeIsLinear` (lit shaders: the specular and diffuse cube; `SkyCubemap.fx`: the background cube).

## Consequences

- A `LinearHdr` view that the planner leaves on the legacy path keeps its legacy colours; a `Legacy` world is bit-identical (`capture.sh` hashes unchanged).
- A physical-atmosphere cube with a sun intensity above 1 is decoded like any display cube: its highlights are brighter in linear light than the authored value suggests. A later task may mark generated cubes linear if the generators are changed to produce linear values.
- Mipmaps are averaged in the encoded space (open point O4); it darkens high-contrast textures slightly in `LinearHdr`.
- `LegacySkyCube.fx` (used by games, not bound by the engine) exposes `ColorPipelineIsLinear`; a game that draws it in `LinearHdr` must bind the constant.
- Every lit pixel evaluates the extra `pow` calls (material diffuse, specular and emissive, the base colour, the ambient tint), even with the constant at 0, because the select evaluates both sides on the GPU; the light colours cost nothing (they are decoded on the CPU).
