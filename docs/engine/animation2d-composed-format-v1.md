# Animation2D composed format V1

## Purpose and scope

This document describes the on-disk `.anim2d` JSON format and the in-memory / runtime
model that CasaEngine uses for composed (multi-part) 2D sprite animations, as implemented by
`Animation2dData`, `Animation2dPartData`, `Animation2dTrackData`,
`Animation2dCollisionKeyframeData`, and sampled at runtime by `Animation2dCompositionSampler`
(via the immutable `Animation2dCompositionData` produced by `Animation2dCompositionAdapter`).

Only behavior actually present in the listed source files is documented. Where the code's
intent could not be confirmed from source, this is marked **unverified**.

## Data model

### Clip (`Animation2dData`, extends `ObjectBase`)

`ObjectBase` (`CasaEngine/Framework/Common/ObjectBase.cs:6-62`) contributes the base fields
`Id` (JSON `id`, GUID) and `Name` (JSON `name`, string), both loaded in
`ObjectBase.Load` (ObjectBase.cs:58-62). `Animation2dData` adds:

| C# property | JSON field | Type | Default | Role |
|---|---|---|---|---|
| `AnimationType` | `animation_type` | enum `AnimationType` (string, case-insensitive) | `AnimationType.Once` | Once vs. Loop playback mode (Animation2dData.cs:17, 240-243). |
| `EventTrackName` | `event_track_name` | string | `""` | Display name for the legacy/implicit event lane; falls back to `GetDefaultLegacyEventTrackName()` when blank (Animation2dData.cs:19, 43-53, 244). |
| `Parts` | `parts` | array of part objects | empty list | The animated "actors" (sprite slots) composing the clip (Animation2dData.cs:21, 251-260). |
| `Tracks` | `tracks` | array of track objects | empty list | Per-part, per-property keyframe timelines (Animation2dData.cs:22, 262-271). |
| `Events` | `events` | array of event objects | empty list | Discrete timed events (Animation2dData.cs:23, 286-306). |
| `CollisionKeyframes` | `collision_keyframes` | array of collision-keyframe objects | empty list | Timeline of collider fixture sets (Animation2dData.cs:29, 273-284). |

### Parts (`Animation2dPartData`)

| C# property | JSON field | Type | Default | Role |
|---|---|---|---|---|
| `Id` | `id` | string | `""` | Stable identifier referenced by tracks/events (`TargetPartId`) (Animation2dPartData.cs:9, 33). |
| `Name` | `name` | string | `""` | Display name; used by `GetLaneDisplayName` when a lane has no track name (Animation2dPartData.cs:11, 34; Animation2dData.cs:213-216). |
| `DefaultSpriteId` | `default_sprite_id` | GUID | `Guid.Empty` | Sprite shown before any Sprite-track keyframe applies (Animation2dPartData.cs:13, 35). |
| `DefaultPosition` | `default_position` | Vector2 object (`{x,y}`) | `Vector2.Zero` | Local offset of the part (Animation2dPartData.cs:15, 36). |
| `DefaultRotation` | `default_rotation` | float | `0` | Local rotation in radians (Animation2dPartData.cs:17, 37; consumed as radians by `Animation2dBoundsCalculator.RotateCorner`, Animation2dBoundsCalculator.cs:44-56). |
| `DefaultDrawOrder` | `default_draw_order` | int | `0` | Base draw order, used for z-sorting (Animation2dPartData.cs:19, 38). |
| `DefaultVisible` | `default_visible` | bool | `true` | Whether the part renders (Animation2dPartData.cs:21, 39). |
| `DefaultFlipX` | `default_flip_x` | bool | `false` | Horizontal flip (Animation2dPartData.cs:23, 40). |
| `DefaultFlipY` | `default_flip_y` | bool | `false` | Vertical flip (Animation2dPartData.cs:25, 41). |

### Tracks (`Animation2dTrackData`)

