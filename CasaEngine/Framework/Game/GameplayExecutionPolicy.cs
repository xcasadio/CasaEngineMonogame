namespace CasaEngine.Framework.Game;

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
    };
}