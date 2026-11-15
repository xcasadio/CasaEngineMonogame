# ADR-0106: Image-based lighting is generated on the GPU and its specular part is stored as a roughness atlas

- **Status**: Proposed
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/pbr-rendering-tasks.md` (arbitration P9, task T6.1 steps 1b and 5; the sections for T6.2 and T7.1 are added by those tasks)

## Context

The PBR ambient needs a diffuse irradiance term and a specular term that follows the roughness. Both come from the environment cube of the world, whatever its format, and must not be computed per frame (arbitration P9).

Verified on the code and on the device (task T6.1):

- No cube of the engine has mips (`TextureCubeLoader` refuses them) and the generated cubes are `Vector4` without mips (`PanoramaEnvironmentGenerator`).
- The runtime backend is OpenGL (MonoGame DesktopGL 3.8.5.1), shader model 3.0 translated to GLSL by `mgfxc` (task T0.3).
- Step 1b of T6.1: `SAMPLE_CUBEMAP_LEVEL` was written as `texCUBElod(sampler, float4(dir, level))` under `OPENGL` (`Macros.fxh`). The compiled effect contains `textureCube(ps_s0, ...)` for it: the translation to GLSL drops the level. Measured on a cube of 128 texels per face with a full chain of 8 levels, each level a distinct solid colour (read back with `GetData`, so the levels are stored): the shader requesting the levels 0, 2 and 4 returned the colour of level 0 each time (0.1, 0.8, 0.25), not those of levels 2 (0.3, 0.6, 0.25) and 4 (0.5, 0.4, 0.25). The OpenGL chain of MonoGame ignores the requested cube level.
- Only the `D3D` variant of the macro (`SampleLevel`) honours the level, and no target of this repository uses it.

## Decision

- **Generation on the GPU, once per source, outside the view loop.** `IblCubeGenerator` draws a full-screen quad with `IblConvolution.fx` into a `Vector4` render target, reads it back with `GetData` and writes the result with `SetData`, so the generated textures do not live in render targets. It restores the render targets, the viewport and the blend, depth, rasterizer and sampler states. It never runs while a view target is bound: the caller (T6.2) runs it between views.
- **Two results.** The diffuse irradiance is a `TextureCube` of 32 texels per face, one level, `Vector4`: the cosine-weighted average of the incoming radiance (a fixed grid of 16 by 32 hemisphere samples, normalised by the sum of the weights), that is `E / pi`, so a uniform cube gives back its colour and the shader multiplies it by the diffuse albedo without a further `1 / pi`. The specular result is the atlas below.
- **The specular result is a 2D atlas, not a cube with mips.** Because the level of a cube sample is ignored on this chain (step 1b), the roughness levels cannot live in the mip chain of a cube. The prefiltered result is a `Texture2D` (`Vector4`, no mips) of 512 by 1536 texels: six latitude-longitude bands of 512 by 256 texels stacked vertically, band `n` holding the GGX-prefiltered radiance for the perceptual roughness `n / 5` (`alpha = roughness^2`; 64 importance samples with a radical-inverse sequence, `N = V = R`). Band 0 is the source resampled. The mapping inside a band is the one of `PanoramaEnvironmentGenerator.GetPanoramaUv` (`u = 0.5 + atan2(x, -z) / 2 pi`, `v = 0.5 - asin(y) / pi`, row 0 at the north pole). A band of 512 by 256 texels gives the angular resolution of a 128-texel cube face.
- **Reading the atlas (rule for T6.3 and T7.1).** A shader computes `b = roughness * 5` (the 5 is a constant of the shader, the number of bands minus one), the two bands `floor(b)` and `min(floor(b) + 1, 5)`, reads each with `tex2D` at `(u, (band + clamp(v, 0.5/256, 1 - 0.5/256)) / 6)` and interpolates linearly by `frac(b)`. The vertical coordinate is kept half a texel inside its band so that the bilinear footprint never reaches the neighbouring band. The sampler is linear, wraps in `u`, clamps in `v`, uses no mips. `IblCubeGenerator.GetAtlasUv` is the CPU twin of that mapping.
- **Colour space.** The generator takes `sourceIsLinear` (the `EnvironmentCubeIsLinear` flag of the source) and decodes every sample of a non-linear source with `pow(abs(c), 2.2)` before accumulating it. The generated textures therefore always hold linear light, whatever the colour pipeline of the view; the cache key stays the source instance, the linearity being a property of the source. The shader never reads a colour pipeline constant.
- **`SAMPLE_CUBEMAP_LEVEL` stays in `Macros.fxh`** (both variants), with a warning that the OpenGL variant ignores the level: it reads the only level of a cube without mips. The convolution uses it for level 0 inside its sample loops. A GPU test pins the step 1b outcome.
- **Sizes and cost.** Irradiance 32 texels per face; atlas 512 by 1536 in `Vector4`, 12.6 MB (no half-float variant: the plan asks for `Vector4`, and the memory stays bounded by the number of distinct source cubes). Measured on the development machine for a 128-texel source: irradiance about 14 ms, atlas about 85 ms (alert threshold of the plan: 500 ms per cube).

## Consequences

- The specular IBL costs two `tex2D` reads and one interpolation instead of one cube read with a level. It needs one more sampler than a mipped cube would, and no cube mip support from the backend.
- The prefilter is not seamless on the poles of the latitude-longitude mapping (one row of texels converges at each pole), a limit shared with the panorama path.
- The source is read at its level 0 without filtered importance sampling (a source cube has no mips), so a high-frequency source at high roughness is noisy with 64 samples. A source with mips would be read by the implicit derivative on this chain.
- If a later MonoGame honours the level of `texCUBElod`, the pinning test fails: that is the signal to reconsider a cube with mips; the atlas stays valid until then.
- The cache, the BRDF table (T6.2), the shader (T6.3) and the probes (T7.1) are decided in their own tasks and added to this record.
