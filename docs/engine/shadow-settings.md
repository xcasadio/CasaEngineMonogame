# Shadow settings

How a world configures the forward directional shadow map and how the lit shaders (`LitForward.fx`, `skinEffect.fx`) apply it. Decisions: ADR-0102 and, for the tap count of the soft-edge filter, ADR-0127. The settings live in `WorldEnvironmentSettings.Shadows` (`ShadowSettings`), are serialized with the world (`shadows` object) and are edited in the world panel of the editor ("Directional Shadows" section).

## The settings

| Setting | Key | Default | Effect |
|---|---|---|---|
| `Enabled` | `enabled` | `false` | Renders the shadow map of the first shadow-casting directional light. |
| `Resolution` | `resolution` | 1024 | Side of the square shadow map, in texels. |
| `DepthBias` | `depth_bias` | 0.001 | Bias on the compared depth, a fraction of the map depth (see "Depth range"). |
| `NormalBias` | `normal_bias` | 0 | Offset of the receiver position along its interpolated vertex normal (never the normal-mapped normal), in world units. |
| `MaxDistance` | `max_distance` | 100 | Half-side, in world units, of the square the map covers around the camera (the lateral extent). |
| `Mode` | `mode` | `OccludeDirectLight` | How the shadow is applied (see below). |
| `ShadowColor` | `shadow_color` | black | Colour of a fully shadowed pixel in `MultiplyLitColor` mode. |
| `FilterRadius` | `filter_radius` | 0 | Soft edge: radius, in texels, of the disc the filter reads on. |
| `FilterTaps` | `filter_taps` | 12 | Soft edge: number of shadow-map reads of the filter, 12, 32 or 64 (only when `FilterRadius` is above 0). |
| `FadeDistance` | `fade_distance` | 0 | Width of the band before `MaxDistance` over which the shadow fades out. |
| `DepthRange` | `depth_range` | 0 | Depth extent of the map, kept separate from `MaxDistance`. |

`mode`, `shadow_color`, `filter_radius`, `filter_taps`, `fade_distance` and `depth_range` are optional in the file: they are written only when they differ from the default, so existing worlds keep their exact format, and an absent key reads as the default. A world with every new setting at its default renders exactly as before (checked on the demo images byte for byte).

```csharp
var shadows = world.EnvironmentSettings.Shadows;
shadows.Enabled = true;
shadows.Resolution = 4096;
shadows.MaxDistance = 100.0f;                       // lateral half-extent of the map around the camera
shadows.DepthRange = 150.0f;                        // depth of the map, independent of MaxDistance
shadows.FadeDistance = 15.0f;                       // fade out between 85 and 100 m from the map centre
shadows.FilterRadius = 2.0f;                        // soft edge, 2 texels
shadows.FilterTaps = 64;                            // 12 (default), 32 or 64 reads for that soft edge
shadows.Mode = ShadowMode.MultiplyLitColor;         // multiply the lit colour instead of removing the light
shadows.ShadowColor = new Vector3(0.25f, 0.26f, 0.27f);
world.EnvironmentSettings.MarkDirty();
```

## Mode and colour

- `OccludeDirectLight` (default): the shadow factor scales the direct diffuse and specular of the shadowed light. The ambient term, the emissive colour and the reflections are not shadowed: a fully shadowed pixel is lit by the ambient term alone. `ShadowColor` is not used.
- `MultiplyLitColor`: the shadowed light is not occluded. The final colour of the pixel, excluding the emissive colour (lit colour, specular and reflection contribution), is multiplied by `lerp(1, ShadowColor, 1 - shadowFactor)`. Alpha and emissive are untouched. This reproduces a game that multiplies the whole image by a shadow tint: a shadow of `(0.25, 0.26, 0.27)` leaves a pixel at a quarter of its lit colour, specular highlights and reflections included.

The shadow factor is computed once per pixel. With several lights, `MultiplyLitColor` tints the contributions of every light, and the ambient term, by the factor of the single shadowed light: a shadowed pixel is darkened even for the unshadowed lights. Use it for one dominant shadow-casting light (the sun). `ReceiveShadows = false` on a component, or no shadowed light, leave the colour unchanged. Vertex-lighting techniques (`LitForward_VertexLighting*`, `LitForward_OneLight*`) have no shadows in either mode.

## Normal bias (`NormalBias`)

The shadow lookup reads the shadow map at the receiver position moved by `NormalBias` along the interpolated vertex normal (normalised), in every lit shader (`LitForward.fx`, `LitPbr.fx`, `skinEffect.fx`). A normal map does not move it: it changes how the pixel is lit, not where its shadow edge is, so a normal-mapped wall keeps a clean shadow edge whatever its relief. Back faces use the same normal as before (no flip).

## Soft edges (`FilterRadius`, `FilterTaps`)

