# ADR-0034: Screen effect alpha channel and a veil that ignores depth

- **Status**: Accepted
- **Date**: 2026-09-20
- **Source**: this chantier: branch `chantier/effet-ecran-alpha`, commits `e9fa401e`, `7a192581`,
  `0ccf1746`; recorded in the journal of `ai-agent/tasks/screen-effect-above-ui-tasks.md`, whose
  ADR-0033 this one follows. Executed without a written plan beforehand, at the author's explicit
  request.

## Context

The screen effect exposed a colour and a blend mode only: `R`, `G`, `B` and `SpriteBlendMode`
(`CasaEngine/Framework/Rendering/ScreenEffects/ScreenEffectService.cs`). The overlay quad was built
opaque (`new Color(R, G, B)`), so a fade's intensity could only be carried by the colour itself.
That works for the additive and subtractive modes, which the Alundra port depends on because the
PlayStation blends its own fades that way, but it cannot express the fade every other engine
performs: a screen-space quad of the target colour whose opacity ramps, blending to
`lerp(scene, colour, alpha)`. Subtractive darkening and alpha darkening are not the same image:
subtraction clips dark areas to black at once and shifts hues, while alpha scales every channel and
preserves them.

Two further facts were measured while testing the result in a demo.

The veil did not cover the screen. It was submitted at the camera's depth
(`ScreenEffectComponent.SubmitOverlay`) into a batch that tests and writes depth
(`SpriteRendererComponent`, `DepthBufferEnable = true`, `DepthBufferWriteEnable = true`,
`LessEqual`). Sprites drawn before it and nearer than the camera wrote their depth and made the
veil's fragments fail the test on their pixels, so those sprites escaped the fade. Five witness
points in the tile map demo kept their base colour through an entire fade; after the fix all five
darken.

The sprite renderer already switches one GPU state per submission inside its draw loop: it tracks
the current blend mode and applies a new `BlendState` whenever the next submission asks for another
one. A per-submission depth toggle is the same shape, so it needs no new mechanism.

## Decision

- Add an alpha channel to the screen effect, **additively**: a `byte A` defaulting to `255`, plus
  overloads of `SetOverlay` and `StartFade` that take it. The existing signatures stay and delegate
  with `a = 255`, so every current caller keeps its behaviour, the subtractive path included. The
  fade interpolates alpha exactly as it interpolates the colour channels, and `Clear()` leaves it
  alone, as it already leaves the colour and the layer alone (ADR-0033).
- Build the overlay quad with that alpha, and rely on the existing `SpriteBlendMode.AlphaBlend`,
  which maps to `BlendState.NonPremultiplied`. The sprite shader emits `texel * Color` without
  premultiplying, and the veil's texture is one opaque white pixel, so the quad yields
  `lerp(scene, Color, A / 255)`.
- Give a submission the ability to **ignore depth**, through a flag on the queued sprite data and an
  optional trailing parameter on `DrawSprite`, defaulting to "tests depth" so nothing else changes.
  The draw loop applies `DepthStencilState.None` for a run of depth-ignoring submissions and the
  batch's normal state otherwise, the same per-run pattern already used for the blend state.
- **The screen effect's veil always ignores depth, in both layers.** A full-screen veil is a
  screen-space thing: it must cover every pixel whatever the scene wrote, which is what every modern
  engine does by disabling both the depth test and depth writes for it.

## Consequences

- A modern fade is now expressible: target colour, alpha from zero to one, alpha blending. The
  additive and subtractive modes remain, so the Alundra port's reproduction of the PlayStation is
  untouched.
- **A visible rendering change for existing consumers**, which the additive framing does not cover:
  because the veil ignores depth unconditionally, a subtractive fade now darkens sprites it
  previously left alone. That is the defect being fixed, but it is a change to observe when the
  engine pointer is bumped in a consuming project.
- Adding an optional parameter to an existing public method is source-compatible but not
  binary-compatible: a consumer assembly compiled against the old signature would need recompiling.
  Without effect here, since consumers build the engine from source as a submodule.
- The coverage of the `BelowUI` layer remains bounded by the frame's phase order, not by depth:
  components drawn in the later phase of `CasaEngineGame.Draw`, such as the line and physics debug
  renderers, still draw over a veil submitted inside the render pipeline. Measured as roughly seven
  thousand pixels in the tile map demo. Deciding which phases a full-screen veil should cover is an
  architecture question left open for the author; `AboveUI`, the layer the port needs, covers what
  it must.
