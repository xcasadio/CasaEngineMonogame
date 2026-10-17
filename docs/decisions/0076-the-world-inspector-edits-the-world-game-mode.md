# ADR-0076: The world inspector edits the world's game mode, and the project save writes it

- **Status**: Accepted
- **Date**: 2026-10-17
- **Source**: this chantier: `ai-agent/tasks/world-game-mode-inspector-tasks.md` (D1 to D8), open point O3 of `ai-agent/tasks/script-types-full-name-tasks.md`

## Context

- The WPF editor, removed on 2026-03-30 (`dc2f4d25`, "remove unused projects"), had "Game Mode" and "Default Pawn" rows in its
  world inspector (`CasaEngine.EditorUI/Controls/EntityControls/EntityControl.xaml`). The MGUI editor never ported them.
- The data model has changed since (`f72b7907`, `08de2874`): a world names its game mode by
  `World.PlayerStartupSettingsAssetId` (a `.gameMode` file, class `PlayerStartupSettings`) and its gameplay mode by
  `World.GameplayModeAssetId` (a `.gameplayMode` file). Both ids are saved with the world.
- The default pawn is not in the world: it is `PlayerStartupSettings.DefaultPawnAssetId`, in the `.gameMode` file. The world holds
  that asset through a counted handle (`World.LoadPlayerStartupSettings`, ADR-0037), so every world that uses the same mode shares
  one instance.
- The editor could not write a `.gameMode`: `EditorAssetJsonSerializer.TrySerialize` had no case for `PlayerStartupSettings`. The
  WPF editor changed the pawn in memory only, and the change was lost at the next load.
- `World.LoadPlayerStartupSettings` was internal to the runtime assembly and its tests.

## Decision

- The world inspector (`EntityDetailsPanel`) shows, under the Script row of ADR-0075, three rows: "Game Mode"
  (`PlayerStartupSettingsAssetId`, `.gameMode` assets only), "Default Pawn" (`DefaultPawnAssetId` of the world's game mode,
  `.entity` assets only) and "Gameplay Mode" (`GameplayModeAssetId`, `.gameplayMode` assets only). Each choice is an undoable
  history command of the World context. The asset picker offers no "none" choice.
- Changing the game mode (and undoing it) reloads the world's settings, so the Default Pawn row follows:
  `World.LoadPlayerStartupSettings` becomes public.
- The default pawn is edited on the shared game mode instance: it applies to every world that uses that mode, and the row says
  so. The row is disabled when the world has no game mode.
- The `.gameMode` is written with the project save. A pending-edits registry keeps, for each mode changed since the last save, a
  handle on its instance (a world reloaded meanwhile, such as by a script reload, finds the same instance) and its original pawn.
  Both executing and undoing a pawn change record the mode first. The project save writes each mode whose pawn differs from its
  original and releases it. A failed write is logged, keeps its entry, and the world stays marked as modified, so opening another
  world still asks to save. Opening another world without saving, or loading a project's first world, puts the original pawn back
  and releases every entry. A script reload keeps the registry.
- The editor writes a `.gameMode` with `id`, `name`, `default_pawn_asset_id`, `player_controller_class`, `ai_controller_class`
  and `hud_class`. Loading is unchanged: the legacy key `hud_classClass` is still read, and no longer written (`hud_class` is
  already the key read first). The save source is the new value `EditorAssetSaveSource.WorldInspector`.

## Consequences

- The editor has its first write path for `.gameMode` files. A file it writes replaces `hud_classClass` by `hud_class`; older
  readers of the engine read both.
- Changing the pawn from one world changes it for every world of that mode, which the row states.
- An unsaved pawn change keeps its mode in memory, through the registry's handle, until the next save or discard.
- The runtime only gains the public `World.LoadPlayerStartupSettings`; asset writing stays in `CasaEngine.EditorServices` and
  `CasaEngine.Editor`.
- Deferred: a `.gameMode` document editor, the other fields of the mode in the inspector, a "none" choice in the asset picker, and
  a pawn set per world.