| C# property | JSON field | Type | Default | Role |
|---|---|---|---|---|
| `LaneId` | `lane_id` | string | `""` | UI grouping key; loaded from JSON, but the editor serializer does **not** write it back out (see below) (Animation2dTrackData.cs:25, 53). |
| `Name` | `name` | string | `""` | Track display name; falls back to `"Track N"` via `GetDefaultTrackName` (Animation2dTrackData.cs:27, 54; Animation2dData.cs:368-371). |
| `TargetPartId` | `target_part_id` | string | `""` | The `Animation2dPartData.Id` this track drives (Animation2dTrackData.cs:29, 55). |
| `Property` | `property` | enum `Animation2dTrackProperty` (string) | `Sprite` | Which part property this track animates (Animation2dTrackData.cs:31, 56). |
| `Interpolation` | `interpolation` | enum `Animation2dInterpolationMode` (string) | `Step` | Interpolation mode; only `Step` is defined and supported (Animation2dTrackData.cs:18-21, 33, 57; enforced at sampling time, see below). |
| `SpriteKeyframes` | `sprite_keyframes` | array of `{time_seconds, value}` (value = GUID) | empty list | Keyframes for `Property == Sprite` (Animation2dTrackData.cs:35, 60, 69-85). |
| `PositionKeyframes` | `position_keyframes` | array of `{time_seconds, value}` (value = Vector2 object) | empty list | Keyframes for `Property == Position` (Animation2dTrackData.cs:37, 61, 87-103). |
| `VisibleKeyframes` | `visible_keyframes` | array of `{time_seconds, value}` (value = bool) | empty list | Keyframes for `Property == Visible` (Animation2dTrackData.cs:39, 62, 105-121). |
| `DrawOrderKeyframes` | `draw_order_keyframes` | array of `{time_seconds, value}` (value = int) | empty list | Keyframes for `Property == DrawOrder` (Animation2dTrackData.cs:41, 63, 123-139). |
| `FlipKeyframes` | `flip_keyframes` | array of `{time_seconds, value}` (value = bool) | empty list | Keyframes for `Property == FlipX` or `Property == FlipY` — both flip axes share the same `flip_keyframes` list on a track (Animation2dTrackData.cs:43, 64, 105-121; applied per-axis in `Animation2dCompositionSampler.ApplyTrack`, Animation2dCompositionSampler.cs:227-240). |
| `RotationKeyframes` | `rotation_keyframes` | array of `{time_seconds, value}` (value = float) | empty list | Keyframes for `Property == Rotation` (Animation2dTrackData.cs:45, 65, 141-157). |

`Animation2dTrackProperty` values: `Sprite`, `Position`, `Visible`, `DrawOrder`, `FlipX`,
`FlipY`, `Rotation` (Animation2dTrackData.cs:7-16). `Animation2dInterpolationMode` currently
declares only `Step` (Animation2dTrackData.cs:18-21).

Each keyframe record is a `readonly record struct` with `TimeSeconds` (float, JSON
`time_seconds`) and a typed `Value` (JSON `value`), one struct type per property kind
(Animation2dTrackData.cs:160-168).

**Editor-write asymmetry (observed):** `EditorAssetJsonSerializer.SaveAnimation2dTrackData`
writes `target_part_id`, `property`, `interpolation`, `name` (via `Animation2dData.GetTrackName`)
and the six keyframe arrays, but does **not** write `lane_id`
(CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:239-267). `lane_id` is only read back
on `Load` (Animation2dTrackData.cs:53) and defaulted from `TargetPartId` by
`Animation2dData.EnsureTrackNames` when blank (Animation2dData.cs:59-62). So round-tripping
through the editor serializer currently drops any distinct `lane_id` that was present in a
hand-authored or externally produced file — unverified whether this is intentional.

### Events (`AnimationEventAsset`, a `readonly record struct`)

Defined in `AnimationClipAsset.cs:42`:
`AnimationEventAsset(float TimeSeconds, string EventName, Guid SpriteAssetId = default, string TargetPartId = "")`.
Serialized by `AnimationEventAssetJsonSerializer`:

