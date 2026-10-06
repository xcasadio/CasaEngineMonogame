# ADR-0069: The `.anim2d` corner track draws a part as a free PSX quad

- **Status**: Accepted
- **Date**: 2026-10-06 (plan E19.g G2b-2 read until READY, closing review, approved by the author on 2026-10-06; work in the engine authorized by the author on 2026-10-03)
- **Source**: this chantier: `ai-agent/tasks/e19g2b2-corner-track-tasks.md` (tasks G2b2-1 to G2b2-3). Parent repository:
  `docs/plan-e19-opcodes.md`, step E19.g G2b-2 (rules G2b2-R1 to R4). Builds on ADR-0068 (`DrawPsxQuad`); replaces no ADR.

## Context

- The original binary draws every entity image as one `POLY_FT4` of four signed vertices added to the anchor: scale, mirror, shear and
  arbitrary quads are all in those vertices. 30.8 % of the entity quads of the corpus are deformed. A composed animation of the engine
  (`.anim2d`) animates a part by Sprite, Position, Visible, DrawOrder, Flip and Rotation tracks: it can only draw a rectangle (the
  size of the sprite, the part rotation and the flips), so a deformed quad cannot be expressed.
- ADR-0068 gives the engine a free-quad entry, `SpriteRendererComponent.DrawPsxQuad`. Nothing in `.anim2d` can feed it yet.

## Decision

- A new track property `Animation2dTrackProperty.Corners`, appended at the end of the enumeration (the numeric values of the existing
  members do not move), and a new keyframe type `Animation2dCornersKeyframeData(TimeSeconds, Enabled, TopLeft, TopRight, BottomLeft,
  BottomRight, SourceOffset)`, held in `Animation2dTrackData.CornerKeyframes`.
- JSON: `corner_keyframes`, written **only when the list is not empty**, so a file without a corner track is read and rewritten byte
  for byte. An active key is `{ "time_seconds", "enabled": true, "top_left", "top_right", "bottom_left", "bottom_right",
  "source_offset" }` (vectors in the format of the other vectors of the file, `source_offset` in integers); a disabled key is
  `{ "time_seconds", "enabled": false }` (corners and offset omitted). On read, a missing field is zero (a missing `enabled`: false);
  `source_offset` accepts any integer (the format is general, a producer may restrict it).
- Meaning: the corners are offsets in pixels (y up) from the value of the `Position` track of the part, in the order of the PS1 data
  (top-left, top-right, bottom-left, bottom-right of the texture rectangle), multiplied by the entity scale like the position.
  `SourceOffset` moves the source window of the sprite (a mirrored axis names its window one texel back). Before the first key and on
  a disabled key the part is drawn as a rectangle, as today. Step semantics, like every other track.
- The sampler (`Animation2dCompositionSampler`) evaluates the track into `Animation2dPartRuntimeState` (`HasCorners`, the four corners,
  `CornerSourceOffset`, reset by `Reset`). The sites that copy or write a track follow: `Animation2dTrackData.Load`,
  `Animation2dCompositionAdapter.CloneTrack`, `Animation2dData.GetDurationSeconds`, `EditorAssetJsonSerializer` and the undo
  snapshot of the animation editor panel; the 3D animation clips (`AnimationAssetDataConverter`, `AnimationClipAsset`) are unrelated
  and do not change.
- Drawing: `AnimatedSpriteComponent.DrawComposedAnimation` draws a part with active corners through `DrawPsxQuad` (the sort-key path
  with the PSX mode of the sprite; the zOrder path stays opaque, as for a rectangle). **The rotation and the flips of the part and the
  `SpriteEffect` of the component are ignored for such a part**: the corners carry the whole geometry. The Alundra project never sets
  that `SpriteEffect`. The bounds (`Animation2dBoundsCalculator`) take the box of the corners.
- The corner track is not shown in the timeline of the animation editor (the editor does not author it); its undo snapshot keeps it.

## Consequences

- Inert for every existing asset: a file without `corner_keyframes` is unchanged on read and on write, and no draw path changes for a
  part without active corners.
- The track is the way a producer (the Alundra converter, G2b-3) expresses a deformed entity image; the engine needs no knowledge of
  the PS1 beyond ADR-0068.
- A part with corners is drawn at the screen resolution rule of ADR-0068: exact at factor 1, a 1:1 quad equal to the rectangle path at
  any factor, a deformed quad smoother than the PS1 enlarged at factors above 1.
- Tests without a graphics device work on the model, the JSON, the sampler, the bounds and the vertex batch
  (`Animation2dCornerTrackTests`, `Animation2dCornerTrackSerializationTests`), and a real exported file is rewritten byte for byte
  (fixture `bankalundra_0_anim1_down.anim2d`).
