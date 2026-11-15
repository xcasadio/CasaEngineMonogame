# Colour pipeline

How a world renders in linear light with a high dynamic range target, bloom and tonemapping (`LinearHdr`), and how it differs from the historical pipeline (`Legacy`). Decisions: see ADR-0103, ADR-0104, ADR-0105, ADR-0106, ADR-0107, ADR-0113.

## Modes

| Mode | What it does |
|---|---|
| `Legacy` (default) | Today's behaviour. Every colour is a display colour, lit and written in display space straight into the target of the view. Images are bit-identical to those of an engine without the colour pipeline. |
| `LinearHdr` | The scene is lit in linear light into a half-float target, tonemapped, and written to the destination of the view; the interface is drawn after, untouched. |

## Settings: the `color_pipeline` key

The settings live in `WorldEnvironmentSettings.ColorPipeline` (`ColorPipelineSettings`), are resolved by `EnvironmentResolver` into `ResolvedEnvironmentSettings` (`ColorPipelineMode`, `Tonemapper`, `Exposure`, `IsLinearHdr`) and can be replaced per view by the environment override (the override replaces the settings as a whole).

| Field | Key | Default | Effect |
|---|---|---|---|
| `Mode` | `mode` | `Legacy` | `Legacy` or `LinearHdr`. |
| `Tonemapper` | `tonemapper` | `AcesApprox` | `AcesApprox` (Narkowicz fit) or `Reinhard`. |
| `Exposure` | `exposure` | 1 | Scalar multiplier applied before the tonemapper. A negative, NaN or infinite value becomes 0. No automatic exposure. |
| `BloomEnabled` | `bloom_enabled` | false | Adds a glow around the bright parts of the scene before the tonemapping (ADR-0107). No effect in `Legacy`. |
| `BloomThreshold` | `bloom_threshold` | 1 | Scene luminance (linear light, largest colour component) above which a pixel feeds the bloom; soft knee of half the threshold. |
| `BloomIntensity` | `bloom_intensity` | 0.5 | Scale of the glow added to the scene. |
| `BloomRadius` | `bloom_radius` | 1 | Scale of the blur tap spacing; larger values spread the glow further. |
| `AntiAliasing` | `anti_aliasing` | `None` | `None` or `Fxaa`. `Fxaa` filters the tonemapped scene before the post-tonemap renderers and the interface (ADR-0113). No effect in `Legacy`, nor in a view that renders its interface with the scene (see the limits). |

The key is an object written only when a field differs from its default, and removed otherwise, so existing worlds keep their format. Keys are read case-insensitively. A world without the key loads as `Legacy`; an older engine reading a world that has it ignores it. The editor's world details panel has a mode combo, a tonemapper combo and an exposure field.

```csharp
var colorPipeline = world.EnvironmentSettings.ColorPipeline;
colorPipeline.Mode = ColorPipelineMode.LinearHdr;
colorPipeline.Tonemapper = TonemapOperator.AcesApprox;
colorPipeline.Exposure = 1.0f;
world.EnvironmentSettings.MarkDirty();
```

```json
"color_pipeline": { "mode": "LinearHdr", "tonemapper": "AcesApprox", "exposure": 1.0 }
```

## Frame order in `LinearHdr`

For a view that really uses the HDR target (`RenderPipeline.RenderHdrView`):

