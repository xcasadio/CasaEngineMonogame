using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Time;

public static class GameTimeHelper
{
    public static FrameTime CreateFrameTime(GameTime gameTime, float timeScale, float totalTime, long frameIndex)
    {
        return FrameTime.FromGameTime(gameTime, timeScale, totalTime, frameIndex);
    }

    public static FrameTime CreateFrameTime(float elapsedTime, long frameIndex = 0)
    {
        return FrameTime.FromElapsedTime(elapsedTime, frameIndex);
    }

    public static float ConvertElapsedTimeToSeconds(GameTime gameTime)
    {
        return gameTime.ElapsedGameTime.Ticks / (float)TimeSpan.TicksPerSecond;
    }

    public static float ConvertTotalTimeToSeconds(GameTime gameTime)
    {
        return gameTime.TotalGameTime.Ticks / (float)TimeSpan.TicksPerSecond;
    }
}