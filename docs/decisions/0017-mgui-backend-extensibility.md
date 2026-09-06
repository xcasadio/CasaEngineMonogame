# ADR-0017: MGUI backend extensibility, Apos.Shapes and NvgSharp behind CasaEngine contracts

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `ai-agent/tasks/casaengine-mgui-backend-extensibility-tasks.md:10-30`

## Context

The task plan states that the goal is not to make MGUI "renderer agnostic" in the absolute sense, but to make the CasaEngine backend modular enough to plug several rendering subsystems under a single MonoGame backend, without deforming `MGUI.Shared`/`MGUI.Core` to absorb a vector API that is not theirs (ai-agent/tasks/casaengine-mgui-backend-extensibility-tasks.md:10-15). This is followed by a section of decisions labelled "non negociables" (ai-agent/tasks/casaengine-mgui-backend-extensibility-tasks.md:16).

## Decision

- MGUI stays the widgets / layout / input / logical clipping layer (source: ai-agent/tasks/casaengine-mgui-backend-extensibility-tasks.md:17).
- The CasaEngine backend stays a concrete MonoGame backend (source: ai-agent/tasks/casaengine-mgui-backend-extensibility-tasks.md:18).
- `Apos.Shapes` is integrated as a 2D primitives engine behind a CasaEngine contract, not directly inside `MGUI.Shared` (source: ai-agent/tasks/casaengine-mgui-backend-extensibility-tasks.md:19).
- `NvgSharp` is integrated as a vector overlay canvas for the editor behind a CasaEngine contract, not as a replacement for `IUIDrawContext` (source: ai-agent/tasks/casaengine-mgui-backend-extensibility-tasks.md:20).
- The `Apos.Shapes` and `NvgSharp` integrations must have a clear fallback to current behavior until the migration is complete (source: ai-agent/tasks/casaengine-mgui-backend-extensibility-tasks.md:21).
- Public MGUI contracts are modified only on a proven blocker, with explicit compatibility (source: ai-agent/tasks/casaengine-mgui-backend-extensibility-tasks.md:22).

## Consequences

- Editor-only vector code stays isolated: `CasaEngine.Tests/UI/CasaMguiBackendOwnershipTests.cs` enforces that `MGUI.Shared`/`MGUI.Core` and non-editor runtime source paths never reference `NvgSharp`, and that the engine project does not reference it while the editor project does (CasaEngine.Tests/UI/CasaMguiBackendOwnershipTests.cs:31-59, :151, :173-174).
- Implementation status observed in code: the `Apos.Shapes` contract `IShapeRenderer2D` exists at `CasaEngine/Framework/UI/Backend/MonoGame/Primitives/IShapeRenderer2D.cs`, with an Apos.Shapes-backed implementation `CasaAposShapeRenderer2D` in the separate `CasaEngine.AposShapes` project (`CasaEngine.AposShapes/Framework/UI/Backend/MonoGame/Primitives/CasaAposShapeRenderer2D.cs`) and a fallback implementation `CasaLegacyShapeRenderer2D` in `CasaEngine/Framework/UI/Backend/MonoGame/Primitives/CasaLegacyShapeRenderer2D.cs` — note this fallback class is named `CasaLegacyShapeRenderer2D`, not `DefaultShapeRenderer` as named in `ai-agent/audits/analysis-decisions-inventory.md:98`.
- `NvgSharp` integration is partial: `CasaEngine.Editor/Rendering/Vector/NvgSharpVectorCanvas.cs` exists as the editor-side overlay canvas, consistent with `ai-agent/audits/analysis-decisions-inventory.md:99` reporting this decision as "Partielle".
