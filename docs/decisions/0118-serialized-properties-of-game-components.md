# ADR-0118: Game components persist the properties they mark with [SerializedProperty]

- **Status**: Accepted
- **Date**: 2026-10-08
- **Source**: this chantier: `ai-agent/tasks/rgce-editor-project-engine-tasks.md`, task T1 and proposal P1, applying proposal P7 of the RacingGame program `ai-agent/tasks/rgce-editor-project-tasks.md` (validated by the author, 2026-10-08)

## Context

- A component is loaded by `ElementFactory.Load<T>`: the `type` key selects the class, then the class's `Load(JObject)` reads its keys (`CasaEngine/Framework/Assets/ElementFactory.cs`). `ObjectBase.Load` reads only `id` and `name`, and `EntityComponent` has no `Load` of its own.
- The editor writes worlds, entity assets and Play snapshots with `EditorEntityJsonSerializer`. Its `SaveComponent` is a closed `switch` over the engine's component types; any other `EntityComponent` is written as `id`, `name` and `type` only (`CasaEngine.EditorServices/EditorEntityJsonSerializer.cs`).
- A component defined by a gameplay DLL therefore lost its fields on every File > Save and every Play. The only extensible round trip was `GameplayProxy.Save/Load`, which the editor cannot assign.
- AGENTS.md §9.9 keeps asset writing in the editor tooling; the runtime only loads.
- Gameplay DLLs run in a collectible load context in the editor, and `ElementFactory` takes care not to pin their types so that scripts can reload.

## Decision

- **Attribute.** A public read-write property marked `[SerializedProperty("json_key")]` (`CasaEngine.Core.Serialization`) is part of its type's serialized form. Supported types: `bool`, `int`, `float`, `double`, `string`, `Guid`, enums (by name), `Vector2`, `Vector3`, `Vector4`, `Quaternion` and `Color`, in the JSON forms of `JsonHelper`.
- **Runtime reading.** `ElementFactory.Load<T>` calls `SerializedProperties.Load` after the type's own `Load`, so every component, scene-component child, entity template and world entity gets its marked properties.
  - A missing or null key keeps the property's current value, which lets a property be added without migrating older files.
  - A value that cannot be read is logged with the type, the key and the component, and the property keeps its value.
- **Editor writing.** `EditorEntityJsonSerializer` writes the marked properties of every component after the keys of its own type (`EditorSerializedPropertyWriter`).
- **Developer errors.** A marked property that is not public read-write, has an unsupported type, or shares its key with another one throws `InvalidOperationException` at the first use of the type.
- **No pinning.** The per-type member table is a `ConditionalWeakTable`, which does not keep a collectible script assembly alive.
- **Clone contract.** A game component's `Clone()` calls `SerializedProperties.Copy(source, clone)`, or copies every marked property itself. Entity templates (`LoadCopy<Entity>(...).Clone()`) and editor duplicates go through `Clone()`.

## Consequences

- Game components round-trip through the editor without any change to the editor; `Projects/CasaEngine.RPGDemo/Components/SampleAnnotatedComponent.cs`, placed in `Projects/RPGDemo/DefaultWorld.world`, is the sample.
- The generic inspector already shows public properties of these types; `Guid` properties named `*AssetId` get its asset picker.
- Lists, nested objects and asset references other than a `Guid` are not supported; a component that needs them still overrides `Load` and needs an editor writer.
- An engine component may mark properties too; its specialised writer runs first, and a marked key replaces the value it wrote.
- Reading uses reflection on the load path only, never per frame.
