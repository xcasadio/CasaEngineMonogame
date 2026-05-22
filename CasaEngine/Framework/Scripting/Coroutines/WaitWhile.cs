namespace CasaEngine.Framework.Scripting.Coroutines;

public sealed class WaitWhile : ICoroutineInstruction
{
    public WaitWhile(Func<bool> predicate)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public Func<bool> Predicate { get; }

    public bool IsCompleted(CoroutineUpdateContext context)
    {
        return !Predicate();
    }
}