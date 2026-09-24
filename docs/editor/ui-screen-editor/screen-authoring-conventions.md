# CasaEngine UI Screen Authoring Conventions

This guide defines the conventions for creating, naming, and structuring UI screens
in CasaEngine. Following these conventions ensures correct editor import, consistent
runtime behaviour, and maintainable XAML assets.

---

## 1. Asset Structure

Every UI screen is represented by two files:

| File | Purpose |
|------|---------|
| `*.uiscreen` | JSON asset descriptor (name, id, source reference), catalogue type `uiscreen` |
| `*.xaml` | MGUI XAML markup defining the visual tree |

> **Not to be confused with `*.screen`.** That extension belongs to a different, legacy format from an older
> widget toolkit — an entity envelope plus a flat list of absolutely positioned widgets. No code reads it.
> See ADR-0035.

The `.uiscreen` file must contain:

```json
{
  "id": "<uuid>",
  "name": "MainMenu",
  "source_xaml_file": "Screens/MainMenu.xaml"
}
```

It may also declare these optional fields:

| Field | Purpose |
|-------|---------|
| `theme_name` | Name of an `MGTheme` (registered in `MGResources`) applied to the loaded window. An unknown or missing name keeps whatever theme the document itself declares. |
| `preview_resolution` | `{ "x": <int>, "y": <int> }`, the resolution the editor preview builds the window at. Defaults to 1920×1080. |
| `resource_files` | List of extra files (resource dictionaries, etc.) the editor loads alongside the XAML. |
| `design_time_data_file` | Path (same resolution rules as `source_xaml_file`) of a JSON document naming a view-model type and giving it property values. The editor preview instantiates that type from the loaded gameplay assembly, populates it, and uses it as the preview's data context; a missing or invalid file is logged and reported in the preview, which then renders without a data context (ADR-0038). The runtime never reads this field. |

The `.uiscreen` format is additive: an envelope written before a field existed loads with that field at
its default, and saving it again does not add fields it never had.

The `source_xaml_file` path may be:
- Absolute, or
- Relative to the `.uiscreen` file (preferred), or
- Relative to the project root (`EngineEnvironment.ProjectPath`).

Each candidate must exist on disk to be used; when none does, loading fails and names every path it tried.

> **Decisions: see ADR-0038** (`docs/decisions/0038-game-screens-are-assets-bound-to-view-models.md`) for why
> screens are catalogued assets, how design-time data is meant to be consumed, and the rest of this chantier's
> decisions.

---

## 2. Naming Conventions

### Assets and files
- Screen asset name: `PascalCase` without spaces (e.g. `MainMenu`, `InventoryHUD`, `PauseMenu`).
- File names match the asset name plus extension (e.g. `MainMenu.uiscreen`, `MainMenu.xaml`).
- Store screens under `Content/Screens/` or a sub-folder that mirrors their category.

### XAML control names
- All named controls use `PascalCase` prefixed with a two-letter type hint:
  - `btn` — Buttons: `btnConfirm`, `btnCancel`
  - `lbl` — Text blocks / labels: `lblTitle`, `lblScore`
  - `inp` — Input boxes: `inpSearch`, `inpPlayerName`
  - `lst` — List views: `lstInventory`, `lstOptions`
  - `pnl` — Panels / containers: `pnlHeader`, `pnlFooter`
  - `img` — Images: `imgAvatar`, `imgBackground`
- Controls that are not referenced in code do not need a `Name` attribute.

---

## 3. XAML Structure Best Practices

### Root element
- Use `MGDockPanel` or `MGStackPanel` as the root unless the screen maps 1:1 to a window.
- Do **not** use a raw `Window` as root unless you intentionally want a floating window — the editor wraps non-Window roots in a preview window automatically.

### Layout hierarchy
- Favour `MGDockPanel` for header/footer/sidebar layouts.
- Favour `MGStackPanel` for lists of vertically or horizontally arranged controls.
- Favour `MGCanvas` (absolute positioning) only when nothing else works; absolute layouts break on different screen resolutions.

### Margins and padding
- Use `Margin` for spacing between siblings: `Margin="8"`
- Use `Padding` for internal insets inside a container: `Padding="16,8,16,8"` (left, top, right, bottom).
- Avoid mixing hardcoded pixel positions with layout-driven containers in the same parent.

---

## 4. Themes and Styling

- Apply shared styles via MGUI's `Style` resources when styling the same control type in many places.
- Prefer palette-level color constants over inline `Color` literals.
- Define a project-wide colour scheme in a shared resource dictionary if multiple screens share the same look.

---

## 5. Resource References

- Textures, fonts, and sounds referenced inside XAML must be registered in the Content pipeline (`.mgcb`).
- Reference textures by their `Content path` key, not an absolute file path.
- Fonts: declare the font family name as registered in `FontStashSharp`.

---

## 6. Bindings (Design-time vs Runtime)

At runtime, bind dynamic data explicitly in code rather than inline XAML binding syntax (MGUI uses code-behind for most data updates).

### Loading a screen at runtime

Derive from `XamlUIScreenBase` (`CasaEngine/Framework/UI/`). It loads the document, then calls
`OnWindowLoaded` once so the screen can find its controls and keep them in fields. Never look a control up by
name from `Update` — that runs every frame.

