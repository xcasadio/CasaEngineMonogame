# Lit ambient term and ambient source

How the lit forward shaders (`LitForward.fx`, `skinEffect.fx`) compute the ambient light of a material, and how a world picks where that ambient comes from. Decisions: ADR-0089 (surface colour applied once), ADR-0090 (ambient source).

## The lit formula

For every lit technique of both shaders:

```
colour = texture * DiffuseColor * (N.L * light + ambient_world * ambient_material) + emissive
```

- `texture` (the base colour, times vertex colour when present) is applied once, by `ComposeLitSurfaceColor` in `Lighting.fxh`.
- `DiffuseColor` is the material's base tint; it multiplies the direct light and the ambient term.
- `ambient_material` is `LitDiffuseMaterial.AmbientColor` (uniform `MaterialAmbientColor`).
- `ambient_world` is the flat `AmbientColor * AmbientIntensity` of the world, or the environment cube sampled along the surface normal (see below).
- Before ADR-0089 the ambient term was multiplied by the texture a second time: a textured material showed `texture^2 * ambient`. Scenes tuned against that look get a brighter ambient now; lower the world ambient or the material ambient to compensate.

## Ambient source

`WorldEnvironmentSettings.AmbientSource` (also on a view's `EnvironmentOverride`):

| Value | Ambient of a lit material |
|---|---|
| `Auto` (default) | With a specular environment cube bound to the world: `cube(normal) * EnvironmentAmbientColor * ambient_material`. Without a cube: `AmbientColor * ambient_material`. This is the historical behaviour. |
| `Flat` | Always `AmbientColor * ambient_material`, even when a cube is bound. |

`Flat` only changes the ambient term. The cube still drives the reflections (`SampleEnvironmentReflection`, `HasEnvironmentCubeTexture`).

Use `Flat` when the world needs a reflection cube (sky reflections on cars, glass) but a controlled, uniform ambient level.

```csharp
world.EnvironmentSettings.SpecularEnvironmentCubemap = skyCube;
world.EnvironmentSettings.AmbientColor = new Vector3(1.0f);
world.EnvironmentSettings.AmbientIntensity = 1.0f;
world.EnvironmentSettings.AmbientSource = AmbientSource.Flat;
world.EnvironmentSettings.MarkDirty();
```

### Serialization

World files carry the optional key `ambient_source` (`"Flat"`; read case-insensitively). It is written only when it is not `Auto`, so existing worlds keep their exact format; an absent key reads as `Auto`. `Clone`, `CopyFrom` and `ResetToDefaults` handle it. The editor's world details panel has an "Ambient Source" combo (undoable).

### Shader binding

`EnvironmentShaderBinder` binds `UseFlatAmbientSource` (1 for `Flat`, 0 otherwise), a uniform distinct from `HasEnvironmentCubeTexture`, declared in both `LitForward.fx` and `skinEffect.fx` at the same slot (`_ps(c36)`, `_cb(c32.z)`). The register annotations of these files are documentation only (`_vs`, `_ps`, `_cb` expand to nothing in `Macros.fxh`); the slot is still kept free of any other parameter of the file.

## Measuring it

`StaticShadowValidationDemo` has a measurement configuration (world ambient 0.5, no fill light, a textured receiver of solid 128/255, a reflective sphere with no material ambient):

| Variable | Effect |
|---|---|
| `CASAENGINE_SHADOW_DEMO_VALIDATION=1` | measurement configuration |
| `CASAENGINE_SHADOW_DEMO_REFLECTION_CUBE=1` | binds a solid (0.2, 0.6, 1.0) cube as the world's specular environment cube |
| `CASAENGINE_SHADOW_DEMO_AMBIENT_SOURCE=Flat\|Auto` | sets `AmbientSource` |

Capture with `CASAENGINE_START_DEMO="Static shadow validation demo"` and `CASAENGINE_CAPTURE_SCREENSHOT_PATH=<png>`. Expected pixels in the receiver's shadow (ambient only): 64 per channel without a cube or with `Flat`; `(13, 38, 64)` with a cube and `Auto`.

## Limits

- The ambient source is world-wide: there is no per-material switch.
- `Flat` does not change `EnvironmentAmbientColor`, which still tints the cube-based ambient in `Auto`.
