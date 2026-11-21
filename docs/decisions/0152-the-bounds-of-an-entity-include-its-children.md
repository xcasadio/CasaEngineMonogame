# ADR-0152: The bounds of an entity include its child components, and the bounds of a camera stand on the camera

- **Status**: Accepted
- **Date**: 2026-11-21
- **Source**: `ai-agent/tasks/oriented-sprites-tasks.md`, decision D7, answer of the author on 2026-11-21; found by the independent review of that plan

## Context

The world draws an entity only when the spatial index returns it for the frustum of the picture, and the index stores one box per entity. A survey on 2026-11-21 found:

- The box of an entity was the box of its root component, extended by the boxes of its entity components. **The child components did not count.**
- `CameraComponent.GetBoundingBox` returned a fixed box from -1 to 1 at the origin of the world, whatever the place of the camera.
- So a component that is a child of a camera (ADR-0151) was drawn only when the frustum held the origin of the world.
- A precedent existed: `TransformComponent.GetBoundingBox` already returned the union of the boxes of its descendants, at any depth, skipping every physics component and what hangs under it, because physics components live in the logical space of the simulation and not in render space. `SceneComponent` implements `IComponentDrawable` itself, so "the descendants that draw" are all the descendants outside physics.
- The index was refreshed for an entity when its root component or one of its entity components was marked; the child components were not consulted.

In other engines the visibility is tested per drawn object, with its own bounds; an object that does not draw, such as a camera, has none. An "always visible" attribute exists in some of them, as a way round for bounds that cannot be computed. Here the bounds of the child are correct; they were only not counted.

## Decision

- **The bounds of an entity count its child components.** `Entity.GetBoundingBox` extends the box of the root component and of each entity component by the boxes of their descendants, at any depth, **skipping every descendant that is a physics component and what hangs under it**: the rule of `TransformComponent`, applied to every entity.
- **The exclusion is about descendants, not about the component the walk starts from.** When the root component, or an entity component, is itself a physics component, what hangs under it is counted. This is what a visual placed under a logical root needs: the render pose of the entity then counts for its visibility.
- **The index follows a child that moves.** The world consults the same descendants, with the same exclusion, to know whether an entity must be indexed again.
- **The bounds of a camera stand on the camera**: its box from -1 to 1 is transformed by the world matrix of the camera.
- **No "always visible" attribute.**

## Consequences

- An entity with no camera and no descendant outside physics keeps exactly its box. An entity whose root is a `TransformComponent` too: it already counted its descendants.
- Any other entity with a child component outside physics has a box that is larger or equal: it is visible more often, never less. The editor framing and the neighbourhood queries, which read the same box, see the larger box. This includes an entity whose root is a physics component with a child outside physics.
- A root that already counts its descendants (`TransformComponent`, `StaticModelComponent` with its sub-meshes) has them counted a second time, for the same result; the cost of that second pass was not measured.
- **The box of every camera entity moves** from the origin of the world to the camera.
- **Limit kept**: an entity draws its root component, which goes down into its children, and among its other components only the primitive ones. A camera must be the root component of its entity for its children to be drawn.
- **Not treated**: a child entity is drawn by its parent and is not in the index; its bounds still do not count for the visibility of its parent. The visibility stays tested per entity, not per drawn object.
- Tests: `EntityBoundsWithChildrenTests`.
