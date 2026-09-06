# ADR-0016: UI screen editor V1

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `docs/editor/ui-screen-editor/architecture.md:195-215`

## Context

Section 7 of `docs/editor/ui-screen-editor/architecture.md` ("Décisions de v1") lists the decisions retained for the first version of the UI screen editor, alongside decisions explicitly deferred (docs/editor/ui-screen-editor/architecture.md:200-215). The surrounding rule states that data meaningful only for display or immediate interaction belongs to the session or the UI, not to the serialized document, and that the v1 preview may be rebuilt entirely after each change as long as architectural isolation stays strict (docs/editor/ui-screen-editor/architecture.md:195-197).

## Decision

- The document model is the single source of truth (source: docs/editor/ui-screen-editor/architecture.md:202).
- MGUI XAML is the primary persistence format (source: docs/editor/ui-screen-editor/architecture.md:203).
- The runtime preview is fully rebuilt in v1 (source: docs/editor/ui-screen-editor/architecture.md:204).
- The editing session centralizes dirty state, selection, current document and preview (source: docs/editor/ui-screen-editor/architecture.md:205).

## Consequences

- Deferred out of v1: incremental preview updates, full support for advanced bindings/styles/resources, advanced visual editing with complete drag-and-drop and smart resize, and reusable components / advanced authoring templates (docs/editor/ui-screen-editor/architecture.md:208-212).
- Runtime mappings to `DocumentNodeId` are allowed only as a preview index, never as a business source of truth (docs/editor/ui-screen-editor/architecture.md:197).
- Implementation status observed in code: `UIScreenDocument` (`CasaEngine.EditorServices/ScreenEditor/DocumentModel/UIScreenDocument.cs`), `UIScreenPreviewBuilder` (`CasaEngine.EditorServices/ScreenEditor/Preview/UIScreenPreviewBuilder.cs`) and `UIScreenEditorSession` (`CasaEngine.EditorServices/ScreenEditor/Session/UIScreenEditorSession.cs`) exist, with corresponding tests under `CasaEngine.Tests/ScreenEditor/`.
