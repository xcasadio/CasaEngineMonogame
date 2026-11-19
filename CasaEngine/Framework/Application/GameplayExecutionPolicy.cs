namespace CasaEngine.Framework.Application;

public sealed class GameplayExecutionPolicy
{
    public required bool IsEditorPreview { get; init; }
    public required bool UseExternalViewManagement { get; init; }
    public required bool InitializePlayerControllers { get; init; }
    public required bool InitializeGameplayOnLoad { get; init; }
    public required bool RunBeginPlay { get; init; }
    public required bool UpdateGameplayScripts { get; init; }
    public required bool UpdateAnimatedSprites { get; init; }
    public required bool UpdatePhysicsComponents { get; init; }
    public required bool UpdatePhysicsEngine { get; init; }

    /// <summary>
    /// True when physics collisions (<c>OnHit</c> and <c>OnHitEnded</c> of an entity's gameplay proxy) and sprite
    /// animation events (<c>AnimationFinished</c>, <c>AnimationLooped</c>, <c>AnimationEventTriggered</c>) call
    /// game code. False in <see cref="GameplayExecutionPolicies.EditorLivePreview"/>: the simulation runs, but no
    /// game code is called. The collision lists of the components stay up to date either way. An entity without a
    /// world, or a world without a policy, notifies.
    /// </summary>
    public required bool RaiseGameplayNotifications { get; init; }

    /// <summary>
    /// True when a script type name that does not resolve (unknown, ambiguous, wrong kind) must throw a
    /// <see cref="CasaEngine.Framework.Assets.ScriptTypeResolutionException"/> at creation. False in the editor
    /// preview: the world then logs a warning and keeps the entity, or the world itself, without its script, so a
    /// project whose gameplay assembly is not built yet still opens and the inspector can show and fix the script
    /// (ADR-0075, D9 and D11).
    /// </summary>
    public required bool ThrowOnScriptTypeResolution { get; init; }
}

public static class GameplayExecutionPolicies
{
    public static GameplayExecutionPolicy Runtime { get; } = new()
    {
        IsEditorPreview = false,
        UseExternalViewManagement = false,
        InitializePlayerControllers = true,
        InitializeGameplayOnLoad = true,
        RunBeginPlay = true,
        UpdateGameplayScripts = true,
        UpdateAnimatedSprites = true,
        UpdatePhysicsComponents = true,
        UpdatePhysicsEngine = true,
        RaiseGameplayNotifications = true,
        ThrowOnScriptTypeResolution = true,
    };

    public static GameplayExecutionPolicy EditorPreview { get; } = new()
    {
        IsEditorPreview = true,
        UseExternalViewManagement = true,
        InitializePlayerControllers = false,
        InitializeGameplayOnLoad = true,
        RunBeginPlay = false,
        UpdateGameplayScripts = false,
        UpdateAnimatedSprites = false,
        UpdatePhysicsComponents = false,
        UpdatePhysicsEngine = false,
        RaiseGameplayNotifications = true,
        ThrowOnScriptTypeResolution = false,
    };

    /// <summary>
    /// Policy of a world that is shown alive in the editor without running game code: sprite animations, particles
    /// and physics run, scripts are not updated and no collision or animation event reaches game code. It is meant
    /// to be set as <see cref="CasaEngine.Framework.Scene.World.World.ExecutionPolicyOverride"/> on a world that
    /// something else drives.
    /// <see cref="GameplayExecutionPolicy.UpdatePhysicsEngine"/> is false with this meaning: the game's physics
    /// component does not step this world, whoever drives the world steps its physics itself. That way a game that
    /// also steps its worlds (the editor in play mode) never steps this one twice.
    /// </summary>
    public static GameplayExecutionPolicy EditorLivePreview { get; } = new()
    {
        IsEditorPreview = false,
        UseExternalViewManagement = true,
        InitializePlayerControllers = false,
        InitializeGameplayOnLoad = true,
        RunBeginPlay = false,
        UpdateGameplayScripts = false,
        UpdateAnimatedSprites = true,
        UpdatePhysicsComponents = true,
        UpdatePhysicsEngine = false,
        RaiseGameplayNotifications = false,
        ThrowOnScriptTypeResolution = false,
    };

    public static GameplayExecutionPolicy EditorSimulation { get; } = new()
    {
        IsEditorPreview = false,
        UseExternalViewManagement = true,
        InitializePlayerControllers = true,
        InitializeGameplayOnLoad = true,
        RunBeginPlay = true,
        UpdateGameplayScripts = true,
        UpdateAnimatedSprites = true,
        UpdatePhysicsComponents = true,
        UpdatePhysicsEngine = true,
        RaiseGameplayNotifications = true,
        ThrowOnScriptTypeResolution = true,
    };
}