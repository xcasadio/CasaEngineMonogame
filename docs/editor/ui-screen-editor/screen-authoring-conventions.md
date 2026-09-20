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

The `source_xaml_file` path may be:
- Absolute, or
- Relative to the `.uiscreen` file (preferred), or
- Relative to the project root (`EngineEnvironment.ProjectPath`).

Each candidate must exist on disk to be used; when none does, loading fails and names every path it tried.

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

    public ScoreScreen(UIScreenAsset asset, string assetFilePath)
        : base(asset, assetFilePath) { }

    protected override void OnWindowLoaded(MGWindow window)
        => _score = FindControl<MGTextBlock>("lblScore");

    public override void Update(GameTime gameTime)
        => _score.Text = Game.Score.ToString();
}
```

A screen with no catalogued asset passes a `XamlDocumentSource` instead. `FindControl<T>` throws — naming the
control and the document — when the name is absent or belongs to another kind of control, rather than handing
back a null that is tripped over later.

The window comes back **unregistered**: `ScreenStack` adds it to the desktop on push and removes it on pop.

Parsing runs in `XamlLoaderMode.Strict`, which is **stricter than the editor's preview** — the preview parses
in `Compatibility`, where no validation runs. A document the editor previews happily can still be refused at
runtime, for an unknown element or a name declared twice. See ADR-0035.

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
