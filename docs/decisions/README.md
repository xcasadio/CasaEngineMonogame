# Architecture Decision Records

This folder records the architecture decisions of CasaEngine: engine and editor architecture, asset formats, public APIs, backends, and the rules that govern the AI agents working on the repository.

## Rules

- One file per decision, named `NNNN-short-title.md` with an increasing four-digit number. Decisions taken together on the same theme may share one file.
- Written in English, from the template [template.md](template.md): Status, Date, Source, Context, Decision, Consequences.
- Every decision taken during a plan or a discussion is recorded here, at the time it is taken (skill `adr`; rule in `AGENTS.md` §10).
- Backfilled decisions keep a `Source` pointing at the original document. Audits under `ai-agent/audits/` are read-only: the decision is copied here, the audit is not edited.
- A decision is never rewritten: a change is a new record that supersedes the old one, whose status becomes `Superseded by ADR-XXXX`.

## Index

| ADR | Title | Status | Date |
|---|---|---|---|
| [ADR-0001](0001-audio-runtime-architecture-v1.md) | Audio runtime architecture V1 (buses, streaming, backend) | Accepted | 2026-08-26 |
| [ADR-0002](0002-audio-asset-format-and-editor-scope-v1.md) | Audio asset format and editor scope V1 | Accepted | 2026-08-26 |
| [ADR-0003](0003-single-3d-simulation.md) | Single 3D simulation, simulation space as a world policy | Accepted | 2026-08 |
| [ADR-0004](0004-collision-layers.md) | Collision layers Shape / Fixture / Body / World, Shape3d as the only public volume vocabulary, no pose on shapes | Accepted | 2026-08 |
| [ADR-0005](0005-collision-channels-profiles.md) | Collision channels, responses and named profiles | Accepted | 2026-08 |
| [ADR-0006](0006-collision-volumes-vs-fields.md) | Collision volumes vs collision fields | Accepted | 2026-08 |
| [ADR-0007](0007-fixtures-animated-by-timeline.md) | Fixtures animated by the animation timeline | Accepted | 2026-08 |
| [ADR-0008](0008-compatibility-posture-replace-not-duplicate.md) | Compatibility posture, replace rather than duplicate | Accepted | 2026-08 |
| [ADR-0009](0009-bepu-backend-replaces-bullet.md) | Physics backend bepuphysics2 replacing BulletSharp | Accepted | 2026-08-22 |
| [ADR-0010](0010-shader-naming-convention.md) | Shader naming convention and applied renames | Accepted | 2026-09-06 (backfill) |
| [ADR-0011](0011-tilemap-render-spaces.md) | Tilemap render spaces | Accepted | 2026-09-06 (backfill) |
| [ADR-0012](0012-materials-shaders-sources-of-truth.md) | Materials and shaders sources of truth matrix | Accepted | 2026-09-06 (backfill) |
| [ADR-0013](0013-pbr-rendering-decisions.md) | PBR rendering decisions | Accepted | 2026-08-09 |
