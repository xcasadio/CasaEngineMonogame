# Asset documents without a dedicated editor

Decisions: see [ADR-0120](../decisions/0120-preview-and-edit-documents-for-assets-without-one.md).

Opening an asset (double-click in the content browser, or `--open-asset <relative path>` in the automation) goes through the routes of `GameEditor`. Three documents cover the files that had no editor.

## Read-only preview

Any file whose extension has no document and is not an engine asset format (`Constants.FileNameExtensions`) opens in a read-only preview tab, one per file (`panel_asset_preview_<hash>`).

| Kind | Extensions | Shows |
|---|---|---|
| Image | `png`, `jpg`, `jpeg`, `bmp`, `gif`, `tga` | size, bit depth, colour type, picture |
| Cube map | `dds` | size, mip count, FourCC, the six faces |
| Audio | `wav`, `ogg` | format, rate, channels, bits, duration; Play and Stop on the editor bus (the file must be catalogued) |
| Bitmap font | `fnt` | face, size, line height, pages, character count, a sample text drawn with the font, the page images |
| Model | `gltf`, `glb` | mesh, material, image and node counts; the model in 3D, in neutral gray |
| Text | any other UTF-8 file without NUL byte in its first 8 KiB | encoding, line count, the first 400 lines |
| Binary | any other file | size; a grayscale view when the size is a square number of bytes (a height map) |

- The parts without UI are in `CasaEngine.EditorServices/AssetPreview`: `AssetPreviewClassifier` (kind of a file), `AssetPreviewReader` (headers), `AssetPreviewGrayscale` (square byte maps).
- The UI is `CasaEngine.Editor/Controls/AssetPreviewPanel.cs`, with `ModelPreviewViewport` for the 3D view.
- The tab owns what it loads (textures, model, font, audio voice) and releases it when it closes.

## `.texture`

`TextureAssetDocumentPanel` edits a `Texture` asset:

- the linked image (`texture_asset_id`) is previewed;
- the sampler is edited: `texture_filter`, `address_u`, `address_v`, `address_w`, `max_anisotropy` (1 to 16). Every change builds a new `SamplerState`; the other sampler fields are kept;
- the fields the panel does not edit are kept on save, including the optional `generate_mipmaps` (ADR-0124, `docs/engine/texture-mipmaps.md`), which is set in the file;
- Save writes the file through `EditorAssetWriterService`; File > Save saves every modified texture document; Reload reads the file again. A modified document shows `*` in its tab title.
- A modified texture document asks "save before losing changes?" when its tab is closed, when the editor quits and when another project is opened (`docs/editor/editor-message-boxes.md`).

The logic without UI is `CasaEngine.EditorServices/Textures/TextureAssetDocument.cs`.

## `.staticModel`

`StaticModelAssetDocumentPanel` shows a `StaticModel` asset in 3D and assigns its materials:

- one row per mesh and per sub-mesh, with its material name and id, a picker restricted to `material` assets, and None to clear it;
- saving changes the material ids and writes vertices, indices, nodes and the other fields back unchanged. A mesh without slot metadata gets the values the loader fills in (`slot_name`, `material_slot_index`, `StaticModelMaterialSlots.EnsureMetadata`);
- Save, Reload, the save with File > Save and the `*` follow the texture document, and so does the question asked before the changes are lost.

The logic without UI is `CasaEngine.EditorServices/StaticModels/StaticModelAssetDocument.cs`.

A mesh may carry a `tangents` array next to `vertices`: one `{ "x", "y", "z", "w" }` entry per vertex, in the same order, written only for a mesh that has tangents. A mesh without it has no tangents and is drawn without its normal map. Decisions: see ADR-0123.

## Writing a world from code

A tool that builds entities in code, without a running game, writes a `.world` file with `EditorWorldWriter.SaveWorld(World world, IReadOnlyList<Entity> entities)` (`CasaEngine.EditorServices`). It writes `world.FileName` under `EngineEnvironment.ProjectPath` in the format `SaveWorld(World)` writes: the same world-level keys in the same order, and one inline entity reference (empty `asset_id`) per entity, in list order. The file loads with `World.Load` and the editor saves it back unchanged.

- The entities are neither initialized nor added to the world, so entities whose components need the game to initialize (for example a `StaticModelComponent`) can be written offline. The world and the entities are left untouched.
- The world must hold no entity references yet: otherwise `InvalidOperationException`. A null world, list or entity throws `ArgumentNullException`.
- The caller sets the ids and names of the world, entities and components; a component without an id constructor gets a random id.
- Only inline entities are written; a reference to an `.entity` asset is not supported by this overload.

## Automation

| Option | Effect |
|---|---|
| `--set-texture-sampler <key>=<value>` | edits the opened `.texture`, saves it, and reports `Automation texture edit: <key>=<value read back from the saved file>` |
| `--set-static-model-material <mesh index>=<material id>` | assigns the material of a mesh of the opened `.staticModel`, saves it, and reports the id read back from the saved file |

- The edited file is snapshotted as bytes and restored byte for byte at the end of the run.
- An invalid key, value, index or id writes `[Automation] Failed to update ...`, and the run exits with code 1.
- A malformed option, with no `=` or an empty side, is ignored when the arguments are parsed, like `--set-material-property` and `--set-particle-property`: the diagnostics then report `<none>` for that edit.
- Example: `CasaEngine.Editor.exe --project <project.json> --open-asset UI/Sprites/Ui.Menu.ButtonsTexture.texture --set-texture-sampler address_u=Wrap --diagnostics-out diag.txt`.

## Limits

- An engine asset format without a document (`.model`, `.skeleton`, `.environment`, `.gameMode`, `.gameplayMode`, `.tileset`, `.buttonsMapping`, `.dialogue`, `.shader`) does not open; the automation reports it with exit code 1.
- A dock layout saved with one of these tabs restores it as "Panel unavailable".
- The 3D preview uses a fixed three-quarter camera that frames the model's bounds.
