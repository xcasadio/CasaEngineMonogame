using CasaEngine.Core.Time;

namespace CasaEngine.Framework.Scripting.Coroutines;

public readonly struct CoroutineUpdateContext
{
    public CoroutineUpdateContext(
        float deltaTime,
        float unscaledDeltaTime,
        float timeScale,
        long frameIndex)
    {
        DeltaTime = deltaTime;
        UnscaledDeltaTime = unscaledDeltaTime;
        TimeScale = timeScale;
        FrameIndex = frameIndex;
    }

    public CoroutineUpdateContext(FrameTime frameTime)
        : this(frameTime.DeltaTime, frameTime.UnscaledDeltaTime, frameTime.TimeScale, frameTime.FrameIndex)
    {
    }

    public float DeltaTime { get; }

    public float UnscaledDeltaTime { get; }

    public float TimeScale { get; }

    public long FrameIndex { get; }
}