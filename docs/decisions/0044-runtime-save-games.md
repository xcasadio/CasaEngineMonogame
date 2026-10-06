# ADR-0044: The runtime owns a generic save-game service for the player's game saves

- **Status**: Accepted
- **Date**: 2026-09-28 (decisions D1-D5 and the answer to O1 taken with the author on 2026-09-27; plan approved on 2026-09-28)
- **Source**: this chantier: `ai-agent/tasks/save-game-service-tasks.md` (decisions D1-D5, technical
  choices approved with the plan, open point O1), branch `chantier/save-game-service`. Consumer: the
  Alundra port (`docs/plan-e16-etat-partie.md` of the parent repository, step E16).

## Context

- The runtime has no save-game service. At run time only two files are written to disk: the display
  settings (`CasaEngine/Framework/Application/DisplaySettingsPersistence.cs`) and the project
  settings (`CasaEngine/Framework/Configuration/Project/ProjectSettingsHelper.cs:101`).
- `AGENTS.md` §9.9 said that saving and exporting belong to the editor and the tooling, and loading
  and running to the runtime. The archived chantier
  `ai-agent/tasks/archive/runtime-save-jobject-cleanup-plan.md` removed every `Save(JObject)` from
  `CasaEngine`. A player's game save is, by nature, a write at run time.
- The Alundra port needs to save and load a player's game. A save file can come from another player
  or be edited by hand: it is untrusted input.
- The reference engines use named slots in a per-user folder (`user://` in Godot,
  `Application.persistentDataPath` in Unity, `Saved/SaveGames` in Unreal) and, in Unreal, a
  save-game object written to a slot (`USaveGame`, `UGameplayStatics::SaveGameToSlot`), serialized in
  both directions by one method (`Serialize(FArchive&)`), with a data version the object reads to
  migrate (`ULocalPlayerSaveGame`).
- The runtime already ships Newtonsoft.Json 13.0.4 (`Directory.Packages.props`) and has no service
  locator: global services are static properties of `GameSettings`
  (`CasaEngine/Framework/Application/GameSettings.cs:9-12`).

## Decision

- **D1** — CasaEngine gets a **generic save-game service in the runtime**. `AGENTS.md` §9.9 is
  clarified: saving and exporting **assets** stay with the editor and the tooling; the player's game
  save is a runtime service (answer to O1).
- **D2** — **Save-game object contract**: the game implements `ISaveGameData`
  (`LatestDataVersion`, `Serialize(SaveGameArchive)`) and the service writes it to a named slot, or
  fills a new instance of the type the caller names when loading.
- **D3** — **Two formats**, chosen by the caller: readable JSON (debugging) and compact binary
  (shipping).
- **D4** — Files live in a **per-user folder**: `LocalApplicationData/<ProjectName>/SaveGames`,
  resolved at the first I/O call. An invalid or default `ProjectName` is a developer error, never
  silently sanitized.
- **D5** — The first consumer is the Alundra port, step E16, after E15.
- Technical choices approved with the plan:
  - **No instantiation driven by the file**: the caller gives the type (`TryLoad<T>()`); the service
    uses neither `TypeNameHandling`, `JsonConvert`, `ToObject`, `JsonSerializer` nor
    `BinaryFormatter`.
  - A **symmetric archive** (`archive.Value("hp", ref hp)` reads or writes depending on the direction),
    without reflection; JSON finds fields by name, binary writes them in call order and relies on the
    data version for compatibility.
  - A **CRC-32** written in the code (no new dependency) on the binary format; a size cap per slot; a
    whitelist for slot names; writes to a temporary file then replaced.
  - One concrete file storage, no interface until a second backend exists; a synchronous API.
  - **Errors**: developer misuse throws; everything else (missing, unreadable, too large, corrupted,
    newer data version, I/O failure) becomes a logged result, never an exception into the game loop;
    an exception thrown by the game's own `Serialize` propagates.
  - CRC-32 detects accidental corruption only; save files are not authenticated; all bounds checks and consumer validation must hold for attacker-crafted files.

## Consequences

- `CasaEngine` gains one write path at run time, limited to this service; the rest of the runtime
  stays read-only.
- The game must validate the meaning of every loaded value (ids, indices, counters, coordinates)
  before touching its live state, then apply the object in one step or not at all; save metadata is
  untrusted text when displayed.
- Deferred: asynchronous API, cloud saves, encryption, compression, file authentication, cleanup of
  orphaned temporary files, several local users, a second storage backend, persistence of Yarn
  variables, and an option to refuse JSON saves in a shipping build (O5).
- Writes made under AppData from the Claude app are virtualized and invisible elsewhere (O3): manual
  recipes run outside the app, tests write under a temporary folder.
