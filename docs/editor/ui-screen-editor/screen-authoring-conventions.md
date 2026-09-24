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

### Image sources: project sprites and 2D animations

An `Image`'s `SourceName` can also name a **sprite** or a **2D animation** (`.anim2d`) of the project, by its asset
id or its catalogue name; the engine's host (`CasaUIAssetProvider`) resolves it and holds what it loaded until the
UI root is released (ADR-0038, ADR-0037). A name it cannot resolve is logged once (`cannot resolve UI image`) and
the image shows nothing. MGUI also asks the host for some of its own optional textures: the title bar's
`DockClose`, for one, which only `MGDesktop.LoadDefaultResources` registers and the game runtime does not call.
That logs the same warning once, while MGUI draws the icon itself.

A 2D animation plays on the UI clock, in real time, not on the game's logic ticks:

| Property | Behaviour |
|---|---|
| `AnimationStartOffset` (`TimeSpan`) | The point of the timeline to restart at. Changing it restarts the animation there. |
| `IsAnimationPlaying` (`bool`, default true) | False restarts the animation at `AnimationStartOffset` and holds that frame; true resumes playing from it. |

Both are bindable, so a view model can start an animation in phase with the game (the Alundra HUD restarts its
magic pips at the frame the original's counter gives). An image that is collapsed or entirely clipped is not
updated (`MGUI/MGUI.Core/UI/MGElement.cs:3936-3945`): its animation pauses until it shows again.

A frame's part position shifts the image in the element's own pixels, **X to the right and Y downward**
(`MGImage` translates its destination by it, `MGUI/MGUI.Core/UI/MGImage.cs:495`, from
`CasaUIAssetProvider.CasaUIAnimatedImage.CurrentDrawOffset`). A world animation's part positions are Y **up**
(`CasaEngine/Framework/Assets/Animations/Animation2dBoundsCalculator.cs:28-31`): author a UI animation in screen
pixels. A clip with several parts shows its first part in draw order only.

Not declarable in XAML: an image's filtering when it is scaled down (`UseLinearFilteringWhenDownscaling`, gap G8
of `ai-agent/audits/mgui-gaps-from-xaml-screens.md`). A pixel-art screen sets it in `OnWindowLoaded`.

---

## 6. Bindings (Design-time vs Runtime)

### Bound screens

A game screen is bound to an observable view model (ADR-0038). The markup declares a binding on every property
that changes, `{dataBinding:MGBinding Path=...}` (nested paths such as `IconSlot3.SourceName` work); the screen
sets its view model as the window's data context once, in `OnWindowLoaded`; the game's code writes the view
model, never the elements.

```csharp
protected override void OnWindowLoaded(MGWindow window)
    => window.WindowDataContext = ViewModel;
```

```xml
<Image Name="MoneyDigit0" Stretch="None"
       SourceName="{dataBinding:MGBinding Path=MoneyDigit0.SourceName}"
       CanvasLeft="{dataBinding:MGBinding Path=MoneyDigit0.Left}"
       CanvasTop="{dataBinding:MGBinding Path=MoneyDigit0.Top}"
       Visibility="{dataBinding:MGBinding Path=MoneyDigit0.Visibility}" />
```

- **Notify only what changed.** A setter compares before raising `PropertyChanged`; pushing the same state again
  must notify nothing.
- **Match the target's type.** A binding whose source property has the same declared type as its target copies
  the value through compiled, typed accessors, without allocating (MGUI ADR-0016). A converter, a string format or
  a type conversion takes the general path. An element's canvas coordinates are bindable through its
  `CanvasLeft` and `CanvasTop` properties, as `int?`.
- **Not bindable**, so the screen applies them in code from its view model: a `RenderTransform`'s properties (gap
  G9) and an image's downscale filtering (gap G8).
- **Dispose the screen.** `XamlUIScreenBase.Dispose` takes the window's bindings out of MGUI's static registry
  (gap G10). A screen that is rebuilt, for example at every world change, must be disposed, or the registry keeps
  its previous window and view model reachable.
- **Tests.** MGUI's binding registry is static and single-threaded: a test class that loads bound XAML or sets a
  data context joins a serial xUnit collection (`MguiDataBindingCollection` in `CasaEngine.Tests`).

A screen that has little to update may still push values in code, as below.

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

A project can also replace the markup of the engine's **dialogue box** with its own `.uiscreen`, named by the
project setting `DialogueScreenAsset`; the element names that markup must keep are listed in
[dialogue-choices-and-bitmap-fonts.md](../../engine/dialogue-choices-and-bitmap-fonts.md).

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
   An `Image`'s **Source** row edits its `SourceName`: type an asset id or name (or a binding), or use the
   browse button under it to pick a sprite or 2D animation from the catalogue, which writes that asset's id
   and updates the preview (ADR-0038).
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
