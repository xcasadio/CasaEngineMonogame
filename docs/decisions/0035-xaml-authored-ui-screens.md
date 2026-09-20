# ADR-0035: UI screens are authored in XAML and loaded at runtime

- **Status**: Accepted
- **Date**: 2026-09-20
- **Source**: this chantier: `ai-agent/tasks/xaml-runtime-bridge-tasks.md`, branch
  `chantier/xaml-runtime-bridge`. Born of a rule the author set on 2026-09-20: everything that is UI, and
  every control definition, is declared in XAML files.

> **On the number.** ADR-0034 exists on the unmerged branch `chantier/effet-ecran-alpha`. This record takes
> 0035 so the two cannot collide when that branch lands, which leaves 0034 absent from this branch's index
> until it does.

## Context

The repository already described this architecture. `docs/editor/ui-screen-editor/screen-authoring-conventions.md`
§6 states it plainly: *"At runtime, bind dynamic data explicitly in code rather than inline XAML binding
syntax"*. The asset format existed too — a JSON envelope carrying `source_xaml_file`, deserialized by
`UIScreenAsset`, registered with the content manager, and shipped as five working examples under
`Projects/SampleProject/Screens/`.

What did not exist was the step between them. `XAMLParser` lives in MGUI.Core, so the runtime can reach it,
but its only caller outside MGUI was `CasaEngine.EditorServices/ScreenEditor/Preview/UIScreenPreviewBuilder.cs`,
and AGENTS.md §9.9 forbids the runtime to depend on the editor. `AssetLoader<UIScreenAsset>` reads the JSON
and stops there. So every screen in the repository — ten of them — built its control tree in C#.

Three measured facts shaped what the bridge could be.

**The editor's preview path cannot be reused.** `UIScreenPreviewBuilder` rewrites the `Name` of every element
to `_cse_<Guid>` so the editor can track selection. Those names are exactly what a runtime screen needs in
order to find its controls, so the runtime needs its own path, not a shared one.

**`UIRoot` cannot exist in the test suite.** It is sealed, and its only constructor takes a `CasaEngineGame`
and a render surface and builds a MonoGame backend. Anything whose construction is reachable only through
`IUIScreen.Initialize(UIRoot)` is therefore untestable. The dialogue screen had already worked around this by
exposing `BuildWindow(MGDesktop)` and driving that from its tests.

**Building a tree and attaching it are two steps, and MGUI wraps only the first.**
`XAMLParser.ParseWindowDefinition` runs inside `XamlLoaderDiagnostics.Execute`; the `ToElement(Desktop)` that
follows it in `LoadRootWindow` does not. Verified by probe: a name an `ItemTemplate` clones onto every item
comes out of `LoadRootWindow` as a bare `MGDuplicateElementNameException`, carrying no file and no position.

## Decision

- **A screen's control tree is declared in XAML.** The document owns what is static: the tree, the names, the
  layout, the styles. Code owns what changes: values, visibility, positions that depend on the resolution,
  and event subscriptions. No inline binding syntax — the code binds explicitly, as §6 of the conventions
  already required.
- **`UIScreenLoader` (`CasaEngine/Framework/UI/MGUI/`) is the runtime bridge**, with two doors onto one
  mechanism: a `UIScreenAsset` plus the path it was read from, or a bare `XamlDocumentSource` for a screen
  with no catalogued asset. It reuses nothing from `CasaEngine.EditorServices`.
- **The asset's file path is a parameter, never read from the asset.** Nothing populates a path on this asset
  type on the way in: all three existing callers construct it by hand and set `FileName` themselves, and none
  goes through `AssetContentManager` even though the type is registered there.
- **Parsing runs in `XamlLoaderMode.Strict`.** An unknown element or a name declared twice must fail at load.
  A duplicate name would otherwise silently break the lookup by name that screens depend on, and
  `Compatibility` mode does not look: its `Execute` is a no-op.
- **Every failure of a screen document surfaces as one type**, `XamlLoaderException`, carrying the file and —
  where the failure has one — the line and column, per AGENTS.md §9.10. That includes the failures MGUI
  leaves bare, which the loader re-describes through `XamlLoaderDiagnostic.FromException` while keeping the
  original as the inner exception.
- **The loader returns an unregistered window.** Adding to `MGDesktop.Windows` belongs to `ScreenStack`,
  which does it on push and undoes it on pop.
- **Anything that must be tested takes an `MGDesktop`, never a `UIRoot`.** `XamlUIScreenBase` is built around
  `BuildWindow(MGDesktop)`; `OnInitialize(UIRoot)` does nothing but hand it `root.Desktop`.
- **The screen envelope's extension is `.uiscreen`**, catalogue type `uiscreen`. `.screen` names a different,
  legacy format from an older widget toolkit, which no code reads.

## Consequences

- A screen's layout can be changed without touching C#, and opened in the screen editor.
- A screen that loses a name in its XAML says so at load, naming the control and the document, instead of
  returning a null that is tripped over later and far from the cause. `FindControl<T>` also separates two
  mistakes that `TryGetElementByName<T>` reports identically — an absent name, and a name belonging to
  another kind of control.
- **Strict mode is stricter than what the editor's preview accepts.** The preview parses in `Compatibility`,
  where no validation runs at all. A document the editor previews happily can therefore be refused at
  runtime. That is the intended direction — the runtime is the one that must not fail silently — but it means
  the two are not interchangeable checks.
- Screen loading is not a hot path: `UIScreenBase.Initialize` guards `OnInitialize` with a one-shot flag and
  runs it once per screen, while `Update(GameTime)` is the per-frame method. File I/O and allocation in the
  loader are therefore fine, and AGENTS.md §9.3 does not apply to it.
- Nothing dedupes by asset identity: two loads of the same screen produce two independent windows. The
  one-shot guard lives one layer up, in `UIScreenBase`.
- `MGDesktop` warns on a non-STA thread and only warns. Headless tests depend on that staying a warning.
- `UIScreenAsset.ResourceFiles` remains inert. No code in the repository reads it, its meaning is written
  nowhere, and inventing one would have been a silent asset-format decision. `PreviewResolution` stays an
  editor concern.