1. The scene is rendered into a target taken from `RenderTargetPool` (`HalfVector4`, `Depth24Stencil8`, the size of the view rectangle after the resolution scale). The clear colour is decoded in floating point.
2. If bloom is enabled for the view, `BloomRenderer` adds the glow to the HDR target (see below).
3. The destination of the legacy path is bound (back buffer, editor host texture, scene target, retained target of the frozen frame, or the render target of the view's own surface) and its depth cleared the way the legacy path clears it.
4. `Tonemap.fx` (`Tonemap_Reinhard` or `Tonemap_AcesApprox`, parameter `Exposure`) is drawn full-rectangle and opaque through a `SpriteBatch`. It tonemaps then encodes with `pow(c, 1 / 2.2)`.
   With `AntiAliasing = Fxaa` (and the interface composed afterwards), steps 3 and 4 become: the tonemapping is drawn into a pooled `Color` target of the view size, then the destination is bound and `FxaaRenderer` draws that target 1:1 into it with `Fxaa.fx` (see below). The steps after are unchanged.
5. The post-tonemap flush: renderers implementing `IPostTonemapRenderer` (`Renderer2DComponent`: screen sprites, text, 2D lines) are flushed on the tonemapped image, with `DepthStencilState.None`.
6. The interface (`IUICompositionService`) is composed on top, untouched.
7. The HDR target goes back to the pool. The existing copy to the back buffer, the frozen-frame retention, the debug overlay and the presenter run after, unchanged.

A split-screen of back-buffer views works because the back buffer's `RenderTargetUsage` is set to `PreserveContents` for the one binding of the destination, then restored (ADR-0104).

## What is decoded, and where

The colours the engine knows are display colours. In `LinearHdr` they are decoded to linear light with the 2.2 power law (`c^2.2`, the inverse of the encoding of `Tonemap.fx`) where they are read. Normal maps, shadow maps and any non-colour data are never decoded.

| Value | Decoded by | Where |
|---|---|---|
| Base colour (texture times detail, after the modulation), vertex colours, `DiffuseColor`, `EmissiveColor`, `SpecularColor`, `MaterialAmbientColor` | `DecodeColor` of `ColorPipeline.fxh` | Shader (lit, unlit, skinned) |
| Environment cube, material reflection cube, local probes, sky cubes and their tint | `DecodeColor` | Shader (`EnvironmentCubeIsLinear` skips it for a cube known to be linear) |
| Particles, 3D sprites, 3D lines: texture times vertex or constant colour | `DecodeColor4` | Shader (`TexturedPrimitive.fx`, `SpriteBatch.fx`, `PsxQuad.fx`, `DebugPrimitiveColor.fx`) |
| Light colours | `ColorSpace.Decode`, before the multiply by intensity | CPU (`ForwardLightBinder`) |
| Ambient colour | `Decode(AmbientColor) * AmbientIntensity` | CPU (`ResolvedEnvironmentSettings.EffectiveAmbientColor`) |
| Shadow colour | `ColorSpace.Decode` | CPU |
| Clear colour of the HDR target | `ColorSpace.Decode` in floating point (`Vector4` overload of `Clear`) | CPU |
| Sky background gradient and sun colour (`SkyBackgroundViewPipeline`) | The gradient is drawn from a second `HalfVector4` texture built from the decoded colours; the sun colour is decoded before its 0.65 factor | CPU |

Only a cube built from a `.hdr` panorama is linear (`BackgroundCubemapIsLinear`, `SpecularEnvironmentCubemapIsLinear`, decided by the file extension). The procedural sky, the physical atmosphere, DDS and authored cubes, probes and the material reflection cube are decoded.

### Which views decode: `LinearColorActive`

`ResolvedEnvironmentSettings.LinearColorActive` is set by `RenderPipeline.Render`, on its own copy of the resolved environment, only when the planner chose the HDR target and the tonemap shader is loaded. Everything built from that copy carries it, and the shaders' `ColorPipelineIsLinear` constant is bound from it (`EnvironmentShaderBinder`, `ForwardLightBinder`, `SkyCubemapRenderer`, the particle, sprite and line renderers). The resolved mode (`IsLinearHdr`) is never used to decode: a view of a `LinearHdr` world that stays on the legacy path (see below) keeps its legacy colours. A copy of the environment made elsewhere (editor previews, tests) keeps the value false.

A view stays on the legacy path when it keeps the previous content of its colour buffer (`ClearColorBuffer` false), has an empty rectangle, or when `Tonemap.fx` is missing (logged once).

## Bloom

`BloomRenderer` (ADR-0107) works on the HDR target of the view, before the tonemapping, so the interface and the screen sprites are never touched. From the scene: a bright pass to half size (per texel, the part above `BloomThreshold` with a soft knee), two 2 x 2 downsamples (quarter and eighth size), a separable 9-tap Gaussian blur of each level (horizontal into a partner target, then vertical back), then the three levels are added to the scene target with `BloomIntensity` shared between them. The shader is `Bloom.fx` (`Bloom_BrightPass`, `Bloom_Downsample`, `Bloom_BlurHorizontal`, `Bloom_BlurVertical`, `Bloom_Composite`), drawn through `SpriteBatch`, hot reloadable like `Tonemap.fx`.

- The bloom targets have the format of the HDR target (`HalfVector4` unless it fell back) and no depth buffer, and come from `RenderTargetPool.Shared`. Their keys (width, height, format, depth format) are tracked with the HDR keys and released by the same rule (`HdrTargetEviction`): a size no view has used for more than four renders is disposed, so a resize leaves no bloom chain behind. Creations and releases are logged at Debug level.
- If the HDR target fell back to `Color`, bloom is skipped for the view with one warning: no value above 1 survives for the threshold to work on.
- The glow reaches about one eighth-size level (a few dozen pixels at radius 1). A view whose pipeline does not implement `IScenePhaseViewPipeline` has its interface in the HDR target, so the bloom also catches it.
- Demo: `CASAENGINE_DEMO_BLOOM=1` (and optionally `CASAENGINE_DEMO_BLOOM_THRESHOLD`, `_INTENSITY`, `_RADIUS`) apply bloom to any demo in `LinearHdr`; in `Colour pipeline demo` the B key toggles it.

## FXAA

`FxaaRenderer` (ADR-0113) is the anti-aliasing of a `LinearHdr` view, a post-process on the display-encoded colour. It sits **after** the tonemapping and **before** the post-tonemap flush and the interface, so screen text, sprites and the interface are never filtered. `Fxaa.fx` has one technique, `Fxaa` (parameter `FxaaTexelSize`), drawn through `SpriteBatch` with a linear clamped sampler, hot reloadable like `Tonemap.fx`. Per pixel it computes the luminance of the encoded colour, leaves a pixel alone when the local contrast is small, otherwise finds the orientation of the edge, follows it up to twelve texels each way, and reads the colour at a position displaced across the edge (the linear filter does the blend); a sub-pixel term catches isolated bright or dark pixels. No pixel is shifted: the output has the size of the input.

- The tonemapped image goes into a `RenderTargetPool.Shared` target (`Color`, no depth, the size of the view rectangle), then the pass writes the destination. The key of that target is stamped like the bloom ones and released by the same rule (`HdrTargetEviction`); `FxaaTargetCreationCount` and `FxaaTargetDisposalCount` count it apart from the bloom. Without FXAA the path is the unchanged one: no intermediate target, no extra draw.
- The pass runs only in a view whose pipeline implements `IScenePhaseViewPipeline` (the interface is composed after it). A view that renders everything in one block (the editor views, `OverlayViewPipeline`) is not filtered, so as not to blur its interface.
- `RenderStats.FxaaPasses` is 1 for a view the pass filtered in the frame, 0 otherwise; the demo harness writes it per view at the moment of a capture (`[DemosGame] FXAA passes at capture`).
- Demo: `CASAENGINE_DEMO_AA=Fxaa` applies it to any demo in `LinearHdr`. In `Colour pipeline demo` the measurement rectangles and segment are `FxaaTextBorderRect`, `FxaaPanelBorderRect` and `FxaaEdgeSegment`.
- It blurs luminance edges only: it does not add sub-pixel detail (no TAA, no MSAA), and a thin bright line on a dark background can still shimmer when the camera moves.

## Usage

A game selects the pipeline per world (snippet above) or per view with the environment override. A renderer that draws display-ready colours (HUD, fonts) implements `IPostTonemapRenderer`; a renderer that draws scene colours declares `float ColorPipelineIsLinear;` before including `ColorPipeline.fxh` in its effect and binds it from `frame.Environment.LinearColorActive`.

For the demos, the harness variables `CASAENGINE_DEMO_COLOR_PIPELINE`, `CASAENGINE_DEMO_TONEMAPPER` and `CASAENGINE_DEMO_EXPOSURE` apply a mode to any demo, and `Colour pipeline demo` (with `CASAENGINE_COLOR_DEMO_LIGHT_INTENSITY` and `CASAENGINE_COLOR_DEMO_BUFFERED`) is the measurement scene.

## Known limits

- **View pipelines without `IScenePhaseViewPipeline` (open point O1).** The interface is composed after the tonemapping only for a pipeline implementing `IScenePhaseViewPipeline` (`DefaultViewPipeline`, `SkyBackgroundViewPipeline` when its inner pipeline does). Any other pipeline (the editor's `OverlayViewPipeline`, a game's own pipeline) is rendered whole, interface included, into the HDR target and tonemapped with the scene; a warning is logged once per pipeline type.
- **`Renderer2DComponent` loses the scene depth test (open point O3).** In `LinearHdr` the screen sprites, text and 2D lines are flushed after the tonemapping: the depth of the scene lives in the HDR target, not in the destination, so they never test it and are drawn over the scene like the interface. A pipeline without `IScenePhaseViewPipeline` keeps them in the HDR target, tonemapped with the scene.
- **Mipmaps are averaged in encoded space (open point O4).** The mip chain (ADR-0100) is built from display values; a high-contrast texture is slightly darker in `LinearHdr` when minified.
- **Emissive colours are treated as display colours.** `EmissiveColor` is decoded like any colour, so an emissive colour is a display colour (at most 1 per channel), not a radiance that can exceed 1; a material cannot yet emit more than white.
- **3D lines are covered by a text test only.** `Line3dRendererComponent` binds the constant (proved by a test on the shader text); no demo draws 3D lines in the measurement scene.
- **`TexturedPrimitive.fx` is shared with the UI backend.** The particle renderer uses its own clone of the effect and binds the constant; the MGUI backend keeps its own instance and never binds it, so the interface keeps its display colours.
- **`DebugSolidColor.fx` is not decoded.** It is the editor's transform gizmo, drawn over the view by the editor.
- **`LegacySkyCube.fx` is bound only by a game.** The engine does not draw it; it exposes `ColorPipelineIsLinear`, and a game drawing it in `LinearHdr` must bind the constant.
- **Format fallback.** If the device refuses `HalfVector4`, the pipeline falls back to `HdrBlendable`, then to `Color`, logged once. With `Color` the target clips above 1 and cannot hold a decoded dark value: the image is then neither HDR nor accurate in the darks.
- **Physical atmosphere.** A cube from the physical atmosphere with a sun intensity above 1 is decoded like any display cube (ADR-0105).
- **Material colours are decoded once per pixel, not inside each light slot.** Decoding the material diffuse and specular colours inside each of the 24 unrolled light slots of `Lighting.fxh` produced a wrong `Legacy` image on the OpenGL backend even with `ColorPipelineIsLinear` at 0. The cause was not found (open point O10). The colours are therefore computed once per pixel in static variables of `Lighting.fxh`; new shaders must do the same.
- **Small `Legacy` cost of the select form (open point O11).** `DecodeColor` is a select, not a branch, so that the constant at 0 returns the colour bit for bit; the GPU evaluates the `pow` on both sides, so a `Legacy` pixel pays the extra `pow` calls (base colour, material diffuse, specular and emissive, ambient tint) for an identical result. Measured in the final cost task: +0.12 ms of wall time (+5 %) on the `Material system demo`, CPU unchanged; accepted, not fixed (see [pbr-rendering.md](pbr-rendering.md)).
- **Format of the retained frozen frame.** The retained and captured frozen-frame targets stay `Color`: they receive the tonemapped image.
- **A view with `ClearColorBuffer` false is not rendered in HDR.**
- **FXAA is not applied to a view that renders its interface with the scene.** A pipeline without `IScenePhaseViewPipeline` (the editor views, `OverlayViewPipeline`) draws everything into the HDR target and is tonemapped in one draw; filtering it would blur its interface, so `AntiAliasing` has no effect there (ADR-0113). The destinations that hold the retained frozen frame and the editor host texture are not exercised by the engine measurements.

## Cost

Each `LinearHdr` view costs one pooled HDR target (8 bytes per pixel plus depth, created once per view size) and one full-rectangle tonemapping draw.

Bloom adds, for a view that enables it, ten `SpriteBatch` passes on targets of at most a quarter of the view pixels and six pooled targets of the HDR format.

FXAA adds, for a view that enables it, one pooled `Color` target of the view size (4 bytes per pixel, no depth) and one full-rectangle `SpriteBatch` draw reading at most 34 texels per pixel.

Decisions: see ADR-0103, ADR-0104, ADR-0105, ADR-0106, ADR-0107, ADR-0113.
