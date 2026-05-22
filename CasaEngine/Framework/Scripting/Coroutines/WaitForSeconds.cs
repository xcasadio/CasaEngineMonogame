namespace CasaEngine.Framework.Scripting.Coroutines;

public sealed class WaitForSeconds : ICoroutineInstruction
{
    private const float CompletionEpsilon = 0.000001f;
    private float _remainingTime;

    public WaitForSeconds(float duration)
    {
        Duration = duration;
        _remainingTime = duration;
    }

    public float Duration { get; }

    public float RemainingTime => _remainingTime;

    public bool IsCompleted(CoroutineUpdateContext context)
    {
        _remainingTime -= context.DeltaTime;
        return _remainingTime <= CompletionEpsilon;
    }
}