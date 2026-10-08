# ADR-0072: A component of unknown type is kept as an unresolved component

- **Status**: Accepted
- **Date**: 2026-10-08
- **Source**: this chantier: `ai-agent/tasks/rgce-editor-project-engine-tasks.md`, task T2 and proposal P2

## Context

- `ElementFactory` resolves a component's `type` by simple class name among the loaded assemblies. An unknown name used to end in `Activator.CreateInstance(null)`, an `ArgumentNullException` that named no type and no component.
- On the world load path this exception was not caught: opening such a world crashed the editor (`ElementFactory.cs`, `Entity.Load`, `SceneComponent.Load`; editor discovery of the RacingGame program).
- A world references the types of a gameplay DLL. That DLL may be missing, outdated, or have renamed a class; dropping the component instead would lose its data at the next save.

## Decision

- `ElementFactory.LoadComponent<T>` loads every component node of an entity: root component, `components`, and the `children_component` of scene components.
- When no loaded assembly declares the node's `type`, or the type is not a `T`:
  - the error is logged with the type, the component's name and its id;
  - the node is kept by an `UnresolvedComponent`, a concrete, empty `SceneComponent` that keeps the node's transform and leaves its children in the node.
- The editor writes an unresolved component back as its kept node, with the component's current id (a clone has a new one) and its current transform. Saving a world or an entity loses nothing.
- `ElementFactory.Create<T>` throws an `ArgumentException` naming the unknown type.
- `UnresolvedComponent` is not offered in the editor's Add Component list.

## Consequences

- A world whose gameplay DLL fails to load still opens in the editor, with an error per unresolved component, and can be saved without loss.
- An unresolved component does nothing at run time: the game logs the error and runs without it.
- Its data cannot be edited in the inspector; once the type exists again, the next load turns the node back into the real component.