```csharp
internal sealed class ScoreScreen : XamlUIScreenBase
{
    private MGTextBlock _score;

    public override UILayer Layer => UILayer.HUD;

    // Acquires "ScoreScreen" through the asset manager and holds the handle for the screen's lifetime
    // (ADR-0037, ADR-0038). assetIdOrName may be the asset's id or its catalogue name, resolved the same
    // way an MGUI Image's SourceName resolves an image (CasaUIAssetProvider).
    public ScoreScreen(AssetContentManager assetContentManager)
        : base(assetContentManager, "ScoreScreen") { }

    protected override void OnWindowLoaded(MGWindow window)
        => _score = FindControl<MGTextBlock>("lblScore");

    public override void Update(GameTime gameTime)
        => _score.Text = Game.Score.ToString();
}
```

The owner must call `Dispose()` on the screen once it is done with it (typically where it already removes the
screen from its `ScreenStack`), to give the handle back; a `CollectUnreferenced()` at the next world change
then frees the envelope if nobody re-acquired it. A screen built once from a caller-supplied `UIScreenAsset` and
its file path, or from an embedded `XamlDocumentSource`, holds nothing here and disposing it is a no-op.

A screen with no catalogued asset passes a `XamlDocumentSource` instead. `FindControl<T>` throws — naming the
control and the document — when the name is absent or belongs to another kind of control, rather than handing
back a null that is tripped over later.

The window comes back **unregistered**: `ScreenStack` adds it to the desktop on push and removes it on pop.

Parsing runs in `XamlLoaderMode.Strict`, which is **stricter than the editor's preview** — the preview parses
in `Compatibility`, where no validation runs. A document the editor previews happily can still be refused at
runtime, for an unknown element or a name declared twice. See ADR-0035.

### Design-time data file (`design_time_data_file`)

The file is a JSON document of the shape:

```json
{
  "view_model_type": "AlundraInventoryViewModel",
  "values": {
    "WeaponName": "Poignard",
    "Slot0": { "SourceName": "hud-heart" }
  }
}
```

- `view_model_type` names the view model's type by its **simple name** (no namespace), the same convention
  `ElementFactory` uses everywhere else in the engine -- not its fully-qualified name. It must be a public
  type with a public parameterless constructor, found either in the engine or in the project's loaded
  gameplay assembly (`ElementFactory.RegisterScriptAssembly`); a name that resolves to more than one type
  across assemblies picks whichever `ElementFactory`'s cache found first, so keep design-time view model
  names distinct.
- `values` is populated onto the instance with `Newtonsoft.Json.JsonConvert.PopulateObject`, so it follows
  the same property-name matching and nested-object rules as any other Newtonsoft.Json deserialization.
- The preview builder (`CasaEngine.EditorServices/ScreenEditor/Preview/UIScreenDesignTimeDataLoader.cs`) sets
  the populated instance as the preview window's `WindowDataContext`, so every `{dataBinding:MGBinding
  Path=...}` in the markup binds against it exactly as it would against a real game view model.

At design time (`UIDesignModeContext.IsDesignTime == true`):
- The preview builder injects placeholder values from `UIScreenMockDataContext`.
- Name controls meaningfully so the mock system can select relevant placeholder text:
  - A `MGTextBlock` named `lblScore` → mock text `"12,500"`
  - A `MGTextBlock` named `lblTitle` → mock text `"Screen Title"`
- Register custom mock values via `UIScreenMockDataContext.Register(key, value)` for project-specific content.

---

## 7. Screen Categories

| Category | Examples |
|----------|---------|
| **Menu** | `MainMenu`, `PauseMenu`, `SettingsMenu` |
| **HUD** | `HealthBar`, `MinimapOverlay`, `QuestTracker` |
| **Popup** | `ConfirmDialog`, `ErrorMessage`, `LevelUp` |
| **Inventory** | `InventoryScreen`, `EquipmentPanel`, `TradeWindow` |
| **Cutscene** | `DialogueBox`, `SubtitleOverlay` |

Place each category in a matching sub-directory under `Content/Screens/`.

---

## 8. Editor Workflow

1. Right-click in Content Browser → **New › UIScreen** to create the paired `.uiscreen` + `.xaml` files.
2. Double-click the `.uiscreen` file to open it in the screen editor.
3. Use the **Toolbox** to drag controls into the hierarchy.
4. Edit properties in the **Properties** panel.
5. Adjust visual position with the drag handles in the preview surface.
6. Switch resolution presets (1280×720, 1920×1080, etc.) to verify responsiveness.
7. Save the XAML file — the preview will hot-reload automatically.
8. All edits are undoable with `Ctrl+Z` / `Ctrl+Y`.

---

## 9. Performance Notes

- Avoid deeply nested layout containers (limit to ~5 levels).
- Prefer `IsHitTestVisible=false` on purely decorative elements to reduce hit-testing cost.
- Large background images should use `Stretch` alignment rather than being scaled in code.

---

*Last updated: see git history for `docs/editor/ui-screen-editor/screen-authoring-conventions.md`.*
