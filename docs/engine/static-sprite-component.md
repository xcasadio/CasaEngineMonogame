# Static sprite component

`StaticSpriteComponent` draws one sprite (a `.sprite` asset, `SpriteData`) as the root component of an entity. This page covers the fields added by [ADR-0129](../decisions/0129-cutscene-actions-drive-named-sprite-entities-over-time.md) so that a cutscene can drive it ([cutscene actions](cutscene_commandes_sequentielles_async_coroutine.md#sprite-repeat-and-exit-on-input-actions-adr-0090)). `Projects/SpriteCutsceneSample` is the working example.

## JSON

All keys are on the component node, next to `spriteDataName`. Every new key is optional, and an absent key gives the former behaviour.

| Key | Type | Default | Meaning |
|---|---|---|---|
| `spriteDataName` | string | none | Name of a catalogued `.sprite`. Absent, empty or `"null"` means no sprite. |
| `color` | object `{"r", "g", "b", "a"}` | white | Tint passed to the renderer. Four integer keys from 0 to 255. |
| `blend_mode` | string | `Opaque` | Exact name of `SpriteBlendMode`: `Opaque`, `AlphaBlend`, `Additive`, `Subtractive`. |
| `draw_order` | integer | `0` | Order of the sprite among sprites of equal Z and no sort key (see [Draw order of sprites of equal depth](#draw-order-of-sprites-of-equal-depth)). |

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

A `color` without one of the four keys, with a non-integer or a value outside 0..255, a `blend_mode` that is not an exact enum name, a `draw_order` that is not a JSON integer of the `int` range (a decimal, a string, a boolean, `null`, an array or an object), or an `is_visible` that is not a boolean, fails the load with an `InvalidDataException` that names the component or the entity, the field and the value. There is no silent default.

### Sprite name resolution

`Load` only keeps `spriteDataName`. The name is resolved in `InitializeWithWorld`, once the entity and its world are known, through the asset catalogue. A name that is not in the catalogue throws an `InvalidDataException` that names the sprite (also for `TryLoadSpriteData`). The copy constructor carries the name, the id, the colour and the blend mode, and a clone builds its sprite in `InitializeWithWorld`, so an entity declared in a `.world` and a cloned asset entity both draw. The editor saves the fields only when they differ from the defaults, so an existing file is unchanged.

## Draw order of a blended sprite

- `Opaque` (the default) keeps the plain draw path, with `Color` as the tint.
- Any other mode applies to the whole sprite. The per-texel semi-transparency of `SpriteData.PsxSemiTransparency` ([sprite-psx-semi-transparency.md](sprite-psx-semi-transparency.md)) is a separate path and is not tied to `blend_mode`.
- **With a `DepthSortable2DComponent`** on the entity, the sprite goes through the renderer overload that takes a sort key, with its blend mode, like an opaque sorted sprite.
- **Without it**, the sprite goes through the key-less blended overloads of `SpriteRendererComponent.DrawSprite` (the internal one with a scissor rectangle, and its twin with a `drawOrder` when `draw_order` is not 0), which queue it without a sort key. It keeps its order by Z, like an opaque sprite. The reason: the comparator compares sort keys as soon as one of the two entries has one (`CompareSpriteDisplayData`), so a keyed entry leaves the Z order. Among entries without a key, the larger Z is drawn first.

## Draw order of sprites of equal depth

[ADR-0131](../decisions/0131-sprites-of-equal-depth-follow-a-draw-order.md). `SpriteRendererComponent` sorts its queue with `List<T>.Sort`, which is not stable, so the order of sprites of the same Z used to be left to the sort. The `draw_order` field lets data choose it. The working example is `Projects/SpriteDrawOrderSample`.

* **Field.** `draw_order` is an optional JSON integer on the component node, 0 when absent. `StaticSpriteComponent.DrawOrder` holds it and the copy constructor carries it. The editor save path writes it only when it is not 0. With `DrawOrder` 0 the component makes the same renderer calls as before; with another value it uses the overloads that take a final `int drawOrder`.
* **Comparator rule** for two entries without a sort key: the larger Z is drawn first (as above); at an equal Z, the smaller `draw_order` is drawn first, so the sprite drawn later is on top; at an equal `draw_order`, the smaller submission rank is drawn first. The submission rank is the position of the entry in the queue of the frame, which makes each frame deterministic instead of left to the sort.
* **Sprites with a sort key are unaffected.** With a `DepthSortable2DComponent`, the entries are compared by their keys and the `draw_order` has no effect, whatever its value. An order for keyed sprites is outside the scope of ADR-0131.
* **Limit (O1).** At an equal Z and an equal `draw_order`, the submission rank comes from the world, which submits its visible entities in the order of a query on its octree. That order depends on the cells and can change when a sprite moves, so such sprites are ordered the same way within a frame but not necessarily from one frame to the next. Data that needs a fixed order gives the overlapping sprites different `draw_order` values.
* **Depth.** Sorted sprites still test and write depth with `LessEqual`, so the order only matters for sprites of the same Z, which blend over each other in the order above.

## 2D convention

Measured on a capture of `Projects/SpriteCutsceneSample` at the Launcher (320x180 design resolution, shown at 960x540):

- One world unit is one design pixel.
- Y points up.
- The 2D camera is centred on its target: with the target on the origin, x is in -160..160 and y in -90..90.
- The hotspot of a sprite (the `hotspot` of the `.sprite` file, `SpriteData.Origin`) is placed on the component position. The sample's sprites have their hotspot at the centre of the 48x48 image, and a sprite sent to (-60, 35) was measured centred there.

## Motion and tint

`StartMove(target, speed)`, `StartTint(color, duration)`, `StopMove()`, `StopTint()` and `StopMotionAndTint()` drive the component over time; the cutscene actions call them. `Update` advances them with the received time, without allocation. The rules (constant speed, exact arrival, linear rounded tint, replacement) are in the cutscene doc. Their state is on the component, so they continue if the cutscene that started them stops.

## Video as a sprite

`VideoSpriteComponent` draws a film in the world the way this component draws a sprite: see [video.md](video.md#9-the-world-video-component). The film neither tests nor writes depth, so a sprite of a smaller Z blends over it. Among the blended sprites of this page the depth test still applies: a smaller Z drawn after a larger Z fails under it (O3 of [ADR-0130](../decisions/0130-a-world-video-component-and-an-any-key-input.md)), so sprites that must blend over each other keep one Z and take different `draw_order` values (`VideoSpriteComponent` carries the same field: see [video.md](video.md#93-playback-draw-and-depth-rule)).
