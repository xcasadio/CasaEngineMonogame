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
        ThrowOnScriptTypeResolution = true,
    };
}