| C# property | JSON field | Type | Default | Role |
|---|---|---|---|---|
| `TimeSeconds` | `time_seconds` | float | `0` | When the event fires (AnimationEventAssetJsonSerializer.cs:12, 34). |
| `EventName` | `event_name` | string | `""` | Event identifier, e.g. `changeSprite`, `restart` (AnimationEventAssetJsonSerializer.cs:13, 35; names in `Animation2dEventNames.cs:5,7`). |
| `SpriteAssetId` | `sprite_asset_id` | GUID | `Guid.Empty` | Only written when non-empty; only read if present (AnimationEventAssetJsonSerializer.cs:16-19, 36). |
| `TargetPartId` | `target_part_id` | string | `""` | Only written when non-blank; only read if present (AnimationEventAssetJsonSerializer.cs:21-24, 37). Used as the lane key for sorting/grouping (`Animation2dData.SortEventsByLaneAndTime`, `GetLaneIds`, Animation2dData.cs:172-202, 348-366). |

`Animation2dData.Load` special-cases an event named `restart`
(`Animation2dEventNames.Restart`): such an event is **not** added to `Events`; instead, if the
JSON has no `animation_type` field at all, its presence sets `AnimationType = Loop`
(Animation2dData.cs:292-313). This is the only backward-compatibility rule found for older
files that expressed looping via a `restart` event rather than an explicit `animation_type`.
The editor serializer symmetrically never writes a `restart` event back out
(EditorAssetJsonSerializer.cs:186-191).

No other legacy/mono-sprite `.anim2d` schema (e.g. a single top-level sprite-sequence without
`parts`/`tracks`) is handled anywhere in `Animation2dData.Load` or the asset loader
(`AssetLoader<T>`, `AssetLoaderRegistry.cs:41`) — **unverified / not found**: if such an older
format exists in this repository's history, this code does not appear to special-case it.

### Collision keyframes (`Animation2dCollisionKeyframeData`)

| C# property | JSON field | Type | Default | Role |
|---|---|---|---|---|
| `TimeSeconds` | `time_seconds` | float | `0` | Activation time of this fixture set (Animation2dCollisionKeyframeData.cs:12, 22). |
| `Fixtures` | `fixtures` | array of collider-fixture objects | empty list | Set of `ColliderFixture` (`CasaEngine.Engine.Physics`) active from this keyframe until the next one; loaded via `ColliderFixture.Load` and saved via `EditorEntityJsonSerializer.SaveColliderFixture` (Animation2dCollisionKeyframeData.cs:14, 25-36; EditorAssetJsonSerializer.cs:210-222). Fixture-level fields are out of scope for this document (defined in `CasaEngine.Engine.Physics.ColliderFixture`, not read here). |

Step semantics documented directly on the type: "the set is active from `TimeSeconds` until
the next keyframe, and no set is active before the first keyframe"
(Animation2dCollisionKeyframeData.cs:6-9).

## Loading rules

`Animation2dData.Load` (Animation2dData.cs:236-316):

1. Calls `base.Load` to populate `Id`/`Name`.
2. Reads `animation_type` (case-insensitive enum parse, default `Once` if missing/invalid) and
   `event_track_name` (default `""`).
3. Clears `Parts`, `Tracks`, `Events`, `CollisionKeyframes`, then repopulates each from its
   JSON array if present (arrays are optional; a missing array leaves the list empty).
4. `parts` and `tracks` are appended in file order — **no sorting is applied to parts or
   tracks**.
5. `collision_keyframes` are appended in file order, then explicitly sorted by `TimeSeconds`
   via `SortCollisionKeyframesByTime` (stable-ish `List.Sort` with a time comparer)
   (Animation2dData.cs:273-284, 343-346).
