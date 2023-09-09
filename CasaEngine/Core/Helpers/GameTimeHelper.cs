using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Helpers;

public static class GameTimeHelper
{
    public static float ConvertElapsedTimeToSeconds(GameTime gameTime)
    {
        return gameTime.ElapsedGameTime.Ticks / (float)TimeSpan.TicksPerSecond;
    }

    public static float ConvertTotalTimeToSeconds(GameTime gameTime)
    {
        return gameTime.TotalGameTime.Ticks / (float)TimeSpan.TicksPerSecond;
    }
}