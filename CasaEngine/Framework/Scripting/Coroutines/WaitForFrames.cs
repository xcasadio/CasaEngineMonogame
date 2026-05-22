namespace CasaEngine.Framework.Scripting.Coroutines;

public sealed class WaitForFrames : ICoroutineInstruction
{
    private int _remainingFrames;

    public WaitForFrames(int frameCount)
    {
        FrameCount = frameCount;
        _remainingFrames = frameCount;
    }

    public int FrameCount { get; }
    public int RemainingFrames => _remainingFrames;

    public bool IsCompleted(CoroutineUpdateContext context)
    {
        _remainingFrames--;
        return _remainingFrames <= 0;
    }
}