6. `events`: each event object is parsed; an event named `restart` is diverted into the
   `AnimationType = Loop` inference described above instead of being stored. Remaining events
   are added to `Events`, then sorted by `SortEventsByLaneAndTime`: primary key
   `TargetPartId` (ordinal string compare), secondary key `TimeSeconds`, tertiary key
   `EventName` (Animation2dData.cs:292-308, 348-366).
7. `EnsureTrackNames()` runs last in both the "no events array" early-return path and the
   normal path: for every track, if `LaneId` is blank it is set to `TargetPartId`; if `Name`
   is blank it is set to `"Track {index+1}"` (1-based display, 0-based index). If
   `EventTrackName` is blank, it is set to `GetDefaultLegacyEventTrackName()`, which is
   `"Track {N+1}"` where `N = max(Parts.Count, GetLaneIds().Count)` (Animation2dData.cs:55-76,
   50-53, 368-371).
8. No explicit validation/rejection of unknown `target_part_id` values happens during `Load`
   itself; `GetInvalidTrackTargetPartIds()` / `GetInvalidEventTargetPartIds()` exist as
   separate query methods callers may use to detect tracks/events whose `TargetPartId` does
   not match any `Parts[i].Id` (Animation2dData.cs:126-170) — calling these is the caller's
   responsibility; `Load` does not call them itself.

`AssetLoader<T>.LoadAsset` (generic asset loader used for `Animation2dData` via
`AssetLoaderRegistry.cs:41`) reads the whole file with `JObject.Parse`, constructs a new
instance, and calls `Load`; any exception is caught and logged via `Logs.WriteException`, and
`null` is returned on failure (AssetLoader.cs:9-24). It reports `IsFileSupported` as always
`false` ("no import from other project", AssetLoader.cs:26-29).

## Duration computation

`GetDurationSeconds()` returns the maximum `TimeSeconds` found across: every track's
`SpriteKeyframes`, `PositionKeyframes`, `VisibleKeyframes`, `DrawOrderKeyframes`,
`FlipKeyframes`, `RotationKeyframes` (last element only, since keyframes are expected in time
order — no explicit re-sort happens here), every `Events[i].TimeSeconds`, and every
`CollisionKeyframes[i].TimeSeconds` (Animation2dData.cs:78-103, 318-341).

## Runtime sampling (`Animation2dCompositionSampler`)

The sampler operates on an immutable `Animation2dCompositionData` snapshot built once by
`Animation2dCompositionAdapter.Create`, which deep-copies parts, tracks, and collision
keyframes (re-sorting collision keyframes by time again) and copies events, alongside the
precomputed `DurationSeconds` and `AnimationType` (Animation2dCompositionAdapter.cs:7-32,
49-140; Animation2dCompositionData.cs:20-34).

- **Time advance / `Update(elapsedTime)`:** if `AnimationType == Loop`, delegates to
  `UpdateLooping`, which wraps time modulo `DurationSeconds`, dispatching any events crossed
  in each wrap segment, and never reports finished (`IsFinished` stays `false`)
  (Animation2dCompositionSampler.cs:48-53, 76-115). Otherwise, if `DurationSeconds <= 0`, the
  clip is immediately finished and sampled at time 0 (lines 56-61). Otherwise time advances
  clamped to `DurationSeconds`, `IsFinished` becomes `true` once the clamp triggers, events in
  the crossed range are dispatched, then tracks are applied at the new time (lines 63-74).
- **`Seek(timeSeconds)`:** clamps to `>= 0`, normalizes via `NormalizeTime` (wraps for `Loop`,
  clamps to `[0, DurationSeconds]` otherwise, or `0` if `DurationSeconds <= 0`), recomputes
  `IsFinished` for non-looping clips when the raw requested time is at/after the duration, and
  re-applies tracks — but does **not** dispatch events (Animation2dCompositionSampler.cs:38-46,
  133-156).
- **Event dispatch (`DispatchEventRange`):** fires an event when
  `startExclusive < event.TimeSeconds <= endInclusive`, i.e. start-exclusive /
  end-inclusive, in `Events` list order (which is lane-then-time sorted at load time, not
  necessarily global time order) (Animation2dCompositionSampler.cs:117-131).