0 keeps the former 4-tap filter. Above 0 the receiver averages depth comparisons taken on a disc of radius `FilterRadius` texels; `FilterTaps` is the number of comparisons: 12 (default) on a fixed Poisson disc, 32 or 64 on a Vogel disc (tap `i` of `N` at radius `sqrt((i + 0.5) / N)` and angle `i * 2.39996323` rad on the unit disc, scaled by the radius). Any other value is brought back to the nearest of the three: 12 below 22, 32 from 22 to 47, 64 from 48. The shader reads the count from the uniform `ShadowFilterTaps` set by `ForwardLightBinder`; it runs the 32-tap or the 64-tap disc for exactly those values and the 12-tap Poisson code, unchanged, for every other value, so a setting left at 12 renders as before (checked on the demo images byte for byte, at radius 0 and at radius 3). In the demo's measurement configuration the width of the transition between 10 % and 90 % of a shadow edge is 0.8 pixel at radius 0, 2.5 pixels at 3 and 4.5 pixels at 6. The disc is not rotated per pixel: a large radius can show its 12-tap pattern as banding on a smooth edge and calls for a larger `DepthBias` or `NormalBias` to avoid self-shadowing on faces turned away from the light. At a grazing angle the penumbra of a shadow is wide and 12 fixed taps show it as a fan of bands; 32 taps make the bands thinner and 64 give a smooth gradient (in the GPU test `ShadowFilterTapsGpuTests`, a penumbra of about 430 pixel columns shows 13 levels at 12 taps, 33 at 32 taps and 62 at 64 taps). The cost is `FilterTaps` shadow-map reads per shadowed pixel (12, 32 or 64) instead of 4, which is not measured in frame time.

## Fade out (`FadeDistance`)

The shadow goes linearly to "lit" between `MaxDistance - FadeDistance` and `MaxDistance`, measured from the centre of the shadow map in the plane of the map as a square (the shape of the map, not a circle). `FadeDistance` is limited to `MaxDistance`. The centre of the map is the camera position, so the band follows the camera. Without a fade the shadow stops at the edge of the map.

## Depth range (`DepthRange`)

By default the depth of the shadow map follows `MaxDistance`: the light is `MaxDistance` upstream of the camera and the far plane is at `2 * MaxDistance`, so shrinking the map to sharpen the shadows also clips tall casters. With `DepthRange > 0` the light is placed `DepthRange` upstream (at least 1) and the far plane at `2 * DepthRange`, while the lateral extent stays `MaxDistance`. It changes only the matrix of the shadow pass: there is no shader uniform.

The depth bias is a fraction of the depth span, so it depends on the depth range `R` (`DepthRange`, or `MaxDistance` when it is 0): the bias in metres is `DepthBias * (2 * R - 0.1)` under DirectX and `2 * DepthBias * (2 * R - 0.1)` under DesktopGL (the shaders remap the depth with `* 0.5 + 0.5` there), for example 0.4 m for `DepthBias` 0.001 and `R` 100 on DesktopGL. Changing `DepthRange` scales it in proportion; lower `DepthBias` by the same ratio to keep the same bias in metres.

## Measuring it

`StaticShadowValidationDemo` in its measurement configuration reads the variables below (see `docs/engine/lit-ambient-and-ambient-source.md` for the configuration itself). Without them the demo image is unchanged.

| Variable | Effect |
|---|---|
| `CASAENGINE_SHADOW_DEMO_MODE=MultiplyLitColor` | `ShadowSettings.Mode` |
| `CASAENGINE_SHADOW_DEMO_COLOR=0.25,0.26,0.27` | `ShadowSettings.ShadowColor` |
| `CASAENGINE_SHADOW_DEMO_FILTER_RADIUS=3` | `ShadowSettings.FilterRadius` |
| `CASAENGINE_SHADOW_DEMO_MAX_DISTANCE=28` | `ShadowSettings.MaxDistance` (48 in the demo by default) |
| `CASAENGINE_SHADOW_DEMO_FADE_DISTANCE=2` | `ShadowSettings.FadeDistance` |
| `CASAENGINE_SHADOW_DEMO_DEPTH_RANGE=96` | `ShadowSettings.DepthRange` |

Capture with `CASAENGINE_START_DEMO="Static shadow validation demo"` and `CASAENGINE_CAPTURE_SCREENSHOT_PATH=<png>` on a 1024 x 768 window. In the measurement configuration the shadowed pixel (310, 420) is `(64, 64, 64)` and the lit pixel (240, 435) `(223, 215, 198)`; with `MultiplyLitColor` and the colour above the shadowed pixel is `(56, 56, 53)`, the lit value times the colour.

## Limits

- One shadow-casting directional light and one shadow map per view; point and spot lights do not cast shadows.
- The map is orthographic and centred on the camera, not on the shadow caster or the player.
- Casters in a transparent render queue are skipped by the shadow pass.
- `FilterRadius`, `FilterTaps`, `FadeDistance`, `ShadowColor` and `Mode` apply to the lit pixel-lighting techniques and to the skinned shader (rigged models); a shadow tint on textured, normal-mapped and skinned surfaces is covered by shader source checks and compilation, the demo scene does not exercise those techniques.
- A world saved with the new keys, read by an older engine version, loses them (unknown keys are ignored).
