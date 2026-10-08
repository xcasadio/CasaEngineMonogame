# ADR-0073: Assets without a document open in a read-only preview, and textures and static models get edit documents

- **Status**: Accepted
- **Date**: 2026-10-08
- **Source**: this chantier: `ai-agent/tasks/rgce-editor-project-engine-tasks.md`, proposals P6, P7 and P8 (validated with the plan), tasks T6, T7 and T8

## Context

- The editor opened an asset only when a route existed for its extension: worlds, entities, screens, materials, sprites, 2D animations, particles, sounds, audio mixers, cutscenes, tile maps. Any other catalogued file did not open, and the automation reported nothing about it. Among those files were images, cube maps, waves, bitmap fonts, glTF models, game data such as RacingGame's `.Track` files and height map, and two engine asset formats, `.texture` and `.staticModel`.
- The RacingGameCasaEngine program requires every catalogued asset of its project to open in the editor, at the level its plan sets: edit for engine formats, preview for media and data (`RacingGame/ai-agent/tasks/rgce-editor-project-tasks.md`, P1).
- The runtime already reads all those files. `EditorAssetJsonSerializer` already writes `Texture` and `StaticModel`, and `EditorAssetWriterService` saves them.

## Decision

- **Read-only preview** (T6). A file whose extension has no document, and that is not an engine asset format (`Constants.FileNameExtensions`), opens in a read-only preview document:
  - images: the picture and its header;
  - DDS cube maps: their six faces;
  - waves and ogg: header information, with play and stop through the editor bus;
  - BMFont `.fnt`: the font information, a sample text drawn with the font, and its pages;
  - glTF and glb: the model in 3D;
  - any other file: text or binary by content, with a text preview, or a grayscale view when its size is a square number of bytes (a height map).
  The classification and the header readers live in `CasaEngine.EditorServices/AssetPreview`, without UI, and are tested.
- **Engine formats are never previewed.** An engine asset format without a document stays unopened until it gets its own document, so that the preview never stands in for an editor.
- **`.texture` document** (T7). It previews the linked image and edits the sampler: filter, the three address modes and max anisotropy. Every change builds a new `SamplerState` and never mutates a shared one.
- **`.staticModel` document** (T8). It previews the model in 3D and lists its meshes and sub-meshes, each with a picker restricted to `material` assets. Saving changes the material ids; vertices, indices and nodes are written back unchanged, and a mesh without slot metadata gets the values the loader fills in.
- **Editor integration.** Both edit documents follow the other asset documents: a dirty state per document (`EditorHistoryContextKind.Texture`, `.StaticModel`), a save with the project, and disposal when the tab closes.
- **Automation.** `--set-texture-sampler <key>=<value>` and `--set-static-model-material <mesh>=<material id>` edit an opened document, save it, and report in the diagnostics the value read back from the saved file. The file is snapshotted as bytes and restored byte for byte at the end of the run. An invalid key, value, index or id writes a `Failed to` diagnostic, so the run exits with code 1 (`EditorAutomationOutcome`, task T5).

## Consequences

- Every file of a project catalogue opens in the editor, at least as a preview. A new engine asset format needs its own document to open at all.
- The preview reads files directly and loads their pictures and models for the preview only. It holds no asset handle, and releases its textures, models, fonts and audio when its tab closes.
- A raw glTF shows in neutral gray: its images are not engine materials.
- The two edit documents can only be driven by the mouse or by the automation options. Their material picker and their combo boxes were not exercised by an automated test.
- A dock layout saved with a preview, texture or static model tab restores it as "Panel unavailable", as it already did for sound documents.
