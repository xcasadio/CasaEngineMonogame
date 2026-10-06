# ADR-0068: Free PSX quads draw at the screen resolution

- **Status**: Accepted
- **Date**: 2026-10-06 (plan E19.g G2b read until READY, closing review, approved by the author on 2026-10-06; work in the engine authorized by the author on 2026-10-03)
- **Source**: this chantier: `ai-agent/tasks/e19g2b-free-quads-tasks.md` (tasks G2b1-1 to G2b1-3). Parent repository:
  `docs/plan-e19-opcodes.md`, step E19.g G2b and O-E19-71 (the author chose the resolution of the screen, D-E19-92); prediction and
  oracle scripts in `docs/plan-e19-g2b-annexe/`. Keeps ADR-0048 (the scene is drawn straight into the enlarged screen buffer) and
  extends ADR-0051 and ADR-0053 (two entries per PSX mode); replaces no ADR.

## Context

- The original binary draws every entity and effect image as one `POLY_FT4`: four free vertices, no scale, mirror or rotation field.
  30.8 % of the 160 355 entity quads are deformed (scaled, turned mirror, parallelogram, arbitrary quad). The port drew rectangles
  only: `SpriteRendererComponent` writes a unit square and a scale matrix.
- The PS1 GPU splits a quad into the triangles (TL, TR, BL) and (TR, BL, BR) (diagonal TR-BL), samples the texel `floor(u + 1/2)` at
  the integer point of the pixel, covers by the top-left rule, and does not cull by winding (a mirrored quad is normal).
- ADR-0048 draws the scene straight into the enlarged back-buffer. At an integer factor k > 1 a deformed quad is therefore sampled per
  screen pixel and does not give uniform k x k blocks of PS1 pixels. Exactness at every factor would need the scene drawn into a
  320 x 240 target and enlarged, which ADR-0048 rejected (O-E19-71, option C).

## Decision

- `SpriteRendererComponent.DrawPsxQuad` (four overloads: with or without a sort key, explicit or device scissor) queues one entry per
  quad (two for a PSX mode, the rule of ADR-0051: opaque texels then STP texels, same key, z and corners). Arguments: the texture,
  the raw texture window of the PS1 data in texels, the four vertices in the order of the data (top-left, top-right, bottom-left,
  bottom-right) in world units (= PS1 pixels, y up), colour, z. The producer gives the raw corners and the raw window (a mirrored axis
  names its window one texel back); the engine flips nothing.
- The entry does not use the core of the sprite. Every field of the pooled entry is assigned, including the two new fields `NoCull`
  and `PsxQuad`, which the sprite core sets to false. The corners go into the vertex slots TR, BR, BL, TL, so the existing index buffer
  draws the PS1 split; neither the index buffer nor `FillVertices` changes. Texture coordinates are the corners of the window, never
  flipped, plus half a texel plus 1/4096 of a texel (an exact tie is resolved upward whatever the rounding of the interpolation).
- Series of `NoCull` entries are drawn with `RasterizerState.CullNone`, the previous state restored; series of `PsxQuad` entries are
  drawn with the new effect `Shaders/PsxQuad.fx`, loaded on the first such entry. `SpriteBatch.fx`, its reload, the tiles, the backdrops
  and `DrawDirectly` do not change.
- Rule at the integer factor k (screen pixels per PS1 pixel), for the screen pixel (sx, sy): coverage by the top-left **corner** of the
  pixel, (sx / k, sy / k) in PS1 units (the vertex shader moves the geometry half a screen pixel right and down, the rasterizer samples
  the centre); texel `floor(u(p) + 1/2)` with p = ((sx + 1/2) / k - 1/2, (sy + 1/2) / k - 1/2), u the affine map of the triangle that
  covers (the pixel shader removes `(k / 2 - 1/2) (ddx + ddy)` from the interpolated coordinate, zero at k = 1). The pixel shader
  chooses the texel itself (`floor`, then a read at the centre of the texel): Direct3D 11 guarantees only 8 sub-texel bits, so the
  sampler decides nothing. The derivative along y is taken with the sign of the back end: `ddy` points up under OpenGL (MonoGame
  DesktopGL) and down under Direct3D, so the vertex shader also passes the screen row and the pixel shader uses the sign of its `ddy`.
- At k = 1 the rule is exactly the PS1 rule. A 1:1 quad (plain or mirrored) draws exactly what the rectangle path draws, at every
  factor. A deformed quad at k > 1 is smoother than the PS1 enlarged (edges and texel boundaries at the screen pixel), with no overall
  shift.

## Consequences

- Inert for every project that does not call `DrawPsxQuad`: no existing path, shader or test changes, and the new effect is not loaded.
- The deviation from "all pixels stay equal" (D-E19-60) is limited to deformed quads at factors above 1 and is accepted by the author
  (O-E19-71, option A); the PS1 look at k = 1 is exact. Drawing the scene in a 320 x 240 target stays possible later as a separate
  decision.
- The model is exact up to the float arithmetic of the shader: the demo `PSX free quads` equals the exact-rational prediction pixel for
  pixel at k = 1 and k = 3 (no pixel falls in the noise zone: the geometry is integral).
- The hardware walker of the PS1 truncates 16.16 slopes: 0.56 % of the texels of a general quad differ from an exact model (measured
  with a port of the PCSX-Redux walker); that residual is not reproduced.
- Tests without a graphics device work on the queue and the vertex batch (`SpriteRendererComponentPsxQuadTests`); the pixels are proved
  by the demo `PSX free quads`, which dumps its back-buffer in process for the comparison with the prediction.
