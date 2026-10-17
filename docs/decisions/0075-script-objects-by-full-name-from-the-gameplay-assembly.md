# ADR-0075: Script objects are created by full name, from the gameplay assembly only

- **Status**: Accepted
- **Date**: 2026-10-17 (decisions D1 to D8 of 2026-10-16, D9 to D11 of 2026-10-17)
- **Source**: this chantier: `ai-agent/tasks/script-types-full-name-tasks.md` (decisions D1 to D11, tasks T1.1 to T5.1). Parent repository: `docs/plan-restructuration-dll.md` section 0 (author decisions D-R-2 to D-R-4, D-R-9, D-R-10, D-R-11) and section 4 (slice R0). Related: ADR-0011, ADR-0018 (the editor reload of the gameplay assembly), ADR-0024 (gameplay mode), ADR-0038 (design-time view models).

## Context

- `ElementFactory.Create<T>(name)` found a type by its simple `Type.Name`, in every non-collectible assembly of the AppDomain plus the registered script assemblies, first match wins, case-insensitive. A name shared by two types silently picked one; a type of the wrong kind returned `null`; the runtime never registered the gameplay assembly (it only ran `Assembly.LoadFile`).
- Five places create a script object from a string read in the data: the entity proxy (`script_class_name`), the world proxy, the player controller (`player_controller_class`), the game mode (`mode_class_name`) and the design-time view model of a screen (`view_model_type`).
- `World.InternalAddEntities` caught every exception of an entity creation, logged it and skipped the entity, so a wrong script name only produced a log line and a missing entity.
- Two engine types were named by a string in the data (`PlayerController`, `ScriptArcBallCamera`).
- The Script row of the entity and world inspector had been lost in the migration to the MGUI editor (commit `dc2f4d25`, a regression: the WPF inspector showed and picked the script).

## Decision

- **D1**: a script object is created by the **full name** of its class (namespace included), exact case, only from the registered gameplay assembly(ies). `ElementFactory.CreateScriptObject<T>(typeName, key, source)` is the creation point; `TryResolveScriptType` and `GetScriptTypeNames(baseType)` serve the editor. Accepted types: public, top-level, concrete, non-generic, with a public parameterless constructor, deriving from the required base. The name index is rebuilt only on registration and unregistration. Components (`"type"` in the JSON) keep the old path.
- **D3**: a name that is not found, is ambiguous or is of the wrong kind throws `ScriptTypeResolutionException`, carrying the key, the value, the file, the assemblies searched and a hint (short name, case, abstract or nested type, ambiguity, wrong kind, no assembly registered).
- **D4**: the exception is thrown at creation, never at `Load`; a load without a gameplay assembly (converter check, tests, an editor before the build) still works.
- **D5**: `AssemblyManager.Load` registers the assembly only with the default loader (and unregisters the one it registered on reload). With a provided loader (the editor shadow copy) it registers nothing and keeps no reference; `EditorScriptAssemblyService` remains the registering owner.
- **D6/D7**: the message cites the file of the object (`FileName`, else the catalog entry of its `AssetId`, else the world for an inline entity, else an unknown file; the controller and the mode cite their `.gameMode`). The `catch` of `World.InternalAddEntities` lets this exception through (`when (e is not ScriptTypeResolutionException)`); every other exception keeps the log and skip behaviour.
- **D2**: engine-owned types are created by code: an empty `player_controller_class` is the engine `PlayerController` (the default, and `RpgGameMode.gameMode`); `Entity.SetGameplayProxy(proxy)` sets a proxy from code and records its full name (the demos and SandBoxGame use it for `ScriptArcBallCamera`).
- **D9/D11**: `GameplayExecutionPolicy.ThrowOnScriptTypeResolution` is true for `Runtime` and `EditorSimulation`, false for `EditorPreview`. In the preview, a tolerance scope (a `[ThreadStatic]` counter, no allocation) opened in `InternalAddEntities`, around `CreateGameplayProxy` and around the world proxy initialization turns the exception into a warning: the entity or the world is **kept without script**, its `GameplayProxyClassName` intact, visible and selectable.
- **D10**: the inspector (`EntityDetailsPanel`) shows the script of the selected entity and world again, as a list of the full names of the registered gameplay proxies (plus the current value when absent), with a warning element and the resolution message when a non-empty name does not resolve; the choice is an undoable history command.

## Consequences

- **Data migration**: the value of `script_class_name`, `player_controller_class`, `mode_class_name` and `view_model_type` changes form, from a simple name to a full name. The RPGDemo data are migrated; a project must rewrite its own files (the converter and the Alundra data in the parent repository do the same). A simple name now fails with a hint naming the full name.
- Two classes with the same simple name in different namespaces are now distinguishable; the same full name in two registered assemblies is reported as ambiguous, with both assemblies cited.
- A wrong script name is no longer a silent missing entity in game: it stops the load with the file and the hint. In the editor preview it stays a warning.
- A test that creates a script by name registers its assembly (module initializer) and uses top-level public types; nested test types are moved to the top level.
- Known limit: `EditorScriptAssemblyService.Unload()` returns false in a Debug build even when everything is released (a temporary holds the assembly while the unload is checked); not changed here.
- Out of scope: components, the MGUI themes, the dynamic compiler, the dead fields `ai_controller_class` and `hud_class`. "Game Mode" and "Default Pawn" of the world are also missing in the MGUI inspector (same `dc2f4d25` loss): to restore separately (plan open point O3).
