namespace CasaEngine.Framework.Scripting.Coroutines;

public sealed class WaitForSecondsRealtime : ICoroutineInstruction
{
    private const float CompletionEpsilon = 0.000001f;
    private float _remainingTime;

    public WaitForSecondsRealtime(float duration)
    {
        Duration = duration;
        _remainingTime = duration;
    }

    public float Duration { get; }
    public float RemainingTime => _remainingTime;

    public bool IsCompleted(CoroutineUpdateContext context)
    {
        _remainingTime -= context.UnscaledDeltaTime;
        return _remainingTime <= CompletionEpsilon;
    }
}