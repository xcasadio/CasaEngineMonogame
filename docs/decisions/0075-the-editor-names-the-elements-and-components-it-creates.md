# ADR-0075: The editor names the screen elements and components it creates

- **Status**: Accepted
- **Date**: 2026-10-09
- **Source**: this chantier: `ai-agent/tasks/editor-default-names-tasks.md`, decisions D1 to D3 (author answers, 2026-10-09), proposals P1 to P5 and P8, tasks T1.1 to T1.3

## Context

- An element added from the screen editor palette had no name, and a duplicated or pasted element kept the name of its original, so a screen could hold two elements with the same name. MGUI indexes element names per window and `MGWindow.IndexElementName` throws `MGDuplicateElementNameException` on a duplicate; the screen loader reports it as a `DuplicateElementName` diagnostic.
- A component added through the "Add component" dialog, or created by dropping a model in the viewport, kept the name `Object <guid>` that `ObjectBase` gives by default.
- A component cloned with its entity lost its name. `EntityComponent(EntityComponent)` does not call the only `ObjectBase` copy constructor that copies `Name`, and other `Clone()` overrides build a new object. Duplicating an entity in the editor therefore gave `Object <guid>` to all its components.
- The naming convention of `screen-authoring-conventions.md` listed six prefixes (`btn`, `lbl`, `inp`, `lst`, `pnl`, `img`), called them "two-letter" while they have three letters, and covered only some of the 17 types of the palette.
- The author asked that every element of the UI, of an asset and of what an asset contains can be named properly.

## Decision

- **D1, convention.** The screen element naming convention is CasaEngine's (`btn`, `lbl` for TextBlock, `img`, `pnl`, `inp`, `lst`). It also applies to the content of RacingGameCasaEngine.
- **D2, default names.** The editor gives a unique default name, at creation, to every screen element (for example `img1`) and every component (for example `StaticModelComponent`) it creates, instead of none or `Object <guid>`. Only the editor names what it creates: the engine default `Object <guid>` of `ObjectBase` is unchanged, and components created by a game's code are named by that code.
- **D3, no validator warning.** The screen validator does not warn about an unnamed or duplicated element.
- **P1, prefix table.** `btn` Button; `lbl` TextBlock; `img` Image; `inp` TextBox; `lst` ListBox; `cmb` ComboBox; `chk` CheckBox; `rdo` RadioButton; `sld` Slider; `prg` ProgressBar; `wnd` Window; `pnl` for the panels and containers (StackPanel, DockPanel, Grid, UniformGrid, ScrollViewer, Border, Expander, OverlayPanel). A type is read with or without the `MG` prefix. A type absent from the table takes its name without `MG`, first letter in lower case (`contextMenu`).
- **P2, new element.** The name is the prefix followed by the smallest integer n >= 1 not used in the document (`img1`, `img2`, `btn1`). The comparison is ordinal, as in the MGUI name index. The container that wraps the root (on add, duplicate and paste) is named `pnl<n>` the same way.
- **P3, duplicate and paste.** Each node of the inserted subtree is processed in document order. A node whose name is free in the document, counting the names already given inside the subtree, keeps it. Otherwise its trailing digits are removed and the smallest free n >= 2 is appended (`btnPlay` becomes `btnPlay2`; `lblStat0` becomes `lblStat6` when `lblStat0` to `lblStat5` exist). An unnamed node is named as in P2. The wrapper of a duplicated or pasted root is named after the names of the subtree.
- **P4, new component.** The name is the name of its type, made unique within the entity as `GenerateUniqueEntityName` does: ` (n)` with n >= 2, case-insensitive, counting the component tree under `RootComponent` and `Components`. It applies to the "Add component" dialog and to the model drop. The "Particle System" name of the particle component is unchanged.
- **P5, label.** `GetComponentLabel` also hides an instance name equal to the type name, so a component named by default is displayed as before.
- **P8, clone keeps the name.** `EntityComponent.CloneWithName()` calls `Clone()` then copies `Name`. The three cloning sites (`Entity` root and `Components`, `SceneComponent` children) call it. The 34 `Clone()` overrides and the copy constructor of `EntityComponent` are unchanged, and the clone keeps a new id.
- The naming logic lives in `CasaEngine.EditorServices` (`UIScreenNodeNames`, `EditorComponentNames`), pure and unit tested.

## Consequences

- Elements and components created in the editor are named and unique, and the names survive the duplication of an entity. A duplicated screen loads without a `DuplicateElementName` diagnostic.
- Undo then redo gives the same names back.
- The names already saved in existing screens and worlds are not changed; the RacingGame plan handles the content of RacingGameCasaEngine.
- A component created by a game's code keeps `Object <guid>` unless that code names it.
- The validator stays silent about unnamed or duplicated elements (D3); a duplicate written by hand in a XAML file is still reported by the loader.
- A duplicated or pasted subtree always ends with distinct names, at the cost of a numeric suffix on the copy.
