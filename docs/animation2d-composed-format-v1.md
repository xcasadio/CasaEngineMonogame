# Animation2D composed format V1

This document describes the `.anim2d` format currently loaded by `Animation2dData` and saved by `EditorAssetJsonSerializer`.

The V1 modernization keeps the existing `.anim2d` extension. Animation2D assets are composed documents made of sprite parts, property tracks, and optional gameplay events.

## Root object

Required root fields:

- `animation_type`: value loaded by `AnimationData.AnimationType` (`Once`, `Loop`, `PingPong`).
- `id`: asset GUID.
- `name`: asset display name.

Optional payload fields:

- `parts`: composed sprite part defaults.
- `tracks`: composed property tracks.
- `events`: gameplay/authoring events dispatched by the composed sampler.

`parts`, `tracks`, and `events` are optional in the loader. Saving only writes them when the corresponding lists are non-empty.

## Composed example

Composed assets use `parts` for default sprite state and `tracks` for timed changes.

```json
{
  "animation_type": "Loop",
  "id": "8f85a801-6fe3-4773-b64a-b2ff1b729c8b",
  "name": "swordman_composed_demo",
  "parts": [
    {
      "id": "body",
      "name": "Body",
      "default_sprite_id": "bac0a7fe-cc71-40ef-a296-69a809e775bf",
      "default_position": { "x": 0.0, "y": 0.0 },
      "default_draw_order": 0,
      "default_visible": true,
      "default_flip_x": false,
      "default_flip_y": false
    },
    {
      "id": "weapon",
      "name": "Weapon",
      "default_sprite_id": "ab69414d-f3ca-409c-b34a-6a8bcde3269e",
      "default_position": { "x": 18.0, "y": -8.0 },
      "default_draw_order": 10,
      "default_visible": true,
      "default_flip_x": false,
      "default_flip_y": false
    }
  ],
  "tracks": [
    {
      "target_part_id": "weapon",
      "property": "Position",
      "interpolation": "Step",
      "position_keyframes": [
        { "time_seconds": 0.0, "value": { "x": 18.0, "y": -8.0 } },
        { "time_seconds": 0.3, "value": { "x": 26.0, "y": -4.0 } }
      ]
    },
    {
      "target_part_id": "weapon",
      "property": "DrawOrder",
      "interpolation": "Step",
      "draw_order_keyframes": [
        { "time_seconds": 0.0, "value": 10 },
        { "time_seconds": 0.3, "value": -5 }
      ]
    }
  ],
  "events": [
    {
      "time_seconds": 0.3,
      "event_name": "WeaponSwapLayer"
    }
  ]
}
```

## Parts

Each entry in `parts` is loaded as `Animation2dPartData`.

- `id`: stable string referenced by tracks.
- `name`: display label.
- `default_sprite_id`: initial sprite GUID.
- `default_position`: local 2D offset `{ "x": number, "y": number }`.
- `default_draw_order`: local ordering inside the composed animation.
- `default_visible`: initial visibility.
- `default_flip_x`: initial horizontal flip flag.
- `default_flip_y`: initial vertical flip flag.

Runtime draw order is stable: parts are ordered by `DrawOrder`, then by source part index.

## Tracks

Each entry in `tracks` is loaded as `Animation2dTrackData`.

- `target_part_id`: part id from `parts`.
- `property`: one of `Sprite`, `Position`, `Visible`, `DrawOrder`, `FlipX`, `FlipY`.
- `interpolation`: currently only `Step`.

Supported keyframe arrays by property:

- `Sprite`: `sprite_keyframes`, values are sprite GUIDs.
- `Position`: `position_keyframes`, values are `{ "x": number, "y": number }`.
- `Visible`: `visible_keyframes`, values are booleans.
- `DrawOrder`: `draw_order_keyframes`, values are integers.
- `FlipX` and `FlipY`: `flip_keyframes`, values are booleans.

All keyframes use `time_seconds` plus `value`.

## Events

`events` reuse the shared `AnimationEventAsset` JSON shape:

- `time_seconds`: event trigger time in seconds.
- `event_name`: event name sent by `AnimatedSpriteComponent.AnimationEventTriggered`.

Events are dispatched by `Animation2dCompositionSampler.Update`. Seeking or resetting applies pose state but does not emit events.

## Editor support

The MGUI editor can open `.anim2d` assets through `GameEditor`.

Current editor scope:

- Inspect parts, tracks/keyframes, events, and invalid track targets.
- Edit part name, default sprite id, default position, default draw order, and default visibility.
- Edit event time/name and add a basic event.
- Save via the central editor asset writer and serializer.

## Not in V1

The current implementation does not include:

- Timeline authoring.
- Onion skinning.
- Blend graphs or animation state machines.
- Non-step interpolation.
- Importer-specific formats.
- Editing composed tracks/keyframes in the editor.
- Per-part collision authoring.