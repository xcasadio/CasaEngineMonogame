# Animation Clip Loop Period

`AnimationClip.DurationSeconds` is the time of the last keyframe (or the explicit duration passed
to the constructor): the clip's playable range is `[0, DurationSeconds]`.

`AnimationClip.LoopPeriodSeconds` is the length of one cycle when the clip is played looped. It
defaults to `DurationSeconds`, which is right when the clip's first pose is duplicated at the end
(the usual loop-closure convention): the last key coincides with the start of the next cycle.

A uniformly sampled clip whose last keyframe is a **distinct** frame - e.g. 18 PSX frames keyed at
0..17/30 s - has a cycle of `DurationSeconds + 1/30`. Looping such a clip on its duration plays
frame 17 and frame 0 at the same instant: a one-frame jump at every seam. Give it the right period
instead:

```csharp
var looped = clip.WithLoopPeriod(clip.DurationSeconds + 1f / 30f); // new clip, same tracks
riggedModel.OverrideRuntimeAnimationAssets(skeleton, clips);
```

What honours the period (looping states only):

- `AnimationClipSampler`: the time wraps on the period and, between the last key and the end of
  the cycle, the tracks interpolate from the last keyframe back to the first.
- `AnimationClipNode.Advance`, `AnimationController` event dispatch, and
  `SkinnedMeshAnimationRuntime.AnimationTimeSeconds` (time within the cycle) wrap on the period.
- `AnimationClipCompressor`, `RetargetProcessor`, and `AnimationClipAsset` (`loop_period_seconds`,
  optional, absent = duration) carry it through.

Non-looping playback is unchanged: the clip still stops at `DurationSeconds`.

Validation: the constructor rejects a period shorter than the duration
(`ArgumentException`) or negative (`ArgumentOutOfRangeException`).