- **Collision keyframe selection (`EvaluateCollisionKeyframeIndex`):** scans
  `CollisionKeyframes` in order and returns the index of the last keyframe whose
  `TimeSeconds <= sampleTime`, or `-1` if none qualify (step/hold semantics, matching the type's
  documented contract) (Animation2dCompositionSampler.cs:16-20, 176-192). Exposed as
  `CurrentCollisionKeyframeIndex` and the underlying list as `CollisionKeyframes`.
- **Applying tracks (`ApplyTracks`):** for each sample, first evaluates the active collision
  keyframe index, then resets all parts to their defaults (`RuntimeState.ApplyDefaults`), then
  for every track whose `TargetPartId` resolves to a known part (`RuntimeState.TryGetPart`),
  applies that track's current value; tracks targeting an unknown part id are silently skipped
  (no error) (Animation2dCompositionSampler.cs:158-174, 194-250). Finally
  `RuntimeState.UpdateDrawOrder()` re-sorts.
- **Interpolation enforcement:** `ApplyTrack` throws `NotSupportedException` if
  `track.Interpolation != Step`, and also throws `NotSupportedException` for any
  `Animation2dTrackProperty` value not in its `switch` (defensive default branch)
  (Animation2dCompositionSampler.cs:196-199, 247-249).
- **Per-property step evaluation:** each `TryEvaluateX` helper scans keyframes in list order
  and keeps the last one whose `TimeSeconds <= sampleTime`, stopping at the first keyframe
  whose time exceeds `sampleTime`; returns `false` (no change applied) if no keyframe qualifies
  — i.e., **keyframes must already be in ascending time order in memory**; nothing in the
  sampler sorts per-track keyframe lists (Animation2dCompositionSampler.cs:252-345). No sort of
  track keyframe lists was found in `Animation2dTrackData.Load` either — ascending order is an
  authoring/loading assumption, not an enforced invariant.
- **Draw order:** `Animation2dCompositionRuntimeState.UpdateDrawOrder()` sorts
  `DrawPartIndices` by `DrawOrder` ascending, then by `SourceIndex` (original part order)
  ascending as a tiebreaker (Animation2dCompositionRuntimeState.cs:58-61, 85-103). Consumers
  (e.g. rendering) are expected to iterate `DrawPartIndices` in that order; the sampler itself
  does not draw anything.

## Limits observed in the code

- Only `Animation2dInterpolationMode.Step` exists and is supported; any other value throws at
  sample time (Animation2dTrackData.cs:18-21; Animation2dCompositionSampler.cs:196-199).
- `FlipX` and `FlipY` share one keyframe list (`FlipKeyframes`) per track; a track cannot
  independently step-key both axes without being duplicated as two tracks
  (`Property = FlipX` and `Property = FlipY`) each with their own `FlipKeyframes`
  (Animation2dTrackData.cs:43; Animation2dCompositionSampler.cs:227-240).
- `lane_id` is read but never written by the editor serializer, so it does not survive an
  editor round-trip as a distinct value from `target_part_id`
  (EditorAssetJsonSerializer.cs:239-267; Animation2dData.cs:59-62).
- No validation rejects a track or event whose `target_part_id` does not match any part id at
  load time; detection is opt-in via `GetInvalidTrackTargetPartIds` /
  `GetInvalidEventTargetPartIds` (Animation2dData.cs:126-170).
- No sorting is applied to `Parts`, `Tracks`, or any track's keyframe list at load time; only
  `Events` (by lane then time then name) and `CollisionKeyframes` (by time) are sorted
  (Animation2dData.cs:283, 308, 343-366).
- `Seek` does not dispatch events; only `Update` does (Animation2dCompositionSampler.cs:38-46
  vs. 63-74, 88-115).

## Example

