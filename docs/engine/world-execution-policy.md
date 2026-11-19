# Execution policy of a world

## Purpose

`GameplayExecutionPolicy` is a set of booleans that says what a game runs: script updates, `BeginPlay`, player controllers, sprite animations, physics, and so on. The game carries one (`CasaEngineGame.ExecutionPolicy`, `Runtime` by default). A world can now carry its own, so that one world runs under rules that differ from the game's (ADR-0146). Only the preview world of the cutscene editor uses this; without an own policy, every world behaves exactly as before.

## World members

- `World.ExecutionPolicyOverride`: the policy of this world, or null to follow the game. It can only be set while the world has no game, that is before `LoadContent(CasaEngineGame)`, because the load-time rules are read during that call; setting it afterwards throws an `InvalidOperationException`. It is not serialized in a `.world` and not copied with the world.
- `World.ExecutionPolicy`: `ExecutionPolicyOverride ?? Game?.ExecutionPolicy`. It is null for a world that has neither an own policy nor a game; each reader keeps the tolerance it had for that case.

The readers that concern one world read `World.ExecutionPolicy`: `World`, `Entity` (script update), `AnimatedSpriteComponent`, `PhysicsBaseComponent`, `ParticleSystemComponent` and `PhysicsSystemComponent` (physics context creation and the stepping of worlds). The readers of `CasaEngineGame` and `GameManager` (window, views, first screen, virtual resolution) keep reading the policy of the game.

## `EditorLivePreview`

A fourth named policy, `GameplayExecutionPolicies.EditorLivePreview`. It is `EditorPreview` except for:

| Member | Value | Meaning |
|---|---|---|
| `IsEditorPreview` | false | Particle systems simulate even when `SimulateInEditor` is off |
| `UpdateAnimatedSprites` | true | Sprite animations advance |
| `UpdatePhysicsComponents` | true | Physics components report the pose of their body |
| `UpdatePhysicsEngine` | false | See below |
| `RaiseGameplayNotifications` | false | See below |

`RunBeginPlay`, `UpdateGameplayScripts`, `InitializePlayerControllers` and `ThrowOnScriptTypeResolution` stay false: no script update, no `BeginPlay`, no player controller.

### `RaiseGameplayNotifications`

A new boolean, true in `Runtime`, `EditorPreview` and `EditorSimulation` (nothing changes for them). When false, the simulation calls no game code: `BepuPhysicsEngine` does not call `OnHit` or `OnHitEnded` of the gameplay proxies of the two entities of a collision, and `AnimatedSpriteComponent` does not raise `AnimationFinished`, `AnimationLooped` or `AnimationEventTriggered`. The collision lists of the components are still kept up to date. The boolean is read on the world of the entity; an entity without a world, or a world without a policy, notifies as before.

### `UpdatePhysicsEngine` per world

The physics component of the game (`PhysicsSystemComponent`) steps the worlds of the views and the current world once per frame. It now skips a world whose own policy has `UpdatePhysicsEngine` false, before asking for its physics context. For `EditorLivePreview` the value false means: the component of the game does not step this world, whoever drives the world steps its physics itself. So a game that also steps its worlds (the editor in play mode) never steps the preview world twice. The preview world calls `World.PhysicsWorld.Update` itself, after `World.Update`, with the step of its transport.

## Usage

```csharp
// The world is not loaded yet, so it has no game: the own policy can be set.
var world = ...;
world.ExecutionPolicyOverride = GameplayExecutionPolicies.EditorLivePreview;

// Load it afterwards; its load-time rules are read from the own policy.
world.LoadContent(game);

// Whoever drives the world steps it, then its physics.
world.Update(seconds);
if (seconds > 0f)
{
    world.PhysicsWorld.Update(seconds);
}
```

In the editor, `PreviewWorldDriver` does this when `PreviewWorldDriverOptions.StepPhysics` is true (false by default, so the other previews do not change), and `CutscenePreviewSession` sets the policy on the copy of the world and turns the option on (see `docs/editor/cutscene-editor.md`, "A live preview world").

## Limits

- A world cannot change its own policy after it is loaded.
- The own policy is not part of any asset format.
- The physics advances by fixed steps of 1/60 s, one per call: a call that holds two steps or more makes one.
- The skeletal animation of 3D models was not read for this change and is not covered.
