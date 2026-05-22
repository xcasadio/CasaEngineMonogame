using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Time;

public readonly struct FrameTime
{
    public FrameTime(
        float deltaTime,
        float unscaledDeltaTime,
        float totalTime,
        float unscaledTotalTime,
        float timeScale,
        long frameIndex)
    {
        DeltaTime = deltaTime;
        UnscaledDeltaTime = unscaledDeltaTime;
        TotalTime = totalTime;
        UnscaledTotalTime = unscaledTotalTime;
        TimeScale = timeScale;
        FrameIndex = frameIndex;
    }

    public float DeltaTime { get; }

    public float UnscaledDeltaTime { get; }

    public float TotalTime { get; }

    public float UnscaledTotalTime { get; }

    public float TimeScale { get; }

    public long FrameIndex { get; }

    public static FrameTime FromElapsedTime(float elapsedTime, long frameIndex = 0)
    {
        return new FrameTime(elapsedTime, elapsedTime, elapsedTime, elapsedTime, 1f, frameIndex);
    }

    public static FrameTime FromGameTime(GameTime gameTime, float timeScale, float totalTime, long frameIndex)
    {
        ArgumentNullException.ThrowIfNull(gameTime);

        float unscaledDeltaTime = GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime);
        float unscaledTotalTime = GameTimeHelper.ConvertTotalTimeToSeconds(gameTime);
        return new FrameTime(
            unscaledDeltaTime * timeScale,
            unscaledDeltaTime,
            totalTime,
            unscaledTotalTime,
            timeScale,
            frameIndex);
    }
}