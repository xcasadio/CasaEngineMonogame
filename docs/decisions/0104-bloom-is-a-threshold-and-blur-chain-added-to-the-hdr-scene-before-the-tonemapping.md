# ADR-0104: Bloom is a threshold and blur chain added to the HDR scene before the tonemapping

- **Status**: Proposed
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/pbr-rendering-tasks.md` (decision D8, arbitration P7, tasks T2.1 and T2.2)
- Renumbered from ADR-0099 on 2026-11-15 when origin/main was merged (ADR-0094 to ADR-0098 now carry the renumbered lighting decisions)

## Context

ADR-0101 renders a `LinearHdr` view into a pooled HDR scene target (`HalfVector4`, falling back to `HdrBlendable` then `Color`) and tonemaps it into the destination of the view before the interface is composed (`RenderPipeline.RenderHdrView`). A bloom needs values above 1, which only that target holds, and must not touch the interface, which is composed after the tonemapping. The author put the bloom in the scope of the PBR work (ADR-0099, D8).

Verified on the code and by measurement (MonoGame DesktopGL 3.8.5.1, tasks T0.3 and T1.3):

- Shaders compile as shader model 3.0 with `OPENGL` defined; one content file, `CasaEngine/Content/Content.mgcb`, compiles them. A pixel-shader-only effect can be drawn through `SpriteBatch.Begin(effect:)`, which binds the drawn texture as sampler 0 (`Tonemap.fx`, ADR-0101).
- `RenderTargetPool` keys its targets on width, height, surface format and depth format, and `TrimFree` disposes the free targets of one such key. The eviction of ADR-0101 (`HdrTargetEviction`) tracks the render index of the last use of each HDR target key.

## Decision

- **Settings.** `ColorPipelineSettings` gains `BloomEnabled` (false), `BloomThreshold` (1), `BloomIntensity` (0.5) and `BloomRadius` (1), serialized additively in the `color_pipeline` object as `bloom_enabled`, `bloom_threshold`, `bloom_intensity`, `bloom_radius` under the rules of ADR-0100 (read case-insensitively, the object written only when a field differs from its default, so a world saved before this decision loads with bloom off). A negative or non-finite number becomes 0. They are resolved into `ResolvedEnvironmentSettings` (`BloomEnabled`, `BloomThreshold`, `BloomIntensity`, `BloomRadius`; `CreateLegacy` carries the defaults with bloom off), taken from the view override like the other fields, and editable in the world details panel. Bloom has no effect in `Legacy`, where no HDR target exists. These defaults are an arbitration of the agent (P7).
- **Shader.** `Bloom.fx` is a pixel-shader-only effect drawn through `SpriteBatch`, registered like `Tonemap.fx` (content entry, `BuiltInShaderCatalog`, hot reload). Techniques: `Bloom_BrightPass` (per texel, the part above `BloomThreshold` with a soft knee of half the threshold, then the average of the 2 x 2 texels of the destination pixel), `Bloom_Downsample` (average of 2 x 2 texels), `Bloom_BlurHorizontal` and `Bloom_BlurVertical` (9-tap separable Gaussian, sigma 2, weights normalized to sum 1, tap spacing `BloomTexelSize`), `Bloom_Composite` (`BloomIntensity` times the texture, drawn with additive blending). The effect reads and writes linear light and never encodes.
- **Chain.** From the HDR scene target: bright pass to half size, downsample to quarter and eighth size, blur of each level (horizontal into a partner target, vertical back), then each level is added to the HDR scene target by the composite technique, before the tonemapping.
- **Target format rule.** The bloom targets have the surface format of the HDR scene target of the view (`hdrTarget.Format`), never a fixed format, and `DepthFormat.None` (they are only sampled and blended). When the format of the HDR target fell back to `Color` there is no headroom above 1 for the threshold to work on: bloom is skipped for the view, with a warning logged once, and no exception leaves `RenderHdrView`.
- **Target lifetime rule.** Bloom targets (the half, quarter and eighth sizes and the partner of each size) are taken from `RenderTargetPool.Shared` and given back at the end of the pass. They are tracked and evicted by the same rule as the HDR targets: the tracked key is (width, height, surface format, depth format), the key of each acquired bloom target is stamped with the render index, and `HdrTargetEviction.CollectStale` selects the keys unused for more than `MaxIdleRenders` renders, whose free targets `TrimFree` then disposes with the depth format of the key. A window resize therefore leaves no bloom chain of an old size behind. Creations and releases of bloom targets are counted and logged at Debug level, like those of the HDR targets.
- **Place in the frame.** The bloom runs in `RenderHdrView`, between the scene and `DrawTonemap`, only for a view whose resolved `BloomEnabled` is true. The interface and the screen sprites are drawn after the tonemapping and are not affected. All GPU state touched (render target, viewport, blend, depth, rasterizer, samplers) is restored, and the pass allocates nothing per frame.

## Consequences

- No change for existing worlds and assets: the settings are off by default, the keys are absent from saved files, and `Legacy` has no HDR target; no image changes. A view in `LinearHdr` with bloom off draws exactly what it drew before this decision.
- Bloom costs, for a view that asks for it, a bright pass, two downsamples, three separable blurs (six draws) and three additive draws, on targets at most a quarter of the pixels of the view, plus six pooled targets of the HDR format (about 8 bytes per pixel in `HalfVector4`).
- The reach of the glow is bounded by the eighth-size level (a few dozen pixels at the default radius): this is a glow, not a veil over the whole image. The radius scales the tap spacing, so large values band.
- A view whose HDR format fell back to `Color` has no bloom.
- Bloom is applied to the scene as a whole, before any per-object exclusion: nothing can opt out of it besides staying under the threshold.
