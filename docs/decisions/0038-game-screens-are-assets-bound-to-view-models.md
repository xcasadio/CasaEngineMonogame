# ADR-0038: Game screens are catalogued assets bound to observable view models

- **Status**: Accepted
- **Date**: 2026-09-24
- **Source**: this chantier: `ai-agent/tasks/bound-screens-tasks.md`, branch `chantier/bound-screens`. Decided with
  the author from 2026-09-22 to 2026-09-24, after the Alundra port found that its screens could not be opened
  in the editor. MGUI counterpart: MGUI ADR-0016 (`MGUI/Docs/decisions/0016-host-resolved-image-sources-and-allocation-free-binding.md`).

## Context

- ADR-0035 made XAML the source of a screen's structure: code finds elements by name and pushes values.
  A screen can take its markup from a catalogued `.uiscreen` asset or from an embedded resource
  (`CasaEngine/Framework/UI/XamlUIScreenBase.cs`, both constructors).
- The only catalogued screens are RPGDemo's; they read the envelope by hand, outside `AssetContentManager`
  (`Projects/CasaEngine.RPGDemo/Scripts/Screens/RpgDemoScreenAssets.cs:20-34`), so no screen asset is held
  through a counted handle as ADR-0037 requires.
- An `Image` in a screen's markup cannot name its image: code assigns the source at run time, so the editor
  preview shows empty frames (RPGDemo's `MainHUD.xaml`, the Alundra inventory).
- The editor's XAML parser drops XML comments and every namespace declaration but the default one
  (`CasaEngine.EditorServices/ScreenEditor/Xaml/UIScreenXamlParser.cs:74, 88`), and the serializer sorts
  attributes by name (`UIScreenXamlSerializer.cs:75, 85`). A property element of an attached property,
  `<Canvas.Left>`, loses its owner type (`UIScreenXamlParser.cs:98, 115`). Saving a screen from the editor
  therefore destroys its comments and can break it.
- The editor preview builds its window without a data context (`ScreenEditor/Preview/UIScreenPreviewBuilder.cs:34, 49`),
  so a bound screen previews empty. The editor has already loaded the game's gameplay DLL when a screen is
  previewed (`CasaEngine/Framework/Configuration/Project/ProjectSettingsHelper.cs:38`, `ElementFactory`).
- The engine's dialogue box is an engine screen whose markup is embedded in `CasaEngine.dll`
  (`CasaEngine/Framework/Dialogue/UI/DialogueScreen.cs:16-22`); a game cannot restyle it.
- `Animation2dCompositionSampler.ApplyTracks` iterates its tracks with `foreach` through an interface
  (`CasaEngine/Framework/Assets/Animations/Animation2dCompositionSampler.cs:163`), which boxes the
  enumerator on every update.

## Decision

- **Images are named by asset.** CasaEngine implements MGUI's host resolution (MGUI ADR-0016): a name that
  parses as a GUID is an asset id, otherwise an asset name from the catalogue. A sprite asset resolves to its
  sheet texture and its source rectangle; a 2D animation asset (`.anim2d`) resolves to an animated source,
  one player per image over shared composition data. Everything resolved is held through counted handles,
  owned by the UI runtime's provider and given back when that runtime is disposed.
- **Screens are assets.** A game screen is a `.uiscreen` envelope and its `.xaml` file in the project,
  acquired through `AssetContentManager` and held by the screen for its lifetime. RPGDemo moves to that path.
- **Screens bind to observable view models.** A screen's code sets its view model as the window's data
  context; the markup binds image sources, visibility, texts and canvas coordinates. A view model raises
  `PropertyChanged` only for values that actually change.
- **UI animations run on the UI clock.** An animated image advances with the UI frame's elapsed time, not
  with a game's logic tick.
- **Design-time data.** A `.uiscreen` may name a design-time data file (optional field
  `design_time_data_file`): a JSON document that names the view-model type and gives property values. The
  editor preview instantiates that type from the loaded gameplay assembly, populates it and uses it as the
  preview's data context. A missing or invalid file is logged and reported in the preview, which then
  renders without a data context.
- **Lossless editor round trip.** Opening and saving a screen keeps its comments, its namespace declarations,
  the owner type of attached properties, the authored order of attributes and the text of markup extensions.
- **Replaceable dialogue screen.** A project may replace the markup of the engine's dialogue screen with its
  own `.uiscreen` asset; the embedded markup remains the fallback. The replacement keeps the element names
  the dialogue screen binds, which are documented.
- **Allocation-free animation sampling.** `Animation2dCompositionSampler` updates without allocating.

## Consequences

- Every screen of a game can be opened, edited and previewed in the editor with its real images and sample
  data, and saved without losing anything.
- The `.uiscreen` format gains one optional field; existing envelopes load unchanged.
- Screen assets and the images they name follow ADR-0037: freed at the next world change when nothing holds
  them any more.
- A view model that notifies on every tick although its values did not change costs pushes for nothing; the
  rule is to compare before notifying.
- The editor's preview depends on the gameplay DLL being loaded for design-time data; without it, the preview
  still renders, without data.
- A replaced dialogue screen that drops a documented element name loses that part of the dialogue display;
  the contract is documented next to the screen.
