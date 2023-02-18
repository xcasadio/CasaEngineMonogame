using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Helper
{
    public static class GameTimeHelper
    {
        public static float GameTimeToMilliseconds(GameTime gameTime)
        {
            return gameTime.ElapsedGameTime.Ticks / (float)TimeSpan.TicksPerSecond;
        }

        public static float TotalGameTimeToMilliseconds(GameTime gameTime)
        {
            return gameTime.TotalGameTime.Ticks / (float)TimeSpan.TicksPerSecond;
        }
    }
}
