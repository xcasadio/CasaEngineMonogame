namespace CasaEngine.Framework.Scripting.Coroutines;

public sealed class CoroutineDebugInfo
{
    public int Id { get; init; }

    public CoroutineHandle Handle { get; init; }

    public string Name { get; init; }

    public string OwnerName { get; init; }

    public string CurrentInstruction { get; init; }

    public bool IsPaused { get; init; }

    public float? RemainingTime { get; init; }

    public string State { get; init; } = string.Empty;
}