Real file from the repository (`CasaEngine.Demos/Content/TileSets/baton_attack2_down.anim2d`),
a single-part, sprite-only clip. Note this file predates fields such as `name` on the track,
`event_track_name`, and `lane_id` — it still loads because every field the loader reads is
optional with a default:

```json
{
  "animation_type": "Once",
  "id": "243de4b8-79dd-4986-9239-437daf4107f6",
  "name": "baton_attack2_down",
  "parts": [
    {
      "id": "sprite",
      "name": "Sprite",
      "default_sprite_id": "2aeef842-a044-46d9-a2e1-de408d6f51e2",
      "default_position": { "x": 0.0, "y": 0.0 },
      "default_draw_order": 0,
      "default_visible": true,
      "default_flip_x": false,
      "default_flip_y": false
    }
  ],
  "tracks": [
    {
      "target_part_id": "sprite",
      "property": "Sprite",
      "interpolation": "Step",
      "sprite_keyframes": [
        { "time_seconds": 0.0,  "value": "2aeef842-a044-46d9-a2e1-de408d6f51e2" },
        { "time_seconds": 0.03, "value": "2d47d3ba-3476-4c87-935f-1de36a8446e5" },
        { "time_seconds": 0.06, "value": "ef6422df-830b-4ee9-b1b3-80692d7004c6" },
        { "time_seconds": 0.09, "value": "786e91da-336c-4c1e-993a-e6afd90daa2e" },
        { "time_seconds": 0.12, "value": "786e91da-336c-4c1e-993a-e6afd90daa2e" }
      ]
    }
  ]
}
```

A minimal example reconstructed to show every field name the current editor serializer would
emit for a clip with an event and a collision keyframe (field names taken directly from
`EditorAssetJsonSerializer` / `AnimationEventAssetJsonSerializer`, not observed together in one
real file):

```json
{
  "id": "00000000-0000-0000-0000-000000000001",
  "name": "example_clip",
  "animation_type": "Loop",
  "event_track_name": "Track 2",
  "parts": [
    {
      "id": "body",
      "name": "Body",
      "default_sprite_id": "00000000-0000-0000-0000-0000000000aa",
      "default_position": { "x": 0.0, "y": 0.0 },
      "default_rotation": 0.0,
      "default_draw_order": 0,
      "default_visible": true,
      "default_flip_x": false,
      "default_flip_y": false
    }
  ],
  "tracks": [
    {
      "name": "Track 1",
      "target_part_id": "body",
      "property": "Position",
      "interpolation": "Step",
      "position_keyframes": [
        { "time_seconds": 0.0, "value": { "x": 0.0, "y": 0.0 } },
        { "time_seconds": 0.5, "value": { "x": 4.0, "y": 0.0 } }
      ]
    }
  ],
  "events": [
    { "time_seconds": 0.25, "event_name": "changeSprite", "target_part_id": "body" }
  ],
  "collision_keyframes": [
    { "time_seconds": 0.0, "fixtures": [] }
  ]
}
```

## Source map

