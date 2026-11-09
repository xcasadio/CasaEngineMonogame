# Static sprite component

`StaticSpriteComponent` draws one sprite (a `.sprite` asset, `SpriteData`) as the root component of an entity. This page covers the fields added by [ADR-0090](../decisions/0090-cutscene-actions-drive-named-sprite-entities-over-time.md) so that a cutscene can drive it ([cutscene actions](cutscene_commandes_sequentielles_async_coroutine.md#sprite-repeat-and-exit-on-input-actions-adr-0090)). `Projects/SpriteCutsceneSample` is the working example.

## JSON

All keys are on the component node, next to `spriteDataName`. Every new key is optional, and an absent key gives the former behaviour.

| Key | Type | Default | Meaning |
|---|---|---|---|
| `spriteDataName` | string | none | Name of a catalogued `.sprite`. Absent, empty or `"null"` means no sprite. |
| `color` | object `{"r", "g", "b", "a"}` | white | Tint passed to the renderer. Four integer keys from 0 to 255. |
| `blend_mode` | string | `Opaque` | Exact name of `SpriteBlendMode`: `Opaque`, `AlphaBlend`, `Additive`, `Subtractive`. |

The entity node gains `is_visible` (boolean, true when absent). `Entity.IsVisible` false cuts the entity's `Draw`, so an entity can start hidden and a cutscene shows it.

The excerpt below shows only the keys this page covers. A loadable entity node also needs its `id`, the root component's `id`, `script_class_name` and `components`, as in `Projects/SpriteCutsceneSample/SpriteWorld.world`.

```json
{
  "name": "orb-left",
  "is_visible": false,
  "root_component": {
    "name": "StaticSpriteComponent",
    "type": "StaticSpriteComponent",
    "coordinates": { "position": { "x": -190, "y": 35, "z": 0 }, "scale": { "x": 1, "y": 1, "z": 1 }, "rotation": { "x": 0, "y": 0, "z": 0, "w": 1 } },
    "children_component": [],
    "spriteDataName": "orb",
    "color": { "r": 255, "g": 255, "b": 255, "a": 255 },
    "blend_mode": "Additive"
  }
}
```

Note the two colour formats: the component uses the object of the entities, a cutscene action uses an array `[r, g, b]` or `[r, g, b, a]`.

### Malformed fields

A `color` without one of the four keys, with a non-integer or a value outside 0..255, a `blend_mode` that is not an exact enum name, or an `is_visible` that is not a boolean, fails the load with an `InvalidDataException` that names the component or the entity, the field and the value. There is no silent default.

### Sprite name resolution

`Load` only keeps `spriteDataName`. The name is resolved in `InitializeWithWorld`, once the entity and its world are known, through the asset catalogue. A name that is not in the catalogue throws an `InvalidDataException` that names the sprite (also for `TryLoadSpriteData`). The copy constructor carries the name, the id, the colour and the blend mode, and a clone builds its sprite in `InitializeWithWorld`, so an entity declared in a `.world` and a cloned asset entity both draw. The editor saves the fields only when they differ from the defaults, so an existing file is unchanged.

## Draw order of a blended sprite

- `Opaque` (the default) keeps the plain draw path, with `Color` as the tint.
- Any other mode applies to the whole sprite. The per-texel semi-transparency of `SpriteData.PsxSemiTransparency` ([sprite-psx-semi-transparency.md](sprite-psx-semi-transparency.md)) is a separate path and is not tied to `blend_mode`.
- **With a `DepthSortable2DComponent`** on the entity, the sprite goes through the renderer overload that takes a sort key, with its blend mode, like an opaque sorted sprite.
- **Without it**, the sprite goes through `SpriteRendererComponent.DrawSprite(Sprite, Vector2, float, Vector2, Color, float zOrder, SpriteBlendMode)`, which queues it without a sort key. It keeps its order by Z, like an opaque sprite. The reason: the comparator compares sort keys as soon as one of the two entries has one (`CompareSpriteDisplayData`), so a keyed entry leaves the Z order. Among entries without a key, the larger Z is drawn first.

## 2D convention

Measured on a capture of `Projects/SpriteCutsceneSample` at the Launcher (320x180 design resolution, shown at 960x540):

- One world unit is one design pixel.
- Y points up.
- The 2D camera is centred on its target: with the target on the origin, x is in -160..160 and y in -90..90.
- The hotspot of a sprite (the `hotspot` of the `.sprite` file, `SpriteData.Origin`) is placed on the component position. The sample's sprites have their hotspot at the centre of the 48x48 image, and a sprite sent to (-60, 35) was measured centred there.

## Motion and tint

`StartMove(target, speed)`, `StartTint(color, duration)`, `StopMove()`, `StopTint()` and `StopMotionAndTint()` drive the component over time; the cutscene actions call them. `Update` advances them with the received time, without allocation. The rules (constant speed, exact arrival, linear rounded tint, replacement) are in the cutscene doc. Their state is on the component, so they continue if the cutscene that started them stops.