| Field / behavior | File:line |
|---|---|
| `Animation2dData.AnimationType` (`animation_type`) | Animation2dData.cs:17, 240-243 |
| `Animation2dData.EventTrackName` (`event_track_name`) | Animation2dData.cs:19, 244 |
| `Animation2dData.Parts` (`parts`) | Animation2dData.cs:21, 251-260 |
| `Animation2dData.Tracks` (`tracks`) | Animation2dData.cs:22, 262-271 |
| `Animation2dData.Events` (`events`) | Animation2dData.cs:23, 286-306 |
| `Animation2dData.CollisionKeyframes` (`collision_keyframes`) | Animation2dData.cs:29, 273-284 |
| Collision keyframes sorted by time on load | Animation2dData.cs:283, 343-346 |
| Events sorted by lane then time then name on load | Animation2dData.cs:308, 348-366 |
| `restart` event → `AnimationType = Loop` inference | Animation2dData.cs:292-313 |
| `EnsureTrackNames` (lane id / track name / event track name defaults) | Animation2dData.cs:55-76 |
| `GetDurationSeconds` | Animation2dData.cs:78-103, 318-341 |
| `GetInvalidTrackTargetPartIds` / `GetInvalidEventTargetPartIds` | Animation2dData.cs:126-170 |
| `ObjectBase.Id`/`Name` (`id`, `name`) | CasaEngine/Framework/Common/ObjectBase.cs:10,12,58-62 |
| `Animation2dPartData` fields | Animation2dPartData.cs:9-43 |
| `Animation2dTrackData` fields incl. `lane_id` read | Animation2dTrackData.cs:7-67 |
| Keyframe record structs | Animation2dTrackData.cs:160-168 |
| `AnimationEventAsset` definition | AnimationClipAsset.cs:42 |
| `AnimationEventAsset` JSON load/save | AnimationEventAssetJsonSerializer.cs:8-38 |
| `Animation2dEventNames.ChangeSprite` / `.Restart` | Animation2dEventNames.cs:5,7 |
| `Animation2dCollisionKeyframeData` fields + step semantics doc comment | Animation2dCollisionKeyframeData.cs:6-40 |
| `AssetLoader<T>.LoadAsset` (generic `.anim2d` runtime loader) | CasaEngine/Framework/Assets/AssetLoader.cs:9-29 |
| `Animation2dData` registered for `.anim2d` loading | CasaEngine/Framework/Assets/AssetLoaderRegistry.cs:41 |
| `.anim2d` extension constant | CasaEngine/Framework/Configuration/Constants.cs:17 |
| Editor serializer entry point for `Animation2dData` | CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:34-35 |
| `SaveAnimation2dData` (writes clip fields, omits nothing except `lane_id`) | CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:155-201 |
| `SaveAnimation2dPartData` | CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:225-241 |
| `SaveAnimation2dTrackData` (no `lane_id` written) | CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:243-267 |
| `SaveAnimation2dCollisionKeyframeData` | CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:210-222 |
| `Animation2dCompositionData` (immutable runtime snapshot) | Animation2dCompositionData.cs:3-34 |
| `Animation2dCompositionAdapter.Create` (deep copy + re-sort collision keyframes) | Animation2dCompositionAdapter.cs:7-32 |
| `Animation2dCompositionSampler.Update` / `UpdateLooping` | Animation2dCompositionSampler.cs:48-115 |
| `Animation2dCompositionSampler.Seek` (no event dispatch) | Animation2dCompositionSampler.cs:38-46 |
| `DispatchEventRange` (start-exclusive, end-inclusive) | Animation2dCompositionSampler.cs:117-131 |
| `EvaluateCollisionKeyframeIndex` (last keyframe ≤ sampleTime, else -1) | Animation2dCompositionSampler.cs:176-192 |
| `ApplyTracks` (defaults reset, per-track apply, skip unknown part id, draw order update) | Animation2dCompositionSampler.cs:158-174 |
| `ApplyTrack` (interpolation/property enforcement, FlipX/FlipY sharing `FlipKeyframes`) | Animation2dCompositionSampler.cs:194-250 |
| Step evaluators (`TryEvaluateGuid/Vector2/Bool/Int/Float`) | Animation2dCompositionSampler.cs:252-345 |
| `Animation2dCompositionRuntimeState.UpdateDrawOrder` (sort by DrawOrder then SourceIndex) | Animation2dCompositionRuntimeState.cs:58-61, 85-103 |
| `Animation2dPartRuntimeState.Reset` (defaults applied per part) | Animation2dPartRuntimeState.cs:25-36 |
| `Animation2dBoundsCalculator` (rotation treated as radians) | Animation2dBoundsCalculator.cs:44-56 |
| `Animation2dSpriteReferenceCollector` (collects `DefaultSpriteId` + Sprite-track keyframe values) | Animation2dSpriteReferenceCollector.cs:5-45 |
| Real example `.anim2d` file | CasaEngine.Demos/Content/TileSets/baton_attack2_down.anim2d |
